using InfraHub.Core.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InfraHub.Web.Filters;

/// <summary>
/// Action Filter yang menggantikan middleware RoleBasedAccess dari Laravel.
/// Cek apakah user yang login memiliki akses ke Area (modul) yang diminta.
/// </summary>
public class RoleBasedAccessFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Skip jika action atau controller memiliki [AllowAnonymous]
        var hasAllowAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em is AllowAnonymousAttribute);
        if (hasAllowAnonymous) return;

        var user = context.HttpContext.User;

        // Skip check jika user belum login — redirect ke login
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new RedirectToActionResult("Login", "Auth", new { area = "Shared" });
            return;
        }

        // Ambil Area name dari route data
        var areaName = context.RouteData.Values["area"]?.ToString();
        if (string.IsNullOrEmpty(areaName)) return; // No area = shared, allow

        // Ambil role dari claims
        var roleName = user.FindFirst("RoleName")?.Value;

        // Cek akses modul
        if (!RoleHelper.CanAccessModule(roleName, areaName))
        {
            // Redirect ke default page sesuai role user
            var defaultUrl = RoleHelper.GetDefaultRedirectUrl(roleName);
            context.Result = new RedirectResult(defaultUrl);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}

/// <summary>
/// Filter untuk mencegah browser back button ke halaman yang di-cache.
/// Setara dengan middleware PreventBackHistory dari Laravel.
/// </summary>
public class NoCacheFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.HttpContext.Response.Headers["Pragma"] = "no-cache";
        context.HttpContext.Response.Headers["Expires"] = "0";
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
