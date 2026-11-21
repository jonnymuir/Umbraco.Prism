using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// AES-256-GCM authenticated encryption for Entra refresh tokens.
/// Wire format: <c>v3.{keyId}.{Base64([12-byte nonce][ciphertext][16-byte tag])}</c>, where the key id
/// names the configured key that encrypted it so keys can be rotated, and the caller's associated
/// data is authenticated but not stored. The AES key is not the configured key itself: it is derived
/// from it with HKDF-SHA256 using the associated data (which names the tenant, user and device) as
/// context, so every credential is encrypted under its own key. A <c>v2.</c> value, encrypted
/// directly under the configured key, and an older unversioned <c>Base64([nonce][ciphertext][tag])</c>
/// value (no associated data) still decrypt.
/// </summary>
public class RefreshTokenEncryptionService : IRefreshTokenEncryptionService
{
    private const string VersionPrefix = "v3.";
    private const string DirectKeyPrefix = "v2.";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32; // 256 bits

    private readonly KeyValuePair<string, byte[]> _current;
    private readonly Dictionary<string, byte[]> _keysById;

    /// <summary>
    /// Initialises the service from bound <see cref="PrismBiometricOptions"/>.
    /// Throws <see cref="InvalidOperationException"/> when <see cref="PrismBiometricOptions.EncryptionKey"/>
    /// is absent or invalid length.
    /// </summary>
    public RefreshTokenEncryptionService(IOptions<PrismBiometricOptions> options)
    {
        var configured = options.Value;
        var key = ParseKey(configured.EncryptionKey, "Prism:Biometric:EncryptionKey");
        _current = KeyValuePair.Create(KeyId(key), key);
        _keysById = new Dictionary<string, byte[]> { [_current.Key] = key };

        for (var i = 0; i < configured.PreviousEncryptionKeys.Length; i++)
        {
            var previous = ParseKey(configured.PreviousEncryptionKeys[i], $"Prism:Biometric:PreviousEncryptionKeys:{i}");
            _keysById.TryAdd(KeyId(previous), previous);
        }
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext, string? associatedData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(RefreshTokenEnvelope.DeriveKey(_current.Value, associatedData), TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, Encoding.UTF8.GetBytes(associatedData ?? string.Empty));

        // Pack: [nonce][ciphertext][tag]
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return $"{VersionPrefix}{_current.Key}.{Convert.ToBase64String(result)}";
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext, string? associatedData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);

        var aad = Encoding.UTF8.GetBytes(associatedData ?? string.Empty);
        if (ciphertext.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            var (key, data) = RefreshTokenEnvelope.Split(ciphertext, VersionPrefix, _keysById);
            return Open(data, [RefreshTokenEnvelope.DeriveKey(key, associatedData)], aad);
        }

        if (ciphertext.StartsWith(DirectKeyPrefix, StringComparison.Ordinal))
        {
            var (key, data) = RefreshTokenEnvelope.Split(ciphertext, DirectKeyPrefix, _keysById);
            return Open(data, [key], aad);
        }

        // Unversioned: encrypted under the key then configured, with no associated data.
        return Open(Convert.FromBase64String(ciphertext), _keysById.Values, []);
    }

    private static string Open(byte[] data, IEnumerable<byte[]> candidateKeys, byte[] associatedData)
    {
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Prism: Encrypted refresh token data is too short.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(data.Length - TagSize);
        var encrypted = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);
        var plaintext = new byte[encrypted.Length];

        CryptographicException? last = null;
        foreach (var key in candidateKeys)
        {
            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, encrypted, tag, plaintext, associatedData);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch (CryptographicException ex)
            {
                last = ex;
            }
        }

        throw last ?? new CryptographicException("Prism: Encrypted refresh token could not be decrypted.");
    }

    /// <summary>An 8 character identifier for a key. It reveals nothing useful about the key.</summary>
    private static string KeyId(byte[] key) => Convert.ToHexStringLower(SHA256.HashData(key).AsSpan(0, 4));

    private static byte[] ParseKey(string? keyString, string settingName)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            throw new InvalidOperationException(
                "Prism: RefreshToken encryption key must be configured. " +
                $"Set '{settingName}' (base64-encoded 32-byte key) in configuration.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyString);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Prism: RefreshToken encryption key must be a valid base64 string. " +
                $"Set '{settingName}' to a base64-encoded 32-byte key.");
        }

        if (key.Length != KeySize)
            throw new InvalidOperationException(
                $"Prism: RefreshToken encryption key must be exactly {KeySize} bytes (256 bits). " +
                $"Got {key.Length} bytes. Generate with: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");

        return key;
    }
}
