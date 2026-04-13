namespace UmbracoPrism.Core.Services;

/// <summary>
/// Canonical provider identifiers for tenant secret resolution.
/// </summary>
public static class PrismSecretProviderNames
{
    /// <summary>
    /// Azure Key Vault-backed secret reference.
    /// </summary>
    public const string AzureKeyVault = "azure-key-vault";

    /// <summary>
    /// Dev-only inline secret used for repo-owned localhost demos.
    /// </summary>
    public const string Inline = "inline";
}
