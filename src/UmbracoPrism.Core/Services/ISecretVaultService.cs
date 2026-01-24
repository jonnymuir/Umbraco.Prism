namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for retrieving secrets from a secure vault.
/// </summary>
public interface ISecretVaultService
{
    /// <summary>
    /// Retrieves a secret value from the vault by its key name.
    /// </summary>
    Task<string> GetSecretAsync(string secretName);
}