using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// <see cref="IUploadTokenService"/> backed by <see cref="IDistributedCache"/> — same mechanism
/// and TTL (<see cref="PrismWorkflowOptions.NonceExpiry"/>) as <see cref="WorkflowStepNonceService"/>,
/// since an uploaded-but-not-yet-submitted file is scoped to the same single stage visit a nonce is.
/// </summary>
public class UploadTokenService : IUploadTokenService
{
    private readonly IDistributedCache _cache;
    private readonly PrismWorkflowOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public UploadTokenService(IDistributedCache cache, IOptions<PrismWorkflowOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<string> CreateAsync(string instanceId, string fieldKey, WorkflowFileReference reference, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var cacheKey = $"prism:workflow:upload-token:{token}";

        var binding = new UploadTokenBinding { InstanceId = instanceId, FieldKey = fieldKey, Reference = reference };
        var json = JsonSerializer.SerializeToUtf8Bytes(binding, JsonOptions);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.NonceExpiry
        };

        await _cache.SetAsync(cacheKey, json, cacheOptions, ct);

        return token;
    }

    public async Task<UploadTokenBinding?> ResolveAsync(string token, CancellationToken ct = default)
    {
        var cacheKey = $"prism:workflow:upload-token:{token}";

        var json = await _cache.GetAsync(cacheKey, ct);

        return json == null ? null : JsonSerializer.Deserialize<UploadTokenBinding>(json, JsonOptions);
    }
}
