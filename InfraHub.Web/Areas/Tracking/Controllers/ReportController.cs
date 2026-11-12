using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
public class ReportController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
