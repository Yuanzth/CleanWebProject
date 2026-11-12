using System;
using System.ComponentModel.DataAnnotations;

namespace ITAssetTrack.Models
{
    public class MaintenancePhoto
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string MaintenanceType { get; set; } = string.Empty; // "Scheduled" or "Unscheduled"

        [Required]
        public Guid MaintenanceId { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string PhotoType { get; set; } = "General"; // "StartPM" / "EndPM" / "ReturnLaptop" / "General"

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
