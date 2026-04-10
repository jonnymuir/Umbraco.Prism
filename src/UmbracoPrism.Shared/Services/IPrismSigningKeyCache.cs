using Microsoft.IdentityModel.Tokens;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Caches Entra OpenID signing keys per tenant to avoid request-path metadata fetches.
/// </summary>
public interface IPrismSigningKeyCache
{
    /// <summary>
    /// Asynchronously fetches and caches signing keys for a tenant when cache is cold or stale.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier used to locate metadata.</param>
    /// <param name="forceRefresh">When <see langword="true"/>, bypasses TTL checks and refreshes cached keys immediately.</param>
    /// <param name="cancellationToken">Cancellation token for the metadata retrieval operation.</param>
    /// <returns>A task that completes when the cache warm operation finishes.</returns>
    Task WarmAsync(string entraTenantId, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cached signing-key state for a tenant, including freshness and key-id match information.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <param name="keyId">Optional signing-key identifier required by the current token.</param>
    /// <returns>A snapshot of the cached signing-key state for the tenant.</returns>
    PrismSigningKeyCacheSnapshot GetSnapshot(string entraTenantId, string? keyId = null);

    /// <summary>
    /// Gets cached signing keys for a tenant.
    /// </summary>
    /// <param name="entraTenantId">The Entra tenant identifier.</param>
    /// <returns>The cached signing keys, or an empty sequence when unavailable.</returns>
    IEnumerable<SecurityKey> GetSigningKeys(string entraTenantId);
}
