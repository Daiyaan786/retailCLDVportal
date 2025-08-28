#nullable enable
using System.Text.Json;
using Azure.Storage.Queues;
//orderqueueservice
namespace retailCLDVportal.Services
{
    public interface IOrderQueueService
    {
        Task EnqueueOrderEventAsync(string type, object payload, CancellationToken ct = default);
        Task EnqueueInventoryEventAsync(string type, object payload, CancellationToken ct = default);
    }

    public sealed class OrderQueueService : IOrderQueueService
    {
        private readonly QueueClient _ordersQ;
        private readonly QueueClient _inventoryQ;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public OrderQueueService(IConfiguration cfg)
        {
            var conn = cfg["Storage:ConnectionString"] ?? throw new InvalidOperationException("Missing Storage:ConnectionString");
            var ordersQueue    = cfg["Storage:OrdersQueue"] ?? "orders-events";
            var inventoryQueue = cfg["Storage:InventoryQueue"] ?? "inventory-events";

            _ordersQ    = new QueueClient(conn, ordersQueue);
            _inventoryQ = new QueueClient(conn, inventoryQueue);

            _ordersQ.CreateIfNotExists();
            _inventoryQ.CreateIfNotExists();
        }

        public Task EnqueueOrderEventAsync(string type, object payload, CancellationToken ct = default)
            => _ordersQ.SendMessageAsync(JsonSerializer.Serialize(new { type, at = DateTime.UtcNow, payload }, _json), ct);

        public Task EnqueueInventoryEventAsync(string type, object payload, CancellationToken ct = default)
            => _inventoryQ.SendMessageAsync(JsonSerializer.Serialize(new { type, at = DateTime.UtcNow, payload }, _json), ct);
    }
}
