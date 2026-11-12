namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_m_devices
/// Laravel Model: Device
/// Kolom gabungan dari migration awal + alter fleet + ip_address + ups credentials + ups_phase_type + ups_type + ups_category + room_sensor_ip
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? Imei { get; set; }
    public string? PhoneNumber { get; set; }
    public string DeviceName { get; set; } = string.Empty;

    // Foreign Keys
    public Guid? SiteId { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid AssetTypeId { get; set; }

    // Physical
    public double? AssetHeight { get; set; }
    public string? FleetType { get; set; }
    public string? NetworkArea { get; set; }

    // UPS-specific
    public string? UpsPhaseType { get; set; }
    public string? UpsType { get; set; }
    public string? UpsCategory { get; set; }
    public string? IpAddress { get; set; }
    public string? RoomSensorIp { get; set; }
    public string? UpsUsername { get; set; }
    public string? UpsPassword { get; set; }

    public bool IsActive { get; set; } = true;

    // Audit Trail
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Navigation
    public Site? Site { get; set; }
    public Zone? Zone { get; set; }
    public AssetType AssetType { get; set; } = null!;
    public ICollection<GensetLog> GensetLogs { get; set; } = new List<GensetLog>();
    public ICollection<TowerLog> TowerLogs { get; set; } = new List<TowerLog>();
    public ICollection<UpsLog> UpsLogs { get; set; } = new List<UpsLog>();
    public ICollection<FleetTrackingLog> FleetTrackingLogs { get; set; } = new List<FleetTrackingLog>();
}
