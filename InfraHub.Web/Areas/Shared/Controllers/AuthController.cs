using System.Security.Claims;
using InfraHub.Core.Helpers;
using InfraHub.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraHub.Web.Areas.Shared.Controllers;

[Area("Shared")]
public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // Jika sudah login, redirect ke default page
        if (User.Identity?.IsAuthenticated ?? false)
        {
            var roleName = User.FindFirst("RoleName")?.Value;
            return Redirect(RoleHelper.GetDefaultRedirectUrl(roleName));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username dan password harus diisi.";
            return View();
        }

        var result = await _authService.LoginAsync(username, password);

        if (!result.Success)
        {
            ViewBag.Error = result.ErrorMessage;
            return View();
        }

        // Build claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()!),
            new(ClaimTypes.Name, result.Username!),
            new("FullName", result.Name!),
            new("RoleName", result.RoleName ?? string.Empty),
            new("RoleId", result.RoleId.ToString() ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });

        // Redirect ke returnUrl atau default page sesuai role
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect(RoleHelper.GetDefaultRedirectUrl(result.RoleName));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
