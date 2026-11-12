using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AgentReport
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? AssetId { get; set; }
        [ForeignKey("AssetId")]
        public Asset? Asset { get; set; }

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Hostname { get; set; }

        [StringLength(100)]
        public string? LoggedInUser { get; set; }

        [StringLength(150)]
        public string? DisplayName { get; set; }

        [StringLength(100)]
        public string? AssignedNrp { get; set; }

        public bool IsUserMismatch { get; set; } = false;

        [StringLength(150)]
        public string? OsVersion { get; set; }

        [StringLength(50)]
        public string? ReportType { get; set; }

        [StringLength(20)]
        public string? ScheduleTriggered { get; set; }

        public int RecordIntervalSeconds { get; set; } = 300;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public long CpuUptimeSeconds { get; set; }

        public int? BatteryHealthPct { get; set; }

        [StringLength(45)]
        public string? LocalIp { get; set; }

        [StringLength(100)]
        public string? Ssid { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        public int CpuMinPct { get; set; }
        public int CpuAvgPct { get; set; }
        public int CpuMaxPct { get; set; }

        public int RamMinPct { get; set; }
        public int RamAvgPct { get; set; }
        public int RamMaxPct { get; set; }

        public int GpuMinPct { get; set; }
        public int GpuAvgPct { get; set; }
        public int GpuMaxPct { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DailyNetworkDownloadMb { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DailyNetworkUploadMb { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DailyNetworkTotalMb { get; set; }

        [StringLength(255)]
        public string? DailyNetworkUsageText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigations
        public ICollection<AgentTopApp> TopApps { get; set; } = new List<AgentTopApp>();
        public ICollection<AgentAppTimeline> AppTimelines { get; set; } = new List<AgentAppTimeline>();
        public ICollection<AgentBatteryTimeline> BatteryTimelines { get; set; } = new List<AgentBatteryTimeline>();
        public ICollection<AgentLoginSession> LoginSessions { get; set; } = new List<AgentLoginSession>();
        public ICollection<AgentInstalledSoftware> InstalledSoftware { get; set; } = new List<AgentInstalledSoftware>();
    }
}
