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
    /// <param name="associatedData">
    /// Context the ciphertext is bound to (for example tenant, user and device). It is not stored,
    /// but decryption fails unless the same value is supplied, so a ciphertext copied to another
    /// credential row is useless.
    /// </param>
    /// <returns>
    /// A versioned string carrying the id of the key used, the nonce, the encrypted data and the
    /// authentication tag.
    /// </returns>
    string Encrypt(string plaintext, string? associatedData = null);

    /// <summary>
    /// Decrypts a previously encrypted refresh token.
    /// </summary>
    /// <param name="ciphertext">Ciphertext from <see cref="Encrypt"/>.</param>
    /// <param name="associatedData">The value given to <see cref="Encrypt"/>.</param>
    /// <returns>The original plaintext refresh token.</returns>
    string Decrypt(string ciphertext, string? associatedData = null);
}
