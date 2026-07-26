using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Generates and validates workflow step nonces using IDistributedCache.
/// Nonces bind form submissions to server-authoritative field definitions,
/// preventing field injection and constraint bypass attacks.
/// </summary>
public class TouchpointNonceService : ITouchpointNonceService
{
    private readonly IDistributedCache _cache;
    private readonly PrismServiceDesignOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public TouchpointNonceService(
        IDistributedCache cache,
        IOptions<PrismServiceDesignOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    /// <summary>
    /// Creates a nonce, caches the step's field definitions under it, and returns the nonce string.
    /// </summary>
    public async Task<string> CreateAsync(IReadOnlyList<FieldRenderPayload> fields, CancellationToken ct = default)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var cacheKey = $"prism:workflow:nonce:{nonce}";

        var json = JsonSerializer.SerializeToUtf8Bytes(fields, JsonOptions);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.NonceExpiry
        };

        await _cache.SetAsync(cacheKey, json, cacheOptions, ct);

        return nonce;
    }

    /// <summary>
    /// Resolves a nonce back to its field definitions. Returns null if the nonce has
    /// expired or never existed — the caller should redirect to GET in this case.
    /// </summary>
    public async Task<IReadOnlyList<FieldRenderPayload>?> ResolveAsync(string nonce, CancellationToken ct = default)
    {
        var cacheKey = $"prism:workflow:nonce:{nonce}";

        var json = await _cache.GetAsync(cacheKey, ct);

        if (json == null)
            return null;

        var fields = JsonSerializer.Deserialize<List<FieldRenderPayload>>(json, JsonOptions);

        return fields;
    }
}
