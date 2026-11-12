using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class ScheduledMaint
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string PlanMonth { get; set; } = DateTime.Today.ToString("yyyy-MM");

        public DateTime? PlanDate { get; set; }
        public DateTime? ActualDate { get; set; }

        public DateTime Tanggal { get; set; } = DateTime.Today;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool StartTimeRecorded { get; set; } = false;

        public string NoDocument { get; set; } = string.Empty;

        [Required]
        public string SerialNumber { get; set; } = string.Empty;

        public string NamaUser { get; set; } = string.Empty;
        public string NRP { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;

        public string DeviceType { get; set; } = "Notebook"; // Desktop / Notebook
        public string DeviceCategory { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public string Processor { get; set; } = string.Empty;
        public string ProcessorKondisi { get; set; } = "Baik"; // Baik / Rusak
        public string Ram { get; set; } = string.Empty;
        public string RamKondisi { get; set; } = "Baik";
        public string Storage { get; set; } = string.Empty;
        public string StorageKondisi { get; set; } = "Baik";
        public string WindowsVersion { get; set; } = string.Empty;
        public string DeviceKondisi { get; set; } = "Baik";

        // Aksesoris - Flag keberadaan
        public bool KeyboardAda { get; set; } = false;
        public bool MouseAda { get; set; } = false;
        public bool MonitorAda { get; set; } = false;
        public bool ChargerAda { get; set; } = false;
        public bool TasLaptopAda { get; set; } = false;
        public bool MultiportAda { get; set; } = false;

        // Aksesoris - Brand & Kondisi
        public string KeyboardBrand { get; set; } = string.Empty;
        public string KeyboardKondisi { get; set; } = "Baik";
        public string MouseBrand { get; set; } = string.Empty;
        public string MouseKondisi { get; set; } = "Baik";
        public string MonitorBrand { get; set; } = string.Empty;
        public string MonitorModel { get; set; } = string.Empty;
        public string MonitorKondisi { get; set; } = "Baik";

        // Aksesoris - SN (lama, disimpan untuk backward compatibility, tidak ditampilkan di UI)
        public string KeyboardSN { get; set; } = string.Empty;
        public string MouseSN { get; set; } = string.Empty;
        public string MonitorSN { get; set; } = string.Empty;

        // Checklist Form PM (FORM PM_New.pdf)
        public bool FisikKondisiBaik { get; set; } = true;
        public bool UEFIPassword { get; set; } = true;
        public bool BootOrderStorage { get; set; } = true;
        public bool BootOrderDisable { get; set; } = true;
        public bool DisableAdmin { get; set; } = true;
        public bool DiskCleanUp { get; set; } = true;
        public bool SpaceSSD { get; set; } = true;

        public string Keluhan { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public string Status { get; set; } = "Planned"; // Planned (Belum PM) / In Progress / Completed (Sudah PM)

        // ===== CONTACT & CONFIRMATION TRACKING (Proses #3) =====
        public DateTime? ContactedAt { get; set; }
        public string? ContactedBy { get; set; }
        public string? ContactMethod { get; set; }        // "WhatsApp" / "Email" / "Phone"
        public string? UserResponse { get; set; }          // "Confirmed" / "Reschedule" / "NoResponse"
        public DateTime? UserConfirmedAt { get; set; }

        // ===== RESCHEDULE LOGS (Proses #4 & #5) =====
        public DateTime? OriginalPlanDate { get; set; }
        public DateTime? Reschedule1Date { get; set; }
        public string? Reschedule1Sig { get; set; }
        public string? Reschedule1Reason { get; set; }
        public DateTime? Reschedule2Date { get; set; }
        public string? Reschedule2Sig { get; set; }
        public string? Reschedule2Reason { get; set; }
        public DateTime? Reschedule3Date { get; set; }
        public string? Reschedule3Sig { get; set; }
        public string? Reschedule3Reason { get; set; }

        // ===== FULL TIMESTAMP PM EXECUTION (Proses #7 & #9) =====
        public DateTime? PmStartedAt { get; set; }
        public DateTime? PmCompletedAt { get; set; }

        // ===== RETURN / HANDOVER TO USER (Proses #10) =====
        public DateTime? ReturnedAt { get; set; }
        public string? UserHandoverSignature { get; set; }   // TTD digital user saat terima laptop (base64)
        public bool UserNotPresent { get; set; } = false;    // true = user tidak ada saat return
        public string? ReturnNotes { get; set; }

        // ===== 3 TANDA TANGAN DIGITAL =====
        public string? ExecBySignature { get; set; }       // Sign 1: Dilaksanakan Oleh (ITAssetTrack/Manage Service)
        public string? LeaderSignature { get; set; }       // Sign 2: Diketahui oleh (ITAssetTrack Leader/IT Site)
        public string? ItosSectionHeadSignature { get; set; } // Sign 3: Diketahui oleh (ITOS Section Head)

        // ===== APPROVAL VIA CHECKBOX (Proses #12) =====
        public bool LeaderApproved { get; set; } = false;
        public DateTime? LeaderApprovedAt { get; set; }
        public string? LeaderApprovedBy { get; set; }
        public bool SectionHeadApproved { get; set; } = false;
        public DateTime? SectionHeadApprovedAt { get; set; }
        public string? SectionHeadApprovedBy { get; set; }

        // ===== TECHNICIAN (Proses #7, #11) =====
        public string? PerformedBy { get; set; }

        // Legacy Alias Properties for backward compatibility
        [NotMapped]
        public string? UserSignature
        {
            get => ExecBySignature;
            set => ExecBySignature = value;
        }

        [NotMapped]
        public string? TechSignature
        {
            get => LeaderSignature;
            set => LeaderSignature = value;
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
