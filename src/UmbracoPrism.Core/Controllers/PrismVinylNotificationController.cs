using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// API endpoints for triggering vinyl-related notifications (back-in-stock alerts).
/// All routes require authenticated PrismMemberCookie session.
/// </summary>
[Route("umbraco/prism/vinyl")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class PrismVinylNotificationController(
    IPrismNotificationService notificationService,
    ILogger<PrismVinylNotificationController> logger) : Controller
{
    /// <summary>
    /// Sends a back-in-stock notification for a vinyl record.
    /// If genre is provided, sends to genre subscribers; otherwise sends to all members.
    /// </summary>
    [HttpPost("back-in-stock")]
    public async Task<IActionResult> BackInStock([FromBody] PrismVinylBackInStockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.TenantId))
            return BadRequest(new { error = "tenantId is required." });

        if (string.IsNullOrWhiteSpace(request.VinylTitle))
            return BadRequest(new { error = "vinylTitle is required." });

        var title = $"🎵 Back in Stock: {request.VinylTitle}";
        var body = $"{request.VinylTitle} is back in stock at the Vinyl Vault!";

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Genre))
            {
                await notificationService.SendNotificationToGenreSubscribersAsync(
                    request.TenantId,
                    request.Genre,
                    title,
                    body);

                logger.LogInformation(
                    "Back-in-stock notification sent to genre '{Genre}' subscribers in tenant {TenantId} for vinyl '{VinylTitle}'.",
                    request.Genre, request.TenantId, request.VinylTitle);
            }
            else
            {
                await notificationService.SendNotificationToAllMembersAsync(
                    request.TenantId,
                    title,
                    body);

                logger.LogInformation(
                    "Back-in-stock notification sent to all members in tenant {TenantId} for vinyl '{VinylTitle}'.",
                    request.TenantId, request.VinylTitle);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send back-in-stock notification for vinyl '{VinylTitle}' in tenant {TenantId}.",
                request.VinylTitle, request.TenantId);
            return StatusCode(500, new { error = "Failed to send notification." });
        }
    }
}
