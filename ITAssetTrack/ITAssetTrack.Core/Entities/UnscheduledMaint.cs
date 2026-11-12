using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class UnscheduledMaint
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [NotMapped]
        public DateTime Tanggal
        {
            get => Date;
            set => Date = value;
        }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string Priority { get; set; } = "Low";
        public string Status { get; set; } = "In Progress";
        public string TroubleCategory { get; set; } = "Hardware Problem";

        [NotMapped]
        public string TroubleKategori
        {
            get => TroubleCategory;
            set => TroubleCategory = value;
        }

        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string Keluhan
        {
            get => Description;
            set => Description = value;
        }
        public string ActionTaken { get; set; } = string.Empty;

        [NotMapped]
        public string Remark
        {
            get => ActionTaken;
            set => ActionTaken = value;
        }

        [Required]
        public string SerialNumber { get; set; } = string.Empty;

        public string NamaUser { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;

        public string DeviceType { get; set; } = "Laptop";
        public string Brand { get; set; } = string.Empty;

        public string Processor { get; set; } = string.Empty;
        public string Ram { get; set; } = string.Empty;
        public string Storage { get; set; } = string.Empty;
        public string WindowsVersion { get; set; } = string.Empty;

        public string KeyboardSN { get; set; } = string.Empty;
        public string MouseSN { get; set; } = string.Empty;
        public string MonitorSN { get; set; } = string.Empty;

        public string? UserSignature { get; set; }
        public string? TechSignature { get; set; }

        // Approvals for Unscheduled PM (Pak Franz/Eddie and Pak Freddy)
        public string? LeaderSignature { get; set; }
        public string? SectionHeadSignature { get; set; }

        public bool LeaderApproved { get; set; }
        public bool SectionHeadApproved { get; set; }

        public string? LeaderApprovedBy { get; set; }
        public string? SectionHeadApprovedBy { get; set; }

        public DateTime? LeaderApprovedAt { get; set; }
        public DateTime? SectionHeadApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
