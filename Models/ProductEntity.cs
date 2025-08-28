#nullable enable
using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace retailCLDVportal.Models
{
   
    /// Product entity stored in Azure Table Storage.
    /// Price is stored in minor units (cents) to avoid floating-point issues.
    /// Media fields point to a blob you will upload to Azure Blob Storage.
  
    public sealed class ProductEntity : ITableEntity
    {
        // ---- Table Storage keys/metadata ----
        public string PartitionKey { get; set; } = default!;     
        public string RowKey { get; set; } = default!;            
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        // ---- Product fields (all optional for now) ----
        [Display(Name = "Product Name")]
        [StringLength(120)]
        public string? Name { get; set; }

        [StringLength(60)]
        public string? Category { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        /// <summary>Price in minor units (e.g., cents). Null if not set.</summary>
        public long? PriceCents { get; set; }

        /// <summary>ISO currency code (e.g., "ZAR").</summary>
        [StringLength(8)]
        public string? Currency { get; set; } = "ZAR";

        public int? StockQuantity { get; set; }

        /// <summary>Explicit availability flag (optional). If null, UI can derive from StockQuantity.</summary>
        public bool? IsAvailable { get; set; }

        // ---- Media (Blob) metadata ----
        /// <summary>Container you’ll use for product media (e.g., "product-media").</summary>
        [StringLength(100)]
        public string? MediaContainer { get; set; }

        /// <summary>Blob name/key inside the container.</summary>
        [StringLength(300)]
        public string? MediaBlobName { get; set; }

        /// <summary>Public or SAS URL to the blob (optional if you generate on the fly).</summary>
        [StringLength(2048)]
        public string? MediaUrl { get; set; }

        [StringLength(150)]
        public string? MediaContentType { get; set; }  // e.g., image/png, video/mp4

        public long? MediaSizeBytes { get; set; }

        // ---- Auditing ----
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // ---- Helpers ----

        public static string ComputePartitionKey(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "_";
            return category.Trim().ToUpperInvariant();
        }

        public static long? ToCents(decimal? price) =>
            price.HasValue ? (long)Math.Round(price.Value * 100m, MidpointRounding.AwayFromZero) : null;

        public static decimal? FromCents(long? cents) =>
            cents.HasValue ? cents.Value / 100m : null;

        public static ProductEntity NewFrom(ProductInput input)
        {
            return new ProductEntity
            {
                PartitionKey   = ComputePartitionKey(input.Category),
                RowKey         = Guid.NewGuid().ToString("N"),
                Name           = input.Name?.Trim(),
                Category       = input.Category?.Trim(),
                Description    = input.Description?.Trim(),
                PriceCents     = ToCents(input.Price),
                StockQuantity  = input.StockQuantity,
                IsAvailable    = input.IsAvailable,
                // Media fields will be set after blob upload (if any)
                CreatedAtUtc   = DateTime.UtcNow
            };
        }

        public void UpdateFrom(ProductInput input)
        {
            Name          = input.Name?.Trim();
            Category      = input.Category?.Trim();
            Description   = input.Description?.Trim();
            PriceCents    = ToCents(input.Price);
            StockQuantity = input.StockQuantity;
            IsAvailable   = input.IsAvailable;
            // If category changed and you want to re-partition:
            PartitionKey  = ComputePartitionKey(Category);
        }
    }
}
