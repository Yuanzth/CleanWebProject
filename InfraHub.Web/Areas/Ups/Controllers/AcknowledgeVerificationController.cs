using System;
using System.Linq;
using System.Threading.Tasks;
using InfraHub.Core.Helpers;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Ups.Controllers;

[Area("Ups")]
[Authorize]
[Route("[area]/Acknowledge")]
[Route("[area]/AcknowledgeVerification")]
public class AcknowledgeVerificationController : Controller
{
    private readonly AppDbContext _context;

    public AcknowledgeVerificationController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var roleName = User.FindFirst("RoleName")?.Value ?? "";
        ViewBag.IsAdmin = RoleHelper.IsAdmin(roleName);
        ViewBag.RoleName = roleName;

        var sites = await _context.Sites.OrderBy(s => s.SiteCode).ToListAsync();
        ViewBag.Sites = sites;

        return View("~/Areas/Ups/Views/AcknowledgeVerification/Index.cshtml");
    }
}
