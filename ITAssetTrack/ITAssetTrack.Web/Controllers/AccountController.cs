using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;
using ITAssetTrack.Infrastructure.Services;
using ITAssetTrack.Models;

namespace ITAssetTrack.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Username dan Password wajib diisi.";
                return View();
            }

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());

            if (user == null || !user.IsActive || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                ViewBag.ErrorMessage = "Username atau Password salah, atau akun Anda dinonaktifkan.";
                return View();
            }

            // Create security claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName),
                new Claim("Username", user.Username),
                new Claim(ClaimTypes.GivenName, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("NRP", user.NRP ?? ""),
                new Claim("Site", user.Site ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            user.LastLoginAt = DateTime.Now;
            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Selamat datang kembali, {user.FullName}!";
            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["LogoutSuccess"] = "Anda telah berhasil keluar dari sistem.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Redirect berdasarkan role
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            return role switch
            {
                "Admin"       => RedirectToAction("AssetDashboard", "Home"),
                "Coordinator" => RedirectToAction("AssetDashboard", "Home"),
                "Technician"  => RedirectToAction("PmExecution", "Maintenance"),
                "Approver1"   => RedirectToAction("PmExecution", "Maintenance", new { tab = "unsigned" }),
                "Approver2"   => RedirectToAction("PmExecution", "Maintenance", new { tab = "unsigned" }),
                "Viewer"      => RedirectToAction("ScheduledList", "Maintenance"),
                _             => RedirectToAction("ScheduledList", "Maintenance")
            };
        }
    }
}
