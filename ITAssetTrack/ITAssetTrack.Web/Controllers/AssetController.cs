using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;
using ITAssetTrack.Models;
using ITAssetTrack.Services;

namespace ITAssetTrack.Controllers
{
    [Authorize(Roles = "Admin,Coordinator,Technician")]
    public class AssetController : Controller
    {
        private readonly AppDbContext _context;

        public AssetController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Asset
        public async Task<IActionResult> Index(string? statusFilter)
        {
            ViewBag.CurrentFilter = statusFilter ?? "All";

            // Counts for summary pills
            ViewBag.TotalAssets = await _context.Assets.CountAsync();
            ViewBag.AvailableCount = await _context.Assets.CountAsync(a => a.Status == "Available");
            ViewBag.InUseCount = await _context.Assets.CountAsync(a => a.Status == "InUse");
            ViewBag.MaintenanceCount = await _context.Assets.CountAsync(a => a.Status == "UnderMaintenance");

            return View();
        }

        // Server-Side DataTables (Yajra Style AJAX Endpoint)
        [HttpPost("/api/asset/datatables")]
        public async Task<IActionResult> GetAssetsDataTable()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var startStr = Request.Form["start"].FirstOrDefault();
            var lengthStr = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim();
            var statusFilter = Request.Form["statusFilter"].FirstOrDefault()?.Trim();

            int pageSize = string.IsNullOrEmpty(lengthStr) ? 10 : Convert.ToInt32(lengthStr);
            int skip = string.IsNullOrEmpty(startStr) ? 0 : Convert.ToInt32(startStr);

            var query = _context.Assets.AsNoTracking();

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            int recordsTotal = await query.CountAsync();

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(a =>
                    a.SerialNumber.Contains(searchValue) ||
                    a.NoSN.Contains(searchValue) ||
                    a.Model.Contains(searchValue) ||
                    a.ComputerName.Contains(searchValue) ||
                    a.AssignedTo.Contains(searchValue) ||
                    a.NRP.Contains(searchValue) ||
                    a.DeviceType.Contains(searchValue) ||
                    a.Brand.Contains(searchValue)
                );
            }

            int recordsFiltered = await query.CountAsync();

            var dataList = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    serialNumber = a.SerialNumber,
                    noSN = a.NoSN,
                    computerName = a.ComputerName,
                    model = a.Model,
                    deviceType = a.DeviceTypeObj != null ? a.DeviceTypeObj.Name : a.DeviceType,
                    brand = a.BrandObj != null ? a.BrandObj.Name : a.Brand,
                    ram = a.Ram,
                    storage = a.Storage,
                    assignedTo = string.IsNullOrEmpty(a.AssignedTo) ? "-" : a.AssignedTo,
                    status = a.Status
                })
                .ToListAsync();

            return Json(new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = dataList
            });
        }

        // GET: Asset/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var asset = await _context.Assets
                .Include(a => a.DeviceTypeObj)
                .Include(a => a.DeviceCategoryObj)
                .Include(a => a.BrandObj)
                .Include(a => a.VendorObj)
                .Include(a => a.AssetAssignments)
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Site)
                .Include(a => a.AssetAssignments)
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Department)
                .Include(a => a.AssetAssignments)
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Division)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (asset == null) return NotFound();

            return View(asset);
        }

        // GET: Asset/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.DeviceTypes = await _context.DeviceTypes.OrderBy(t => t.Name).ToListAsync();
            ViewBag.DeviceCategories = await _context.DeviceCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Vendors = await _context.Vendors.OrderBy(v => v.Name).ToListAsync();
            return View();
        }

        // POST: Asset/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Asset asset)
        {
            if (ModelState.IsValid)
            {
                if (asset.Id == Guid.Empty) asset.Id = Guid.NewGuid();
                asset.CreatedAt = DateTime.Now;

                // Sync fallback string names for quick view rendering
                if (asset.DeviceTypeId.HasValue)
                {
                    var dt = await _context.DeviceTypes.FindAsync(asset.DeviceTypeId.Value);
                    if (dt != null) asset.DeviceType = dt.Name;
                }
                if (asset.BrandId.HasValue)
                {
                    var b = await _context.Brands.FindAsync(asset.BrandId.Value);
                    if (b != null) asset.Brand = b.Name;
                }

                _context.Add(asset);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Asset baru berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.DeviceTypes = await _context.DeviceTypes.OrderBy(t => t.Name).ToListAsync();
            ViewBag.DeviceCategories = await _context.DeviceCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Vendors = await _context.Vendors.OrderBy(v => v.Name).ToListAsync();
            return View(asset);
        }

        // GET: Asset/Edit/{id}
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var asset = await _context.Assets.FindAsync(id);
            if (asset == null) return NotFound();

            ViewBag.DeviceTypes = await _context.DeviceTypes.OrderBy(t => t.Name).ToListAsync();
            ViewBag.DeviceCategories = await _context.DeviceCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Vendors = await _context.Vendors.OrderBy(v => v.Name).ToListAsync();
            return View(asset);
        }

        // POST: Asset/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id, Asset asset)
        {
            if (id != asset.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    asset.UpdatedAt = DateTime.Now;
                    _context.Update(asset);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Data asset berhasil diperbarui.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssetExists(asset.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.DeviceTypes = await _context.DeviceTypes.OrderBy(t => t.Name).ToListAsync();
            ViewBag.DeviceCategories = await _context.DeviceCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();
            ViewBag.Vendors = await _context.Vendors.OrderBy(v => v.Name).ToListAsync();
            return View(asset);
        }

        // POST: Asset/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset != null)
            {
                _context.Assets.Remove(asset);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Asset berhasil dihapus." });
            }
            return Json(new { success = false, message = "Asset tidak ditemukan." });
        }

        // POST: Asset/ImportExcel
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            var importService = new AssetExcelImportService(_context);
            AssetExcelImportResult result;

            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                result = await importService.ImportExcelStreamAsync(stream);
            }
            else
            {
                // Auto fallback to local root master excel file
                string defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Asset PC & NB_HQ_DEPT.xlsx");
                if (!System.IO.File.Exists(defaultPath))
                {
                    defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "Asset PC & NB_HQ_DEPT.xlsx");
                }
                result = await importService.ImportExcelAsync(defaultPath);
            }

            TempData["SuccessMessage"] = string.Join(" ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // API Endpoint: GET /api/asset/lookup-by-sn/{sn}
        [HttpGet("/api/asset/lookup-by-sn/{sn}")]
        public async Task<IActionResult> LookupBySN(string sn)
        {
            if (string.IsNullOrWhiteSpace(sn)) return BadRequest(new { message = "Serial Number tidak boleh kosong." });

            var asset = await _context.Assets
                .Include(a => a.BrandObj)
                .Include(a => a.DeviceTypeObj)
                .Include(a => a.AssetAssignments.Where(assign => assign.IsActive))
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Site)
                .Include(a => a.AssetAssignments.Where(assign => assign.IsActive))
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Department)
                .Include(a => a.AssetAssignments.Where(assign => assign.IsActive))
                    .ThenInclude(assign => assign.Employee)
                        .ThenInclude(emp => emp.Division)
                .FirstOrDefaultAsync(a => a.SerialNumber.ToLower() == sn.Trim().ToLower() || a.NoSN.ToLower() == sn.Trim().ToLower());

            if (asset == null) return NotFound(new { message = $"Asset dengan SN '{sn}' tidak ditemukan." });

            var activeAssignment = asset.AssetAssignments.FirstOrDefault(a => a.IsActive);
            var emp = activeAssignment?.Employee;

            return Json(new
            {
                assetId = asset.Id,
                serialNumber = asset.SerialNumber,
                noSN = asset.NoSN,
                computerName = asset.ComputerName,
                model = asset.Model,
                deviceType = asset.DeviceTypeObj?.Name ?? asset.DeviceType,
                brand = asset.BrandObj?.Name ?? asset.Brand,
                ram = asset.Ram,
                storage = asset.Storage,
                processor = asset.Processor,
                gpu = asset.Gpu,
                operatingSystem = asset.OperatingSystem,
                windowsVersion = asset.WindowsVersion,
                status = asset.Status,
                assignedTo = emp?.FullName ?? asset.AssignedTo,
                nrp = emp?.NRP ?? asset.NRP,
                site = emp?.Site?.Code ?? asset.Site,
                department = emp?.Department?.Name ?? asset.Department,
                division = emp?.Division?.Code ?? asset.Division
            });
        }


        private bool AssetExists(Guid id)
        {
            return _context.Assets.Any(e => e.Id == id);
        }
    }
}
