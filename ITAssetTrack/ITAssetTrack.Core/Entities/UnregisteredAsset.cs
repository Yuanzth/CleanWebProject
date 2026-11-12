using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class UnregisteredAsset
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string Hostname { get; set; } = string.Empty;

        [StringLength(100)]
        public string LoggedInUser { get; set; } = string.Empty;

        [StringLength(150)]
        public string OsVersion { get; set; } = string.Empty;

        [StringLength(45)]
        public string LocalIp { get; set; } = string.Empty;

        [StringLength(100)]
        public string Ssid { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        public DateTime LastSeenAt { get; set; } = DateTime.Now;

        public int ReportCount { get; set; } = 1;

        [StringLength(50)]
        public string Status { get; set; } = "Pending Review";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
