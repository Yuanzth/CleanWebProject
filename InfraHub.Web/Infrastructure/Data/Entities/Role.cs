namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_r_roles
/// Laravel Model: Role
/// </summary>
public class Role
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}
