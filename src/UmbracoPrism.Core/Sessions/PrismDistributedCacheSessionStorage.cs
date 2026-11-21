using Microsoft.Extensions.Caching.Distributed;

namespace UmbracoPrism.Core.Sessions;

/// <summary>
/// Keeps sessions in the host's <see cref="IDistributedCache"/>, such as Redis. Entries expire on
/// their own, so there is nothing to clean up.
/// </summary>
public sealed class PrismDistributedCacheSessionStorage(IDistributedCache cache) : IPrismSessionStorage
{
    private const string KeyPrefix = "prism:session:";

    public Task SaveAsync(string key, byte[] data, DateTimeOffset expiresAt, string? subject, CancellationToken cancellationToken) =>
        cache.SetAsync(KeyPrefix + key, data, new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt }, cancellationToken);

    public async Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken) =>
        await cache.GetAsync(KeyPrefix + key, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        cache.RemoveAsync(KeyPrefix + key, cancellationToken);

    public Task<int> RemoveExpiredAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}
