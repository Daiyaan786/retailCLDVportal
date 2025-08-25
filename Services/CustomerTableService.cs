#nullable enable
using Azure;
using Azure.Data.Tables;
using retailCLDVportal.Models;

namespace retailCLDVportal.Services
{
    public interface ICustomerTableService
    {
        Task<(bool ok, string? error, CustomerEntity? saved)> AddAsync(CustomerInput input, CancellationToken ct = default);
        Task<CustomerEntity?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);

        // Partition-scoped listing (keep for cases where you know the PK)
        Task<IReadOnlyList<CustomerEntity>> ListByPartitionAsync(string partitionKey, int take = 50, CancellationToken ct = default);

        // Table-wide listing (used by Orders create page for the dropdown)
        Task<IReadOnlyList<CustomerEntity>> ListAsync(int take = 500, CancellationToken ct = default);

        Task<(bool ok, string? error)> DeleteAsync(string partitionKey, string rowKey, CancellationToken ct = default);
        Task<(bool ok, string? error, CustomerEntity? updated)> UpdateAsync(string partitionKey, string rowKey, CustomerInput input, CancellationToken ct = default);
    }

    public sealed class CustomerTableService : ICustomerTableService
    {
        private readonly TableClient _table;

        public CustomerTableService(IConfiguration config)
        {
            var conn = config["Storage:ConnectionString"] ?? throw new InvalidOperationException("Missing Storage:ConnectionString");
            var tableName = config["Storage:CustomerTableName"] ?? "Customers";

            _table = new TableServiceClient(conn).GetTableClient(tableName);
            _table.CreateIfNotExists();
        }

        public async Task<(bool ok, string? error, CustomerEntity? saved)> AddAsync(CustomerInput input, CancellationToken ct = default)
        {
            try
            {
                var entity = CustomerEntity.NewFrom(input);     // ensures UTC for DateOfBirth, keys, etc.
                await _table.AddEntityAsync(entity, ct);
                return (true, null, entity);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<CustomerEntity?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        {
            try
            {
                var resp = await _table.GetEntityAsync<CustomerEntity>(partitionKey, rowKey, cancellationToken: ct);
                return resp.Value;
            }
            catch (RequestFailedException)
            {
                return null; // 404 or similar
            }
        }

        public async Task<IReadOnlyList<CustomerEntity>> ListByPartitionAsync(string partitionKey, int take = 50, CancellationToken ct = default)
        {
            var results = new List<CustomerEntity>(take);
            await foreach (var e in _table.QueryAsync<CustomerEntity>(x => x.PartitionKey == partitionKey, maxPerPage: take, cancellationToken: ct))
            {
                results.Add(e);
                if (results.Count >= take) break;
            }
            return results
                .OrderBy(c => c.Surname)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        // Table-wide listing (no PK filter)
        public async Task<IReadOnlyList<CustomerEntity>> ListAsync(int take = 500, CancellationToken ct = default)
        {
            var results = new List<CustomerEntity>(take);
            await foreach (var e in _table.QueryAsync<CustomerEntity>(maxPerPage: take, cancellationToken: ct))
            {
                results.Add(e);
                if (results.Count >= take) break;
            }
            return results
                .OrderBy(c => c.Surname)
                .ThenBy(c => c.FirstName)
                .ToList();
        }

        public async Task<(bool ok, string? error)> DeleteAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        {
            try
            {
                await _table.DeleteEntityAsync(partitionKey, rowKey, ETag.All, ct);
                return (true, null);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Idempotent deletes
                return (true, null);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool ok, string? error, CustomerEntity? updated)> UpdateAsync(string partitionKey, string rowKey, CustomerInput input, CancellationToken ct = default)
        {
            try
            {
                var resp = await _table.GetEntityAsync<CustomerEntity>(partitionKey, rowKey, cancellationToken: ct);
                var entity = resp.Value;

                entity.UpdateFrom(input); // your normalization/UTC logic lives on the entity
                await _table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace, ct);

                return (true, null, entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return (false, "Customer not found.", null);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }
        }
    }
}
