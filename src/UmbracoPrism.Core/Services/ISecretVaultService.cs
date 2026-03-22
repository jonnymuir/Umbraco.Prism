namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for retrieving secrets from a secure vault.
/// </summary>
public interface ISecretVaultService
{
    /// <summary>
    /// Retrieves a secret value from the vault by its key name.
    /// </summary>
    /// <param name="secretName">The key vault secret name reference stored for a tenant.</param>
    /// <returns>The resolved secret value, or an empty string when unavailable.</returns>
    Task<string> GetSecretAsync(string secretName);
}