#nullable enable
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using retailCLDVportal.Models;
//producttableservice
namespace retailCLDVportal.Services
{
    public interface IProductTableService
    {
        Task<(bool ok, string? error, ProductEntity? saved)> AddAsync(ProductInput input, CancellationToken ct = default);
        Task<ProductEntity?> GetAsync(string pk, string rk, CancellationToken ct = default);
        Task<IReadOnlyList<ProductEntity>> ListAsync(int take = 200, CancellationToken ct = default);
        Task<(bool ok, string? error, ProductEntity? updated)> UpdateAsync(string pk, string rk, ProductInput input, CancellationToken ct = default);
        Task<(bool ok, string? error)> DeleteAsync(string pk, string rk, CancellationToken ct = default);
    }

    public sealed class ProductTableService : IProductTableService
    {
        private readonly TableClient _table;
        private readonly BlobContainerClient _container;

        public ProductTableService(IConfiguration config)
        {
            var conn = config["Storage:ConnectionString"] ?? throw new InvalidOperationException("Missing Storage:ConnectionString");
            var tableName = config["Storage:ProductTableName"] ?? "Products";
            var containerName = config["Storage:ProductMediaContainer"] ?? "product-media";

            _table = new TableServiceClient(conn).GetTableClient(tableName);
            _table.CreateIfNotExists();

            _container = new BlobServiceClient(conn).GetBlobContainerClient(containerName);
            _container.CreateIfNotExists(PublicAccessType.Blob); // simple public read for class project
        }

        public async Task<(bool ok, string? error, ProductEntity? saved)> AddAsync(ProductInput input, CancellationToken ct = default)
        {
            try
            {
                var entity = ProductEntity.NewFrom(input);

                if (input.MediaFile is not null && input.MediaFile.Length > 0)
                {
                    var ext = Path.GetExtension(input.MediaFile.FileName);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
                    var blobName = $"{entity.RowKey}{ext}";

                    var blob = _container.GetBlobClient(blobName);
                    await using var s = input.MediaFile.OpenReadStream();
                    await blob.UploadAsync(s, new BlobHttpHeaders { ContentType = input.MediaFile.ContentType ?? "application/octet-stream" }, cancellationToken: ct);

                    entity.MediaContainer = _container.Name;
                    entity.MediaBlobName = blobName;
                    entity.MediaUrl = blob.Uri.ToString();
                    entity.MediaContentType = input.MediaFile.ContentType;
                    entity.MediaSizeBytes = input.MediaFile.Length;
                }

                await _table.AddEntityAsync(entity, ct);
                return (true, null, entity);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<ProductEntity?> GetAsync(string pk, string rk, CancellationToken ct = default)
        {
            try
            {
                var resp = await _table.GetEntityAsync<ProductEntity>(pk, rk, cancellationToken: ct);
                return resp.Value;
            }
            catch (RequestFailedException)
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<ProductEntity>> ListAsync(int take = 200, CancellationToken ct = default)
        {
            var results = new List<ProductEntity>(take);
            await foreach (var e in _table.QueryAsync<ProductEntity>(maxPerPage: take, cancellationToken: ct))
            {
                results.Add(e);
                if (results.Count >= take) break;
            }
            return results.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }

        public async Task<(bool ok, string? error, ProductEntity? updated)> UpdateAsync(string pk, string rk, ProductInput input, CancellationToken ct = default)
        {
            try
            {
                var resp = await _table.GetEntityAsync<ProductEntity>(pk, rk, cancellationToken: ct);
                var entity = resp.Value;

                // If a new media file is uploaded, replace blob
                if (input.MediaFile is not null && input.MediaFile.Length > 0)
                {
                    // optionally delete old
                    if (!string.IsNullOrWhiteSpace(entity.MediaBlobName))
                        await _container.DeleteBlobIfExistsAsync(entity.MediaBlobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

                    var ext = Path.GetExtension(input.MediaFile.FileName);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
                    var newBlobName = $"{entity.RowKey}{ext}";
                    var blob = _container.GetBlobClient(newBlobName);
                    await using var s = input.MediaFile.OpenReadStream();
                    await blob.UploadAsync(s, new BlobHttpHeaders { ContentType = input.MediaFile.ContentType ?? "application/octet-stream" }, cancellationToken: ct);

                    entity.MediaContainer = _container.Name;
                    entity.MediaBlobName = newBlobName;
                    entity.MediaUrl = blob.Uri.ToString();
                    entity.MediaContentType = input.MediaFile.ContentType;
                    entity.MediaSizeBytes = input.MediaFile.Length;
                }

                entity.UpdateFrom(input);
                await _table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace, ct);
                return (true, null, entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return (false, "Product not found.", null);
            }
            catch (RequestFailedException ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool ok, string? error)> DeleteAsync(string pk, string rk, CancellationToken ct = default)
        {
            try
            {
                // Try fetch to also delete blob if present
                ProductEntity? e = null;
                try { e = await GetAsync(pk, rk, ct); } catch { /* ignore */ }

                await _table.DeleteEntityAsync(pk, rk, ETag.All, ct);

                if (e is not null && !string.IsNullOrWhiteSpace(e.MediaBlobName))
                    await _container.DeleteBlobIfExistsAsync(e.MediaBlobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

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
