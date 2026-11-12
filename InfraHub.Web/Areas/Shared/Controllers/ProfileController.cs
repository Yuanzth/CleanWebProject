using System.Security.Claims;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Shared.Controllers;

[Area("Shared")]
[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var username = User.Identity?.Name;
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
            return RedirectToAction("Login", "Auth");

        return View(user);
    }
}
