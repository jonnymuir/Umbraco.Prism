using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Warns at startup about downstream API tenants whose bearer settings are weaker than they could be.
/// Without an explicit <c>Audience</c>, a generic OIDC tenant accepts any token issued to its web
/// client (including that client's ID token) through the <c>azp</c> rule.
/// </summary>
internal sealed class PrismBearerConfigurationCheck(IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var tenants = configuration.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>() ?? [];
        var missing = tenants
            .Where(t => !string.IsNullOrWhiteSpace(t.OidcAuthority) && string.IsNullOrWhiteSpace(t.Audience))
            .Select(t => t.Code)
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Prism: these PrismBusinessApp tenants use an OIDC authority but have no Audience, so the API would " +
                $"accept none of their tokens: {string.Join(", ", missing)}. Set PrismBusinessApp:Tenants:n:Audience to " +
                "the API's own audience (the identity provider must add it to access tokens).");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
