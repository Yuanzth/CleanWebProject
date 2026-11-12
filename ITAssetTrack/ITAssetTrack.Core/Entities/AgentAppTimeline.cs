using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentAppTimeline
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
        public string AppName { get; set; } = string.Empty;

        [StringLength(30)]
        public string? StartTime { get; set; }

        [StringLength(30)]
        public string? EndTime { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DurationMinutes { get; set; }

        public bool IsAFK { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
