using UmbracoPrism.Core.Sessions;

namespace UmbracoPrism.Core.Tests;

/// <summary>Session storage for tests that need a working ticket store and no database.</summary>
    internal sealed class InMemoryPrismSessionStorage : IPrismSessionStorage
    {
        public Dictionary<string, (byte[] Data, DateTimeOffset ExpiresAt, string? Subject)> Rows { get; } = [];

        public Task SaveAsync(string key, byte[] data, DateTimeOffset expiresAt, string? subject, CancellationToken cancellationToken)
        {
            Rows[key] = (data, expiresAt, subject);
            return Task.CompletedTask;
        }

        public Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.TryGetValue(key, out var row) ? row.Data : null);

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            Rows.Remove(key);
            return Task.CompletedTask;
        }

        public Task<int> RemoveExpiredAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
