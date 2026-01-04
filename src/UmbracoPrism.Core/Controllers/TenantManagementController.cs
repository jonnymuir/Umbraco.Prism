using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing tenants in the Prism package.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[VersionedApiBackOfficeRoute("prism")]
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
    [HttpPost("tenants")]
    public IActionResult SaveTenant([FromBody] PrismTenantSchema tenant)
    {
        if (tenant == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        db.Save(tenant);
        return Ok();
    }
}