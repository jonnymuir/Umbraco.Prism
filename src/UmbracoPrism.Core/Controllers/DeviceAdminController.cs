using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Admin-only controller for managing biometric device credentials.
/// Requires PrismMemberCookie authentication and PrismAdmins authorization policy.
/// </summary>
[Route("api/prism/device")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[Authorize(Policy = "PrismAdmins")]
public class DeviceAdminController(
    IUmbracoDatabaseFactory databaseFactory,
    IPrismContext prismContext,
    ILogger<DeviceAdminController> logger) : Controller
{
    /// <summary>
    /// Revokes a biometric device credential by DeviceId, scoped to the current tenant.
    /// Soft-deletes by setting RevokedAt. Returns 204 on success; 404 if the device
    /// is not found within the current tenant.
    /// </summary>
    [HttpDelete("{deviceId}")]
    public IActionResult Revoke(string deviceId)
    {
        // 1. Verify tenant context
        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("Device admin revoke: no tenant context resolved");
            return BadRequest(new { error = "No tenant context available." });
        }

        // 2. Look up credential scoped to current tenant (prevents cross-tenant deletion)
        var tenantId = tenant.Id.ToString();
        using var db = databaseFactory.CreateDatabase();

        var credential = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE DeviceId = @0 AND TenantId = @1", deviceId, tenantId);

        if (credential == null)
        {
            logger.LogWarning(
                "Device admin revoke: device {DeviceId} not found in tenant {TenantId}",
                deviceId, tenantId);
            return NotFound();
        }

        // 3. Soft-delete (idempotent — already-revoked records stay revoked)
        if (credential.RevokedAt == null)
        {
            credential.RevokedAt = DateTime.UtcNow;
            db.Update(credential);

            logger.LogInformation(
                "Device admin revoke: revoked device {DeviceId} in tenant {TenantId}",
                deviceId, tenantId);
        }

        return NoContent();
    }
}
