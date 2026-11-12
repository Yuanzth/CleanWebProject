namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_ups_logs
/// </summary>
public class UpsLog
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime DeviceTime { get; set; }

    // Input (PLN)
    public float? VoltageInA { get; set; }
    public float? VoltageInB { get; set; }
    public float? VoltageInC { get; set; }
    public float? CurrentInA { get; set; }
    public float? CurrentInB { get; set; }
    public float? CurrentInC { get; set; }
    public float? InputFrequency { get; set; }

    // Output (Beban)
    public float? VoltageOutA { get; set; }
    public float? VoltageOutB { get; set; }
    public float? VoltageOutC { get; set; }
    public float? CurrentOutA { get; set; }
    public float? CurrentOutB { get; set; }
    public float? CurrentOutC { get; set; }
    public float? OutputFrequency { get; set; }

    // Battery & Temperature
    public float? BatteryPercent { get; set; }
    public float? BatteryVoltage { get; set; }
    public int? BatteryRuntimeMin { get; set; }
    public float? LoadPercent { get; set; }
    public float? TotalWatts { get; set; }
    public float? AmbientTemp { get; set; }
    public float? RoomTemp { get; set; }

    // Status & Grounding
    public string? UpsStatus { get; set; }
    public float? GroundingResistance { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
