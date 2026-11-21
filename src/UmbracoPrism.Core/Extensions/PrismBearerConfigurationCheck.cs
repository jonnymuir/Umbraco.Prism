using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Warns at startup about downstream API tenants whose bearer settings are weaker than they could be.
/// Without an explicit <c>Audience</c>, a generic OIDC tenant accepts any token issued to its web
/// client (including that client's ID token) through the <c>azp</c> rule.
/// </summary>
internal sealed partial class PrismBearerConfigurationCheck(IConfiguration configuration, ILogger<PrismBearerConfigurationCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var tenants = configuration.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>() ?? [];
        foreach (var tenant in tenants.Where(t => !string.IsNullOrWhiteSpace(t.OidcAuthority) && string.IsNullOrWhiteSpace(t.Audience)))
        {
            LogMissingAudience(logger, LogScrub.Line(tenant.OidcAuthority) ?? string.Empty);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Bearer tenant {Authority} has no Audience configured, so the API accepts tokens issued to its web client (azp). Set PrismBusinessApp:Tenants:n:Audience to the API's own audience.")]
    private static partial void LogMissingAudience(ILogger logger, string authority);
}
