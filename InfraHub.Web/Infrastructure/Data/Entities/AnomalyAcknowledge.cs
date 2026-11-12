namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_anomaly_acknowledges
/// </summary>
public class AnomalyAcknowledge
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string Panel { get; set; } = string.Empty;
    public string AnomalyType { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
