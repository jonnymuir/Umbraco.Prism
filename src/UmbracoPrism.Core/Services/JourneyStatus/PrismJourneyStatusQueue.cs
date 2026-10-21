using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Logging;

namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>A status waiting to be delivered to a member's devices.</summary>
internal sealed record PrismJourneyStatusUpdate(string UserId, string TenantId, PrismJourneyStatus Status);

/// <summary>
/// Holds published statuses in memory until <see cref="PrismJourneyStatusSender"/> delivers them.
/// A status still queued when the site stops is lost, which costs the member one lock screen
/// update, never the journey's own state. When the queue is full the oldest status is dropped,
/// since a newer one for the same journey supersedes it.
/// </summary>
internal sealed class PrismJourneyStatusQueue(ILogger<PrismJourneyStatusQueue> logger) : IPrismJourneyStatusService
{
    private readonly Channel<PrismJourneyStatusUpdate> _channel = Channel.CreateBounded<PrismJourneyStatusUpdate>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<PrismJourneyStatusUpdate> Reader => _channel.Reader;

    public void Publish(string userId, string tenantId, PrismJourneyStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (status.Problem() is { } problem)
        {
            throw new ArgumentException(problem, nameof(status));
        }

        _channel.Writer.TryWrite(new PrismJourneyStatusUpdate(userId, tenantId, status));
        logger.LogDebug("Journey status {Kind} queued for journey {Key}.", status.Kind, LogScrub.Line(status.Key));
    }
}
