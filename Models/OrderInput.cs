#nullable enable
using System.ComponentModel.DataAnnotations;

namespace retailCLDVportal.Models
{
    public sealed class OrderInput
    {
        [Required]
        public string? CustomerPk { get; set; }
        [Required]
        public string? CustomerRk { get; set; }

        [Required]
        public string? ProductPk { get; set; }
        [Required]
        public string? ProductRk { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        // Read-only display fields (optional to bind back)
        public string? CustomerName { get; set; }
        public string? ProductName  { get; set; }
        public long? UnitPriceCents { get; set; } // copied from Product
        public string? Currency     { get; set; }  // e.g., ZAR
    }
}
