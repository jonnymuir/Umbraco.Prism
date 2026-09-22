using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for managing Prism page-access policies via the Umbraco Management API. Same
/// shape as <see cref="TenantManagementController"/> — raw NPoco against the schema, no
/// repository layer, with the resolver's cache invalidated after every write.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
[Authorize(Policy = "PrismAdmins")]
[VersionedApiBackOfficeRoute("prism")]
[ApiExplorerSettings(GroupName = "Prism")]
[MapToApi("Prism")]
public class PageAccessManagementController(
    IUmbracoDatabaseFactory databaseFactory,
    IPrismPageAccessResolver pageAccessResolver,
    IContentService contentService,
    IDocumentUrlService documentUrlService) : ManagementApiControllerBase
{
    [HttpGet("page-access")]
    public ActionResult<IEnumerable<PrismPageAccessPolicyResponse>> GetPolicies()
    {
        using var db = databaseFactory.CreateDatabase();
        var policies = db.Fetch<PrismPageAccessPolicySchema>();
        return Ok(policies.Select(ToResponse));
    }

    [HttpPost("page-access")]
    public IActionResult CreatePolicy([FromBody] PrismPageAccessPolicyRequest policy)
    {
        if (policy == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();

        if (HasExistingPolicyForContent(db, policy.ContentKey, excludeId: null))
        {
            return BadRequest(new { error = "This page already has an access policy." });
        }

        var schema = new PrismPageAccessPolicySchema
        {
            Id = 0,
            ContentKey = policy.ContentKey,
            ContentRoute = ResolveRoute(policy.ContentKey),
            RequiresSignIn = policy.RequiresSignIn,
            TenantAllowListJson = SerializeAllowList(policy.AllowedTenantNames)
        };

        db.Insert(schema);
        pageAccessResolver.Invalidate("page-access-policy-create");

        return Ok(ToResponse(schema));
    }

    [HttpPut("page-access/{id:int}")]
    public IActionResult UpdatePolicy(int id, [FromBody] PrismPageAccessPolicyRequest policy)
    {
        if (policy == null) return BadRequest();

        using var db = databaseFactory.CreateDatabase();
        var existing = db.SingleOrDefaultById<PrismPageAccessPolicySchema>(id);
        if (existing == null) return NotFound();

        if (HasExistingPolicyForContent(db, policy.ContentKey, excludeId: id))
        {
            return BadRequest(new { error = "This page already has an access policy." });
        }

        existing.ContentKey = policy.ContentKey;
        // Re-resolved unconditionally, not only when ContentKey changes — cheap, and keeps this
        // the single place ContentRoute can ever go stale (alongside the publish-time refresh
        // handler) rather than two places that could disagree.
        existing.ContentRoute = ResolveRoute(policy.ContentKey);
        existing.RequiresSignIn = policy.RequiresSignIn;
        existing.TenantAllowListJson = SerializeAllowList(policy.AllowedTenantNames);

        db.Update(existing);
        pageAccessResolver.Invalidate("page-access-policy-update");

        return Ok(ToResponse(existing));
    }

    [HttpDelete("page-access/{id:int}")]
    public IActionResult DeletePolicy(int id)
    {
        using var db = databaseFactory.CreateDatabase();
        var existing = db.SingleOrDefaultById<PrismPageAccessPolicySchema>(id);
        if (existing == null) return NotFound();

        db.Delete<PrismPageAccessPolicySchema>(id);
        pageAccessResolver.Invalidate("page-access-policy-delete");

        return Ok();
    }

    private string? ResolveRoute(Guid contentKey) =>
        documentUrlService.GetLegacyRouteFormat(contentKey, culture: null, isDraft: false);

    private static bool HasExistingPolicyForContent(IUmbracoDatabase db, Guid contentKey, int? excludeId)
    {
        return (db.Fetch<PrismPageAccessPolicySchema>() ?? [])
            .Any(other => other.ContentKey == contentKey && (excludeId == null || other.Id != excludeId.Value));
    }

    private static string? SerializeAllowList(string[]? allowedTenantNames)
    {
        if (allowedTenantNames == null || allowedTenantNames.Length == 0) return null;
        return JsonSerializer.Serialize(allowedTenantNames);
    }

    private PrismPageAccessPolicyResponse ToResponse(PrismPageAccessPolicySchema schema)
    {
        return new PrismPageAccessPolicyResponse
        {
            Id = schema.Id,
            ContentKey = schema.ContentKey,
            ContentRoute = schema.ContentRoute,
            RequiresSignIn = schema.RequiresSignIn,
            AllowedTenantNames = string.IsNullOrWhiteSpace(schema.TenantAllowListJson)
                ? []
                : JsonSerializer.Deserialize<string[]>(schema.TenantAllowListJson) ?? [],
            ContentName = contentService.GetById(schema.ContentKey)?.Name
        };
    }
}
