namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_genset_logs
/// </summary>
public class GensetLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime DeviceTime { get; set; }

    // Params
    public float? Voltage { get; set; }
    public float? Current { get; set; }
    public float? PowerKw { get; set; }
    public float? EnergyKwh { get; set; }
    public float? Frequency { get; set; }
    public float? PowerFactor { get; set; }
    public float? Illuminance { get; set; }
    public float? FuelLevel { get; set; }
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
