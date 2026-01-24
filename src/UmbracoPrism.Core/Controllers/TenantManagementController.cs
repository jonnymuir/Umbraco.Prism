using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Persistence;
using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing tenants in the Prism package via the Umbraco Management API.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class TenantManagementController(IUmbracoDatabaseFactory databaseFactory, AppCaches appCaches) : ManagementApiControllerBase
{
    /// <summary>
    /// Gets all registered tenants.
    /// </summary>
    [HttpGet("tenants")]
    public ActionResult<IEnumerable<PrismTenantSchema>> GetTenants()
    {
        using var db = databaseFactory.CreateDatabase();
        var tenants = db.Fetch<PrismTenantSchema>();
        return Ok(tenants);
    }

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    [HttpPost("tenants")]
    public IActionResult CreateTenant([FromBody] PrismTenantSchema tenant)
    {
        if (tenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        
        // Ensure ID is 0 for a new record
        tenant.Id = 0; 
        db.Insert(tenant);

        // Clear cache for the new hostname
        appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{tenant.Hostname}");
        
        return Ok(tenant);
    }

    /// <summary>
    /// Updates an existing tenant.
    /// </summary>
    [HttpPut("tenants/{id:int}")]
    public IActionResult UpdateTenant(int id, [FromBody] PrismTenantSchema updatedTenant)
    {
        if (updatedTenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        
        // 1. Fetch the existing record to find the old hostname (for cache clearing)
        var existing = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (existing == null) return NotFound();

        string oldHostname = existing.Hostname;

        // 2. Map updated values
        existing.Name = updatedTenant.Name;
        existing.Hostname = updatedTenant.Hostname;
        existing.ThemeColor = updatedTenant.ThemeColor;
        existing.EntraTenantId = updatedTenant.EntraTenantId;
        existing.EntraClientId = updatedTenant.EntraClientId;
        existing.SecretKeyName = updatedTenant.SecretKeyName;

        // 3. Persist
        db.Update(existing);

        // 4. Clear cache for BOTH old and new hostnames to prevent routing issues
        appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{oldHostname}");
        if (oldHostname != updatedTenant.Hostname)
        {
            appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{updatedTenant.Hostname}");
        }
        
        return Ok(existing);
    }

    /// <summary>
    /// Deletes a tenant by its database ID.
    /// </summary>
    [HttpDelete("tenants/{id:int}")]
    public IActionResult DeleteTenant(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        
        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant == null) return NotFound();

        db.Delete<PrismTenantSchema>(id);

        // Clear cache so the site stops recognizing this hostname immediately
        appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{tenant.Hostname}");
        
        return Ok();
    }
}