namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_tower_logs
/// </summary>
public class TowerLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime DeviceTime { get; set; }

    // Weather & Environment
    public float? WindSpeed { get; set; }
    public float? WindDirection { get; set; }
    public float? Temperature { get; set; }
    public float? Humidity { get; set; }
    public float? Pressure { get; set; }
    public float? Rainfall { get; set; }
    public float? Illuminance { get; set; }
    public float? SolarRadiation { get; set; }
    public float? Pm2_5 { get; set; }
    public float? Pm10 { get; set; }

    // Structural
    public float? TiltX { get; set; }
    public float? TiltY { get; set; }
    public float? GroundResistance { get; set; }
    public int? SpdCount { get; set; }
    public float? LightningCurrent { get; set; }

    // Pre-calculated (ETL)
    public float? DeviationM { get; set; }
    public string? WeatherStatus { get; set; }
    public string? AirQuality { get; set; }
    public string? WindCardinal { get; set; }

    // GPS
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Acknowledge
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
