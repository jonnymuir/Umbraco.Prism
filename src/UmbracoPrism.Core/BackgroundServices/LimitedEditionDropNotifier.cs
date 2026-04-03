using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.BackgroundServices;

/// <summary>
/// Background scheduled task that simulates limited edition vinyl drop notifications.
/// Reads config: <c>Prism:Notifications:LimitedEditionDropIntervalMinutes</c> (default: 60 minutes).
/// If value is 0, the notifier is disabled.
/// </summary>
public class LimitedEditionDropNotifier : BackgroundService
{
    private const string IntervalConfigKey = "Prism:Notifications:LimitedEditionDropIntervalMinutes";
    private const string TenantIdConfigKey = "Prism:Notifications:LimitedEditionTenantId";

    private readonly IPrismNotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LimitedEditionDropNotifier> _logger;

    public LimitedEditionDropNotifier(
        IPrismNotificationService notificationService,
        IConfiguration configuration,
        ILogger<LimitedEditionDropNotifier> logger)
    {
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var intervalMinutes = _configuration.GetValue<int>(IntervalConfigKey, 60);
            
            if (intervalMinutes <= 0)
            {
                _logger.LogInformation(
                    "LimitedEditionDropNotifier is disabled ({ConfigKey} = {Value}).",
                    IntervalConfigKey, intervalMinutes);
                return;
            }

            var interval = TimeSpan.FromMinutes(intervalMinutes);
            _logger.LogInformation(
                "LimitedEditionDropNotifier started with interval: {Interval}",
                interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken);

                try
                {
                    await FireNotificationAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "LimitedEditionDropNotifier: Failed to send notification.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LimitedEditionDropNotifier stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LimitedEditionDropNotifier: Fatal error in background service.");
        }
    }

    private async Task FireNotificationAsync(CancellationToken cancellationToken)
    {
        var tenantId = _configuration[TenantIdConfigKey];

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning(
                "LimitedEditionDropNotifier: {ConfigKey} not configured — skipping notification.",
                TenantIdConfigKey);
            return;
        }

        const string title = "🎵 Limited Edition Drop!";
        const string body = "A limited edition vinyl has just dropped in the Vinyl Vault. Don't miss out!";

        _logger.LogInformation(
            "LimitedEditionDropNotifier: Sending notification to tenant {TenantId}",
            tenantId);

        await _notificationService.SendNotificationToAllMembersAsync(tenantId, title, body, cancellationToken);
    }
}
