namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_m_zones
/// Laravel Model: Zone
/// </summary>
public class Zone
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Site Site { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
