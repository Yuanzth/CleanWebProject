using System;
using System.ComponentModel.DataAnnotations;

namespace ITAssetTrack.Models
{
    public class PmPlan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string MonthYear { get; set; } = string.Empty;

        public int TargetQuantity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
