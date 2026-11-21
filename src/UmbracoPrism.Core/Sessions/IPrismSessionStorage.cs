namespace UmbracoPrism.Core.Sessions;

/// <summary>
/// Where member sign-in sessions are kept on the server. Prism ships two: the Umbraco database
/// (the default, nothing to set up) and any <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// such as Redis. A host with another store registers its own implementation.
/// </summary>
public interface IPrismSessionStorage
{
    /// <summary>Saves a session under its key, replacing any earlier value. <paramref name="subject"/> identifies the member, for removing all of their sessions.</summary>
    Task SaveAsync(string key, byte[] data, DateTimeOffset expiresAt, string? subject, CancellationToken cancellationToken);

    /// <summary>Returns the session's data, or null when the key is unknown or the session has expired.</summary>
    Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes sessions past their expiry and returns how many went. A store that expires entries itself returns 0.</summary>
    Task<int> RemoveExpiredAsync(CancellationToken cancellationToken);
}
