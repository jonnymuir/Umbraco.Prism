using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Configuration;

/// <summary>
/// Configures <see cref="PrismBiometricOptions"/> by fetching signing and encryption keys 
/// from Azure Key Vault at options-resolution time (lazy, not during IConfigurationBuilder).
/// If Prism:VaultUri is not configured, this becomes a no-op (local development scenario).
/// </summary>
public class PrismKeyVaultConfigureOptions : IConfigureOptions<PrismBiometricOptions>
{
    private const string SigningKeySecretName = "Prism--Biometric--SigningKey";
    private const string EncryptionKeySecretName = "Prism--Biometric--EncryptionKey";

    private readonly IConfiguration _configuration;

    public PrismKeyVaultConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(PrismBiometricOptions options)
    {
        var vaultUri = _configuration["Prism:VaultUri"];

        if (string.IsNullOrWhiteSpace(vaultUri))
            return;

        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"Prism: VaultUri '{vaultUri}' must be a valid HTTPS URI (e.g. https://yourname.vault.azure.net/).");

        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(0.8),
                MaxDelay = TimeSpan.FromSeconds(8),
                Mode = RetryMode.Exponential
            }
        };

        var client = new SecretClient(uri, new DefaultAzureCredential(), clientOptions);

        try
        {
            var signingKey = client.GetSecret(SigningKeySecretName).Value.Value;
            var encryptionKey = client.GetSecret(EncryptionKeySecretName).Value.Value;

            options.SigningKey = signingKey;
            options.EncryptionKey = encryptionKey;
        }
        catch (RequestFailedException ex) when (ex.Status == 401)
        {
            throw new InvalidOperationException(
                "Prism: Key Vault authentication failed. Ensure the application identity has been granted 'Get' permission on secrets. " +
                "In production, enable Managed Identity. Locally, ensure you are signed in to Azure CLI (`az login`).",
                ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.Status == 403)
        {
            throw new InvalidOperationException(
                $"Prism: Key Vault secret not found or access denied (status {ex.Status}). " +
                "Ensure the required Prism biometric secrets (Prism:Biometric) exist in the vault and the identity has 'Get' permission.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Prism: Key Vault temporarily unavailable after retries. Check network connectivity and vault availability.",
                ex);
        }
    }
}
