namespace UmbracoPrism.Core.Services;

/// <summary>
/// What a stored biometric refresh token is bound to: the same tenant, user and device must be
/// presented to decrypt it, so a ciphertext copied onto another credential row does not open.
/// </summary>
public static class BiometricCredentialContext
{
    /// <summary>The associated data for a credential's encrypted refresh token.</summary>
    public static string For(string tenantId, string userId, string deviceId) =>
        $"prism-biometric|{tenantId}|{userId}|{deviceId}";
}
