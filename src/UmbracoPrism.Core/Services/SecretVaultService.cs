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
    private readonly SecretClient? _secretClient;
    private readonly IAppPolicyCache _runtimeCache;

    private readonly ILogger<SecretVaultService> _logger;

    /// <summary>
    /// Initializes the Key Vault-backed secret service.
    /// </summary>
    /// <param name="configuration">Application configuration containing the Prism vault URI.</param>
    /// <param name="appCaches">Application cache container used for secret value caching.</param>
    /// <param name="logger">Logger for vault lookup diagnostics without secret contents.</param>
    public SecretVaultService(IConfiguration configuration, AppCaches appCaches, ILogger<SecretVaultService> logger)
    {
        // You'll add "Prism:VaultUri" to your appsettings.json
        var vaultUri = configuration["Prism:VaultUri"];

        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;

        // If no URI is provided, we just don't initialize the client
        if (!string.IsNullOrEmpty(vaultUri))
        {
            _secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        }
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

        // We cache the secret for 1 hour to avoid hitting Azure on every single request
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
        }, TimeSpan.FromHours(1));

        if (secretTask == null) return string.Empty;

        string? secretValue = await secretTask;

        return secretValue ?? string.Empty;
    }
}