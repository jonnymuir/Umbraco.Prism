namespace UmbracoPrism.Core.Services;

/// <summary>
/// Issues, validates, and hashes BiometricToken JWTs used for device-based biometric login.
/// </summary>
public interface IBiometricTokenService
{
    /// <summary>
    /// Issues a signed BiometricToken JWT for a registered device.
    /// </summary>
    /// <param name="deviceId">Unique identifier for the device being registered.</param>
    /// <param name="tenantId">Tenant the device is registered against.</param>
    /// <param name="userOid">Entra user object ID of the registering user.</param>
    /// <param name="lifetime">Duration before the token expires.</param>
    /// <returns>A compact serialized, HMAC-SHA256 signed JWT string.</returns>
    string IssueToken(string deviceId, string tenantId, string userOid, TimeSpan lifetime);

    /// <summary>
    /// Validates a BiometricToken JWT — verifies signature, lifetime, and required claims.
    /// </summary>
    /// <param name="token">The compact JWT string to validate.</param>
    /// <returns>Extracted <see cref="BiometricTokenClaims"/> when the token is valid.</returns>
    /// <exception cref="Microsoft.IdentityModel.Tokens.SecurityTokenException">
    /// Thrown when the token is invalid, expired, or has been tampered with.
    /// </exception>
    BiometricTokenClaims ValidateToken(string token);

    /// <summary>
    /// Produces a deterministic SHA-256 hex digest of the raw JWT string for DB storage and lookup.
    /// The hash — not the raw JWT — is persisted in <c>prismBiometricTokens.TokenHash</c>.
    /// </summary>
    /// <param name="token">The raw JWT string.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    string HashToken(string token);
}
