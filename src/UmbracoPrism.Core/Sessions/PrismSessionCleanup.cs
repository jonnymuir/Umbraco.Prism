using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.Core.Sessions;

/// <summary>Deletes expired sessions from a store that does not expire them itself. A session is never honoured past its expiry whether or not this has run.</summary>
internal sealed partial class PrismSessionCleanup(
    IPrismSessionStorage storage,
    IRuntimeState runtimeState,
    ILogger<PrismSessionCleanup> logger) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// The first run waits, so it never competes with the package migration that creates the table
    /// for the database (SQLite allows one writer at a time).
    /// </summary>
    internal static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            var removed = await storage.RemoveExpiredAsync(cancellationToken);
            if (removed > 0)
            {
                LogRemoved(removed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {Count} expired member sessions.")]
    private partial void LogRemoved(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Expired member sessions could not be removed; the next run will try again.")]
    private partial void LogFailed(Exception ex);
}
