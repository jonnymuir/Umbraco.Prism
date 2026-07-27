using Microsoft.IdentityModel.Tokens;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Immutable view of tenant signing-key cache touchpoint used for fail-closed token validation.
/// </summary>
/// <param name="Keys">Cached signing keys for the tenant.</param>
/// <param name="ShouldRefresh">Whether the cache should be refreshed proactively.</param>
/// <param name="IsExpired">Whether the cache has exceeded the hard trust-material lifetime.</param>
/// <param name="ContainsRequestedKey">Whether the cached keys contain the requested key identifier.</param>
public sealed record PrismSigningKeyCacheSnapshot(
    IReadOnlyCollection<SecurityKey> Keys,
    bool ShouldRefresh,
    bool IsExpired,
    bool ContainsRequestedKey);