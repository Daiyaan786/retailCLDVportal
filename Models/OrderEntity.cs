#nullable enable
using Azure;
using Azure.Data.Tables;

namespace retailCLDVportal.Models
{
    public sealed class OrderEntity : ITableEntity
    {
        // Keys/ETag/Timestamp
        public string PartitionKey { get; set; } = default!;  
        public string RowKey { get; set; } = default!;         
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        // Business fields
        public long OrderNo { get; set; }             // sortable numeric (e.g., unix ms)
        public string? Status { get; set; }           // Placed, Cancelled, etc.

        // Foreign refs (denormalized for display too)
        public string? CustomerPk { get; set; }
        public string? CustomerRk { get; set; }
        public string? CustomerName { get; set; }

        public string? ProductPk { get; set; }
        public string? ProductRk { get; set; }
        public string? ProductName { get; set; }

        public int Quantity { get; set; }
        public long UnitPriceCents { get; set; }
        public long TotalCents { get; set; }
        public string? Currency { get; set; }

        public DateTime CreatedUtc { get; set; }

        public static OrderEntity New(string? custPk, string? custRk, string? custName,
                                      string? prodPk, string? prodRk, string? prodName,
                                      int qty, long unitCents, string? currency)
        {
            var now = DateTime.UtcNow;
            return new OrderEntity
            {
                PartitionKey    = $"ORD-{now:yyyy-MM}",
                RowKey          = Guid.NewGuid().ToString("N"),
                OrderNo         = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Status          = "Placed",
                CustomerPk      = custPk,
                CustomerRk      = custRk,
                CustomerName    = custName,
                ProductPk       = prodPk,
                ProductRk       = prodRk,
                ProductName     = prodName,
                Quantity        = qty,
                UnitPriceCents  = unitCents,
                TotalCents      = unitCents * qty,
                Currency        = string.IsNullOrWhiteSpace(currency) ? "ZAR" : currency,
                CreatedUtc      = now
            };
        }
    }
}
