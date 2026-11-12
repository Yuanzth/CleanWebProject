using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentLoginSession
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ReportId { get; set; }
        [ForeignKey("ReportId")]
        public AgentReport? Report { get; set; }

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Hostname { get; set; }

        [StringLength(100)]
        public string? LoggedInUser { get; set; }

        [StringLength(150)]
        public string? DisplayName { get; set; }

        public DateTime SessionStart { get; set; }
        public DateTime? SessionEnd { get; set; }
        public int? DurationMinutes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
