namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_m_sites
/// Laravel Model: Site
/// </summary>
public class Site
{
    public Guid Id { get; set; }
    public string SiteCode { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Navigation
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<UserAssignment> UserAssignments { get; set; } = new List<UserAssignment>();
    public ICollection<SiteBasemap> SiteBasemaps { get; set; } = new List<SiteBasemap>();
    public ICollection<SiteBoundary> SiteBoundaries { get; set; } = new List<SiteBoundary>();
    public ICollection<SiteCoverage> SiteCoverages { get; set; } = new List<SiteCoverage>();
}
