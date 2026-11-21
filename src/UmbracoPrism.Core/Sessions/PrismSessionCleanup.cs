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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
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

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {Count} expired member sessions.")]
    private partial void LogRemoved(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Expired member sessions could not be removed; the next run will try again.")]
    private partial void LogFailed(Exception ex);
}
