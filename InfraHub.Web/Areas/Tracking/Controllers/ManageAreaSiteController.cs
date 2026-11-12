using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
[Route("Tracking/Manage/AreaSite")]
public class ManageAreaSiteController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { draw = 1, recordsTotal = 0, recordsFiltered = 0, data = new object[0] });
        }

        ViewBag.Sites = new List<dynamic> 
        {
            new { id = "1", site_code = "SITE01", site_name = "Mock Site 1" }
        };

        return View("~/Areas/Tracking/Views/Manage/AreaSite.cshtml");
    }
}
