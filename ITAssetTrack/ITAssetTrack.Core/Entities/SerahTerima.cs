using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAssetTrack.Models
{
    public class SerahTerima
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string NoDocument { get; set; } = string.Empty;

        // Legacy Alias
        [NotMapped]
        public string NoDokumen
        {
            get => NoDocument;
            set => NoDocument = value;
        }

        [Required]
        public string Tipe { get; set; } = "Penyerahan"; // Penyerahan / Pengembalian

        // Legacy Alias
        [NotMapped]
        public string Jenis
        {
            get => Tipe;
            set => Tipe = value;
        }

        public DateTime Date { get; set; } = DateTime.Today;

        // Legacy Alias
        [NotMapped]
        public DateTime Tanggal
        {
            get => Date;
            set => Date = value;
        }

        public string NamaUser { get; set; } = string.Empty;
        public string NRP { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;

        public string DeviceType { get; set; } = "Laptop";
        public string Brand { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;
        public string AdditionalHardware { get; set; } = string.Empty;

        public string Ram { get; set; } = string.Empty;
        public string Storage { get; set; } = string.Empty;
        public string KeyboardSN { get; set; } = string.Empty;
        public string MouseSN { get; set; } = string.Empty;
        public string MonitorSN { get; set; } = string.Empty;

        public string Alasan { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        // Legacy Alias
        [NotMapped]
        public string Catatan
        {
            get => Remarks;
            set => Remarks = value;
        }

        public string Status { get; set; } = "Pending";

        public string? UserSignature { get; set; }
        public string? AdminSignature { get; set; }

        // Signatures & Names Aliases
        public string NamaPenyerah { get; set; } = string.Empty;
        public string NamaPenerima { get; set; } = string.Empty;
        public string NamaDiketahui { get; set; } = string.Empty;

        public string? SignaturePenyerah { get; set; }
        public string? SignaturePenerima { get; set; }
        public string? SignatureDiketahui { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
