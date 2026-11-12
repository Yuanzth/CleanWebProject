using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITAssetTrack.Models
{
    public class Department
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
