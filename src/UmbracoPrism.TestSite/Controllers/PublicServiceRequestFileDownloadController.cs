using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayfinder.Umbraco.Configuration;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Serves a file previously uploaded against a service request instance's <c>file-upload</c>
/// field — the "view this document" link on a check-answers/summary-list row, or the field's own
/// "already uploaded" state. Mounted at Wayfinder.Umbraco's own
/// <see cref="WayfinderServiceDesignOptions.FileEndpointBasePath"/> default (<c>/service-request</c>)
/// — the package renders download/upload links against that base but owns no controller of its
/// own at it, since a host needs to enforce ownership its own way (see that option's remarks).
/// </summary>
/// <remarks>
/// Deliberately unauthenticated at the framework level — this demo has both an anonymous-first
/// persona (public-visitor) and an authenticated one (NJF Contributions Team); security comes
/// from resolving the requester's identity/access profile the same way
/// <see cref="WayfinderServiceDesignOptions.ResolveTenantId"/>/<c>ResolveUserId</c>/
/// <c>ResolveAccessProfile</c> do, and requiring it to own the instance
/// (<see cref="UmbracoProcessManagerEngine.TryGetOwnedFileReference"/>), not from a login challenge.
/// </remarks>
[ApiController]
[Route("service-request/files")]
public class PublicServiceRequestFileDownloadController(
    UmbracoProcessManagerEngine engine,
    IServiceRequestFileStorage fileStorage,
    IOptions<WayfinderServiceDesignOptions> optionsAccessor) : ControllerBase
{
    [HttpGet("{instanceId}/{fieldKey}")]
    public async Task<IActionResult> Download(string instanceId, string fieldKey, CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        var tenantId = options.ResolveTenantId!(HttpContext);
        var userId = options.ResolveUserId(HttpContext);
        var accessProfile = options.ResolveAccessProfile!(HttpContext);

        var reference = engine.TryGetOwnedFileReference(instanceId, tenantId, userId, accessProfile, fieldKey);
        if (reference is null)
        {
            return NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(reference, cancellationToken);
        return File(stream, string.IsNullOrEmpty(reference.ContentType) ? "application/octet-stream" : reference.ContentType, reference.OriginalFileName);
    }
}
