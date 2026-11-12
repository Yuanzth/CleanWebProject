using System;

namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: tbl_t_device_acknowledges
/// Menyimpan data log acknowledgement per device dengan fitur upload bukti (evidence).
/// </summary>
public class DeviceAcknowledge
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty; // Menggunakan string untuk support kombinasi ID
    public Guid AcknowledgedBy { get; set; } // FK ke tbl_m_users
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; } // Mandatory: waktu selesai kejadian
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? EvidencePath { get; set; }
    public DateTime CreatedAt { get; set; }

    // DCO Verification Fields
    public string VerificationStatus { get; set; } = "Pending"; // "Pending", "Verified", "Rejected"
    public Guid? VerifiedBy { get; set; } // FK ke tbl_m_users (DCO user)
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNotes { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public User? Verifier { get; set; }
}
