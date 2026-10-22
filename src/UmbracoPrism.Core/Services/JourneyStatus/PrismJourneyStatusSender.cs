using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Logging;

namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>Delivers queued statuses one at a time. A failed delivery is logged and the next goes on.</summary>
internal sealed class PrismJourneyStatusSender(
    PrismJourneyStatusQueue queue,
    PrismJourneyStatusDelivery delivery,
    ILogger<PrismJourneyStatusSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var update in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await delivery.DeliverAsync(update, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Journey status for journey {Key} was not delivered.", LogScrub.Line(update.Status.Key));
            }
        }
    }
}
