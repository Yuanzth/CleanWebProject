namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_m_users
/// Laravel Model: User
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public ICollection<UserAssignment> UserAssignments { get; set; } = new List<UserAssignment>();
}
