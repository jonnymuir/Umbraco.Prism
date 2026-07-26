using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Accepts a single <c>file-upload</c> field's file as soon as a visitor chooses it, ahead of
/// the stage's own whole-page submission — the server half of the accessible progress-bar
/// upload experience (<c>prism-file-upload.ts</c>). Saves the file immediately via the same
/// <see cref="IServiceRequestFileStorage"/> <see cref="PrismServiceRequestPageController{TViewModel}.HandlePost"/>
/// already uses, and hands back an opaque token bound to this exact instance/field
/// (<see cref="IUploadTokenService"/>) for the client to carry in a hidden input until the real
/// submission — see that method's own remarks for how it resolves the token back.
/// </summary>
/// <remarks>
/// Same anonymous-at-the-framework-level, identity-resolved-and-ownership-checked security model
/// as <see cref="CmsServiceRequestFileDownloadController"/> — plus the same nonce-bound
/// authoritative-fields check <c>HandlePost</c> performs for a whole-page submission, so an
/// upload can't be aimed at a field that isn't actually part of the visitor's current stage.
/// </remarks>
[ApiController]
[Route("prism/cms-workflow/upload")]
public class CmsServiceRequestFileUploadController(
    CmsProcessManager engine,
    CmsServiceRequestVisitorIdentityResolver identityResolver,
    IServiceRequestFileStorage fileStorage,
    ITouchpointNonceService nonceService,
    IUploadTokenService uploadTokenService,
    IAntiforgery antiforgery) : ControllerBase
{
    // Kept in sync with PrismServiceRequestPageController.DefaultMaxFileSizeBytes by hand — a plain
    // literal here (rather than a cross-reference through that generic class) so this
    // [RequestSizeLimit] attribute argument stays a straightforward compile-time constant.
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    [HttpPost("{instanceId}/{fieldKey}")]
    [RequestSizeLimit(DefaultMaxFileSizeBytes)]
    public async Task<IActionResult> Upload(
        string instanceId,
        string fieldKey,
        [FromForm] string nonce,
        CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest("Invalid request.");
        }

        var authoritativeFields = await nonceService.ResolveAsync(nonce, cancellationToken);
        var field = authoritativeFields?.FirstOrDefault(f =>
            f.FieldKey.Equals(fieldKey, StringComparison.Ordinal)
            && f.FieldType.Equals("file-upload", StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            // Covers an expired/unknown nonce and a field that isn't actually part of the
            // current stage identically — a visitor never needs to distinguish the two.
            return BadRequest("This field is no longer part of the current step.");
        }

        var (tenantId, userId, _) = identityResolver.Resolve();
        if (!engine.IsOwnedInstance(instanceId, tenantId, userId, CmsQueue.AccessProfile))
        {
            return NotFound();
        }

        var file = Request.Form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file was received.");
        }

        var maxSizeBytes = field.MaxSizeBytes ?? DefaultMaxFileSizeBytes;
        if (file.Length > maxSizeBytes)
        {
            return BadRequest($"{field.Label} must be smaller than {maxSizeBytes / (1024 * 1024)}MB.");
        }

        if (field.AcceptedFileTypes is { Count: > 0 })
        {
            var extension = Path.GetExtension(file.FileName);
            if (!field.AcceptedFileTypes.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest($"{field.Label} must be one of: {string.Join(", ", field.AcceptedFileTypes)}.");
            }
        }

        var reference = await fileStorage.SaveAsync(instanceId, fieldKey, file, cancellationToken);
        var token = await uploadTokenService.CreateAsync(instanceId, fieldKey, reference, cancellationToken);

        return Ok(new
        {
            token,
            fileName = reference.OriginalFileName,
            sizeBytes = reference.SizeBytes,
            downloadUrl = $"/prism/cms-workflow/files/{Uri.EscapeDataString(instanceId)}/{Uri.EscapeDataString(fieldKey)}"
        });
    }
}
