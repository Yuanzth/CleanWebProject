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
using OfficeOpenXml;
using ITAssetTrack.Data;
using ITAssetTrack.Models;

namespace ITAssetTrack.Controllers
{
    [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2,Viewer")]
    public class SerahTerimaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SerahTerimaController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: SerahTerima/Penyerahan
        [Authorize(Roles = "Admin,Coordinator,Technician,Approver1,Approver2")]
        public IActionResult Penyerahan()
        {
            var model = new SerahTerima { Tipe = "Penyerahan" };
            // Auto-generate document number
            var count = _context.SerahTerimas.Count() + 1;
            model.NoDokumen = $"InfraHub/HQ_DEPT/PM{DateTime.Now.Year}/{count:D3}";
            return View(model);
        }

        // POST: SerahTerima/Penyerahan
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator,Technician")]
        public async Task<IActionResult> Penyerahan(SerahTerima model, string submitAction, List<IFormFile>? photos)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            model.Tipe = "Penyerahan";
            model.Status = submitAction == "Draft" ? "Draft" : "Submitted";
            model.CreatedAt = DateTime.Now;

            // Ensure non-null for required strings if somehow they slip through
            model.NamaUser ??= string.Empty;
            model.NRP ??= string.Empty;
            model.Site ??= string.Empty;
            model.Department ??= string.Empty;
            model.Division ??= string.Empty;
            model.NoDokumen ??= string.Empty;
            
            model.ComputerName ??= string.Empty;
            model.DeviceType ??= string.Empty;
            model.Brand ??= string.Empty;
            model.Series ??= string.Empty;
            model.SerialNumber ??= string.Empty;
            model.Ram ??= string.Empty;
            model.Storage ??= string.Empty;
            model.OperatingSystem ??= string.Empty;
            
            model.KeyboardSN ??= string.Empty;
            model.MouseSN ??= string.Empty;
            model.MonitorSN ??= string.Empty;
            model.AdditionalHardware ??= string.Empty;
            model.Alasan ??= string.Empty;
            model.Catatan ??= string.Empty;

            model.NamaPenyerah ??= string.Empty;
            model.NamaPenerima ??= string.Empty;
            model.NamaDiketahui ??= string.Empty;

            _context.SerahTerimas.Add(model);

            // Update or Create Asset
            if (submitAction != "Draft")
            {
                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == model.SerialNumber);
                if (asset == null)
                {
                    asset = new Asset
                    {
                        Id = Guid.NewGuid(),
                        SerialNumber = model.SerialNumber,
                        DeviceType = model.DeviceType,
                        Series = model.Series,
                        ComputerName = model.ComputerName,
                        OperatingSystem = model.OperatingSystem,
                        Ram = model.Ram,
                        Storage = model.Storage,
                        KeyboardSN = model.KeyboardSN,
                        MouseSN = model.MouseSN,
                        MonitorSN = model.MonitorSN,
                        AdditionalHardware = model.AdditionalHardware,
                        Division = model.Division,
                        Site = model.Site,
                        AssignedTo = model.NamaUser,
                        NRP = model.NRP,
                        Status = "InUse"
                    };
                    _context.Assets.Add(asset);
                }
                else
                {
                    asset.AssignedTo = model.NamaUser;
                    asset.NRP = model.NRP;
                    asset.Division = model.Division;
                    asset.Site = model.Site;
                    asset.ComputerName = model.ComputerName;
                    asset.DeviceType = model.DeviceType;
                    asset.Series = model.Series;
                    asset.Ram = model.Ram;
                    asset.Storage = model.Storage;
                    asset.OperatingSystem = model.OperatingSystem;
                    asset.KeyboardSN = model.KeyboardSN;
                    asset.MouseSN = model.MouseSN;
                    asset.MonitorSN = model.MonitorSN;
                    asset.AdditionalHardware = model.AdditionalHardware;
                    asset.Status = "InUse";
                }
            }

            await _context.SaveChangesAsync(); // Save to get the model.Id for photos

            if (photos != null && photos.Count > 0)
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "serahterima");
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

                        _context.SerahTerimaPhotos.Add(new SerahTerimaPhoto
                        {
                            Id = Guid.NewGuid(),
                            SerahTerimaId = model.Id,
                            FilePath = $"/uploads/serahterima/{fileName}"
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = submitAction == "Draft" ? "Draft disimpan." : "Serah Terima berhasil disubmit beserta lampiran foto.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var nrp = User.FindFirst("NRP")?.Value ?? "";

            IQueryable<SerahTerima> query = _context.SerahTerimas;

            if (role == "Viewer")
            {
                query = query.Where(s => s.NRP == nrp);
            }

            var list = await query
                                     .OrderByDescending(s => s.Date)
                                     .ToListAsync();

            // Load associated photo evidence
            var ids = list.Select(s => s.Id).ToList();
            var photos = await _context.SerahTerimaPhotos
                                       .Where(p => ids.Contains(p.SerahTerimaId))
                                       .ToListAsync();

            ViewBag.Photos = photos.GroupBy(p => p.SerahTerimaId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

            return View(list);
        }

        // GET: SerahTerima/Pengembalian
        [Authorize(Roles = "Admin,Coordinator,Technician")]
        public async Task<IActionResult> Pengembalian(Guid? id, string? sn, string? nrp)
        {
            var model = new SerahTerima { Tipe = "Pengembalian", Tanggal = DateTime.Today };
            var count = await _context.SerahTerimas.CountAsync() + 1;
            model.NoDokumen = $"InfraHub/HQ_DEPT/PM{DateTime.Now.Year}/{count:D3}";

            SerahTerima? penyerahan = null;

            // 1. Check by ID (Penyerahan record ID)
            if (id.HasValue && id.Value != Guid.Empty)
            {
                penyerahan = await _context.SerahTerimas.FirstOrDefaultAsync(s => s.Id == id.Value);
            }

            // 2. Check by Serial Number
            if (penyerahan == null && !string.IsNullOrWhiteSpace(sn))
            {
                penyerahan = await _context.SerahTerimas
                    .Where(s => s.SerialNumber == sn && s.Tipe == "Penyerahan")
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            // 3. Check by NRP
            if (penyerahan == null && !string.IsNullOrWhiteSpace(nrp))
            {
                penyerahan = await _context.SerahTerimas
                    .Where(s => s.NRP == nrp && s.Tipe == "Penyerahan")
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            // Also check master Asset table if penyerahan record not found directly
            var targetSN = penyerahan?.SerialNumber ?? sn;
            var asset = !string.IsNullOrWhiteSpace(targetSN) 
                ? await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == targetSN)
                : null;

            // Pre-fill model if penyerahan or asset exists
            if (penyerahan != null)
            {
                model.NamaUser = penyerahan.NamaUser;
                model.NRP = penyerahan.NRP;
                model.Site = penyerahan.Site;
                model.Division = penyerahan.Division;
                model.Department = penyerahan.Department;

                model.SerialNumber = penyerahan.SerialNumber;
                model.ComputerName = penyerahan.ComputerName;
                model.Series = penyerahan.Series;
                model.DeviceType = penyerahan.DeviceType;
                model.Ram = penyerahan.Ram;
                model.Storage = penyerahan.Storage;
                model.OperatingSystem = penyerahan.OperatingSystem;

                model.KeyboardSN = penyerahan.KeyboardSN;
                model.MouseSN = penyerahan.MouseSN;
                model.MonitorSN = penyerahan.MonitorSN;
                model.AdditionalHardware = penyerahan.AdditionalHardware;
            }
            else if (asset != null)
            {
                model.NamaUser = asset.AssignedTo;
                model.NRP = asset.NRP;
                model.Site = asset.Site;
                model.Division = asset.Division;
                model.Department = asset.Department;

                model.SerialNumber = asset.SerialNumber;
                model.ComputerName = asset.ComputerName;
                model.Series = asset.Series;
                model.DeviceType = asset.DeviceType;
                model.Ram = asset.Ram;
                model.Storage = asset.Storage;
                model.OperatingSystem = asset.WindowsVersion ?? asset.OperatingSystem;

                model.KeyboardSN = asset.KeyboardSN;
                model.MouseSN = asset.MouseSN;
                model.MonitorSN = asset.MonitorSN;
                model.AdditionalHardware = asset.AdditionalHardware;
            }

            return View(model);
        }

        // POST: SerahTerima/Pengembalian
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Coordinator,Technician")]
        public async Task<IActionResult> Pengembalian(SerahTerima model, string submitAction, List<IFormFile>? photos)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            model.Tipe = "Pengembalian";
            model.Status = submitAction == "Draft" ? "Draft" : "Submitted";
            model.CreatedAt = DateTime.Now;

            model.NamaUser ??= string.Empty;
            model.NRP ??= string.Empty;
            model.Site ??= string.Empty;
            model.Department ??= string.Empty;
            model.Division ??= string.Empty;
            model.NoDokumen ??= string.Empty;
            
            model.SerialNumber ??= string.Empty;
            
            model.ComputerName ??= string.Empty;
            model.DeviceType ??= string.Empty;
            model.Brand ??= string.Empty;
            model.Series ??= string.Empty;
            model.Ram ??= string.Empty;
            model.Storage ??= string.Empty;
            model.OperatingSystem ??= string.Empty;
            
            model.KeyboardSN ??= string.Empty;
            model.MouseSN ??= string.Empty;
            model.MonitorSN ??= string.Empty;
            model.AdditionalHardware ??= string.Empty;
            model.Alasan ??= string.Empty;
            model.Catatan ??= string.Empty;

            model.NamaPenyerah ??= string.Empty;
            model.NamaPenerima ??= string.Empty;
            model.NamaDiketahui ??= string.Empty;

            _context.SerahTerimas.Add(model);

            if (submitAction != "Draft")
            {
                var asset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == model.SerialNumber);
                if (asset != null)
                {
                    asset.Status = "Returned";
                    asset.AssignedTo = "";
                    asset.NRP = "";
                }
            }

            await _context.SaveChangesAsync();

            if (photos != null && photos.Count > 0)
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "serahterima");
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

                        _context.SerahTerimaPhotos.Add(new SerahTerimaPhoto
                        {
                            Id = Guid.NewGuid(),
                            SerahTerimaId = model.Id,
                            FilePath = $"/uploads/serahterima/{fileName}"
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = submitAction == "Draft" ? "Draft disimpan." : "Pengembalian berhasil disubmit beserta foto.";
            return RedirectToAction(nameof(Index));
        }

        // GET: SerahTerima/GetAssetBySN
        [HttpGet]
        public async Task<IActionResult> GetAssetBySN(string sn)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return Json(null);

            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == sn);
            if (asset == null)
                return Json(null);

            var latest = await _context.SerahTerimas
                .Where(s => s.SerialNumber == sn && s.Tipe == "Penyerahan")
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            return Json(new
            {
                namaUser = !string.IsNullOrEmpty(asset.AssignedTo) ? asset.AssignedTo : (latest?.NamaUser ?? ""),
                computerName = asset.ComputerName,
                site = !string.IsNullOrEmpty(asset.Site) ? asset.Site : (latest?.Site ?? ""),
                division = !string.IsNullOrEmpty(asset.Division) ? asset.Division : (latest?.Division ?? ""),
                deviceType = asset.DeviceType,
                series = asset.Series,
                serialNumber = asset.SerialNumber,
                ram = asset.Ram,
                storage = asset.Storage,
                operatingSystem = asset.WindowsVersion ?? asset.OperatingSystem,
                keyboardSN = asset.KeyboardSN,
                mouseSN = asset.MouseSN,
                monitorSN = asset.MonitorSN,
                additionalHardware = asset.AdditionalHardware,
                nrp = !string.IsNullOrEmpty(asset.NRP) ? asset.NRP : (latest?.NRP ?? "")
            });
        }

        // GET: SerahTerima/GetAssetByNRP
        [HttpGet]
        public async Task<IActionResult> GetAssetByNRP(string nrp)
        {
            if (string.IsNullOrWhiteSpace(nrp))
                return Json(null);

            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.NRP == nrp && a.Status == "InUse");
            if (asset == null)
            {
                asset = await _context.Assets.FirstOrDefaultAsync(a => a.NRP == nrp);
            }

            var latest = await _context.SerahTerimas
                .Where(s => s.NRP == nrp && s.Tipe == "Penyerahan")
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (asset == null && latest == null)
                return Json(null);

            return Json(new
            {
                namaUser = asset?.AssignedTo ?? latest?.NamaUser ?? "",
                computerName = asset?.ComputerName ?? latest?.ComputerName ?? "",
                site = asset?.Site ?? latest?.Site ?? "",
                division = asset?.Division ?? latest?.Division ?? "",
                deviceType = asset?.DeviceType ?? latest?.DeviceType ?? "",
                series = asset?.Series ?? latest?.Series ?? "",
                serialNumber = asset?.SerialNumber ?? latest?.SerialNumber ?? "",
                ram = asset?.Ram ?? latest?.Ram ?? "",
                storage = asset?.Storage ?? latest?.Storage ?? "",
                operatingSystem = asset?.OperatingSystem ?? latest?.OperatingSystem ?? "",
                keyboardSN = asset?.KeyboardSN ?? latest?.KeyboardSN ?? "",
                mouseSN = asset?.MouseSN ?? latest?.MouseSN ?? "",
                monitorSN = asset?.MonitorSN ?? latest?.MonitorSN ?? "",
                additionalHardware = asset?.AdditionalHardware ?? latest?.AdditionalHardware ?? "",
                nrp = nrp
            });
        }

        // ─── API: Autocomplete – Return semua SN + label singkat ────────────────
        [HttpGet]
        public async Task<IActionResult> GetAllSN(string? q)
        {
            var query = _context.Assets.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a => a.SerialNumber.Contains(q) || (a.ComputerName != null && a.ComputerName.Contains(q)));

            var list = await query
                .OrderBy(a => a.SerialNumber)
                .Take(50)
                .Select(a => new {
                    sn = a.SerialNumber,
                    label = $"{a.SerialNumber} — {a.ComputerName ?? ""} ({a.Series ?? ""})"
                })
                .ToListAsync();

            return Json(list);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportKepemilikan(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid excel file.";
                return RedirectToAction("Index");
            }

            ExcelPackage.License.SetNonCommercialOrganization("InfraHub");

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        TempData["Error"] = "Excel file is empty.";
                        return RedirectToAction("Index");
                    }

                    int rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var sn = worksheet.Cells[row, 1].Text.Trim();
                        var namaUser = worksheet.Cells[row, 2].Text.Trim();
                        var nrp = worksheet.Cells[row, 3].Text.Trim();
                        var division = worksheet.Cells[row, 4].Text.Trim();

                        if (string.IsNullOrEmpty(sn) || string.IsNullOrEmpty(namaUser)) continue;

                        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == sn);
                        if (asset != null)
                        {
                            // Update Asset
                            asset.AssignedTo = namaUser;
                            asset.NRP = nrp;
                            asset.Division = division;
                            asset.Status = "InUse";

                            // Create SerahTerima Penyerahan Record
                            var count = await _context.SerahTerimas.CountAsync() + 1;
                            var st = new SerahTerima
                            {
                                Id = Guid.NewGuid(),
                                Tipe = "Penyerahan",
                                NoDokumen = $"InfraHub/HQ_DEPT/PM{DateTime.Now.Year}/{count:D3}",
                                Tanggal = DateTime.Now,
                                NamaUser = namaUser,
                                NRP = nrp,
                                Division = division,
                                Site = asset.Site,
                                ComputerName = asset.ComputerName,
                                DeviceType = asset.DeviceType,
                                Series = asset.Series,
                                SerialNumber = sn,
                                Ram = asset.Ram,
                                Storage = asset.Storage,
                                OperatingSystem = asset.OperatingSystem,
                                AdditionalHardware = "Laptop, Charger, Tas (Imported)",
                                Status = "Submitted",
                                CreatedAt = DateTime.Now
                            };
                            _context.SerahTerimas.Add(st);
                        }
                    }
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Data kepemilikan berhasil di-import.";
            return RedirectToAction("Index");
        }

        // GET: SerahTerima/Print/{id}
        [AllowAnonymous]
        public async Task<IActionResult> Print(Guid id)
        {
            var st = await _context.SerahTerimas.FirstOrDefaultAsync(s => s.Id == id);
            if (st == null) return NotFound();

            ViewBag.Photos = await _context.SerahTerimaPhotos
                .Where(p => p.SerahTerimaId == st.Id)
                .ToListAsync();

            return View(st);
        }

        // GET: SerahTerima/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var st = await _context.SerahTerimas.FindAsync(id);
            if (st == null) return NotFound();

            ViewBag.Photos = await _context.SerahTerimaPhotos
                .Where(p => p.SerahTerimaId == st.Id)
                .ToListAsync();

            // Viewer hanya boleh membuka jika itu dokumen Pengembalian miliknya yang belum ditandatangani
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var nrp = User.FindFirst("NRP")?.Value ?? "";
            if (role == "Viewer")
            {
                if (st.NRP != nrp || st.Tipe != "Pengembalian" || !string.IsNullOrEmpty(st.SignaturePenerima))
                {
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(st);
        }

        // POST: SerahTerima/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, string? SignaturePenyerahData, string? SignaturePenerimaData, string? SignatureDiketahuiData, string? NamaPenyerah, string? NamaPenerima, string? NamaDiketahui)
        {
            var st = await _context.SerahTerimas.FindAsync(id);
            if (st == null) return NotFound();

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var nrp = User.FindFirst("NRP")?.Value ?? "";
            if (role == "Viewer")
            {
                if (st.NRP != nrp || st.Tipe != "Pengembalian" || !string.IsNullOrEmpty(st.SignaturePenerima))
                {
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!string.IsNullOrEmpty(SignaturePenyerahData)) {
                st.SignaturePenyerah = SignaturePenyerahData;
                if (!string.IsNullOrEmpty(NamaPenyerah)) st.NamaPenyerah = NamaPenyerah;
            }
            if (!string.IsNullOrEmpty(SignaturePenerimaData)) {
                st.SignaturePenerima = SignaturePenerimaData;
                if (!string.IsNullOrEmpty(NamaPenerima)) st.NamaPenerima = NamaPenerima;
            }
            if (!string.IsNullOrEmpty(SignatureDiketahuiData)) {
                st.SignatureDiketahui = SignatureDiketahuiData;
                if (!string.IsNullOrEmpty(NamaDiketahui)) st.NamaDiketahui = NamaDiketahui;
            }

            st.UpdatedAt = DateTime.Now;
            
            // Jika semua tanda tangan lengkap, ubah status menjadi Submitted
            if (!string.IsNullOrEmpty(st.SignaturePenyerah) && 
                !string.IsNullOrEmpty(st.SignaturePenerima) && 
                !string.IsNullOrEmpty(st.SignatureDiketahui))
            {
                st.Status = "Submitted";
            }

            _context.SerahTerimas.Update(st);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tanda tangan berhasil disimpan.";
            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public async Task<IActionResult> SeedHandover()
        {
            var existing = await _context.SerahTerimas.Where(s => s.NRP == "P000004").ToListAsync();
            if (existing.Any())
            {
                var existingIds = existing.Select(e => e.Id).ToList();
                var existingPhotos = await _context.SerahTerimaPhotos.Where(p => existingIds.Contains(p.SerahTerimaId)).ToListAsync();
                _context.SerahTerimaPhotos.RemoveRange(existingPhotos);
                _context.SerahTerimas.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }

            var penyerahan = new SerahTerima
            {
                Id = Guid.NewGuid(),
                Tipe = "Penyerahan",
                NoDokumen = "InfraHub/HQ_DEPT/ST2026/001",
                Tanggal = DateTime.Now.AddMonths(-3),
                NamaUser = "Auditor / Guest Viewer",
                NRP = "P000004",
                Division = "ITOS",
                Site = "HQ_DEPT",
                ComputerName = "HQ_DEPTLAPGUEST01",
                DeviceType = "Notebook",
                Series = "Latitude 5430",
                SerialNumber = "SN-GUEST-LAPTOP-12345",
                Ram = "16 GB",
                Storage = "512 GB SSD",
                OperatingSystem = "Windows 11 Pro",
                AdditionalHardware = "Laptop, Charger, Tas, Keyboard",
                Status = "Submitted",
                SignaturePenyerah = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,25 Q30,5 50,25 T90,25' fill='none' stroke='black' stroke-width='2'/></svg>",
                SignaturePenerima = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,25 L90,25' fill='none' stroke='black' stroke-width='2'/></svg>",
                SignatureDiketahui = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,10 L90,40' fill='none' stroke='black' stroke-width='2'/></svg>",
                NamaPenyerah = "ITAssetTrack Technician",
                NamaPenerima = "Auditor / Guest Viewer",
                NamaDiketahui = "ITOS Leader",
                CreatedAt = DateTime.Now.AddMonths(-3)
            };

            var pengembalian = new SerahTerima
            {
                Id = Guid.NewGuid(),
                Tipe = "Pengembalian",
                NoDokumen = "InfraHub/HQ_DEPT/ST2026/002",
                Tanggal = DateTime.Now,
                NamaUser = "Auditor / Guest Viewer",
                NRP = "P000004",
                Division = "ITOS",
                Site = "HQ_DEPT",
                ComputerName = "HQ_DEPTLAPGUEST01",
                DeviceType = "Notebook",
                Series = "Latitude 5430",
                SerialNumber = "SN-GUEST-LAPTOP-12345",
                Ram = "16 GB",
                Storage = "512 GB SSD",
                OperatingSystem = "Windows 11 Pro",
                AdditionalHardware = "Laptop, Charger, Tas",
                Status = "Draft",
                SignaturePenyerah = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,25 Q30,5 50,25 T90,25' fill='none' stroke='black' stroke-width='2'/></svg>",
                NamaPenyerah = "ITAssetTrack Technician",
                CreatedAt = DateTime.Now
            };

            _context.SerahTerimas.AddRange(penyerahan, pengembalian);
            await _context.SaveChangesAsync();

            var photo1 = new SerahTerimaPhoto
            {
                Id = Guid.NewGuid(),
                SerahTerimaId = penyerahan.Id,
                FilePath = "https://images.unsplash.com/photo-1593642632823-8f785ba67e45?w=500&auto=format&fit=crop",
                Caption = "Kondisi Fisik Laptop Penyerahan"
            };
            var photo2 = new SerahTerimaPhoto
            {
                Id = Guid.NewGuid(),
                SerahTerimaId = penyerahan.Id,
                FilePath = "https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=500&auto=format&fit=crop",
                Caption = "Kelengkapan Charger & Tas"
            };
            
            var photo3 = new SerahTerimaPhoto
            {
                Id = Guid.NewGuid(),
                SerahTerimaId = pengembalian.Id,
                FilePath = "https://images.unsplash.com/photo-1593642632823-8f785ba67e45?w=500&auto=format&fit=crop",
                Caption = "Kondisi Fisik Laptop Pengembalian"
            };

            _context.SerahTerimaPhotos.AddRange(photo1, photo2, photo3);
            await _context.SaveChangesAsync();

            return Content("Success seeding 1 Penyerahan and 1 Pengembalian with photos for Viewer (P000004)!");
        }
    }
}
