using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Endpoints for mobile push notification token registration and genre subscriptions.
/// All routes require an authenticated PrismMemberCookie session.
///
/// SEC-PT2-009 ANTIFORGERY POLICY: This controller is a Capacitor mobile app API.
/// [IgnoreAntiforgeryToken] is deliberate — see BiometricController for rationale.
/// </summary>
[Route("umbraco/prism/push")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[IgnoreAntiforgeryToken]
public class PrismNotificationController(
    IPrismNotificationService notificationService,
    IPrismContext prismContext,
    INotificationRateLimitService rateLimitService,
    ILogger<PrismNotificationController> logger) : Controller
{
    // ── Device token registration ────────────────────────────────────────────

    /// <summary>
    /// Registers or updates the FCM push token for the authenticated user's current device.
    /// </summary>
    /// <remarks>
    /// Explicit <c>PrismStrictIsolation</c>: a JSON API registering device state under a tenant,
    /// so a mismatched-tenant principal should get a hard 403 rather than fall through. See
    /// <see cref="BiometricController.Register"/> for the same reasoning.
    /// </remarks>
    [HttpPost("register")]
    [Authorize(Policy = "PrismStrictIsolation")]
    public async Task<IActionResult> RegisterToken([FromBody] PrismPushRegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request?.PushToken))
            return BadRequest(new { error = "pushToken is required." });

        if (request.PushToken.Length > 500)
            return BadRequest(new { error = "pushToken must not exceed 500 characters." });

        var (userId, tenantId) = ResolveUserAndTenant();
        if (userId == null || tenantId == null)
            return Unauthorized(new { error = "User identity or tenant context could not be determined." });

        // Rate limiting: 10 registrations per hour per user+tenant
        var (isLimited, retryAfter) = rateLimitService.CheckTokenRegistrationLimit(userId, tenantId);
        if (isLimited)
        {
            Response.Headers.Append("Retry-After", retryAfter.ToString());
            return StatusCode(429, new { error = "rate_limited", retryAfterSeconds = retryAfter });
        }

        await notificationService.RegisterDeviceTokenAsync(userId, tenantId, request.PushToken);
        logger.LogInformation("Push token registered for user {UserId} in tenant {TenantId}.", userId, tenantId);
        return Ok();
    }

    /// <summary>
    /// Clears the FCM push token for the authenticated user (on logout or opt-out).
    /// </summary>
    [HttpDelete("register")]
    public async Task<IActionResult> UnregisterToken()
    {
        var (userId, tenantId) = ResolveUserAndTenant();
        if (userId == null || tenantId == null)
            return Unauthorized(new { error = "User identity or tenant context could not be determined." });

        await notificationService.UnregisterDeviceTokenAsync(userId, tenantId);
        logger.LogInformation("Push token unregistered for user {UserId} in tenant {TenantId}.", userId, tenantId);
        return Ok();
    }

    // ── Genre subscriptions ──────────────────────────────────────────────────

    /// <summary>
    /// Subscribes the authenticated user to a notification genre within the current tenant.
    /// </summary>
    /// <remarks>
    /// Explicit <c>PrismStrictIsolation</c>: see <see cref="RegisterToken"/>.
    /// </remarks>
    [HttpPost("subscribe")]
    [Authorize(Policy = "PrismStrictIsolation")]
    public async Task<IActionResult> Subscribe([FromBody] PrismSubscribeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request?.Genre))
            return BadRequest(new { error = "genre is required." });

        var (userId, tenantId) = ResolveUserAndTenant();
        if (userId == null || tenantId == null)
            return Unauthorized(new { error = "User identity or tenant context could not be determined." });

        // Rate limiting: 20 subscriptions per hour per user+tenant
        var (isLimited, retryAfter) = rateLimitService.CheckSubscriptionLimit(userId, tenantId);
        if (isLimited)
        {
            Response.Headers.Append("Retry-After", retryAfter.ToString());
            return StatusCode(429, new { error = "rate_limited", retryAfterSeconds = retryAfter });
        }

        await notificationService.SubscribeToGenreAsync(userId, tenantId, request.Genre);
        logger.LogInformation("User {UserId} subscribed to genre '{Genre}' in tenant {TenantId}.", userId, request.Genre, tenantId);
        return Ok();
    }

    /// <summary>
    /// Unsubscribes the authenticated user from a notification genre within the current tenant.
    /// </summary>
    [HttpDelete("subscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PrismSubscribeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request?.Genre))
            return BadRequest(new { error = "genre is required." });

        var (userId, tenantId) = ResolveUserAndTenant();
        if (userId == null || tenantId == null)
            return Unauthorized(new { error = "User identity or tenant context could not be determined." });

        await notificationService.UnsubscribeFromGenreAsync(userId, tenantId, request.Genre);
        logger.LogInformation("User {UserId} unsubscribed from genre '{Genre}' in tenant {TenantId}.", userId, request.Genre, tenantId);
        return Ok();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (string? userId, string? tenantId) ResolveUserAndTenant()
    {
        var userId = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        var tenant = prismContext.CurrentTenant;
        var tenantId = tenant?.Id.ToString();

        return (userId, tenantId);
    }
}
