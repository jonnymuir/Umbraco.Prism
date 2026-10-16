# Extending Prism for your business domain

How to build business features on top of Umbraco Prism: your own models, endpoints and
notifications, running inside Prism's tenancy. The reference site's record shop (the "Vinyl
Vault") is the worked example throughout.

## What Prism gives you, and what you add

**Prism Core provides:**

- Multi-tenancy: hostname-based tenant resolution, per-tenant OIDC sign-in and branding.
- The current tenant for a request, through `IPrismContext.CurrentTenant`.
- Page access policies (**Settings → Advanced → Page access**).
- Push notifications to generated mobile apps (`IPrismNotificationService`), with device
  registration, genre subscriptions and rate limiting. See [Notifications](../notifications.md).
- Mobile app generation.

**Your application adds:** your domain models, your own endpoints and handlers, your document
types, and any service journeys. Prism has no service-design features of its own: for citizen
journeys and caseworker queues, install
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco), as the reference site does.

## Example: a "back in stock" notification

### 1. Define your domain model

[`PrismVinylBackInStockRequest.cs`](../../src/UmbracoPrism.TestSite/Controllers/Models/PrismVinylBackInStockRequest.cs)
holds what the shop knows about a record: its title and, optionally, a genre. Prism knows nothing
about records; it just sends the notification.

### 2. Add an endpoint that sends it

Abridged from
[`PrismVinylNotificationController.cs`](../../src/UmbracoPrism.TestSite/Controllers/PrismVinylNotificationController.cs):

```csharp
[Route("umbraco/prism/vinyl")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[IgnoreAntiforgeryToken] // a mobile-app API authenticated by the member cookie; see BiometricController
public class PrismVinylNotificationController(
    IPrismNotificationService notificationService,
    IPrismContext prismContext,
    ILogger<PrismVinylNotificationController> logger) : Controller
{
    [HttpPost("back-in-stock")]
    [Authorize(Policy = "RequireVinylAdmin")] // broadcasting is a staff action, not any member's
    public async Task<IActionResult> BackInStock([FromBody] PrismVinylBackInStockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.VinylTitle))
            return BadRequest(new { error = "vinylTitle is required." });

        var tenant = prismContext.CurrentTenant;
        if (tenant == null)
            return BadRequest(new { error = "Tenant context not available." });

        var tenantId = tenant.Id.ToString();
        var title = $"Back in stock: {request.VinylTitle}";
        var body = $"{request.VinylTitle} is back in stock at the Vinyl Vault.";

        if (!string.IsNullOrWhiteSpace(request.Genre))
            await notificationService.SendNotificationToGenreSubscribersAsync(tenantId, request.Genre, title, body);
        else
            await notificationService.SendNotificationToAllMembersAsync(tenantId, title, body);

        return Ok();
    }
}
```

What matters here:

- **Authorize the action, not just the controller.** The member cookie only proves someone is
  signed in. Sending to every member of a tenant is a staff action, so the action also requires a
  role (`RequireVinylAdmin`, registered in `TestSiteComposer` as `policy.RequireRole("vinyl-admin")`).
  Without it, any member could broadcast to everyone.
- **Take the tenant from `IPrismContext`, never from the request body.** The tenant comes from the
  signed-in session, so a caller can't send into another tenant.
- **Handle a missing tenant.** `CurrentTenant` is null outside a tenant-resolved request.

### 3. Notify when content is published

For the common case you don't need code. List the document types that should notify under
`Prism:Notifications:NotifiableContentTypes`, and give each one a `prismTenantId` property (and,
optionally, `notificationGenre`). Prism's own handler does the rest; see
[Notifications](../notifications.md#sending-notifications).

For custom logic, write your own handler. A publish happens in the backoffice, so there is no
tenant-resolved request: read the tenant from the content, not from `IPrismContext`.

```csharp
public class NewReleaseNotificationHandler(
    IPrismNotificationService notifications,
    ILogger<NewReleaseNotificationHandler> logger)
    : INotificationAsyncHandler<ContentPublishedNotification>
{
    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var record in notification.PublishedEntities.Where(e => e.ContentType.Alias == "vinyl"))
        {
            var tenantId = record.GetValue<string>("prismTenantId");
            if (string.IsNullOrWhiteSpace(tenantId))
                continue;

            try
            {
                await notifications.SendNotificationToAllMembersAsync(
                    tenantId, "New release", $"{record.Name} is now available.", cancellationToken);
            }
            catch (Exception ex)
            {
                // A notification failure must never fail the editor's publish.
                logger.LogError(ex, "New release notification failed in tenant {TenantId}.", tenantId);
            }
        }
    }
}

// In your composer:
builder.AddNotificationAsyncHandler<ContentPublishedNotification, NewReleaseNotificationHandler>();
```

### 4. Notify on a schedule

[`LimitedEditionDropNotifier`](../../src/UmbracoPrism.TestSite/BackgroundServices/LimitedEditionDropNotifier.cs)
is a `BackgroundService` that sends a notification on an interval
(`Prism:Notifications:LimitedEditionDropIntervalMinutes`; `0` turns it off) to the tenant named in
`Prism:Notifications:LimitedEditionTenantId`. A background service has no request, so it names its
tenant in configuration and resolves its services from a fresh DI scope for each run.

## Notify one person

To notify a single member, for example when their order changes, call
`SendNotificationToUserAsync`. It returns `false` when nothing was sent (no registered device, or
Firebase not configured), so check it before telling anyone the notification went out:

```csharp
var sent = await notifications.SendNotificationToUserAsync(
    userId, tenantId, "Order confirmed", $"Order #{orderId} is confirmed.", deepLinkPath: "/orders");
if (!sent)
    logger.LogInformation("No device to notify for user {UserId} in tenant {TenantId}.", userId, tenantId);
```

From an Umbraco Automate automation, use the **Send Prism push notification** action instead
(see [Notifications](../notifications.md#sending-notifications) for where it lives).

## Testing your extensions

This repo tests behaviour through public seams with xUnit, Moq and AwesomeAssertions (see
`CLAUDE.md`). For the controller above, set up a tenant and assert on what was handed to the
notification service:

```csharp
[Fact]
public async Task ABackInStockAlertWithAGenre_GoesOnlyToThatGenresSubscribers()
{
    var notifications = new Mock<IPrismNotificationService>();
    var context = new Mock<IPrismContext>();
    context.Setup(c => c.CurrentTenant).Returns(new PrismTenant { Id = 7 });
    var controller = new PrismVinylNotificationController(
        notifications.Object, context.Object, NullLogger<PrismVinylNotificationController>.Instance);

    var result = await controller.BackInStock(new PrismVinylBackInStockRequest { VinylTitle = "The Wall", Genre = "rock" });

    result.Should().BeOfType<OkResult>();
    notifications.Verify(n => n.SendNotificationToGenreSubscribersAsync(
        "7", "rock", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

Also add a test that goes red if the authorization regresses. `Phase1SecurityRegressionTests`
does this for the example: it checks that `BackInStock` keeps its `RequireVinylAdmin` policy, and
that the policy refuses a member without the `vinyl-admin` role.

## Deployment

- Put domain secrets (API keys, credentials) in your secret store alongside Prism's; see
  [Secret management](../secret-management.md).
- Add your own tables with Umbraco package migrations.
- Log with the tenant id in context, so a problem can be traced to one tenant.

## Further reading

- [Notifications](../notifications.md): what Prism's notification service does.
- [Push notification setup](../PUSH_SETUP.md): Firebase and the generated app.
- [Calling a business app as the signed-in member](../walkthroughs/authenticated-business-app-call.md):
  calling your own API with the member's token.
- [Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco#readme): service journeys
  and caseworker queues.
