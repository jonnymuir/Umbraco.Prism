using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayfinder.Umbraco.Configuration;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Accepts a single <c>file-upload</c> field's file as soon as a visitor chooses it, ahead of
/// the stage's own whole-page submission — the server half of the accessible progress-bar
/// upload experience. Saves the file immediately via the same <see cref="IServiceRequestFileStorage"/>
/// the stage's whole-page submission already uses, and hands back an opaque token bound to this
/// exact instance/field (<see cref="IUploadTokenService"/>) for the client to carry in a hidden
/// input until the real submission.
/// </summary>
/// <remarks>
/// Same identity-resolved-and-ownership-checked security model as
/// <see cref="PublicServiceRequestFileDownloadController"/> — plus the same nonce-bound
/// authoritative-fields check the stage's own whole-page submission performs, so an upload can't
/// be aimed at a field that isn't actually part of the visitor's current stage.
/// </remarks>
[ApiController]
[Route("service-request/upload")]
public class PublicServiceRequestFileUploadController(
    UmbracoProcessManagerEngine engine,
    IServiceRequestFileStorage fileStorage,
    IStageNonceService nonceService,
    IUploadTokenService uploadTokenService,
    IOptions<WayfinderServiceDesignOptions> optionsAccessor,
    IAntiforgery antiforgery) : ControllerBase
{
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

        var options = optionsAccessor.Value;
        var tenantId = options.ResolveTenantId!(HttpContext);
        var userId = options.ResolveUserId(HttpContext);
        var accessProfile = options.ResolveAccessProfile!(HttpContext);

        var authoritativeFields = await nonceService.ResolveAsync(nonce, instanceId, userId, cancellationToken);
        var field = authoritativeFields?.FirstOrDefault(f =>
            f.FieldKey.Equals(fieldKey, StringComparison.Ordinal)
            && f.FieldType.Equals("file-upload", StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            // Covers an expired/unknown nonce and a field that isn't actually part of the
            // current stage identically — a visitor never needs to distinguish the two.
            return BadRequest("This field is no longer part of the current step.");
        }

        if (!engine.IsOwnedInstance(instanceId, tenantId, userId, accessProfile))
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
            downloadUrl = $"/service-request/files/{Uri.EscapeDataString(instanceId)}/{Uri.EscapeDataString(fieldKey)}"
        });
    }
}
