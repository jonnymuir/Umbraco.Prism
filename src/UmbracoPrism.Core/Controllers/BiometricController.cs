using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Handles biometric device registration and token exchange for mobile app users.
/// Registration requires an authenticated PrismMemberCookie session; exchange is
/// unauthenticated — the BiometricToken JWT is the sole credential.
/// </summary>
[Route("umbraco/prism/mobile/biometric")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class BiometricController(
    IUmbracoDatabaseFactory databaseFactory,
    IBiometricTokenService biometricTokenService,
    IRefreshTokenEncryptionService encryptionService,
    IPrismContext prismContext,
    IPrismTokenRefreshService tokenRefreshService,
    ISecretVaultService vault,
    IOptions<PrismBiometricOptions> biometricOptions,
    ILogger<BiometricController> logger) : Controller
{
    /// <summary>
    /// Registers a biometric credential for the authenticated user's device.
    /// Issues a signed BiometricToken JWT, encrypts the active Entra refresh token,
    /// and persists the credential record. Upserts if a record already exists for
    /// this tenant + device combination.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] BiometricRegistrationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 1. Verify tenant context
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("Biometric register: no tenant context resolved");
            return BadRequest(new { error = "No tenant context available." });
        }

        // 2. Extract user OID from cookie claims
        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            logger.LogWarning("Biometric register: user OID claim not found in principal");
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        // 3. Extract the Entra refresh token from the active session
        var authResult = await HttpContext.AuthenticateAsync("PrismMemberCookie");
        if (!authResult.Succeeded || authResult.Properties == null)
        {
            logger.LogWarning("Biometric register: authentication result unavailable");
            return Unauthorized(new { error = "Session authentication failed." });
        }

        var tokens = authResult.Properties.GetTokens();
        var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogWarning("Biometric register: no refresh token in session for user {UserOid}", userOid);
            return BadRequest(new { error = "No refresh token available in current session." });
        }

        // 4. Issue the BiometricToken JWT
        var lifetime = TimeSpan.FromDays(biometricOptions.Value.TokenLifetimeDays);
        var tenantId = tenant.Id.ToString();
        var jwt = biometricTokenService.IssueToken(request.DeviceId, tenantId, userOid, lifetime);

        // 5. Hash the JWT (never store the raw token server-side)
        var tokenHash = biometricTokenService.HashToken(jwt);

        // 6. Encrypt the refresh token (AES-256-GCM)
        var refreshTokenEnc = encryptionService.Encrypt(refreshToken);

        // 7. Persist (upsert by TenantId + UserId + DeviceId)
        var expiresAt = DateTime.UtcNow.Add(lifetime);

        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE TenantId = @0 AND DeviceId = @1 AND UserId = @2", tenantId, request.DeviceId, userOid);

        if (existing != null)
        {
            existing.TokenHash = tokenHash;
            existing.RefreshTokenEnc = refreshTokenEnc;
            existing.ExpiresAt = expiresAt;
            existing.RegisteredAt = DateTime.UtcNow;
            existing.RevokedAt = null;
            existing.FailedAttempts = 0;
            existing.LockedUntil = null;
            existing.LastUsedAt = null;
            existing.DeviceName = request.DeviceName;
            existing.Platform = request.Platform;
            db.Update(existing);

            logger.LogInformation(
                "Biometric register: upserted credential for device {DeviceId} tenant {TenantId}",
                request.DeviceId, tenantId);
        }
        else
        {
            var record = new PrismDeviceCredentialSchema
            {
                DeviceId = request.DeviceId,
                TenantId = tenantId,
                UserId = userOid,
                DeviceName = request.DeviceName,
                TokenHash = tokenHash,
                RefreshTokenEnc = refreshTokenEnc,
                FailedAttempts = 0,
                RegisteredAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Platform = request.Platform,
            };
            db.Insert(record);

            logger.LogInformation(
                "Biometric register: created credential for device {DeviceId} tenant {TenantId}",
                request.DeviceId, tenantId);
        }

        // 8. Return the token to the client (stored in device Keychain/Keystore)
        return Ok(new BiometricRegistrationResponse
        {
            BiometricToken = jwt,
            ExpiresAt = expiresAt,
        });
    }

    /// <summary>
    /// Exchanges a BiometricToken JWT for a PrismMemberCookie session.
    /// This endpoint is unauthenticated — the biometric token IS the credential.
    /// Performs rolling refresh token rotation on every successful exchange.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] BiometricExchangeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 1. Verify tenant context
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("Biometric exchange: no tenant context resolved");
            return BadRequest(new { error = "No tenant context available." });
        }

        // 2. Validate biometric token JWT (signature, lifetime, claims)
        BiometricTokenClaims claims;
        try
        {
            claims = biometricTokenService.ValidateToken(request.BiometricToken);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            logger.LogWarning("Biometric exchange: token validation failed — {Reason}", ex.Message);
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        // 3. Verify tenantId claim matches request tenant
        var tenantId = tenant.Id.ToString();
        if (!string.Equals(claims.TenantId, tenantId, StringComparison.Ordinal))
        {
            logger.LogWarning("Biometric exchange: tenant mismatch (token={TokenTid}, request={RequestTid})",
                claims.TenantId, tenantId);
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        // 4. Hash the token and look up the credential row (include TenantId + UserId per security policy)
        var tokenHash = biometricTokenService.HashToken(request.BiometricToken);

        using var db = databaseFactory.CreateDatabase();
        var credential = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE TokenHash = @0 AND TenantId = @1 AND UserId = @2",
            tokenHash, claims.TenantId, claims.UserOid);

        if (credential == null)
        {
            logger.LogWarning("Biometric exchange: no credential found for token hash");
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        // 5. Assert not revoked and not expired
        if (credential.RevokedAt != null)
        {
            logger.LogWarning("Biometric exchange: credential has been revoked");
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        if (credential.ExpiresAt <= DateTime.UtcNow)
        {
            logger.LogWarning("Biometric exchange: credential has expired");
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        // 6. DeviceId binding check — JWT deviceId must match DB row
        if (!string.Equals(credential.DeviceId, claims.DeviceId, StringComparison.Ordinal))
        {
            logger.LogWarning("Biometric exchange: device mismatch (token={TokenDev}, db={DbDev})",
                claims.DeviceId, credential.DeviceId);
            return Unauthorized(new { error = "device_mismatch" });
        }

        // 7. UserId binding check — prevent cross-user token substitution
        if (!string.Equals(credential.UserId, claims.UserOid, StringComparison.Ordinal))
        {
            logger.LogWarning("Biometric exchange: userId mismatch (token={TokenUid}, db={DbUid})",
                claims.UserOid, credential.UserId);
            return Unauthorized(new { error = "biometric_token_invalid" });
        }

        // 8. Decrypt the stored Entra refresh token
        string refreshToken;
        try
        {
            refreshToken = encryptionService.Decrypt(credential.RefreshTokenEnc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Biometric exchange: failed to decrypt refresh token for device {DeviceId}", credential.DeviceId);
            return Unauthorized(new { error = "credential_refresh_failed" });
        }

        // 9. Call Entra /token endpoint with the decrypted refresh_token
        if (string.IsNullOrWhiteSpace(tenant.EntraTenantId) ||
            string.IsNullOrWhiteSpace(tenant.EntraClientId) ||
            string.IsNullOrWhiteSpace(tenant.SecretKeyName))
        {
            logger.LogWarning("Biometric exchange: tenant missing Entra configuration");
            return Unauthorized(new { error = "credential_refresh_failed" });
        }

        var clientSecret = await vault.GetSecretAsync(tenant.SecretKeyName);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            logger.LogWarning("Biometric exchange: client secret could not be resolved from vault");
            return Unauthorized(new { error = "credential_refresh_failed" });
        }

        var tokenEndpoint = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token";
        var formParameters = new Dictionary<string, string>
        {
            { "client_id", tenant.EntraClientId },
            { "client_secret", clientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "scope", $"openid profile offline_access {tenant.EntraClientId}/.default" }
        };

        var tokenResult = await tokenRefreshService.RefreshAsync(
            tokenEndpoint, formParameters, HttpContext.RequestAborted);

        if (!tokenResult.Success || tokenResult.AccessToken == null)
        {
            logger.LogWarning("Biometric exchange: Entra token refresh failed for device {DeviceId}", credential.DeviceId);
            return Unauthorized(new { error = "credential_refresh_failed" });
        }

        // 10. Rolling rotation — re-encrypt and store the new refresh token
        var newRefreshToken = tokenResult.RefreshToken ?? refreshToken;
        credential.RefreshTokenEnc = encryptionService.Encrypt(newRefreshToken);
        credential.LastUsedAt = DateTime.UtcNow;
        db.Update(credential);

        logger.LogInformation(
            "Biometric exchange: successful for device {DeviceId} tenant {TenantId}",
            credential.DeviceId, tenantId);

        // 11. Build ClaimsPrincipal and issue PrismMemberCookie
        var identity = new ClaimsIdentity("PrismMemberCookie");
        identity.AddClaim(new Claim("oid", claims.UserOid));
        identity.AddClaim(new Claim("tid", tenant.EntraTenantId));

        var principal = new ClaimsPrincipal(identity);

        var expiresAt = DateTimeOffset.UtcNow
            .AddSeconds(tokenResult.ExpiresIn ?? 3600)
            .ToString("o");

        var authProps = new AuthenticationProperties();
        authProps.StoreTokens([
            new AuthenticationToken { Name = "access_token", Value = tokenResult.AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = newRefreshToken },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt },
        ]);

        await HttpContext.SignInAsync("PrismMemberCookie", principal, authProps);

        return Ok();
    }

    /// <summary>
    /// Removes the authenticated user's biometric credential for a specific device on the current tenant.
    /// Soft-deletes by setting RevokedAt; idempotent (returns 204 even if no active record exists).
    /// </summary>
    [HttpDelete("unenrol/{deviceId}")]
    public IActionResult Unenrol(string deviceId)
    {
        // 1. Verify tenant context
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("Biometric unenrol: no tenant context resolved");
            return BadRequest(new { error = "No tenant context available." });
        }

        // 2. Extract user OID from cookie claims
        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
        {
            logger.LogWarning("Biometric unenrol: user OID claim not found in principal");
            return Unauthorized(new { error = "User identity could not be determined." });
        }

        // 3. Look up and soft-delete the credential (scoped by TenantId + UserId + DeviceId)
        var tenantId = tenant.Id.ToString();
        using var db = databaseFactory.CreateDatabase();

        var credential = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE TenantId = @0 AND UserId = @1 AND DeviceId = @2 AND RevokedAt IS NULL", tenantId, userOid, deviceId);

        if (credential != null)
        {
            credential.RevokedAt = DateTime.UtcNow;
            db.Update(credential);

            logger.LogInformation(
                "Biometric unenrol: revoked credential for user {UserOid} device {DeviceId} tenant {TenantId}",
                userOid, deviceId, tenantId);
        }
        else
        {
            logger.LogInformation(
                "Biometric unenrol: no active credential found for user {UserOid} device {DeviceId} tenant {TenantId} (idempotent)",
                userOid, deviceId, tenantId);
        }

        return NoContent();
    }
}
