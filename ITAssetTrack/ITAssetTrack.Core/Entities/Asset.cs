using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class Asset
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string NoSN { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string ComputerName { get; set; } = string.Empty;

        [StringLength(150)]
        public string Model { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;
        public string WindowsVersion { get; set; } = string.Empty;

        public string Ram { get; set; } = string.Empty;
        public string Storage { get; set; } = string.Empty;
        public string Processor { get; set; } = string.Empty;
        public string Gpu { get; set; } = string.Empty;

        [StringLength(50)]
        public string Status { get; set; } = "Available";

        public string Remarks { get; set; } = string.Empty;

        // Foreign Keys
        public Guid? DeviceTypeId { get; set; }
        [ForeignKey("DeviceTypeId")]
        public DeviceType? DeviceTypeObj { get; set; }

        public Guid? DeviceCategoryId { get; set; }
        [ForeignKey("DeviceCategoryId")]
        public DeviceCategory? DeviceCategoryObj { get; set; }

        public Guid? BrandId { get; set; }
        [ForeignKey("BrandId")]
        public Brand? BrandObj { get; set; }

        public Guid? VendorId { get; set; }
        [ForeignKey("VendorId")]
        public Vendor? VendorObj { get; set; }

        // Accessories & Legacy String Fallbacks
        public string DeviceType { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string NRP { get; set; } = string.Empty;
        public string KeyboardSN { get; set; } = string.Empty;
        public string MouseSN { get; set; } = string.Empty;
        public string MonitorSN { get; set; } = string.Empty;
        public string AdditionalHardware { get; set; } = string.Empty;

        // Telemetry Data (Agent)
        public DateTime? LastOnlineTime { get; set; }
        public bool IsAFK { get; set; } = false;
        public string Uptime { get; set; } = string.Empty;
        public string ActiveApps { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string LastIp { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string LastLoggedInUser { get; set; } = string.Empty;
        
        public int? LastBatteryHealthPct { get; set; }
        public bool IsOnline { get; set; } = false;
        public bool IsUserMismatch { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigations
        public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
        public ICollection<AgentReport> AgentReports { get; set; } = new List<AgentReport>();
    }
}
