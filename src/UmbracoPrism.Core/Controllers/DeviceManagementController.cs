using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Lets a backoffice administrator see and revoke the biometric device credentials of a tenant.
/// Revoking sets <c>RevokedAt</c>; the credential's next exchange is refused.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[Authorize(Policy = "PrismAdmins")]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public partial class DeviceManagementController(
    IUmbracoDatabaseFactory databaseFactory,
    ILogger<DeviceManagementController> logger) : ManagementApiControllerBase
{
    /// <summary>Lists the device credentials registered on a tenant, newest first.</summary>
    [HttpGet("tenants/{id:int}/devices")]
    public IActionResult GetDevices(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        if (db.SingleOrDefaultById<PrismTenantSchema>(id) is null) return NotFound();

        var credentials = db.Fetch<PrismDeviceCredentialSchema>(
            "WHERE TenantId = @0 ORDER BY RegisteredAt DESC", id.ToString(CultureInfo.InvariantCulture));

        return Ok(credentials.Select(c => new PrismDeviceResponse
        {
            DeviceId = c.DeviceId,
            DeviceName = c.DeviceName,
            Platform = c.Platform,
            UserId = c.UserId,
            RegisteredAt = c.RegisteredAt,
            LastUsedAt = c.LastUsedAt,
            ExpiresAt = c.ExpiresAt,
            RevokedAt = c.RevokedAt,
        }));
    }

    /// <summary>
    /// Revokes one device credential of the tenant. A device that belongs to another tenant, or
    /// does not exist, is a 404. Revoking an already revoked credential succeeds and changes nothing.
    /// </summary>
    [HttpDelete("tenants/{id:int}/devices/{deviceId}")]
    public IActionResult RevokeDevice(int id, string deviceId)
    {
        using var db = databaseFactory.CreateDatabase();

        var credential = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE DeviceId = @0 AND TenantId = @1", deviceId, id.ToString(CultureInfo.InvariantCulture));
        if (credential is null) return NotFound();

        if (credential.RevokedAt is null)
        {
            credential.RevokedAt = DateTime.UtcNow;
            db.Update(credential);
            var scrubbedDevice = LogScrub.Line(deviceId);
            var administrator = LogScrub.Line(User.Identity?.Name);
            LogRevoked(scrubbedDevice, id, administrator);
        }

        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Device revoked by an administrator: device {DeviceId}, tenant {TenantId}, by {Administrator}")]
    private partial void LogRevoked(string? deviceId, int tenantId, string? administrator);
}
