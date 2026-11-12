using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
public class ReplayController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
