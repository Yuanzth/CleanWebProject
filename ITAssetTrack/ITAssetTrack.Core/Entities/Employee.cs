using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class Employee
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [StringLength(50)]
        public string NRP { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        public string Phone { get; set; } = string.Empty;

        public Guid? SiteId { get; set; }
        [ForeignKey("SiteId")]
        public Site? Site { get; set; }

        public Guid? DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        public Guid? DivisionId { get; set; }
        [ForeignKey("DivisionId")]
        public Division? Division { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigations
        public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
    }
}
