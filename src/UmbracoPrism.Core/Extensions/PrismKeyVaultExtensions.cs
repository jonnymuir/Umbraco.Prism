using Azure.Identity;
using Microsoft.AspNetCore.Builder;

namespace UmbracoPrism.Core.Extensions;

public static class PrismKeyVaultExtensions
{
    /// <summary>
    /// Adds Azure Key Vault as a configuration source using the vault URI specified in Prism:VaultUri.
    /// Skips silently if Prism:VaultUri is not configured (local development scenario).
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Prism:VaultUri is configured but is not a valid HTTPS URI.
    /// </exception>
    public static WebApplicationBuilder AddPrismKeyVault(this WebApplicationBuilder builder)
    {
        var vaultUri = builder.Configuration["Prism:VaultUri"];
        
        if (string.IsNullOrWhiteSpace(vaultUri))
            return builder;
        
        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"Prism: VaultUri '{vaultUri}' must be a valid HTTPS URI (e.g. https://yourname.vault.azure.net/).");
        
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
        
        return builder;
    }
}
