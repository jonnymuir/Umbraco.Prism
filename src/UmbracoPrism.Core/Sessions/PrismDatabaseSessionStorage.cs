using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Sessions;

/// <summary>Keeps sessions in the <c>prismSessions</c> table of the Umbraco database.</summary>
public sealed class PrismDatabaseSessionStorage(IUmbracoDatabaseFactory databaseFactory, TimeProvider clock) : IPrismSessionStorage
{
    public Task SaveAsync(string key, byte[] data, DateTimeOffset expiresAt, string? subject, CancellationToken cancellationToken)
    {
        var row = new PrismSessionSchema
        {
            SessionKey = key,
            Subject = subject,
            Data = Convert.ToBase64String(data),
            ExpiresAt = expiresAt.UtcDateTime,
        };

        using var db = databaseFactory.CreateDatabase();
        if (db.Update(row) == 0)
        {
            try
            {
                db.Insert(row);
            }
            catch (Exception) when (db.Update(row) > 0)
            {
                // A concurrent request stored the same session first; its update has now landed.
            }
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        var row = db.SingleOrDefaultById<PrismSessionSchema>(key);
        var live = row is not null && row.ExpiresAt > clock.GetUtcNow().UtcDateTime;
        return Task.FromResult(live ? Convert.FromBase64String(row!.Data) : null);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        db.Execute("DELETE FROM prismSessions WHERE SessionKey = @0", key);
        return Task.CompletedTask;
    }

    public Task<int> RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        return Task.FromResult(db.Execute("DELETE FROM prismSessions WHERE ExpiresAt <= @0", clock.GetUtcNow().UtcDateTime));
    }
}
