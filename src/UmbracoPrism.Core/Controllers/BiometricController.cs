using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Handles biometric device registration for mobile app users.
/// All endpoints require an authenticated PrismMemberCookie session.
/// </summary>
[Route("umbraco/prism/mobile/biometric")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class BiometricController(
    IUmbracoDatabaseFactory databaseFactory,
    IBiometricTokenService biometricTokenService,
    IRefreshTokenEncryptionService encryptionService,
    IPrismContext prismContext,
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
}
