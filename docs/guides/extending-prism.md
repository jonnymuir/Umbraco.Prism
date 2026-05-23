# Extending Prism for Your Business Domain

This guide explains how to extend Umbraco Prism Core with business-specific handlers, controllers, and domain models. The TestSite demonstrates this pattern with the vinyl record store example.

## The Prism Extension Model

Prism Core provides a **thin, reusable platform**. Your application adds the **business logic and domain-specific extensions**.

### Prism Core Provides

- Multi-tenant infrastructure (hostname resolution, branding, OIDC)
- Notification service foundation (`IPrismNotificationService`)
- Config-driven event handling (`PrismContentPublishedHandler`)
- Subscription persistence and rate limiting
- Workflow rendering and validation
- Mobile app generation and push notifications

### Your Application Adds

- **Domain models** — Data structures specific to your business
- **Notification handlers** — Controllers and endpoints triggered by events
- **Workflow endpoints** — State machines and business logic
- **Custom API routes** — Domain-specific REST/GraphQL endpoints
- **Umbraco content types** — Document types for your data

---

## Example: Building a Vinyl Record Store Extension

The TestSite includes a vinyl record store example. Here's how it extends Prism:

### 1. Define Your Domain Model

**File:** `src/UmbracoPrism.TestSite/Controllers/Models/PrismVinylBackInStockRequest.cs`

```csharp
namespace UmbracoPrism.TestSite.Controllers.Models;

public class PrismVinylBackInStockRequest
{
    public string VinylTitle { get; set; }
    public string Genre { get; set; }
}
```

This model is specific to your domain. Prism doesn't know about vinyl records—it just sends notifications.

### 2. Create a Domain-Specific Notification Controller

**File:** `src/UmbracoPrism.TestSite/Controllers/PrismVinylNotificationController.cs`

```csharp
[Route("umbraco/prism/vinyl")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[IgnoreAntiforgeryToken]
public class PrismVinylNotificationController(
    IPrismNotificationService notificationService,
    IPrismContext prismContext,
    ILogger<PrismVinylNotificationController> logger) : Controller
{
    [HttpPost("back-in-stock")]
    public async Task<IActionResult> BackInStock([FromBody] PrismVinylBackInStockRequest request)
    {
        var tenant = prismContext.CurrentTenant;
        var title = $"🎵 Back in Stock: {request.VinylTitle}";
        var body = $"{request.VinylTitle} is back in stock at the Vinyl Vault!";

        if (!string.IsNullOrWhiteSpace(request.Genre))
        {
            // Send to members subscribed to this genre
            await notificationService.SendNotificationToGenreSubscribersAsync(
                tenant.Id.ToString(),
                request.Genre,
                title,
                body);
        }
        else
        {
            // Broadcast to all members
            await notificationService.SendNotificationToAllMembersAsync(
                tenant.Id.ToString(),
                title,
                body);
        }

        return Ok();
    }
}
```

**Key design points:**
- Inject `IPrismNotificationService` from Core
- Use `IPrismContext` to get the current tenant (automatic)
- Handle your business logic (genre filtering, message formatting)
- Let Prism handle the low-level notification dispatch, rate limiting, and persistence

### 3. Wire Up Content-Published Events (Optional)

If you want notifications to trigger automatically when content is published, extend `PrismContentPublishedHandler`:

**File:** `src/UmbracoPrism.TestSite/BackgroundServices/VinylPublishNotificationHandler.cs`

```csharp
public class VinylPublishNotificationHandler : INotificationHandler<ContentPublishedNotification>
{
    private readonly IPrismNotificationService _notificationService;
    private readonly IPrismContext _prismContext;

    public VinylPublishNotificationHandler(
        IPrismNotificationService notificationService,
        IPrismContext prismContext)
    {
        _notificationService = notificationService;
        _prismContext = prismContext;
    }

    public async Task Handle(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        // Check if this is a vinyl record content type
        if (notification.PublishedEntities.Any(e => e.ContentType.Alias == "vinyl"))
        {
            var entity = notification.PublishedEntities.First(e => e.ContentType.Alias == "vinyl");
            
            var title = "🎵 New Vinyl Available";
            var body = $"{entity.Name} is now available for purchase!";

            // Send to all members in the current tenant
            await _notificationService.SendNotificationToAllMembersAsync(
                _prismContext.CurrentTenant.Id.ToString(),
                title,
                body);
        }
    }
}
```

Register in your composer:

```csharp
public class VinylComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<ContentPublishedNotification, VinylPublishNotificationHandler>();
    }
}
```

---

## Best Practices for Extensions

### 1. Separate Core from Domain-Specific Code

- **Core logic:** Use Prism services (`IPrismNotificationService`, `IPrismContext`)
- **Domain logic:** Use your models and custom handlers

This separation makes your code easy to test and your business logic portable.

### 2. Leverage Tenant Context

Always use `IPrismContext.CurrentTenant` to ensure your handler respects multi-tenancy:

```csharp
var tenantId = prismContext.CurrentTenant.Id.ToString();
await notificationService.SendNotificationToAllMembersAsync(tenantId, title, body);
```

### 3. Use Dependency Injection

Inject services rather than accessing static instances. This makes testing easier:

```csharp
public class MyHandler(
    IPrismNotificationService notificationService,
    IPrismContext prismContext)
{
    // Testable constructor injection
}
```

### 4. Log Meaningful Information

Include context in your logs so you can debug multi-tenant scenarios:

```csharp
logger.LogInformation(
    "Back-in-stock notification sent to genre '{Genre}' subscribers in tenant {TenantId}.",
    request.Genre, tenantId);
```

### 5. Handle Errors Gracefully

Notification failures shouldn't break your workflow. Catch exceptions and log them:

```csharp
try
{
    await notificationService.SendNotificationToAllMembersAsync(tenantId, title, body);
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to send notification in tenant {TenantId}.", tenantId);
    return StatusCode(500, new { error = "Failed to send notification." });
}
```

---

## Extending Workflows

Workflows are defined in your Business App (the backend API), not in Prism Core. Your workflow endpoints define:

- **Step types** (question, confirmation, waiting, status-timeline, etc.)
- **Field definitions** (what data to collect)
- **State transitions** (what happens when a user advances)
- **Validation rules** (business logic to enforce)

Prism Core handles rendering, client-side validation, and form submission. Your Business App handles all the business logic.

**Workflow endpoints your app must implement:**
- `GET /api/workflow/get-current` — Return the current step
- `POST /api/workflow/advance` — Process the action and return the next step
- `POST /api/workflow/submit` (optional) — For explicit submission without advancing

See [Setting Up a Prism Workflow](./workflow-setup.md) for complete examples.

---

## Extending Notifications

Prism provides a generic notification foundation. Your app can extend by:

### 1. Adding Subscription Filters

Create custom queries to filter members by subscription preferences:

```csharp
public async Task SendVinylNotificationAsync(string tenantId, string vinylId, string genre)
{
    // Query your custom database for members subscribed to this genre
    var subscribers = await GetSubscribersByGenreAsync(tenantId, genre);
    
    foreach (var subscriber in subscribers)
    {
        await notificationService.SendNotificationAsync(
            tenantId,
            subscriber.Id,
            "New Vinyl",
            $"Check out this {genre} vinyl!");
    }
}
```

### 2. Adding Notification Triggers

Respond to Umbraco events, scheduled tasks, or API calls:

```csharp
[HttpPost("notify-purchase-complete")]
public async Task<IActionResult> NotifyPurchaseComplete([FromBody] PurchaseNotification notification)
{
    var tenant = prismContext.CurrentTenant;
    
    await notificationService.SendNotificationAsync(
        tenant.Id.ToString(),
        notification.MemberId,
        "Purchase Complete",
        $"Your order #{notification.OrderId} has been confirmed.");
    
    return Ok();
}
```

### 3. Rate Limiting Across Multiple Tenants

Prism's rate limiting is built-in and tenant-aware. No additional configuration needed—it just works.

---

## Testing Your Extensions

### Unit Testing Your Handler

```csharp
[TestFixture]
public class VinylNotificationControllerTests
{
    private Mock<IPrismNotificationService> _notificationService;
    private Mock<IPrismContext> _prismContext;
    private PrismVinylNotificationController _controller;

    [SetUp]
    public void Setup()
    {
        _notificationService = new Mock<IPrismNotificationService>();
        _prismContext = new Mock<IPrismContext>();
        _controller = new PrismVinylNotificationController(
            _notificationService.Object,
            _prismContext.Object,
            new Logger<PrismVinylNotificationController>(...));
    }

    [Test]
    public async Task BackInStock_SendsNotificationToGenreSubscribers()
    {
        var request = new PrismVinylBackInStockRequest 
        { 
            VinylTitle = "Pink Floyd - The Wall", 
            Genre = "Rock" 
        };
        
        var result = await _controller.BackInStock(request);

        _notificationService.Verify(
            x => x.SendNotificationToGenreSubscribersAsync(
                It.IsAny<string>(),
                "Rock",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        
        Assert.That(result, Is.TypeOf<OkResult>());
    }
}
```

### Integration Testing

Use the TestSite as a reference:

1. Seed test data (members, subscriptions)
2. Call your handler endpoint
3. Verify notifications were sent via Prism's API

---

## Deployment Considerations

When deploying your extended Prism application:

1. **Database migrations** — If you add new content types or subscription models, create and run migrations
2. **Secrets** — Domain-specific API keys or credentials should go in Azure Key Vault alongside Prism secrets
3. **Health checks** — Include your custom notification endpoint in health checks
4. **Monitoring** — Log failures and track notification delivery rates per tenant

---

## Further Reading

- [Notification Architecture](../design/notifications-architecture.md) — Deep dive into Prism's notification system
- [Notification API Reference](../notifications-design.md) — Complete API documentation
- [Setting Up a Prism Workflow](./workflow-setup.md) — Workflow definition and endpoints
- [TestSite Reference](../../src/UmbracoPrism.TestSite/README.md) — Complete working example
