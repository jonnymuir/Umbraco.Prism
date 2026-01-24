using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;

namespace UmbracoPrism.Core.Services;

public class SecretVaultService : ISecretVaultService
{
    private readonly SecretClient? _secretClient;
    private readonly IAppPolicyCache _runtimeCache;

    public SecretVaultService(IConfiguration configuration, AppCaches appCaches)
    {
        // You'll add "Prism:VaultUri" to your appsettings.json
        var vaultUri = configuration["Prism:VaultUri"];

        _runtimeCache = appCaches.RuntimeCache;

       // If no URI is provided, we just don't initialize the client
        if (!string.IsNullOrEmpty(vaultUri))
        {
            _secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        }
    }

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
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value;
        }, TimeSpan.FromHours(1));

        if (secretTask == null) return string.Empty;

        string? secretValue = await secretTask;

        return secretValue ?? string.Empty;
    }
}