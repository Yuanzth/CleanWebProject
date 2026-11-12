namespace InfraHub.Infrastructure.Data.Entities;

public class SiteBasemap
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string? ProviderName { get; set; }
    public string? FilePath { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Site Site { get; set; } = null!;
}

public class SiteBoundary
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string? ProviderName { get; set; }
    public string? FilePath { get; set; }
    public string? PolygonData { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Site Site { get; set; } = null!;
}

public class SiteCoverage
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string? ProviderName { get; set; }
    public string? FilePath { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Site Site { get; set; } = null!;
}
