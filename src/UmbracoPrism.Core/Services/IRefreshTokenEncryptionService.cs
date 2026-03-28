namespace UmbracoPrism.Core.Services;

/// <summary>
/// Encrypts and decrypts Entra refresh tokens using AES-256-GCM.
/// The raw refresh token is never stored; only the ciphertext is persisted.
/// </summary>
public interface IRefreshTokenEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext refresh token using AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">The raw Entra refresh token.</param>
    /// <returns>Base64-encoded ciphertext containing nonce, encrypted data, and authentication tag.</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a previously encrypted refresh token.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded ciphertext from <see cref="Encrypt"/>.</param>
    /// <returns>The original plaintext refresh token.</returns>
    string Decrypt(string ciphertext);
}
