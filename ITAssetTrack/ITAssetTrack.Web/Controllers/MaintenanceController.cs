using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;
using ITAssetTrack.Infrastructure.Services;
using ITAssetTrack.Models;

namespace ITAssetTrack.Controllers
{
    [Authorize]
    public class MaintenanceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ScheduledPmExcelImportService _excelImportService;

        public MaintenanceController(
            AppDbContext context, 
            IWebHostEnvironment env, 
            ScheduledPmExcelImportService excelImportService)
        {
            _context = context;
            _env = env;
            _excelImportService = excelImportService;
        }

        // ==========================================
        // UNSCHEDULED MAINTENANCE
        // ==========================================


        [Authorize(Roles = "Admin,Coordinator,Technician,Viewer,Approver1,Approver2")]
        public async Task<IActionResult> UnscheduledList(string? status, string? priority, string? listTab)
        {
            listTab ??= "list";
            ViewBag.ActiveListTab = listTab;
            ViewBag.FilterStatus = status;
            ViewBag.FilterPriority = priority;

            // Main list data
            var query = _context.UnscheduledMaints.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);
            if (!string.IsNullOrEmpty(priority))
                query = query.Where(p => p.Priority == priority);
            var list = await query.OrderByDescending(p => p.Date).ToListAsync();

            // Auto-clean any test records where LeaderApprovedBy was erroneously set by technician
            var invalidTechApprovals = await _context.UnscheduledMaints
                .Where(u => u.LeaderApproved && u.LeaderApprovedBy != null && (u.LeaderApprovedBy.Contains("teknisi") || u.LeaderApprovedBy == "PIC TEAM"))
                .ToListAsync();
            if (invalidTechApprovals.Any())
            {
                foreach (var t in invalidTechApprovals)
                {
                    t.LeaderApproved = false;
                    t.LeaderSignature = null;
                    t.LeaderApprovedBy = null;
                    t.LeaderApprovedAt = null;
                }
                await _context.SaveChangesAsync();
            }

            // Approval tab data
            bool isApprover1 = User.IsInRole("Approver1");
            bool isApprover2 = User.IsInRole("Approver2");
            bool isTechnician = User.IsInRole("Technician");
            var completeQuery = _context.UnscheduledMaints.Where(u => u.Status == "Complete");

            List<UnscheduledMaint> pendingList;
            List<UnscheduledMaint> approvedList;

            if (isApprover1)
            {
                pendingList = await completeQuery.Where(u => !string.IsNullOrEmpty(u.TechSignature) && !u.LeaderApproved).OrderByDescending(u => u.Date).ToListAsync();
                approvedList = await completeQuery.Where(u => u.LeaderApproved).OrderByDescending(u => u.Date).ToListAsync();
            }
            else if (isApprover2)
            {
                pendingList = await completeQuery.Where(u => u.LeaderApproved && !u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                approvedList = await completeQuery.Where(u => u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
            }
            else if (isTechnician)
            {
                pendingList = await completeQuery.Where(u => string.IsNullOrEmpty(u.TechSignature)).OrderByDescending(u => u.Date).ToListAsync();
                approvedList = await completeQuery.Where(u => !string.IsNullOrEmpty(u.TechSignature)).OrderByDescending(u => u.Date).ToListAsync();
            }
            else
            {
                // Admin/Coordinator see all complete tickets needing any approval or tech signature
                pendingList = await completeQuery.Where(u => string.IsNullOrEmpty(u.TechSignature) || !u.LeaderApproved || !u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                approvedList = await completeQuery.Where(u => !string.IsNullOrEmpty(u.TechSignature) && u.LeaderApproved && u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
            }

            var allApprovalIds = pendingList.Select(t => t.Id).Union(approvedList.Select(t => t.Id)).ToList();
            ViewBag.ApprovalList = pendingList;
            ViewBag.ApprovedList = approvedList;
            ViewBag.ApprovalPhotos = await _context.MaintenancePhotos
                .Where(p => p.MaintenanceType == "Unscheduled" && allApprovalIds.Contains(p.MaintenanceId))
                .ToListAsync();

            return View(list);
        }


        [Authorize(Roles = "Admin,Coordinator,Technician")]
        public async Task<IActionResult> UnscheduledForm()
        {
            ViewBag.PendingApprovalCount = await _context.UnscheduledMaints
                .CountAsync(u => u.Status == "Complete" && (!u.LeaderApproved || !u.SectionHeadApproved));
            return View(new UnscheduledMaint());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnscheduledForm(UnscheduledMaint model, List<IFormFile>? photos)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            model.CreatedAt = DateTime.Now;
            // StartTime otomatis dicatat saat teknisi submit form (waktu mulai pengerjaan)
            model.StartTime = DateTime.Now.TimeOfDay;
            _context.UnscheduledMaints.Add(model);
            await _context.SaveChangesAsync();

            if (photos != null && photos.Count > 0)
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "maintenance");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                foreach (var file in photos)
                {
                    if (file.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        _context.MaintenancePhotos.Add(new MaintenancePhoto
                        {
                            Id = Guid.NewGuid(),
                            MaintenanceType = "Unscheduled",
                            MaintenanceId = model.Id,
                            FilePath = $"/uploads/maintenance/{fileName}"
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Form Unscheduled Maintenance berhasil disubmit.";
            return RedirectToAction(nameof(UnscheduledList));
        }

        public async Task<IActionResult> UnscheduledDetail(Guid id)
        {
            var ticket = await _context.UnscheduledMaints.FindAsync(id);
            if (ticket == null)
            {
                TempData["Error"] = "Tiket perbaikan tidak ditemukan.";
                return RedirectToAction("Index", "Asset");
            }

            ViewBag.Photos = await _context.MaintenancePhotos
                                .Where(p => p.MaintenanceId == id && p.MaintenanceType == "Unscheduled")
                                .ToListAsync();

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUnscheduledStatus(Guid id, string status, string remark, string? userSignature, string? techSignature)
        {
            var ticket = await _context.UnscheduledMaints.FindAsync(id);
            if (ticket == null)
            {
                TempData["Error"] = "Tiket tidak ditemukan.";
                return RedirectToAction("Index", "Asset");
            }

            // Fallback: jika StartTime belum tercatat (tiket lama sebelum fitur ini), set sekarang
            if (ticket.StartTime == TimeSpan.Zero)
            {
                ticket.StartTime = ticket.CreatedAt != default ? ticket.CreatedAt.TimeOfDay : DateTime.Now.TimeOfDay;
            }

            ticket.Status = status;
            ticket.Remark = remark;

            if (!string.IsNullOrEmpty(userSignature))
            {
                ticket.UserSignature = userSignature;
                // Catat EndTime saat User (pemilik device) TTD serah terima
                if (ticket.EndTime == TimeSpan.Zero)
                {
                    ticket.EndTime = DateTime.Now.TimeOfDay;
                }
            }
            if (!string.IsNullOrEmpty(techSignature))
                ticket.TechSignature = techSignature;

            ticket.UpdatedAt = DateTime.Now;

            _context.Update(ticket);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Status tiket perbaikan berhasil diperbarui.";
            return RedirectToAction(nameof(UnscheduledDetail), new { id = ticket.Id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUnscheduled(Guid id)
        {
            var pm = await _context.UnscheduledMaints.FindAsync(id);
            if (pm != null)
            {
                _context.UnscheduledMaints.Remove(pm);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Data Unscheduled PM berhasil dihapus.";
            }
            return RedirectToAction(nameof(UnscheduledList));
        }

        // ==========================================
        // SCHEDULED MAINTENANCE (PREVENTIVE MAINTENANCE)
        // ==========================================

        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
        public async Task<IActionResult> ScheduledList(string? monthYear, string? tab = "pending", string? historyFilter = "signed")
        {
            // Available months list for filter
            var availableMonths = await _context.ScheduledMaints
                .Select(s => s.PlanMonth)
                .Distinct()
                .OrderByDescending(m => m)
                .ToListAsync();

            string currentMonthStr = DateTime.Today.ToString("yyyy-MM");
            if (!availableMonths.Any())
            {
                availableMonths.Add(currentMonthStr);
            }

            string activeMonthYear;
            if (!string.IsNullOrEmpty(monthYear))
            {
                activeMonthYear = monthYear;
            }
            else if (availableMonths.Contains(currentMonthStr))
            {
                activeMonthYear = currentMonthStr;
            }
            else
            {
                activeMonthYear = availableMonths.First();
            }

            historyFilter = string.IsNullOrEmpty(historyFilter) ? "signed" : historyFilter;
            ViewBag.ActiveMonthYear = activeMonthYear;
            ViewBag.AvailableMonths = availableMonths;
            ViewBag.ActiveTab = tab ?? "pending";
            ViewBag.HistoryFilter = historyFilter;

            var monthQuery = _context.ScheduledMaints.Where(s => s.PlanMonth == activeMonthYear);

            // Auto-clean any legacy test data where SectionHeadApproved was true while LeaderApproved was false
            var invalidSectionHeadData = await monthQuery.Where(s => !s.LeaderApproved && s.SectionHeadApproved).ToListAsync();
            if (invalidSectionHeadData.Any())
            {
                foreach (var item in invalidSectionHeadData)
                {
                    item.SectionHeadApproved = false;
                    item.SectionHeadApprovedBy = null;
                    item.SectionHeadApprovedAt = null;
                }
                await _context.SaveChangesAsync();
            }

            var completedStatuses = new[] { "Completed", "PmDone", "Returned" };
            ViewBag.TargetCount = await monthQuery.CountAsync();
            ViewBag.CompletedCount = await monthQuery.CountAsync(s => completedStatuses.Contains(s.Status));
            ViewBag.PendingCount = await monthQuery.CountAsync(s => !completedStatuses.Contains(s.Status));

            ViewBag.HistorySignedUserCount = 0;
            ViewBag.HistoryUnsignedUserCount = 0;

            List<ScheduledMaint> list;
            if (tab == "completed" || tab == "history")
            {
                list = await monthQuery.Where(s => completedStatuses.Contains(s.Status)).OrderByDescending(s => s.ActualDate ?? s.Tanggal).ToListAsync();
            }
            else
            {
                list = await monthQuery.Where(s => !completedStatuses.Contains(s.Status)).OrderBy(s => s.PlanDate ?? s.Tanggal).ThenBy(s => s.NamaUser).ToListAsync();
            }

            var itemIds = list.Select(l => l.Id).ToList();
            var photosList = await _context.MaintenancePhotos
                .Where(p => itemIds.Contains(p.MaintenanceId) && p.MaintenanceType == "Scheduled")
                .ToListAsync();

            ViewBag.PhotosMap = photosList.GroupBy(p => p.MaintenanceId).ToDictionary(g => g.Key, g => g.ToList());

            return View(list);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> ImportScheduledPlan(IFormFile excelFile, string? monthYear)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Silakan pilih file Excel Rencana PM (.xlsx).";
                return RedirectToAction(nameof(ScheduledList), new { monthYear, tab = "pending" });
            }

            try
            {
                using var stream = excelFile.OpenReadStream();
                var importResult = await _excelImportService.ImportExcelStreamAsync(stream, monthYear);

                TempData["Success"] = importResult.Messages.FirstOrDefault() ?? "Import Excel Rencana PM berhasil diselesaikan.";
                return RedirectToAction(nameof(ScheduledList), new { monthYear = importResult.MonthYear, tab = "pending" });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Gagal mengimpor file Excel: {ex.Message}";
                return RedirectToAction(nameof(ScheduledList), new { monthYear, tab = "pending" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RecordStartTime(Guid id)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return NotFound(new { success = false, message = "Data PM tidak ditemukan." });

            item.StartTime = DateTime.Now.TimeOfDay;
            item.StartTimeRecorded = true;
            item.Status = "In Progress";
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            string formattedTime = DateTime.Today.Add(item.StartTime).ToString("HH:mm");
            return Json(new { success = true, startTime = formattedTime, message = "Waktu mulai PM berhasil dicatat." });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> ResetAllAugustPm(string? returnTo = "scheduled")
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM itassettrack.tbl_r_maintenance_photo WHERE MaintenanceType = 'Scheduled'");
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE itassettrack.tbl_t_scheduled_maint SET
                    Status = 'Planned',
                    ContactedBy = NULL,
                    ContactedAt = NULL,
                    ContactMethod = NULL,
                    UserResponse = NULL,
                    PmStartedAt = NULL,
                    PmCompletedAt = NULL,
                    ReturnedAt = NULL,
                    StartTime = '00:00:00',
                    EndTime = '00:00:00',
                    StartTimeRecorded = 0,
                    PerformedBy = '',
                    Keluhan = '',
                    Remark = '',
                    ExecBySignature = NULL,
                    UserHandoverSignature = NULL,
                    LeaderSignature = NULL,
                    ItosSectionHeadSignature = NULL,
                    LeaderApproved = 0,
                    LeaderApprovedBy = NULL,
                    LeaderApprovedAt = NULL,
                    SectionHeadApproved = 0,
                    SectionHeadApprovedBy = NULL,
                    SectionHeadApprovedAt = NULL,
                    UserNotPresent = 0,
                    ReturnNotes = NULL,
                    FisikKondisiBaik = 0,
                    UEFIPassword = 0,
                    BootOrderStorage = 0,
                    BootOrderDisable = 0,
                    DisableAdmin = 0,
                    DiskCleanUp = 0,
                    SpaceSSD = 0,
                    KeyboardAda = 0,
                    MouseAda = 0,
                    MonitorAda = 0,
                    ChargerAda = 0,
                    TasLaptopAda = 0,
                    MultiportAda = 0,
                    UpdatedAt = GETDATE()");

            TempData["Success"] = "Seluruh data PM telah BERHASIL DI-RESET TOTAL ke Tahap Awal (Belum PM / Planned).";

            if (returnTo == "execution")
            {
                return RedirectToAction(nameof(PmExecution));
            }
            return RedirectToAction(nameof(ScheduledList), new { tab = "pending" });
        }

        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
        public async Task<IActionResult> ScheduledForm(Guid? id, Guid? planId, string? sn)
        {
            ScheduledMaint? model = null;

            if (id.HasValue && id.Value != Guid.Empty)
            {
                model = await _context.ScheduledMaints.FindAsync(id.Value);
            }
            else if (planId.HasValue && planId.Value != Guid.Empty)
            {
                model = await _context.ScheduledMaints.FindAsync(planId.Value);
            }
            else if (!string.IsNullOrEmpty(sn))
            {
                model = await _context.ScheduledMaints
                    .FirstOrDefaultAsync(s => s.SerialNumber.ToLower() == sn.ToLower());
            }

            if (model != null && (User.IsInRole("Approver1") || User.IsInRole("Approver2")))
            {
                var completedStatuses = new[] { "Completed", "PmDone", "Returned" };
                bool isFuturePlan = model.PlanDate.HasValue && model.PlanDate.Value.Date > DateTime.Today;
                if (!completedStatuses.Contains(model.Status) && isFuturePlan)
                {
                    TempData["Error"] = "Rencana PM belum masuk tanggal pelaksanaan. PIC TEAM dan Section Head belum dapat mengakses form ini.";
                    return RedirectToAction(nameof(ScheduledList), new { monthYear = model.PlanMonth, tab = "pending" });
                }
            }

            // Jika tidak ada ID → mode Tambah Target PM (form ringkas)
            bool isNewTarget = !id.HasValue && !planId.HasValue && string.IsNullOrEmpty(sn);
            ViewBag.IsNewTarget = isNewTarget;

            if (model == null)
            {
                model = new ScheduledMaint
                {
                    Id = Guid.NewGuid(),
                    PlanMonth = DateTime.Today.ToString("yyyy-MM"),
                    Tanggal = DateTime.Today,
                    PlanDate = DateTime.Today,
                    Status = isNewTarget ? "Planned" : "In Progress"
                };

                if (!string.IsNullOrEmpty(sn))
                {
                    var masterAsset = await _context.Assets
                        .Include(a => a.BrandObj)
                        .Include(a => a.DeviceTypeObj)
                        .FirstOrDefaultAsync(a => a.SerialNumber.ToLower() == sn.ToLower());

                    if (masterAsset != null)
                    {
                        model.SerialNumber = masterAsset.SerialNumber;
                        model.ComputerName = masterAsset.ComputerName;
                        model.NamaUser = masterAsset.AssignedTo;
                        model.NRP = masterAsset.NRP;
                        model.Site = masterAsset.Site;
                        model.Division = masterAsset.Division;
                        model.Brand = masterAsset.BrandObj?.Name ?? masterAsset.Brand;
                        model.DeviceType = masterAsset.DeviceTypeObj?.Name ?? masterAsset.DeviceType;
                        model.Model = masterAsset.Model;
                        model.Ram = masterAsset.Ram;
                        model.Storage = masterAsset.Storage;
                        model.Processor = masterAsset.Processor;
                        model.WindowsVersion = masterAsset.WindowsVersion;
                    }
                }
            }

            if (!isNewTarget && string.IsNullOrEmpty(model.NoDocument))
            {
                int count = await _context.ScheduledMaints.CountAsync() + 1;
                model.NoDocument = $"InfraHub/HQ_DEPT/PM/{DateTime.Now.Year}/{count:D3}";
            }

            if (model != null && !string.IsNullOrEmpty(model.SerialNumber))
            {
                var masterAsset = await _context.Assets
                    .Include(a => a.BrandObj)
                    .Include(a => a.DeviceTypeObj)
                    .FirstOrDefaultAsync(a => a.SerialNumber.ToLower() == model.SerialNumber.ToLower());

                if (masterAsset != null)
                {
                    if (string.IsNullOrEmpty(model.ComputerName)) model.ComputerName = masterAsset.ComputerName;
                    if (string.IsNullOrEmpty(model.NamaUser)) model.NamaUser = masterAsset.AssignedTo;
                    if (string.IsNullOrEmpty(model.NRP)) model.NRP = masterAsset.NRP;
                    if (string.IsNullOrEmpty(model.Site)) model.Site = masterAsset.Site;
                    if (string.IsNullOrEmpty(model.Division)) model.Division = masterAsset.Division;
                    if (string.IsNullOrEmpty(model.Brand)) model.Brand = masterAsset.BrandObj?.Name ?? masterAsset.Brand;
                    if (string.IsNullOrEmpty(model.DeviceType)) model.DeviceType = masterAsset.DeviceTypeObj?.Name ?? masterAsset.DeviceType;
                    if (string.IsNullOrEmpty(model.Model)) model.Model = masterAsset.Model;
                    if (string.IsNullOrEmpty(model.Ram)) model.Ram = masterAsset.Ram;
                    if (string.IsNullOrEmpty(model.Storage)) model.Storage = masterAsset.Storage;
                    if (string.IsNullOrEmpty(model.Processor)) model.Processor = masterAsset.Processor;
                    if (string.IsNullOrEmpty(model.WindowsVersion)) model.WindowsVersion = masterAsset.WindowsVersion;
                }
            }

            if (model != null)
            {
                if (isNewTarget || model.Status == "Planned" || model.Status == "Belum PM" || model.PerformedBy == "Elyas")
                {
                    model.PerformedBy = string.Empty;
                }
            }

            if (model != null && !isNewTarget)
            {
                model.FisikKondisiBaik = true;
                model.UEFIPassword = true;
                model.BootOrderStorage = true;
                model.BootOrderDisable = true;
                model.DisableAdmin = true;
                model.DiskCleanUp = true;
                model.SpaceSSD = true;

                if (model.Status == "Confirmed" && !model.PmStartedAt.HasValue)
                {
                    model.Status = "InProgress";
                    model.PmStartedAt = DateTime.Now;
                    model.StartTime = DateTime.Now.TimeOfDay;
                    model.StartTimeRecorded = true;
                }
                if (!model.ChargerAda) model.ChargerAda = true;
                if (!model.TasLaptopAda) model.TasLaptopAda = true;

                await _context.SaveChangesAsync();
            }

            ViewBag.Photos = await _context.MaintenancePhotos
                .Where(p => p.MaintenanceId == model.Id)
                .ToListAsync();

            return View(model);
        }

        /// <summary>
        /// POST: Simpan target PM baru (mode form ringkas / Tambah Target PM)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2")]
        public async Task<IActionResult> SaveTargetPm(ScheduledMaint entity)
        {
            var existing = await _context.ScheduledMaints.FindAsync(entity.Id);
            if (existing != null)
            {
                // Update identitas saja
                existing.PlanDate = entity.PlanDate ?? DateTime.Today;
                existing.SerialNumber = entity.SerialNumber;
                existing.NamaUser = entity.NamaUser;
                existing.NRP = entity.NRP;
                existing.Site = entity.Site;
                existing.Division = entity.Division;
                existing.Brand = entity.Brand;
                existing.Model = entity.Model;
                existing.ComputerName = entity.ComputerName;
                existing.UpdatedAt = DateTime.Now;
                _context.ScheduledMaints.Update(existing);
            }
            else
            {
                entity.Status = "Planned";
                entity.CreatedAt = DateTime.Now;
                entity.PlanMonth = (entity.PlanDate ?? DateTime.Today).ToString("yyyy-MM");
                if (string.IsNullOrEmpty(entity.NoDocument))
                {
                    int count = await _context.ScheduledMaints.CountAsync(s => s.Status == "Completed") + 1;
                    entity.NoDocument = $"InfraHub/HQ_DEPT/PM/{DateTime.Now.Year}/{count:D3}";
                }
                _context.ScheduledMaints.Add(entity);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Target PM berhasil ditambahkan ke rencana bulan ini.";
            string planMonth = (entity.PlanDate ?? DateTime.Today).ToString("yyyy-MM");
            return RedirectToAction(nameof(ScheduledList), new { monthYear = planMonth, tab = "pending" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
        public async Task<IActionResult> ScheduledForm(ScheduledMaint entity, 
            string? ExecBySignatureData, 
            string? UserHandoverSignatureData,
            string? LeaderSignatureData, 
            string? ItosSignatureData,
            List<IFormFile>? photos)
        {
            var existing = await _context.ScheduledMaints.FindAsync(entity.Id);

            if (existing == null)
            {
                entity.CreatedAt = DateTime.Now;
                entity.Status = "Completed";
                entity.ActualDate = DateTime.Now;
                entity.EndTime = DateTime.Now.TimeOfDay;

                if (!string.IsNullOrEmpty(ExecBySignatureData)) entity.ExecBySignature = ExecBySignatureData;
                if (!string.IsNullOrEmpty(LeaderSignatureData)) entity.LeaderSignature = LeaderSignatureData;
                if (!string.IsNullOrEmpty(ItosSignatureData)) entity.ItosSectionHeadSignature = ItosSignatureData;

                _context.ScheduledMaints.Add(entity);
            }
            else
            {
                existing.PlanMonth = entity.PlanMonth;
                existing.Tanggal = entity.Tanggal;
                existing.PlanDate = entity.PlanDate ?? entity.Tanggal;
                existing.ActualDate = DateTime.Now;
                if (entity.StartTime != TimeSpan.Zero)
                    existing.StartTime = entity.StartTime;
                else if (existing.PmStartedAt.HasValue && existing.StartTime == TimeSpan.Zero)
                    existing.StartTime = existing.PmStartedAt.Value.TimeOfDay;
                existing.EndTime = DateTime.Now.TimeOfDay;
                existing.NoDocument = string.IsNullOrEmpty(entity.NoDocument) ? existing.NoDocument : entity.NoDocument;

                if (!string.IsNullOrWhiteSpace(entity.SerialNumber)) existing.SerialNumber = entity.SerialNumber;
                if (!string.IsNullOrWhiteSpace(entity.NamaUser)) existing.NamaUser = entity.NamaUser;
                if (!string.IsNullOrWhiteSpace(entity.NRP)) existing.NRP = entity.NRP;
                if (!string.IsNullOrWhiteSpace(entity.Site)) existing.Site = entity.Site;
                if (!string.IsNullOrWhiteSpace(entity.Division)) existing.Division = entity.Division;
                if (!string.IsNullOrWhiteSpace(entity.ContactInfo)) existing.ContactInfo = entity.ContactInfo;
                if (!string.IsNullOrWhiteSpace(entity.Email)) existing.Email = entity.Email;
                if (!string.IsNullOrWhiteSpace(entity.ComputerName)) existing.ComputerName = entity.ComputerName;

                if (!string.IsNullOrWhiteSpace(entity.DeviceType)) existing.DeviceType = entity.DeviceType;
                if (!string.IsNullOrWhiteSpace(entity.Brand)) existing.Brand = entity.Brand;
                if (!string.IsNullOrWhiteSpace(entity.Model)) existing.Model = entity.Model;
                if (!string.IsNullOrWhiteSpace(entity.Processor)) existing.Processor = entity.Processor;
                if (!string.IsNullOrWhiteSpace(entity.ProcessorKondisi)) existing.ProcessorKondisi = entity.ProcessorKondisi;
                if (!string.IsNullOrWhiteSpace(entity.Ram)) existing.Ram = entity.Ram;
                if (!string.IsNullOrWhiteSpace(entity.RamKondisi)) existing.RamKondisi = entity.RamKondisi;
                if (!string.IsNullOrWhiteSpace(entity.Storage)) existing.Storage = entity.Storage;
                if (!string.IsNullOrWhiteSpace(entity.StorageKondisi)) existing.StorageKondisi = entity.StorageKondisi;
                if (!string.IsNullOrWhiteSpace(entity.WindowsVersion)) existing.WindowsVersion = entity.WindowsVersion;
                if (!string.IsNullOrWhiteSpace(entity.DeviceKondisi)) existing.DeviceKondisi = entity.DeviceKondisi;

                bool isApproverPost = (User.IsInRole("Approver1") || User.IsInRole("Approver2")) && !User.IsInRole("Admin") && !User.IsInRole("Coordinator") && !User.IsInRole("Technician");

                if (!isApproverPost)
                {
                    existing.KeyboardAda = entity.KeyboardAda;
                    if (!string.IsNullOrWhiteSpace(entity.KeyboardBrand)) existing.KeyboardBrand = entity.KeyboardBrand;
                    if (!string.IsNullOrWhiteSpace(entity.KeyboardKondisi)) existing.KeyboardKondisi = entity.KeyboardKondisi;
                    existing.MouseAda = entity.MouseAda;
                    if (!string.IsNullOrWhiteSpace(entity.MouseBrand)) existing.MouseBrand = entity.MouseBrand;
                    if (!string.IsNullOrWhiteSpace(entity.MouseKondisi)) existing.MouseKondisi = entity.MouseKondisi;
                    existing.MonitorAda = entity.MonitorAda;
                    if (!string.IsNullOrWhiteSpace(entity.MonitorBrand)) existing.MonitorBrand = entity.MonitorBrand;
                    if (!string.IsNullOrWhiteSpace(entity.MonitorModel)) existing.MonitorModel = entity.MonitorModel;
                    if (!string.IsNullOrWhiteSpace(entity.MonitorKondisi)) existing.MonitorKondisi = entity.MonitorKondisi;
                    existing.ChargerAda = entity.ChargerAda;
                    existing.TasLaptopAda = entity.TasLaptopAda;
                    existing.MultiportAda = entity.MultiportAda;

                    existing.FisikKondisiBaik = entity.FisikKondisiBaik;
                    existing.UEFIPassword = entity.UEFIPassword;
                    existing.BootOrderStorage = entity.BootOrderStorage;
                    existing.BootOrderDisable = entity.BootOrderDisable;
                    existing.DisableAdmin = entity.DisableAdmin;
                    existing.DiskCleanUp = entity.DiskCleanUp;
                    existing.SpaceSSD = entity.SpaceSSD;

                    if (entity.Keluhan != null) existing.Keluhan = entity.Keluhan;
                    if (entity.Remark != null) existing.Remark = entity.Remark;
                    
                    if (entity.PerformedBy != null)
                        existing.PerformedBy = entity.PerformedBy;
                }

                existing.UserNotPresent = entity.UserNotPresent;
                if (!string.IsNullOrWhiteSpace(entity.ReturnNotes)) existing.ReturnNotes = entity.ReturnNotes;
                if (entity.UserNotPresent || !string.IsNullOrEmpty(UserHandoverSignatureData))
                {
                    if (!existing.ReturnedAt.HasValue) existing.ReturnedAt = DateTime.Now;
                }

                if (entity.LeaderApproved || existing.LeaderApproved)
                {
                    existing.LeaderApproved = true;
                    if (!string.IsNullOrWhiteSpace(entity.LeaderApprovedBy)) existing.LeaderApprovedBy = entity.LeaderApprovedBy;
                    if (string.IsNullOrWhiteSpace(existing.LeaderApprovedBy)) existing.LeaderApprovedBy = "Franz";
                    if (!existing.LeaderApprovedAt.HasValue) existing.LeaderApprovedAt = DateTime.Now;
                }

                if (existing.LeaderApproved && (entity.SectionHeadApproved || existing.SectionHeadApproved))
                {
                    existing.SectionHeadApproved = true;
                    if (!string.IsNullOrWhiteSpace(entity.SectionHeadApprovedBy)) existing.SectionHeadApprovedBy = entity.SectionHeadApprovedBy;
                    if (string.IsNullOrWhiteSpace(existing.SectionHeadApprovedBy)) existing.SectionHeadApprovedBy = "Eddi";
                    if (!existing.SectionHeadApprovedAt.HasValue) existing.SectionHeadApprovedAt = DateTime.Now;
                }
                else if (!existing.LeaderApproved)
                {
                    existing.SectionHeadApproved = false;
                    existing.SectionHeadApprovedBy = null;
                    existing.SectionHeadApprovedAt = null;
                }

                existing.Status = "Completed";
                existing.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(ExecBySignatureData)) existing.ExecBySignature = ExecBySignatureData;
                if (!string.IsNullOrEmpty(UserHandoverSignatureData)) existing.UserHandoverSignature = UserHandoverSignatureData;
                if (!string.IsNullOrEmpty(LeaderSignatureData)) existing.LeaderSignature = LeaderSignatureData;
                if (!string.IsNullOrEmpty(ItosSignatureData)) existing.ItosSectionHeadSignature = ItosSignatureData;

                _context.ScheduledMaints.Update(existing);
            }

            await _context.SaveChangesAsync();

            if (photos != null && photos.Count > 0)
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "maintenance");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                foreach (var file in photos)
                {
                    if (file.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        _context.MaintenancePhotos.Add(new MaintenancePhoto
                        {
                            Id = Guid.NewGuid(),
                            MaintenanceType = "Scheduled",
                            MaintenanceId = entity.Id,
                            FilePath = $"/uploads/maintenance/{fileName}"
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Form Scheduled PM berhasil disubmit dan 3 Tanda Tangan Digital telah tersimpan.";
            return RedirectToAction(nameof(ScheduledList), new { monthYear = entity.PlanMonth, tab = "completed" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> DeleteScheduledPlanItem(Guid id)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item != null)
            {
                string mYear = item.PlanMonth;
                _context.ScheduledMaints.Remove(item);
                await _context.SaveChangesAsync();

                // Recalculate target for the month
                var pmPlan = await _context.PmPlans.FirstOrDefaultAsync(p => p.MonthYear == mYear);
                if (pmPlan != null)
                {
                    pmPlan.TargetQuantity = await _context.ScheduledMaints.CountAsync(s => s.PlanMonth == mYear);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Target Plan PM item berhasil dihapus.";
                return RedirectToAction(nameof(ScheduledList), new { monthYear = mYear, tab = "pending" });
            }
            return RedirectToAction(nameof(ScheduledList));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> DeleteScheduledPlanMonth(string monthYear)
        {
            if (string.IsNullOrEmpty(monthYear)) return RedirectToAction(nameof(ScheduledList));

            var items = await _context.ScheduledMaints.Where(s => s.PlanMonth == monthYear).ToListAsync();
            if (items.Any())
            {
                _context.ScheduledMaints.RemoveRange(items);
            }

            var pmPlan = await _context.PmPlans.FirstOrDefaultAsync(p => p.MonthYear == monthYear);
            if (pmPlan != null)
            {
                _context.PmPlans.Remove(pmPlan);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Seluruh Target Rencana PM untuk Bulan '{monthYear}' berhasil dihapus.";
            return RedirectToAction(nameof(ScheduledList));
        }

        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
        public async Task<IActionResult> PrintScheduledPm(Guid id)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null)
            {
                TempData["Error"] = "Dokumen Form PM tidak ditemukan.";
                return RedirectToAction(nameof(ScheduledList));
            }

            ViewBag.Photos = await _context.MaintenancePhotos
                .Where(p => p.MaintenanceId == id && p.MaintenanceType == "Scheduled")
                .OrderBy(p => p.UploadedAt)
                .ToListAsync();

            return View(item);
        }

        // ==========================================
        // PM EXECUTION DASHBOARD
        // ==========================================

        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2")]
        public async Task<IActionResult> PmExecution(string? monthYear, string? tab = "today")
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            bool isApprover1 = User.IsInRole("Approver1");
            bool isApprover2 = User.IsInRole("Approver2");
            bool isApprover = isApprover1 || isApprover2;

            if (isApprover && string.IsNullOrEmpty(tab))
            {
                tab = "unsigned";
            }
            tab ??= "today";

            // Available months
            var availableMonths = await _context.ScheduledMaints
                .Select(s => s.PlanMonth)
                .Distinct()
                .OrderByDescending(m => m)
                .ToListAsync();

            if (!availableMonths.Any())
                availableMonths.Add(today.ToString("yyyy-MM"));

            string activeMonthYear = string.IsNullOrEmpty(monthYear) ? today.ToString("yyyy-MM") : monthYear;
            ViewBag.ActiveMonthYear = activeMonthYear;
            ViewBag.AvailableMonths = availableMonths;
            ViewBag.ActiveTab = tab;

            // Load month data
            var allThisMonth = await _context.ScheduledMaints
                .Where(s => s.PlanMonth == activeMonthYear)
                .ToListAsync();

            // Calculate Role-Tailored Signature Counts
            int unsignedCount;
            int signedHistoryCount;

            if (isApprover1)
            {
                // Pak Franz (PIC TEAM): Belum TTD = Teknisi & User sudah TTD, tapi PIC TEAM belum approved
                unsignedCount = allThisMonth.Count(s =>
                    (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                    && !string.IsNullOrEmpty(s.ExecBySignature)
                    && (!string.IsNullOrEmpty(s.UserHandoverSignature) || s.UserNotPresent)
                    && (!s.LeaderApproved && string.IsNullOrEmpty(s.LeaderSignature)));
                signedHistoryCount = allThisMonth.Count(s => s.LeaderApproved || !string.IsNullOrEmpty(s.LeaderSignature));
            }
            else if (isApprover2)
            {
                // Pak Eddi (Section Head): Belum TTD = PIC TEAM sudah approved tapi Section Head belum approved
                unsignedCount = allThisMonth.Count(s =>
                    (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                    && (s.LeaderApproved || !string.IsNullOrEmpty(s.LeaderSignature))
                    && (!s.SectionHeadApproved && string.IsNullOrEmpty(s.ItosSectionHeadSignature)));
                signedHistoryCount = allThisMonth.Count(s => s.SectionHeadApproved || !string.IsNullOrEmpty(s.ItosSectionHeadSignature));
            }
            else
            {
                // Admin / Teknisi / Coordinator
                unsignedCount = allThisMonth.Count(s =>
                    (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                    && (string.IsNullOrEmpty(s.ExecBySignature) 
                        || string.IsNullOrEmpty(s.UserHandoverSignature) 
                        || string.IsNullOrEmpty(s.LeaderSignature) 
                        || string.IsNullOrEmpty(s.ItosSectionHeadSignature)));
                signedHistoryCount = allThisMonth.Count(s => s.LeaderApproved || s.SectionHeadApproved || s.Status == "Completed");
            }

            ViewBag.UnsignedCount = unsignedCount;
            ViewBag.SignedHistoryCount = signedHistoryCount;

            ViewBag.TodayCount = allThisMonth.Count(s =>
                s.PlanDate.HasValue && s.PlanDate.Value.Date == today
                && s.Status != "Completed" && s.Status != "Returned" && s.Status != "PmDone");
            ViewBag.TomorrowCount = allThisMonth.Count(s =>
                s.PlanDate.HasValue && s.PlanDate.Value.Date == tomorrow
                && (s.Status == "Planned" || s.Status == "Contacted"));
            ViewBag.PendingCount = allThisMonth.Count(s =>
                s.Status == "Planned" || s.Status == "Contacted" || s.Status == "Confirmed" || s.Status == "Rescheduled");
            ViewBag.InProgressCount = allThisMonth.Count(s => s.Status == "InProgress");
            ViewBag.DoneCount = allThisMonth.Count(s => s.Status == "PmDone" || s.Status == "Returned");
            ViewBag.CompletedCount = allThisMonth.Count(s => s.Status == "Completed");
            ViewBag.AllThisMonthCount = allThisMonth.Count;

            // Filter list by active tab
            List<ScheduledMaint> list;
            switch (tab)
            {
                case "unsigned":
                    if (isApprover1)
                    {
                        list = allThisMonth
                            .Where(s => (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                                && !string.IsNullOrEmpty(s.ExecBySignature)
                                && (!string.IsNullOrEmpty(s.UserHandoverSignature) || s.UserNotPresent)
                                && (!s.LeaderApproved && string.IsNullOrEmpty(s.LeaderSignature)))
                            .OrderByDescending(s => s.PmCompletedAt ?? s.UpdatedAt)
                            .ToList();
                    }
                    else if (isApprover2)
                    {
                        list = allThisMonth
                            .Where(s => (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                                && (s.LeaderApproved || !string.IsNullOrEmpty(s.LeaderSignature))
                                && (!s.SectionHeadApproved && string.IsNullOrEmpty(s.ItosSectionHeadSignature)))
                            .OrderByDescending(s => s.LeaderApprovedAt ?? s.PmCompletedAt ?? s.UpdatedAt)
                            .ToList();
                    }
                    else
                    {
                        list = allThisMonth
                            .Where(s => (s.Status == "InProgress" || s.Status == "PmDone" || s.Status == "Returned" || s.Status == "Completed")
                                && (string.IsNullOrEmpty(s.ExecBySignature) 
                                    || string.IsNullOrEmpty(s.UserHandoverSignature) 
                                    || string.IsNullOrEmpty(s.LeaderSignature) 
                                    || string.IsNullOrEmpty(s.ItosSectionHeadSignature)))
                            .OrderByDescending(s => s.PmCompletedAt ?? s.UpdatedAt)
                            .ToList();
                    }
                    break;

                case "signed_history":
                case "history":
                    if (isApprover1)
                    {
                        list = allThisMonth
                            .Where(s => s.LeaderApproved || !string.IsNullOrEmpty(s.LeaderSignature))
                            .OrderByDescending(s => s.LeaderApprovedAt ?? s.PmCompletedAt ?? s.UpdatedAt)
                            .ToList();
                    }
                    else if (isApprover2)
                    {
                        list = allThisMonth
                            .Where(s => s.SectionHeadApproved || !string.IsNullOrEmpty(s.ItosSectionHeadSignature))
                            .OrderByDescending(s => s.SectionHeadApprovedAt ?? s.LeaderApprovedAt ?? s.UpdatedAt)
                            .ToList();
                    }
                    else
                    {
                        list = allThisMonth
                            .Where(s => s.Status == "Completed" || s.LeaderApproved || s.SectionHeadApproved)
                            .OrderByDescending(s => s.PmCompletedAt ?? s.ActualDate ?? s.UpdatedAt)
                            .ToList();
                    }
                    break;

                case "tomorrow":
                    list = allThisMonth
                        .Where(s => s.PlanDate.HasValue && s.PlanDate.Value.Date == tomorrow
                            && (s.Status == "Planned" || s.Status == "Contacted"))
                        .OrderBy(s => s.NamaUser)
                        .ToList();
                    break;
                case "pending":
                    list = allThisMonth
                        .Where(s => s.Status == "Planned" || s.Status == "Contacted" || s.Status == "Confirmed" || s.Status == "Rescheduled")
                        .OrderBy(s => s.PlanDate ?? s.Tanggal)
                        .ThenBy(s => s.NamaUser)
                        .ToList();
                    break;
                case "inprogress":
                    list = allThisMonth
                        .Where(s => s.Status == "InProgress")
                        .OrderBy(s => s.PmStartedAt)
                        .ToList();
                    break;
                case "done":
                    list = allThisMonth
                        .Where(s => s.Status == "PmDone" || s.Status == "Returned")
                        .OrderByDescending(s => s.PmCompletedAt)
                        .ToList();
                    break;
                default: // "today"
                    list = allThisMonth
                        .Where(s => s.PlanDate.HasValue && s.PlanDate.Value.Date == today
                            && s.Status != "Completed")
                        .OrderBy(s => s.Status == "InProgress" ? 0 : s.Status == "PmDone" ? 1 : s.Status == "Returned" ? 2 : 3)
                        .ThenBy(s => s.NamaUser)
                        .ToList();
                    break;
            }

            // Load photos for all items in list
            var itemIds = list.Select(l => l.Id).ToList();
            ViewBag.AllPhotos = itemIds.Any()
                ? await _context.MaintenancePhotos
                    .Where(p => itemIds.Contains(p.MaintenanceId) && p.MaintenanceType == "Scheduled")
                    .ToListAsync()
                : new List<ITAssetTrack.Models.MaintenancePhoto>();

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> ContactUser(Guid id, string contactedBy, string contactMethod)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            item.ContactedAt = DateTime.Now;
            item.ContactedBy = contactedBy;
            item.ContactMethod = contactMethod;
            item.Status = "Contacted";
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"User {item.NamaUser} berhasil dihubungi via {contactMethod}." });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmUser(Guid id)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            item.UserResponse = "Confirmed";
            item.UserConfirmedAt = DateTime.Now;
            item.Status = "Confirmed";
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"User {item.NamaUser} konfirmasi OK untuk PM." });
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleUser(Guid id, DateTime newDate, string reason)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            // Save original date on first reschedule
            if (!item.OriginalPlanDate.HasValue)
                item.OriginalPlanDate = item.PlanDate ?? item.Tanggal;

            // Find next available slot
            if (!item.Reschedule1Date.HasValue)
            {
                item.Reschedule1Date = newDate;
                item.Reschedule1Reason = reason;
            }
            else if (!item.Reschedule2Date.HasValue)
            {
                item.Reschedule2Date = newDate;
                item.Reschedule2Reason = reason;
            }
            else if (!item.Reschedule3Date.HasValue)
            {
                item.Reschedule3Date = newDate;
                item.Reschedule3Reason = reason;
            }
            else
            {
                return Json(new { success = false, message = "Sudah mencapai batas maksimum 3 kali reschedule." });
            }

            item.PlanDate = newDate;
            item.UserResponse = "Reschedule";
            item.Status = "Planned"; // Reset to Planned with new date
            item.ContactedAt = null; // Reset contact tracking
            item.ContactedBy = null;
            item.ContactMethod = null;
            item.UserConfirmedAt = null;
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"PM di-reschedule ke {newDate:dd MMM yyyy}. Alasan: {reason}" });
        }

        [HttpGet]
        public async Task<IActionResult> ResetPmTest(Guid? id)
        {
            if (id.HasValue && id.Value != Guid.Empty)
            {
                var item = await _context.ScheduledMaints.FindAsync(id.Value);
                if (item != null)
                {
                    item.PmStartedAt = null;
                    item.StartTime = TimeSpan.Zero;
                    item.StartTimeRecorded = false;
                    item.Status = "Confirmed";
                    await _context.SaveChangesAsync();
                    return Content($"PM {id.Value} reset successfully to Confirmed status with null start time!");
                }
                return Content("PM not found.");
            }
            
            var items = await _context.ScheduledMaints.Where(s => s.Status == "InProgress").ToListAsync();
            foreach (var s in items)
            {
                s.PmStartedAt = null;
                s.StartTime = TimeSpan.Zero;
                s.StartTimeRecorded = false;
                s.Status = "Confirmed";
            }
            await _context.SaveChangesAsync();
            return Content($"All InProgress PMs ({items.Count}) reset successfully to Confirmed status with null start time!");
        }

        [HttpPost]
        public async Task<IActionResult> StartPm(Guid id, string? performedBy)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            item.PmStartedAt = DateTime.Now;
            item.StartTime = DateTime.Now.TimeOfDay;
            item.StartTimeRecorded = true;
            item.ActualDate = DateTime.Today;
            item.PerformedBy = performedBy;
            item.Status = "InProgress";
            item.UpdatedAt = DateTime.Now;

            var formattedTime = DateTime.Now.ToString(@"HH\:mm", System.Globalization.CultureInfo.InvariantCulture);
            return Json(new { success = true, message = $"PM dimulai pada {formattedTime}. Teknisi: {performedBy}", startTime = formattedTime });
        }

        [HttpPost]
        public async Task<IActionResult> EndPm(ScheduledMaint entity)
        {
            var item = await _context.ScheduledMaints.FindAsync(entity.Id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            // Save all form fields submitted when ending PM
            item.KeyboardAda = entity.KeyboardAda;
            if (!string.IsNullOrWhiteSpace(entity.KeyboardBrand)) item.KeyboardBrand = entity.KeyboardBrand;
            if (!string.IsNullOrWhiteSpace(entity.KeyboardKondisi)) item.KeyboardKondisi = entity.KeyboardKondisi;
            item.MouseAda = entity.MouseAda;
            if (!string.IsNullOrWhiteSpace(entity.MouseBrand)) item.MouseBrand = entity.MouseBrand;
            if (!string.IsNullOrWhiteSpace(entity.MouseKondisi)) item.MouseKondisi = entity.MouseKondisi;
            item.MonitorAda = entity.MonitorAda;
            if (!string.IsNullOrWhiteSpace(entity.MonitorBrand)) item.MonitorBrand = entity.MonitorBrand;
            if (!string.IsNullOrWhiteSpace(entity.MonitorModel)) item.MonitorModel = entity.MonitorModel;
            if (!string.IsNullOrWhiteSpace(entity.MonitorKondisi)) item.MonitorKondisi = entity.MonitorKondisi;
            item.ChargerAda = entity.ChargerAda;
            item.TasLaptopAda = entity.TasLaptopAda;
            item.MultiportAda = entity.MultiportAda;

            item.FisikKondisiBaik = entity.FisikKondisiBaik;
            item.UEFIPassword = entity.UEFIPassword;
            item.BootOrderStorage = entity.BootOrderStorage;
            item.BootOrderDisable = entity.BootOrderDisable;
            item.DisableAdmin = entity.DisableAdmin;
            item.DiskCleanUp = entity.DiskCleanUp;
            item.SpaceSSD = entity.SpaceSSD;

            if (entity.Keluhan != null) item.Keluhan = entity.Keluhan;
            if (entity.Remark != null) item.Remark = entity.Remark;
            if (!string.IsNullOrWhiteSpace(entity.PerformedBy)) item.PerformedBy = entity.PerformedBy;

            if (!item.PmStartedAt.HasValue) item.PmStartedAt = DateTime.Now.AddMinutes(-30);
            if (item.StartTime == TimeSpan.Zero) item.StartTime = item.PmStartedAt.Value.TimeOfDay;

            item.PmCompletedAt = DateTime.Now;
            item.EndTime = DateTime.Now.TimeOfDay;
            item.Status = "PmDone";
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            string duration = "";
            if (item.PmStartedAt.HasValue)
            {
                var dur = DateTime.Now - item.PmStartedAt.Value;
                duration = $" Durasi: {(int)dur.TotalMinutes} menit.";
            }

            var formattedEndTime = DateTime.Now.ToString(@"HH\:mm", System.Globalization.CultureInfo.InvariantCulture);
            return Json(new { success = true, message = $"PM selesai pada {formattedEndTime}.{duration}", endTime = formattedEndTime });
        }

        [HttpPost]
        public async Task<IActionResult> ReturnLaptop(Guid id, string? userSignature, bool userNotPresent, string? returnNotes)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            item.ReturnedAt = DateTime.Now;
            item.UserNotPresent = userNotPresent;
            item.ReturnNotes = returnNotes;
            item.Status = "Returned";
            item.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrEmpty(userSignature) && userSignature.StartsWith("data:image"))
                item.UserHandoverSignature = userSignature;

            await _context.SaveChangesAsync();

            string msg = userNotPresent
                ? "Laptop dikembalikan (user tidak ada). Catatan: " + (returnNotes ?? "-")
                : $"Laptop dikembalikan ke {item.NamaUser} dan sudah ditandatangani.";

            return Json(new { success = true, message = msg });
        }

        [HttpPost]
        public async Task<IActionResult> CompletePm(Guid id, string? execSignature,
            bool leaderApproved, string? leaderApprovedBy,
            bool sectionHeadApproved, string? sectionHeadApprovedBy)
        {
            var item = await _context.ScheduledMaints.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            // TTD ITAssetTrack (Sign 1)
            if (!string.IsNullOrEmpty(execSignature) && execSignature.StartsWith("data:image"))
                item.ExecBySignature = execSignature;

            // Approval Checkboxes (Sign 2 & 3)
            if (leaderApproved || item.LeaderApproved)
            {
                item.LeaderApproved = true;
                if (!item.LeaderApprovedAt.HasValue) item.LeaderApprovedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(leaderApprovedBy)) item.LeaderApprovedBy = leaderApprovedBy;
                if (string.IsNullOrWhiteSpace(item.LeaderApprovedBy)) item.LeaderApprovedBy = "Franz";
            }

            if (item.LeaderApproved && (sectionHeadApproved || item.SectionHeadApproved))
            {
                item.SectionHeadApproved = true;
                if (!item.SectionHeadApprovedAt.HasValue) item.SectionHeadApprovedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(sectionHeadApprovedBy)) item.SectionHeadApprovedBy = sectionHeadApprovedBy;
                if (string.IsNullOrWhiteSpace(item.SectionHeadApprovedBy)) item.SectionHeadApprovedBy = "Eddi";
            }
            else if (!item.LeaderApproved)
            {
                item.SectionHeadApproved = false;
                item.SectionHeadApprovedBy = null;
                item.SectionHeadApprovedAt = null;
            }

            // Generate document number if empty
            if (string.IsNullOrEmpty(item.NoDocument))
            {
                int count = await _context.ScheduledMaints.CountAsync(s => s.Status == "Completed") + 1;
                item.NoDocument = $"InfraHub/HQ_DEPT/PM/{DateTime.Now.Year}/{count:D3}";
            }

            item.Status = "Completed";
            item.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"PM untuk {item.NamaUser} ({item.SerialNumber}) berhasil diselesaikan dan ditandatangani." });
        }

        [HttpPost]
        public async Task<IActionResult> UploadPmPhoto(Guid maintId, string photoType, IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return Json(new { success = false, message = "Tidak ada foto yang dipilih." });

            var item = await _context.ScheduledMaints.FindAsync(maintId);
            if (item == null) return Json(new { success = false, message = "Data PM tidak ditemukan." });

            string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "maintenance");
            if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

            string fileName = $"{photoType}_{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            string filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            var photoEntity = new MaintenancePhoto
            {
                Id = Guid.NewGuid(),
                MaintenanceType = "Scheduled",
                MaintenanceId = maintId,
                FilePath = $"/uploads/maintenance/{fileName}",
                PhotoType = photoType,
                Caption = photoType switch
                {
                    "StartPM" => "Foto Evidence Start PM",
                    "EndPM" => "Foto Evidence Selesai PM",
                    "ReturnLaptop" => "Foto Evidence Return Laptop",
                    _ => "Foto Evidence PM"
                }
            };

            _context.MaintenancePhotos.Add(photoEntity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Foto evidence berhasil diupload.", filePath = photoEntity.FilePath, photoId = photoEntity.Id });
        }

        [HttpPost]
        public async Task<IActionResult> DeletePmPhoto(Guid photoId)
        {
            var photo = await _context.MaintenancePhotos.FindAsync(photoId);
            if (photo == null) return Json(new { success = false, message = "Foto tidak ditemukan." });

            // Delete physical file
            var physicalPath = Path.Combine(_env.WebRootPath, photo.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            _context.MaintenancePhotos.Remove(photo);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Foto berhasil dihapus." });
        }

        // ==========================================
        // API UTILITY
        // ==========================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> DevResetPmData(string? date)
        {
            // Safety: only allow in Development or if explicitly requested
            var targetDate = string.IsNullOrEmpty(date)
                ? DateTime.Today
                : DateTime.Parse(date);

            // Find all PM records for the target date
            var records = await _context.ScheduledMaints
                .Where(s => s.PlanDate.HasValue && s.PlanDate.Value.Date == targetDate.Date)
                .ToListAsync();

            if (!records.Any())
                return Json(new { success = false, message = $"Tidak ada data PM untuk tanggal {targetDate:dd MMM yyyy}." });

            var resetCount = 0;
            var photoDeleteCount = 0;

            foreach (var rec in records)
            {
                // Delete associated photos (DB records + physical files)
                var photos = await _context.MaintenancePhotos
                    .Where(p => p.MaintenanceId == rec.Id)
                    .ToListAsync();

                foreach (var photo in photos)
                {
                    var physicalPath = Path.Combine(_env.WebRootPath, photo.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(physicalPath))
                        System.IO.File.Delete(physicalPath);
                    photoDeleteCount++;
                }
                _context.MaintenancePhotos.RemoveRange(photos);

                // Reset PM fields back to Planned (Belum Dihubungi / Menghubungkan)
                rec.Status = "Planned";
                rec.ContactedAt = null;
                rec.ContactedBy = null;
                rec.ContactMethod = null;
                rec.UserResponse = null;
                rec.UserConfirmedAt = null;
                rec.PmStartedAt = null;
                rec.PmCompletedAt = null;
                rec.StartTime = TimeSpan.Zero;
                rec.EndTime = TimeSpan.Zero;
                rec.PerformedBy = null;
                rec.Keluhan = string.Empty;
                rec.Remark = string.Empty;
                rec.ExecBySignature = null;
                rec.UserHandoverSignature = null;
                rec.LeaderSignature = null;
                rec.ItosSectionHeadSignature = null;
                rec.LeaderApproved = false;
                rec.LeaderApprovedBy = null;
                rec.LeaderApprovedAt = null;
                rec.SectionHeadApproved = false;
                rec.SectionHeadApprovedBy = null;
                rec.SectionHeadApprovedAt = null;
                rec.UserNotPresent = false;
                rec.ReturnNotes = null;
                rec.ReturnedAt = null;
                rec.FisikKondisiBaik = false;
                rec.UEFIPassword = false;
                rec.BootOrderStorage = false;
                rec.BootOrderDisable = false;
                rec.DisableAdmin = false;
                rec.DiskCleanUp = false;
                rec.SpaceSSD = false;
                rec.KeyboardAda = false;
                rec.MouseAda = false;
                rec.MonitorAda = false;
                rec.StartTimeRecorded = false;
                rec.UpdatedAt = DateTime.Now;

                resetCount++;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Berhasil reset {resetCount} data PM untuk {targetDate:dd MMM yyyy}. {photoDeleteCount} foto dihapus.",
                resetCount,
                photoDeleteCount,
                date = targetDate.ToString("yyyy-MM-dd")
            });
        }

        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
        public async Task<IActionResult> UnscheduledApprovals(string? tab)
        {
            tab ??= "pending";
            ViewBag.ActiveTab = tab;

            bool isApprover1 = User.IsInRole("Approver1");
            bool isApprover2 = User.IsInRole("Approver2");

            // Fetch all tickets with status Complete
            var query = _context.UnscheduledMaints.Where(u => u.Status == "Complete");

            List<UnscheduledMaint> tickets;

            if (isApprover1)
            {
                if (tab == "pending")
                {
                    tickets = await query.Where(u => !string.IsNullOrEmpty(u.TechSignature) && !u.LeaderApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
                else
                {
                    tickets = await query.Where(u => u.LeaderApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
            }
            else if (isApprover2)
            {
                if (tab == "pending")
                {
                    // Approver2 (Freddy - Dept Head) approves only after Approver1 has approved
                    tickets = await query.Where(u => u.LeaderApproved && !u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
                else
                {
                    tickets = await query.Where(u => u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
            }
            else
            {
                // Admin, Coordinator, Technician, Viewer can see all
                if (tab == "pending")
                {
                    tickets = await query.Where(u => !u.LeaderApproved || !u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
                else
                {
                    tickets = await query.Where(u => u.LeaderApproved && u.SectionHeadApproved).OrderByDescending(u => u.Date).ToListAsync();
                }
            }

            // Also load photos for modal preview if needed
            var ticketIds = tickets.Select(t => t.Id).ToList();
            ViewBag.Photos = await _context.MaintenancePhotos
                .Where(p => p.MaintenanceType == "Unscheduled" && ticketIds.Contains(p.MaintenanceId))
                .ToListAsync();

            return View(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2")]
        public async Task<IActionResult> ApproveUnscheduledTicket(Guid id, string signature)
        {
            var ticket = await _context.UnscheduledMaints.FindAsync(id);
            if (ticket == null) return Json(new { success = false, message = "Tiket perbaikan tidak ditemukan." });

            if (string.IsNullOrEmpty(signature) || !signature.StartsWith("data:image"))
            {
                return Json(new { success = false, message = "Tanda tangan tidak valid." });
            }

            bool isApprover1 = User.IsInRole("Approver1");
            bool isApprover2 = User.IsInRole("Approver2");
            bool isTechnician = User.IsInRole("Technician");

            if (isTechnician)
            {
                // Technician only signs TechSignature
                ticket.TechSignature = signature;
                ticket.UpdatedAt = DateTime.Now;
            }
            else if (isApprover1)
            {
                // Approver 1 signs PIC TEAM signature slot
                ticket.LeaderApproved = true;
                ticket.LeaderSignature = signature;
                ticket.LeaderApprovedBy = User.Identity?.Name ?? "Franz";
                ticket.LeaderApprovedAt = DateTime.Now;
                ticket.UpdatedAt = DateTime.Now;
            }
            else if (isApprover2)
            {
                // Approver 2 signs Section Head signature slot
                if (!ticket.LeaderApproved)
                {
                    return Json(new { success = false, message = "Tiket harus disetujui oleh PIC TEAM (Franz) terlebih dahulu." });
                }
                ticket.SectionHeadApproved = true;
                ticket.SectionHeadSignature = signature;
                ticket.SectionHeadApprovedBy = User.Identity?.Name ?? "Eddi";
                ticket.SectionHeadApprovedAt = DateTime.Now;
                ticket.UpdatedAt = DateTime.Now;
            }
            else
            {
                // Admin / Coordinator
                if (string.IsNullOrEmpty(ticket.TechSignature))
                {
                    ticket.TechSignature = signature;
                }
                else if (!ticket.LeaderApproved)
                {
                    ticket.LeaderApproved = true;
                    ticket.LeaderSignature = signature;
                    ticket.LeaderApprovedBy = User.Identity?.Name ?? "Franz";
                    ticket.LeaderApprovedAt = DateTime.Now;
                }
                else
                {
                    ticket.SectionHeadApproved = true;
                    ticket.SectionHeadSignature = signature;
                    ticket.SectionHeadApprovedBy = User.Identity?.Name ?? "Eddi";
                    ticket.SectionHeadApprovedAt = DateTime.Now;
                }
                ticket.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Persetujuan perbaikan berhasil disimpan." });
        }

        [HttpGet]
        public async Task<IActionResult> GetAssetBySN(string sn)
        {
            if (string.IsNullOrEmpty(sn)) return BadRequest();
            var asset = await _context.Assets
                .Include(a => a.BrandObj)
                .Include(a => a.DeviceTypeObj)
                .FirstOrDefaultAsync(a => a.SerialNumber.ToLower() == sn.ToLower());

            if (asset == null) return NotFound();

            return Json(new
            {
                serialNumber = asset.SerialNumber,
                computerName = asset.ComputerName,
                assignedTo = asset.AssignedTo,
                nrp = asset.NRP,
                site = asset.Site,
                division = asset.Division,
                brand = asset.BrandObj?.Name ?? asset.Brand,
                deviceType = asset.DeviceTypeObj?.Name ?? asset.DeviceType,
                model = asset.Model,
                ram = asset.Ram,
                storage = asset.Storage,
                processor = asset.Processor,
                windowsVersion = asset.WindowsVersion,
                keyboardSN = asset.KeyboardSN,
                mouseSN = asset.MouseSN,
                monitorSN = asset.MonitorSN
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2")]
        public async Task<IActionResult> SaveScheduledSignature(Guid id, string? execSig, string? userSig, bool? userNotPresent, bool? leaderApproved, bool? sectionHeadApproved)
        {
            var ticket = await _context.ScheduledMaints.FindAsync(id);
            if (ticket == null) return Json(new { success = false, message = "PM Terjadwal tidak ditemukan." });

            if (execSig != null)
            {
                ticket.ExecBySignature = execSig;
            }

            if (userSig != null)
            {
                ticket.UserHandoverSignature = userSig;
            }

            if (userNotPresent.HasValue)
            {
                ticket.UserNotPresent = userNotPresent.Value;
                if (userNotPresent.Value && !ticket.ReturnedAt.HasValue) ticket.ReturnedAt = DateTime.Now;
            }

            if (leaderApproved.HasValue && leaderApproved.Value && User.IsInRole("Approver1"))
            {
                ticket.LeaderApproved = true;
                ticket.LeaderApprovedBy = User.Identity?.Name ?? "Franz";
                if (!ticket.LeaderApprovedAt.HasValue) ticket.LeaderApprovedAt = DateTime.Now;
            }

            if (sectionHeadApproved.HasValue && sectionHeadApproved.Value && User.IsInRole("Approver2"))
            {
                ticket.SectionHeadApproved = true;
                ticket.SectionHeadApprovedBy = User.Identity?.Name ?? "Eddi";
                if (!ticket.SectionHeadApprovedAt.HasValue) ticket.SectionHeadApprovedAt = DateTime.Now;
            }

            ticket.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Pengesahan berhasil disimpan." });
        }
    }
}


