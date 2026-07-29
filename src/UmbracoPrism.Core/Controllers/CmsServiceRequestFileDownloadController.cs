using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Services.ServiceDesign;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Serves a file previously uploaded against a CMS Workflow instance's <c>file-upload</c>
/// field — the "view this document" link on a check-answers/summary-list row or the field's
/// own "already uploaded" state.
/// </summary>
/// <remarks>
/// Deliberately unauthenticated at the framework level, exactly like
/// <see cref="CmsServiceRequestPageController"/> itself (CMS Workflow journeys are anonymous-first) —
/// security comes from resolving the requester's identity the same way
/// (<see cref="CmsServiceRequestVisitorIdentityResolver"/>) and requiring it to own the instance
/// (<see cref="UmbracoProcessManagerEngine.TryGetOwnedFileReference"/>), not from a login challenge.
/// </remarks>
[ApiController]
[Route("prism/cms-workflow/files")]
public class CmsServiceRequestFileDownloadController(
    UmbracoProcessManagerEngine engine,
    CmsServiceRequestVisitorIdentityResolver identityResolver,
    IServiceRequestFileStorage fileStorage) : ControllerBase
{
    [HttpGet("{instanceId}/{fieldKey}")]
    public async Task<IActionResult> Download(string instanceId, string fieldKey, CancellationToken cancellationToken)
    {
        var (tenantId, userId, _) = identityResolver.Resolve();
        var reference = engine.TryGetOwnedFileReference(
            instanceId, tenantId, userId, CmsQueue.AccessProfile, fieldKey);

        if (reference is null)
        {
            return NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(reference, cancellationToken);
        return File(stream, string.IsNullOrEmpty(reference.ContentType) ? "application/octet-stream" : reference.ContentType, reference.OriginalFileName);
    }
}
