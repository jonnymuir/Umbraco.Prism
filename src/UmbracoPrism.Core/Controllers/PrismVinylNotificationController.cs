using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// API endpoints for triggering vinyl-related notifications (back-in-stock alerts).
/// All routes require authenticated PrismMemberCookie session.
///
/// SEC-PT2-009 ANTIFORGERY POLICY: This controller is a Capacitor mobile app API.
/// [IgnoreAntiforgeryToken] is deliberate — see BiometricController for rationale.
/// </summary>
[Route("umbraco/prism/vinyl")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[IgnoreAntiforgeryToken]
public class PrismVinylNotificationController(
    IPrismNotificationService notificationService,
    IPrismContext prismContext,
    ILogger<PrismVinylNotificationController> logger) : Controller
{
    /// <summary>
    /// Sends a back-in-stock notification for a vinyl record.
    /// If genre is provided, sends to genre subscribers; otherwise sends to all members.
    /// The tenant is determined from the authenticated user's session, not from request data.
    /// </summary>
    [HttpPost("back-in-stock")]
    public async Task<IActionResult> BackInStock([FromBody] PrismVinylBackInStockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.VinylTitle))
            return BadRequest(new { error = "vinylTitle is required." });

        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
        {
            logger.LogWarning("BackInStock called without valid tenant context.");
            return BadRequest(new { error = "Tenant context not available." });
        }

        var tenantId = tenant.Id.ToString();
        var title = $"🎵 Back in Stock: {request.VinylTitle}";
        var body = $"{request.VinylTitle} is back in stock at the Vinyl Vault!";

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Genre))
            {
                await notificationService.SendNotificationToGenreSubscribersAsync(
                    tenantId,
                    request.Genre,
                    title,
                    body);

                logger.LogInformation(
                    "Back-in-stock notification sent to genre '{Genre}' subscribers in tenant {TenantId} for vinyl '{VinylTitle}'.",
                    request.Genre, tenantId, request.VinylTitle);
            }
            else
            {
                await notificationService.SendNotificationToAllMembersAsync(
                    tenantId,
                    title,
                    body);

                logger.LogInformation(
                    "Back-in-stock notification sent to all members in tenant {TenantId} for vinyl '{VinylTitle}'.",
                    tenantId, request.VinylTitle);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send back-in-stock notification for vinyl '{VinylTitle}' in tenant {TenantId}.",
                request.VinylTitle, tenantId);
            return StatusCode(500, new { error = "Failed to send notification." });
        }
    }
}
