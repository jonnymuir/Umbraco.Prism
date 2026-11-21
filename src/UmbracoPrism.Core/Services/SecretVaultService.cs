using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Retrieves tenant client secrets from Azure Key Vault with runtime caching.
/// </summary>
public class SecretVaultService : ISecretVaultService
{
    /// <summary>How long a failed lookup is remembered, so a vault outage is not hit on every request.</summary>
    internal static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(15);

    private readonly SecretClient? _secretClient;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly TimeSpan _cacheLifetime;
    private readonly TimeProvider _timeProvider;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _failedAt = new(StringComparer.Ordinal);

    private readonly ILogger<SecretVaultService> _logger;

    /// <summary>
    /// Initializes the Key Vault-backed secret service.
    /// </summary>
    /// <param name="configuration">Application configuration containing the Prism vault URI.</param>
    /// <param name="appCaches">Application cache container used for secret value caching.</param>
    /// <param name="logger">Logger for vault lookup diagnostics without secret contents.</param>
    public SecretVaultService(IConfiguration configuration, AppCaches appCaches, ILogger<SecretVaultService> logger)
        : this(CreateClient(configuration), configuration, appCaches, logger, TimeProvider.System)
    {
    }

    internal SecretVaultService(
        SecretClient? secretClient,
        IConfiguration configuration,
        AppCaches appCaches,
        ILogger<SecretVaultService> logger,
        TimeProvider timeProvider)
    {
        _secretClient = secretClient;
        _timeProvider = timeProvider;

        // A rotated secret reaches Prism within this many minutes (default 60).
        _cacheLifetime = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Prism:Secrets:CacheMinutes", 60)));
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;
    }

    private static SecretClient? CreateClient(IConfiguration configuration)
    {
        // You'll add "Prism:VaultUri" to your appsettings.json
        var vaultUri = configuration["Prism:VaultUri"];

        // If no URI is provided, we just don't initialize the client
        return string.IsNullOrEmpty(vaultUri)
            ? null
            : new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    /// <summary>
    /// Retrieves a tenant secret value from the configured vault and caches it.
    /// </summary>
    /// <param name="secretName">The secret name reference to resolve.</param>
    /// <returns>The secret value, or an empty string when the secret cannot be resolved.</returns>
    public async Task<string> GetSecretAsync(string secretName)
    {
        if (_secretClient == null || string.IsNullOrEmpty(secretName))
            return string.Empty;

        if (string.IsNullOrEmpty(secretName)) return string.Empty;

        string cacheKey = $"Prism_Secret_{secretName}";

        if (_runtimeCache == null) return string.Empty;

        // A recent failure is not retried for a few seconds, and is never cached as a value.
        if (_failedAt.TryGetValue(secretName, out var failedAt) && _timeProvider.GetUtcNow() - failedAt < FailureBackoff)
        {
            return string.Empty;
        }

        // A successful lookup is cached to avoid hitting Azure on every request
        var secretTask = _runtimeCache.GetCacheItem(cacheKey, async () =>
        {
            try
            {
                KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
                return secret.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Prism: The secret '{SecretName}' was not found in the Key Vault. Ensure the name matches the Azure portal.", secretName);
                return null;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Prism: Azure Key Vault request failed for secret '{SecretName}'. Status: {Status}", secretName, ex.Status);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prism: An unexpected error occurred while retrieving secret '{SecretName}'.", secretName);
                return null;
            }
        }, _cacheLifetime);

        if (secretTask == null) return string.Empty;

        string? secretValue = await secretTask;

        if (secretValue is null)
        {
            // The cache holds the task, so a failed lookup would otherwise be served for the whole
            // lifetime. Drop it and back off briefly instead.
            _runtimeCache.ClearByKey(cacheKey);
            _failedAt[secretName] = _timeProvider.GetUtcNow();
            return string.Empty;
        }

        _failedAt.TryRemove(secretName, out _);
        return secretValue;
    }

    /// <inheritdoc />
    public Task<string> ResolveSecretAsync(string? provider, string? reference)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult(string.Empty);
        }

        var normalizedProvider = provider.Trim();

        if (string.Equals(normalizedProvider, PrismSecretProviderNames.AzureKeyVault, StringComparison.OrdinalIgnoreCase))
        {
            return GetSecretAsync(reference.Trim());
        }

        if (string.Equals(normalizedProvider, PrismSecretProviderNames.Inline, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(reference);
        }

        _logger.LogError(
            "Prism: Secret resolution failed because provider '{Provider}' is not supported.",
            normalizedProvider);

        return Task.FromResult(string.Empty);
    }
}
