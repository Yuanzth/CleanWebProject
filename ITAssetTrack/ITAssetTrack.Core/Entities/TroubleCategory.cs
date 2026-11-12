using System;
using System.ComponentModel.DataAnnotations;

namespace ITAssetTrack.Models
{
    public class TroubleCategory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
