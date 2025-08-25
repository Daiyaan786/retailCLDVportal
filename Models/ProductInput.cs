#nullable enable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace retailCLDVportal.Models
{
    /// <summary>
    /// View/Input model used by the New Product form.
    /// All fields are optional for now; we can tighten validation later.
    /// </summary>
    public class ProductInput
    {
        [Display(Name = "Product Name")]
        [StringLength(120)]
        public string? Name { get; set; }

        [StringLength(60)]
        public string? Category { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        // Form-facing price (decimal) – we'll convert to cents in the Table entity.
        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        public decimal? Price { get; set; }   // optional for now

        [Display(Name = "Stock Quantity")]
        public int? StockQuantity { get; set; }  // optional for now

        // Optional media upload (image or video)
        [Display(Name = "Upload Image/Video")]
        public IFormFile? MediaFile { get; set; }

        // Optional explicit availability toggle (if you want to surface it)
        public bool? IsAvailable { get; set; }
    }
}
