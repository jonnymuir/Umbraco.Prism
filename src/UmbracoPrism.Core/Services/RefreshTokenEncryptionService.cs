using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// AES-256-GCM authenticated encryption for Entra refresh tokens.
/// Wire format: Base64([12-byte nonce][ciphertext][16-byte tag]).
/// </summary>
public class RefreshTokenEncryptionService : IRefreshTokenEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32; // 256 bits

    private readonly byte[] _key;

    /// <summary>
    /// Initialises the service from bound <see cref="PrismBiometricOptions"/>.
    /// Throws <see cref="InvalidOperationException"/> when <see cref="PrismBiometricOptions.EncryptionKey"/>
    /// is absent or invalid length.
    /// </summary>
    public RefreshTokenEncryptionService(IOptions<PrismBiometricOptions> options)
    {
        var keyString = options.Value.EncryptionKey;

        if (string.IsNullOrWhiteSpace(keyString))
            throw new InvalidOperationException(
                "Prism: RefreshToken encryption key must be configured. " +
                "Set 'Prism:Biometric:EncryptionKey' (base64-encoded 32-byte key) in configuration.");

        try
        {
            _key = Convert.FromBase64String(keyString);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Prism: RefreshToken encryption key must be a valid base64 string. " +
                "Set 'Prism:Biometric:EncryptionKey' to a base64-encoded 32-byte key.");
        }

        if (_key.Length != KeySize)
            throw new InvalidOperationException(
                $"Prism: RefreshToken encryption key must be exactly {KeySize} bytes (256 bits). " +
                $"Got {_key.Length} bytes. Generate with: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Pack: [nonce][ciphertext][tag]
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);

        var data = Convert.FromBase64String(ciphertext);

        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Prism: Encrypted refresh token data is too short.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(data.Length - TagSize);
        var encrypted = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);

        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
