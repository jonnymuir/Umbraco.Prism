using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The member cookie, the mobile handover ticket and antiforgery tokens are only as strong as the
/// Data Protection key ring. Outside Development this checks that the host has chosen where keys
/// are stored and how they are protected, and refuses to start when it has not. Set
/// <c>Prism:DataProtection:RequireConfiguredKeys</c> to <c>false</c> to log a warning and start anyway.
/// </summary>
internal sealed partial class PrismDataProtectionGuard(
    IOptions<KeyManagementOptions> keyManagement,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<PrismDataProtectionGuard> logger) : IHostedService
{
    internal const string RequireConfigurationKey = "Prism:DataProtection:RequireConfiguredKeys";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        var problems = Inspect(keyManagement.Value);
        if (problems.Count == 0)
        {
            return Task.CompletedTask;
        }

        var message = "Data Protection is not fully configured: " + string.Join(" ", problems);
        if (configuration.GetValue(RequireConfigurationKey, true))
        {
            throw new InvalidOperationException(message);
        }

        LogDataProtection(logger, message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The ways the key ring is left to framework defaults, or an empty list when the host chose both.</summary>
    internal static List<string> Inspect(KeyManagementOptions options)
    {
        var problems = new List<string>();
        if (options.XmlRepository is null)
        {
            problems.Add("Keys are not persisted to a store the host chose (use PersistKeysTo...), so they may be lost on restart or shared across instances unevenly.");
        }

        if (options.XmlEncryptor is null)
        {
            problems.Add("Keys are not protected at rest (use ProtectKeysWith...), so anyone who can read the key store can forge a member cookie.");
        }

        return problems;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private static partial void LogDataProtection(ILogger logger, string message);
}
