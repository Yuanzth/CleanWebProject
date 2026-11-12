using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Infrastructure.Auth;

/// <summary>
/// Cookie-based authentication service.
/// Password hashing uses BCrypt (same as Laravel's Hash::make).
/// </summary>
public class CookieAuthService : IAuthService
{
    private readonly AppDbContext _db;

    public CookieAuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Username atau password salah."
            };
        }

        // Verify password using BCrypt (compatible with Laravel's Hash::make)
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Username atau password salah."
            };
        }

        return new AuthResult
        {
            Success = true,
            UserId = user.Id,
            Username = user.Username,
            Name = user.Name,
            RoleName = user.Role?.RoleName,
            RoleId = user.RoleId
        };
    }

    public Task LogoutAsync()
    {
        // Cookie sign-out is handled by the controller via HttpContext.SignOutAsync
        return Task.CompletedTask;
    }
}
