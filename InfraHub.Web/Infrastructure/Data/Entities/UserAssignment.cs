namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_r_user_assignments
/// Laravel Model: UserAssignment
/// </summary>
public class UserAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Site Site { get; set; } = null!;
}
