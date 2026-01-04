using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing tenants in the Prism package.
/// </summary>
[VersionedApiBackOfficeRoute("prism/v1")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class TenantManagementController(IUmbracoDatabaseFactory databaseFactory) : ManagementApiControllerBase
{
    /// <summary>
    /// Gets all tenants.
    /// </summary>
    /// <returns></returns>
    [HttpGet("tenants")]
    public ActionResult<IEnumerable<PrismTenantSchema>> GetTenants()
    {
        using var db = databaseFactory.CreateDatabase();
        var tenants = db.Fetch<PrismTenantSchema>();
        return Ok(tenants);
    }

    /// <summary>
    /// Saves a tenant.
    /// </summary>
    /// <param name="tenant"></param>
    /// <returns></returns>
    [HttpPost("tenant")]
    public IActionResult SaveTenant([FromBody] PrismTenantSchema tenant)
    {
        using var db = databaseFactory.CreateDatabase();
        db.Save(tenant);
        return Ok();
    }
}