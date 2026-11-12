using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class AssetAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid AssetId { get; set; }
        [ForeignKey("AssetId")]
        public Asset Asset { get; set; } = null!;

        [Required]
        public Guid EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;

        public DateTime AssignDate { get; set; } = DateTime.Now;
        public DateTime? ReturnDate { get; set; }

        public bool IsActive { get; set; } = true;

        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
