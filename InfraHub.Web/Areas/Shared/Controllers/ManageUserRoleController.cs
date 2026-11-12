using InfraHub.Infrastructure.Data;
using InfraHub.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Shared.Controllers;

[Area("Shared")]
[Authorize]
public class ManageUserRoleController : Controller
{
    private readonly AppDbContext _context;

    public ManageUserRoleController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        // Only Administrator can access this view
        if (User.FindFirst("RoleName")?.Value != "Administrator")
            return Forbid();

        ViewBag.Roles = _context.Roles.ToList();
        ViewBag.Sites = _context.Sites.ToList();
        ViewBag.Users = _context.Users.ToList();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string type)
    {
        if (type == "role")
        {
            var data = await _context.Roles.OrderByDescending(x => x.Id).ToListAsync();
            return Json(new { data = data });
        }
        
        if (type == "user")
        {
            var data = await _context.Users
                .Include(u => u.Role)
                .OrderByDescending(x => x.CreatedAt)
                .Select(u => new {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Username,
                    RoleName = u.Role != null ? u.Role.RoleName : "-"
                })
                .ToListAsync();
            return Json(new { data = data });
        }

        if (type == "assignment")
        {
            var data = await _context.UserAssignments
                .Include(ua => ua.User)
                .Include(ua => ua.Site)
                .OrderByDescending(x => x.Id)
                .Select(ua => new {
                    ua.Id,
                    UserName = ua.User != null ? ua.User.Name : "-",
                    SiteName = ua.Site != null ? ua.Site.SiteName : "-",
                    SiteCode = ua.Site != null ? ua.Site.SiteCode : "-"
                })
                .ToListAsync();
            return Json(new { data = data });
        }

        return BadRequest("Invalid type");
    }

    // User Endpoints
    [HttpGet("Shared/ManageUserRole/User/{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Json(new { id = user.Id, name = user.Name, username = user.Username, email = user.Email, role_id = user.RoleId });
    }

    [HttpPost("Shared/ManageUserRole/User")]
    public async Task<IActionResult> CreateUser([FromForm] User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        
        // In real app, hash password here!
        // user.Password = Hash(user.Password)
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Json(new { success = "User added successfully" });
    }

    [HttpPut("Shared/ManageUserRole/User/{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromForm] User userUpdate)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });

        user.Name = userUpdate.Name;
        user.Username = userUpdate.Username;
        user.Email = userUpdate.Email;
        user.RoleId = userUpdate.RoleId;
        
        if (!string.IsNullOrEmpty(userUpdate.Password))
        {
            user.Password = userUpdate.Password;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Json(new { success = "User updated successfully" });
    }

    [HttpDelete("Shared/ManageUserRole/User/{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Json(new { success = "User deleted successfully" });
    }

    // Assignment Endpoints
    [HttpPost("Shared/ManageUserRole/Assignment")]
    public async Task<IActionResult> CreateAssignment([FromForm] UserAssignment assignment)
    {
        // Check if exists
        bool exists = await _context.UserAssignments.AnyAsync(a => a.UserId == assignment.UserId && a.SiteId == assignment.SiteId);
        if (exists) return BadRequest(new { error = "Assignment already exists!" });

        _context.UserAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        return Json(new { success = "Assignment added successfully" });
    }

    [HttpDelete("Shared/ManageUserRole/Assignment/{id}")]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        var assignment = await _context.UserAssignments.FindAsync(id);
        if (assignment == null) return NotFound(new { error = "Assignment not found" });

        _context.UserAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        return Json(new { success = "Assignment deleted successfully" });
    }
}
