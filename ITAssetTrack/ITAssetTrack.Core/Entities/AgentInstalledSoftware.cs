using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentInstalledSoftware
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ReportId { get; set; }

        [ForeignKey("ReportId")]
        public AgentReport? Report { get; set; }

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string SoftwareName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? SoftwareVersion { get; set; }

        [StringLength(255)]
        public string? Publisher { get; set; }

        [StringLength(50)]
        public string? InstallDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
