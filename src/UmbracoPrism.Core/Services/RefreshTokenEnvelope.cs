using System.Security.Cryptography;
using System.Text;

namespace UmbracoPrism.Core.Services;

/// <summary>Key derivation and parsing for the versioned encrypted refresh token values.</summary>
internal static class RefreshTokenEnvelope
{
    private const string DerivationContext = "prism-biometric-encryption|";
    private const int KeySize = 32;

    /// <summary>The AES key for one credential: HKDF-SHA256 of the configured key, with its context as info.</summary>
    public static byte[] DeriveKey(byte[] configuredKey, string? associatedData) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, configuredKey, KeySize, salt: [],
            info: Encoding.UTF8.GetBytes(DerivationContext + associatedData));

    /// <summary>
    /// Splits <c>{prefix}{keyId}.{base64}</c> into the configured key it names and the decoded data.
    /// Throws when the key id is not one of <paramref name="keysById"/>.
    /// </summary>
    public static (byte[] Key, byte[] Data) Split(string ciphertext, string prefix, IReadOnlyDictionary<string, byte[]> keysById)
    {
        var parts = ciphertext[prefix.Length..].Split('.', 2);
        if (parts.Length != 2 || !keysById.TryGetValue(parts[0], out var key))
        {
            throw new CryptographicException("Prism: Encrypted refresh token names a key that is not configured.");
        }

        return (key, Convert.FromBase64String(parts[1]));
    }
}
