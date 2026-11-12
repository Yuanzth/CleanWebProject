namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_s_unregistered_devices
/// Laravel Model: UnregisteredDevice
/// </summary>
public class UnregisteredDevice
{
    public Guid Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
    public DateTime? LastSeen { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
