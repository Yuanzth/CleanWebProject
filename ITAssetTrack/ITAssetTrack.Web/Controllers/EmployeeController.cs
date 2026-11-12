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
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _context;

        public EmployeeController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Employee
        public IActionResult Index()
        {
            return View();
        }

        // Server-Side DataTables (Yajra Style AJAX Endpoint)
        [HttpPost("/api/employee/datatables")]
        public async Task<IActionResult> GetEmployeesDataTable()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var startStr = Request.Form["start"].FirstOrDefault();
            var lengthStr = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim();

            int pageSize = string.IsNullOrEmpty(lengthStr) ? 10 : Convert.ToInt32(lengthStr);
            int skip = string.IsNullOrEmpty(startStr) ? 0 : Convert.ToInt32(startStr);

            var query = _context.Employees
                .Include(e => e.Site)
                .Include(e => e.Department)
                .Include(e => e.Division)
                .Include(e => e.AssetAssignments.Where(a => a.IsActive))
                    .ThenInclude(a => a.Asset)
                .AsQueryable();

            int recordsTotal = await query.CountAsync();

            if (!string.IsNullOrEmpty(searchValue))
            {
                string searchKey = searchValue.ToLower();
                query = query.Where(e =>
                    e.FullName.ToLower().Contains(searchKey) ||
                    e.NRP.ToLower().Contains(searchKey) ||
                    (e.Site != null && e.Site.Code.ToLower().Contains(searchKey)) ||
                    (e.Department != null && e.Department.Name.ToLower().Contains(searchKey)) ||
                    (e.Division != null && e.Division.Code.ToLower().Contains(searchKey))
                );
            }

            int recordsFiltered = await query.CountAsync();

            var dataList = await query
                .OrderBy(e => e.FullName)
                .Skip(skip)
                .Take(pageSize)
                .Select(e => new
                {
                    id = e.Id,
                    nrp = string.IsNullOrEmpty(e.NRP) ? "-" : e.NRP,
                    fullName = e.FullName,
                    email = e.Email,
                    phone = e.Phone,
                    site = e.Site != null ? e.Site.Code : "-",
                    department = e.Department != null ? e.Department.Name : "-",
                    division = e.Division != null ? e.Division.Code : "-",
                    activeAssetSN = e.AssetAssignments
                        .Where(a => a.IsActive && a.Asset != null)
                        .Select(a => a.Asset.SerialNumber)
                        .FirstOrDefault() ?? "-"
                })
                .ToListAsync();

            return Json(new
            {
                draw = string.IsNullOrEmpty(draw) ? 0 : Convert.ToInt32(draw),
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = dataList
            });
        }

        // GET: Employee/Details/{id}
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Site)
                .Include(e => e.Department)
                .Include(e => e.Division)
                .Include(e => e.AssetAssignments)
                    .ThenInclude(a => a.Asset)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (employee == null) return NotFound();

            return View(employee);
        }

        // GET: Employee/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Sites = await _context.Sites.OrderBy(s => s.Code).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Divisions = await _context.Divisions.OrderBy(d => d.Code).ToListAsync();
            return View();
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,NRP,FullName,Email,Phone,SiteId,DepartmentId,DivisionId")] Employee employee)
        {
            if (ModelState.IsValid)
            {
                if (employee.Id == Guid.Empty) employee.Id = Guid.NewGuid();
                employee.CreatedAt = DateTime.Now;
                _context.Add(employee);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Employee berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Sites = await _context.Sites.OrderBy(s => s.Code).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Divisions = await _context.Divisions.OrderBy(d => d.Code).ToListAsync();
            return View(employee);
        }

        // GET: Employee/Edit/{id}
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            ViewBag.Sites = await _context.Sites.OrderBy(s => s.Code).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Divisions = await _context.Divisions.OrderBy(d => d.Code).ToListAsync();
            return View(employee);
        }

        // POST: Employee/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,NRP,FullName,Email,Phone,SiteId,DepartmentId,DivisionId")] Employee employee)
        {
            if (id != employee.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    employee.UpdatedAt = DateTime.Now;
                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Data Employee berhasil diperbarui.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Sites = await _context.Sites.OrderBy(s => s.Code).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Divisions = await _context.Divisions.OrderBy(d => d.Code).ToListAsync();
            return View(employee);
        }

        // POST: Employee/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Data Employee berhasil dihapus." });
            }
            return Json(new { success = false, message = "Employee tidak ditemukan." });
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }
    }
}
