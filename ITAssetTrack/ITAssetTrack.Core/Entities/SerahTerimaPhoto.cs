using System;
using System.ComponentModel.DataAnnotations;

namespace ITAssetTrack.Models
{
    public class SerahTerimaPhoto
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SerahTerimaId { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
