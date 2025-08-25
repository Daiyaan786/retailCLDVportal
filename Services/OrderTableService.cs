#nullable enable
using Azure;
using Azure.Data.Tables;
using retailCLDVportal.Models;

namespace retailCLDVportal.Services
{
    public interface IOrderTableService
    {
        Task<(bool ok, string? error, OrderEntity? saved)> PlaceAsync(OrderInput input, CancellationToken ct = default);
        Task<IReadOnlyList<OrderEntity>> ListAsync(int take = 500, CancellationToken ct = default);
        Task<OrderEntity?> GetAsync(string pk, string rk, CancellationToken ct = default);
        Task<(bool ok, string? error)> DeleteAsync(string pk, string rk, CancellationToken ct = default);

        // NEW: edit/update
        Task<(bool ok, string? error, OrderEntity? updated)> UpdateAsync(string pk, string rk, OrderInput input, CancellationToken ct = default);
    }

    public sealed class OrderTableService : IOrderTableService
    {
        private readonly TableClient _table;
        private readonly ICustomerTableService _customers;
        private readonly IProductTableService _products;
        private readonly IOrderQueueService _queues;

        public OrderTableService(
            IConfiguration cfg,
            ICustomerTableService customers,
            IProductTableService products,
            IOrderQueueService queues)
        {
            var conn = cfg["Storage:ConnectionString"] ?? throw new InvalidOperationException("Missing Storage:ConnectionString");
            var tableName = cfg["Storage:OrderTableName"] ?? "Orders";

            _table = new TableServiceClient(conn).GetTableClient(tableName);
            _table.CreateIfNotExists();

            _customers = customers;
            _products  = products;
            _queues    = queues;
        }

        public async Task<(bool ok, string? error, OrderEntity? saved)> PlaceAsync(OrderInput input, CancellationToken ct = default)
        {
            // 1) Fetch customer & product
            var cust = await _customers.GetAsync(input.CustomerPk!, input.CustomerRk!, ct);
            if (cust is null) return (false, "Customer not found.", null);

            var prod = await _products.GetAsync(input.ProductPk!, input.ProductRk!, ct);
            if (prod is null) return (false, "Product not found.", null);

            var stock = prod.StockQuantity ?? 0;
            var unitCents = prod.PriceCents ?? 0;
            if (unitCents <= 0) return (false, "Product has no price.", null);
            if (input.Quantity <= 0) return (false, "Quantity must be at least 1.", null);
            if (input.Quantity > stock) return (false, "Quantity exceeds available stock.", null);

            // 2) Build order
            var entity = OrderEntity.New(
                input.CustomerPk, input.CustomerRk, $"{cust.FirstName} {cust.Surname}".Trim(),
                input.ProductPk,  input.ProductRk,  prod.Name,
                input.Quantity, unitCents, prod.Currency
            );

            // 3) Save
            try
            {
                await _table.AddEntityAsync(entity, ct);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }

            // 4) Queue events
            await _queues.EnqueueOrderEventAsync("order-placed", new
            {
                entity.OrderNo,
                entity.PartitionKey,
                entity.RowKey,
                entity.CustomerName,
                entity.ProductName,
                entity.Quantity,
                UnitPriceCents = entity.UnitPriceCents,
                TotalCents     = entity.TotalCents,
                entity.Currency,
                entity.CreatedUtc
            }, ct);

            await _queues.EnqueueInventoryEventAsync("inventory-reserve", new
            {
                ProductPk = input.ProductPk,
                ProductRk = input.ProductRk,
                ReserveQty = input.Quantity,
                Reason = $"Order {entity.OrderNo}"
            }, ct);

            return (true, null, entity);
        }

        public async Task<(bool ok, string? error, OrderEntity? updated)> UpdateAsync(string pk, string rk, OrderInput input, CancellationToken ct = default)
        {
            // Load existing order
            OrderEntity existing;
            try
            {
                var resp = await _table.GetEntityAsync<OrderEntity>(pk, rk, cancellationToken: ct);
                existing = resp.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return (false, "Order not found.", null);
            }

            // Fetch latest customer & product
            var cust = await _customers.GetAsync(input.CustomerPk!, input.CustomerRk!, ct);
            if (cust is null) return (false, "Customer not found.", null);

            var prod = await _products.GetAsync(input.ProductPk!, input.ProductRk!, ct);
            if (prod is null) return (false, "Product not found.", null);

            var newQty   = input.Quantity;
            var stock    = prod.StockQuantity ?? 0;
            var unit     = prod.PriceCents ?? 0;
            var currency = string.IsNullOrWhiteSpace(prod.Currency) ? "ZAR" : prod.Currency;

            if (newQty <= 0)   return (false, "Quantity must be at least 1.", null);
            if (unit   <= 0)   return (false, "Product has no price.", null);
            if (newQty > stock) return (false, "Quantity exceeds available stock.", null);

            // Keep previous values for inventory adjustment
            var oldProdPk = existing.ProductPk;
            var oldProdRk = existing.ProductRk;
            var oldQty    = existing.Quantity;

            // Update fields
            existing.CustomerPk     = input.CustomerPk;
            existing.CustomerRk     = input.CustomerRk;
            existing.CustomerName   = $"{cust.FirstName} {cust.Surname}".Trim();

            existing.ProductPk      = input.ProductPk;
            existing.ProductRk      = input.ProductRk;
            existing.ProductName    = prod.Name;

            existing.Quantity       = newQty;
            existing.UnitPriceCents = unit;
            existing.TotalCents     = unit * newQty;
            existing.Currency       = currency;
            // existing.Status remains as-is

            // Persist
            try
            {
                await _table.UpdateEntityAsync(existing, ETag.All, TableUpdateMode.Replace, ct);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }

            // Order updated event
            await _queues.EnqueueOrderEventAsync("order-updated", new
            {
                existing.OrderNo,
                existing.PartitionKey,
                existing.RowKey,
                existing.CustomerName,
                existing.ProductName,
                existing.Quantity,
                existing.UnitPriceCents,
                existing.TotalCents,
                existing.Currency
            }, ct);

            // Inventory delta events
            var productChanged = !(string.Equals(oldProdPk, existing.ProductPk, StringComparison.Ordinal) &&
                                   string.Equals(oldProdRk, existing.ProductRk, StringComparison.Ordinal));

            if (productChanged)
            {
                // Release old product, reserve new product
                await _queues.EnqueueInventoryEventAsync("inventory-release", new
                {
                    ProductPk = oldProdPk,
                    ProductRk = oldProdRk,
                    ReleaseQty = oldQty,
                    Reason = $"Order {existing.OrderNo} edit"
                }, ct);

                await _queues.EnqueueInventoryEventAsync("inventory-reserve", new
                {
                    ProductPk = existing.ProductPk,
                    ProductRk = existing.ProductRk,
                    ReserveQty = existing.Quantity,
                    Reason = $"Order {existing.OrderNo} edit"
                }, ct);
            }
            else
            {
                var delta = existing.Quantity - oldQty;
                if (delta > 0)
                {
                    await _queues.EnqueueInventoryEventAsync("inventory-reserve", new
                    {
                        ProductPk = existing.ProductPk,
                        ProductRk = existing.ProductRk,
                        ReserveQty = delta,
                        Reason = $"Order {existing.OrderNo} edit"
                    }, ct);
                }
                else if (delta < 0)
                {
                    await _queues.EnqueueInventoryEventAsync("inventory-release", new
                    {
                        ProductPk = existing.ProductPk,
                        ProductRk = existing.ProductRk,
                        ReleaseQty = -delta,
                        Reason = $"Order {existing.OrderNo} edit"
                    }, ct);
                }
            }

            return (true, null, existing);
        }

        public async Task<IReadOnlyList<OrderEntity>> ListAsync(int take = 500, CancellationToken ct = default)
        {
            var list = new List<OrderEntity>(take);
            await foreach (var e in _table.QueryAsync<OrderEntity>(maxPerPage: take, cancellationToken: ct))
            {
                list.Add(e);
                if (list.Count >= take) break;
            }
            return list.OrderBy(e => e.OrderNo).ToList();
        }

        public async Task<OrderEntity?> GetAsync(string pk, string rk, CancellationToken ct = default)
        {
            try
            {
                var resp = await _table.GetEntityAsync<OrderEntity>(pk, rk, cancellationToken: ct);
                return resp.Value;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<(bool ok, string? error)> DeleteAsync(string pk, string rk, CancellationToken ct = default)
        {
            try
            {
                await _table.DeleteEntityAsync(pk, rk, ETag.All, ct);
                await _queues.EnqueueOrderEventAsync("order-deleted", new { pk, rk }, ct);
                return (true, null);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return (true, null);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
