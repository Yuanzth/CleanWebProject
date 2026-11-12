namespace InfraHub.Core.Interfaces;

/// <summary>
/// Interface untuk Authentication Service.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync();
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? Name { get; set; }
    public string? RoleName { get; set; }
    public Guid? RoleId { get; set; }
}
