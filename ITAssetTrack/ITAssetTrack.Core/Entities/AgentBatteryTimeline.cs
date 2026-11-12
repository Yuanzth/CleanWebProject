using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentBatteryTimeline
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
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [StringLength(20)]
        public string? StartTime { get; set; }

        [StringLength(20)]
        public string? EndTime { get; set; }

        public int StartPct { get; set; }
        public int EndPct { get; set; }
    }
}
