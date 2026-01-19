using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Persistence;
using Umbraco.Cms.Core.Cache;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing tenants in the Prism package.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class TenantManagementController(IUmbracoDatabaseFactory databaseFactory, AppCaches appCaches) : ManagementApiControllerBase
{
    /// <summary>
    /// Gets all tenants.
    /// </summary>
    [HttpGet("tenants")]
    public ActionResult<IEnumerable<PrismTenantSchema>> GetTenants()
    {
        using var db = databaseFactory.CreateDatabase();
        var tenants = db.Fetch<PrismTenantSchema>();
        return Ok(tenants);
    }

    /// <summary>
    /// Saves or updates a tenant.
    /// </summary>
    [HttpPost("tenants")]
    public IActionResult SaveTenant([FromBody] PrismTenantSchema tenant)
    {
        if (tenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        
        // NPoco's Save method automatically performs an Insert if Id is 0, 
        // or an Update if the Id already exists.
        db.Save(tenant);

        appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{tenant.Hostname}");
        
        return Ok();
    }

    /// <summary>
    /// Deletes a tenant by its integer ID.
    /// </summary>
    /// <param name="id">The database ID of the tenant.</param>
    [HttpDelete("tenants/{id:int}")]
    public IActionResult DeleteTenant(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        
        // Check if it exists first to provide a better API response
        var tenant = db.SingleOrDefaultById<PrismTenantSchema>(id);
        if (tenant == null) return NotFound();

        db.Delete<PrismTenantSchema>(id);

        appCaches.RuntimeCache.ClearByKey($"Prism_Tenant_{tenant.Hostname}");
        
        return Ok();
    }
}