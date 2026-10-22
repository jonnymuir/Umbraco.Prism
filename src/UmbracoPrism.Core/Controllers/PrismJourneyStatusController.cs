using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Controllers.Models;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.JourneyStatus;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Where an app built with journey statuses registers the device, and the Live Activities it
/// runs (Design/mobile-journey-status.md). The app calls these natively, with the member's own
/// session cookie, since iOS can start a Live Activity while the app has no page open. They
/// identify the member as push notifications do.
///
/// [IgnoreAntiforgeryToken] for the same reason as <see cref="PrismNotificationController"/>: a
/// native app API with no form to carry a token. JSON bodies only, which a page on another site
/// cannot send without a CORS preflight this site never allows.
/// </summary>
[Route("umbraco/prism/journey-status")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie", Policy = "PrismStrictIsolation")]
[IgnoreAntiforgeryToken]
[Consumes("application/json")]
public sealed class PrismJourneyStatusController(PrismJourneyStatusDelivery delivery, IPrismUserContext userContext) : Controller
{
    /// <summary>
    /// Registers this device's app as one that shows statuses. 409 until the app has registered for
    /// push notifications, which statuses travel through; the app tries again when next opened.
    /// </summary>
    [HttpPost("device")]
    public IActionResult RegisterDevice([FromBody] PrismJourneyStatusDeviceRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (Member() is not { } member) return Unauthorized();

        var startToken = request.Platform == PrismJourneyStatusPlan.Ios ? request.StartToken : null;
        return delivery.RegisterDevice(member, request.Platform, startToken)
            ? NoContent()
            : Conflict(new { error = "Register for push notifications first." });
    }

    /// <summary>A Live Activity has started on this iPhone, and this is its own token.</summary>
    [HttpPost("activity")]
    public async Task<IActionResult> ActivityStarted([FromBody] PrismJourneyStatusActivityRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (Member() is not { } member) return Unauthorized();

        await delivery.ActivityStartedAsync(member, request.Key, request.Token, cancellationToken);
        return NoContent();
    }

    private PrismJourneyStatusMember? Member() =>
        userContext.IsAuthenticated && userContext.Email is { } email && userContext.CurrentTenant?.Hostname is { } host
            ? new PrismJourneyStatusMember(email, host)
            : null;
}
