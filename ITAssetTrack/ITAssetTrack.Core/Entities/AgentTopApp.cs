using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentTopApp
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

        public int RecordCount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedDurationMinutes { get; set; }

        public bool IsAFK { get; set; } = false;

        public DateTime PeriodStart { get; set; }
    }
}
