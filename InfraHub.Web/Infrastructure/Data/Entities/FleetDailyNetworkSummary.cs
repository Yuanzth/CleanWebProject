namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_fleet_daily_network_summary
/// Laravel Model: FleetDailyNetworkSummary
/// </summary>
public class FleetDailyNetworkSummary
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public Guid DeviceId { get; set; }
    public string? SiteCode { get; set; }
    public int TotalPoints { get; set; }
    public int PointsInsideBoundary { get; set; }
    public int PointsOutsideBoundary { get; set; }
    public int TotalExcellentRsrp { get; set; }
    public int TotalGoodRsrp { get; set; }
    public int TotalFairRsrp { get; set; }
    public int TotalPoorRsrp { get; set; }
    public int TotalVeryPoorRsrp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
