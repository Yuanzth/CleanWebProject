namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_fleet_tracking_logs
/// </summary>
public class FleetTrackingLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime DeviceTime { get; set; }

    // GPS
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Movement
    public float? SpeedKmh { get; set; }
    public float? HeadingDegree { get; set; }
    public float? Altitude { get; set; }

    // GPS Status
    public string? GpsStatus { get; set; }
    public int? SatelliteInUse { get; set; }

    // Network Quality
    public decimal? NetworkRssi { get; set; }
    public decimal? NetworkRsrp { get; set; }
    public decimal? NetworkRsrq { get; set; }
    public decimal? NetworkSinr { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
