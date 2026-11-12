using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;
using ITAssetTrack.Models;

namespace ITAssetTrack.Controllers
{
    [Authorize(Roles = "Admin,Coordinator")]
    public class AssetAssignmentController : Controller
    {
        private readonly AppDbContext _context;

        public AssetAssignmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: AssetAssignment
        public IActionResult Index()
        {
            return View();
        }

        // Server-Side DataTables (Yajra Style AJAX Endpoint)
        [HttpPost("/api/asset-assignment/datatables")]
        public async Task<IActionResult> GetAssignmentsDataTable()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var startStr = Request.Form["start"].FirstOrDefault();
            var lengthStr = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim();

            int pageSize = string.IsNullOrEmpty(lengthStr) ? 10 : Convert.ToInt32(lengthStr);
            int skip = string.IsNullOrEmpty(startStr) ? 0 : Convert.ToInt32(startStr);

            var query = _context.AssetAssignments
                .Include(a => a.Asset)
                    .ThenInclude(asset => asset.BrandObj)
                .Include(a => a.Asset)
                    .ThenInclude(asset => asset.DeviceTypeObj)
                .Include(a => a.Employee)
                    .ThenInclude(emp => emp.Site)
                .Include(a => a.Employee)
                    .ThenInclude(emp => emp.Department)
                .Include(a => a.Employee)
                    .ThenInclude(emp => emp.Division)
                .Where(a => a.IsActive)
                .AsQueryable();

            int recordsTotal = await query.CountAsync();

            if (!string.IsNullOrEmpty(searchValue))
            {
                string searchKey = searchValue.ToLower();
                query = query.Where(a =>
                    a.Employee.FullName.ToLower().Contains(searchKey) ||
                    a.Employee.NRP.ToLower().Contains(searchKey) ||
                    a.Asset.SerialNumber.ToLower().Contains(searchKey) ||
                    a.Asset.Model.ToLower().Contains(searchKey) ||
                    (a.Employee.Site != null && a.Employee.Site.Code.ToLower().Contains(searchKey))
                );
            }

            int recordsFiltered = await query.CountAsync();

            var dataList = await query
                .OrderByDescending(a => a.AssignDate)
                .Skip(skip)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    assetId = a.AssetId,
                    employeeName = a.Employee.FullName,
                    department = a.Employee.Department != null ? a.Employee.Department.Name : "-",
                    nrp = string.IsNullOrEmpty(a.Employee.NRP) ? "-" : a.Employee.NRP,
                    site = a.Employee.Site != null ? a.Employee.Site.Code : "-",
                    division = a.Employee.Division != null ? a.Employee.Division.Code : "-",
                    serialNumber = a.Asset.SerialNumber,
                    brandModel = (a.Asset.BrandObj != null ? a.Asset.BrandObj.Name : a.Asset.Brand) + " " + a.Asset.Model,
                    assignDate = a.AssignDate.ToString("dd MMM yyyy")
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

        // GET: AssetAssignment/Assign
        public async Task<IActionResult> Assign()
        {
            ViewBag.AvailableAssets = await _context.Assets
                .Where(a => a.Status == "Available")
                .OrderBy(a => a.SerialNumber)
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .Include(e => e.Site)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            return View();
        }

        // POST: AssetAssignment/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid assetId, Guid employeeId, string? notes)
        {
            var asset = await _context.Assets.FindAsync(assetId);
            var employee = await _context.Employees.FindAsync(employeeId);

            if (asset == null || employee == null)
            {
                TempData["ErrorMessage"] = "Asset atau Employee tidak ditemukan.";
                return RedirectToAction(nameof(Assign));
            }

            // Deactivate any current active assignment for this asset
            var existingAssignment = await _context.AssetAssignments
                .FirstOrDefaultAsync(a => a.AssetId == assetId && a.IsActive);

            if (existingAssignment != null)
            {
                existingAssignment.IsActive = false;
                existingAssignment.ReturnDate = DateTime.Now;
            }

            // Create new assignment
            var assignment = new AssetAssignment
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                EmployeeId = employeeId,
                AssignDate = DateTime.Now,
                IsActive = true,
                Notes = notes ?? string.Empty
            };

            // Update asset status and quick fields
            asset.Status = "InUse";
            asset.AssignedTo = employee.FullName;
            asset.NRP = employee.NRP;

            _context.AssetAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Asset {asset.SerialNumber} berhasil dialokasikan ke {employee.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: AssetAssignment/Return/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(Guid id)
        {
            var assignment = await _context.AssetAssignments
                .Include(a => a.Asset)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
            {
                return Json(new { success = false, message = "Penugasan tidak ditemukan." });
            }

            assignment.IsActive = false;
            assignment.ReturnDate = DateTime.Now;

            if (assignment.Asset != null)
            {
                assignment.Asset.Status = "Available";
                assignment.Asset.AssignedTo = string.Empty;
                assignment.Asset.NRP = string.Empty;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Perangkat berhasil dikembalikan ke status Available." });
        }
    }
}
