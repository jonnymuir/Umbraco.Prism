# Umbraco-Specific Notifications Integration & Demo Content Design

**Author:** Brewster (Umbraco Platform Specialist)  
**Date:** 2026-04-03  
**Updated:** 2026-04-03 (Vinyl Vault Demo Redesign)  
**Status:** Design Document — No Implementation

---

## Overview

This document provides a comprehensive design for integrating push notifications into Umbraco.Prism with two key focuses:

1. **Platform Integration:** How Umbraco content notifications, member groups, and backoffice extensions interact with the push notification system
2. **Demo Site — "Vinyl Vault":** A vinyl record shop theme that demonstrates content subscription notifications, API-triggered notifications, and scheduled notification scenarios in a fun, relatable context

---

## Part 1: Umbraco Platform Integration Design

### 1.1 Content Notification Hooks

#### Recommendation: Opt-In Hook Pattern

**Primary Hook:** `ContentPublishedNotification`  
**Secondary Hook (Optional):** `ContentSavedNotification` for draft preview notifications

**Architecture:**

```csharp
// Core library provides the hook interface
public interface IPrismContentNotificationHandler
{
    Task OnContentPublishedAsync(
        IContent content, 
        CancellationToken cancellationToken);
        
    Task OnContentSavedAsync(
        IContent content, 
        CancellationToken cancellationToken);
}

// Consumer implements in their own Composer
public class NotificationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IPrismContentNotificationHandler, MyContentNotifier>();
        
        // Prism registers its own notification handler that delegates to consumers
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, 
            PrismContentPublishedHandler>();
    }
}
```

**Opt-In Attribute Pattern:**

Use Document Type composition or property-level attribute to mark content types for notification eligibility:

```csharp
// Option A: Document Type Composition
// Create a "Notifiable Content" composition in backoffice with properties:
// - notifyOnPublish (boolean) — default false
// - notificationTitle (text) — override content name
// - notificationBody (textarea) — custom message template
// - notificationGroups (Member Group Picker) — target groups

// Option B: Property-level attribute (code-first approach)
public class EventPageContentType
{
    [NotifyOnPublish(
        MessageTemplate = "New event: {Name}",
        TargetGroups = new[] { "Event Subscribers" })]
    public string Title { get; set; }
}
```

**Recommended Approach:** Use Document Type composition for maximum flexibility and backoffice-editor control. This allows editors to:
- Toggle notifications on/off per content item
- Override notification text without code deployment
- Target specific member groups dynamically

**Implementation Pattern:**

```csharp
public class PrismContentPublishedHandler 
    : INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IPrismContentNotificationHandler? _customHandler;
    private readonly IPrismNotificationService _notificationService;
    
    public async Task HandleAsync(
        ContentPublishedNotification notification, 
        CancellationToken cancellationToken)
    {
        foreach (var entity in notification.PublishedEntities)
        {
            // Check if this content has "Notifiable Content" composition
            var publishedContent = _umbracoContextAccessor
                .GetRequiredUmbracoContext()
                .Content?.GetById(entity.Id);
                
            if (publishedContent?.HasProperty("notifyOnPublish") == true 
                && publishedContent.Value<bool>("notifyOnPublish"))
            {
                // Allow consumer to customize notification payload
                await _customHandler?.OnContentPublishedAsync(entity, cancellationToken);
                
                // Or use built-in notification templating
                var title = publishedContent.Value<string>("notificationTitle") 
                    ?? publishedContent.Name;
                var body = publishedContent.Value<string>("notificationBody") 
                    ?? $"New content published: {publishedContent.Name}";
                    
                var groups = publishedContent.Value<IEnumerable<string>>("notificationGroups");
                
                await _notificationService.SendToMemberGroupsAsync(
                    title, body, groups, cancellationToken);
            }
        }
    }
}
```

**Decision Rationale:**
- **Published-only by default:** Most notifications should go to end users only when content is "live"
- **Draft notifications optional:** Some scenarios (e.g., "Your requested document is under review") might want saved-but-unpublished triggers — make this opt-in via a separate `notifyOnSave` checkbox
- **Composition over inheritance:** Allows adding notification capability to ANY document type without schema rebuild
- **Consumer hook for advanced scenarios:** Enterprise users may want custom notification logic (e.g., "only notify if price increased by >10%") — the `IPrismContentNotificationHandler` escape hatch enables this

---

### 1.2 Member Group Integration

#### Member Groups as Notification Audiences

Umbraco's built-in Member Group system is a **perfect fit** for notification targeting. Here's why:

**Strengths:**
- Already familiar to Umbraco editors
- Built-in backoffice UI for group management
- Can be assigned via code or manually
- Supports hierarchical logic (member can be in multiple groups)

**Subscription State Mapping:**

Two architectural options:

#### **Option A: Member Group = Topic (Simpler)**

```
Member Groups:
- "Event Subscribers" → subscribes to Event content type notifications
- "News Subscribers" → subscribes to News content type notifications
- "VIP Members" → receives all notifications + special offers

Subscription Management:
- Member joins/leaves groups via custom controller or backoffice
- No additional database tables needed
- Leverages Umbraco's existing `IMemberGroupService`
```

**Pros:**
- Zero schema changes
- Backoffice-editable
- Works immediately with existing member infrastructure

**Cons:**
- Groups are all-or-nothing (can't subscribe to "Sports events" only, must subscribe to all events)
- Mixing notification groups with functional groups (e.g., "Premium Members") can cause confusion

---

#### **Option B: Custom Subscription State (More Flexible)**

```
Database Table: PrismMemberSubscriptions
- MemberId (FK to umbracoMember)
- TopicKey (GUID) → maps to content type, tag, category, etc.
- DeviceToken (optional) → device-specific subscriptions
- CreatedAt, UpdatedAt

Notification Resolution:
1. Check if member has active subscription to content.ContentType.Key
2. Or check if member is in group referenced by content.notificationGroups
3. Send to union of both sets (deduplicated)
```

**Pros:**
- Fine-grained topic control (subscribe to specific categories, tags, or content nodes)
- Device-specific subscriptions (e.g., "notify me on mobile but not web")
- Audit trail of subscription changes

**Cons:**
- Additional database table + migration
- Custom backoffice UI needed (Member Group picker won't suffice)

---

#### **Recommendation:** Start with **Option A** for v1

**Rationale:**
- Umbraco developers expect Member Groups to work this way
- Enables immediate adoption without schema migration
- Can add Option B later as "advanced subscription settings" if needed

**Implementation Pattern:**

```csharp
public interface IPrismNotificationService
{
    // Send to all members in specified groups
    Task SendToMemberGroupsAsync(
        string title, 
        string body, 
        IEnumerable<string> groupNames,
        CancellationToken cancellationToken);
        
    // Send to individual member (for direct notifications)
    Task SendToMemberAsync(
        string title, 
        string body, 
        int memberId,
        CancellationToken cancellationToken);
        
    // Send to all members in tenant (broadcast)
    Task SendToAllMembersAsync(
        string title, 
        string body,
        CancellationToken cancellationToken);
}

public class PrismNotificationService : IPrismNotificationService
{
    private readonly IMemberService _memberService;
    private readonly IPushNotificationProvider _pushProvider; // APNs/FCM adapter
    private readonly IPrismDeviceCredentialRepository _deviceRepo;
    
    public async Task SendToMemberGroupsAsync(
        string title, string body, IEnumerable<string> groupNames, 
        CancellationToken ct)
    {
        var memberIds = new HashSet<int>();
        
        foreach (var groupName in groupNames)
        {
            var group = _memberService.GetByName(groupName);
            if (group == null) continue;
            
            var membersInGroup = _memberService.GetAllMembersOfGroup(group.Id);
            foreach (var member in membersInGroup)
            {
                memberIds.Add(member.Id);
            }
        }
        
        // Get device tokens for these members
        var devices = await _deviceRepo.GetByMemberIdsAsync(memberIds, ct);
        
        // Send via push notification provider
        await _pushProvider.SendBatchAsync(title, body, devices, ct);
    }
}
```

---

### 1.3 Backoffice Integration

#### Should Editors Send Notifications from the Backoffice?

**Short Answer:** Yes, but **v2, not v1**.

**Rationale:**

**Benefits:**
- Empowers content editors to send ad-hoc announcements ("System maintenance in 30 minutes")
- Natural workflow: "I just published this event → notify subscribers immediately"
- Reduces dependency on developer intervention for one-off campaigns

**Complexity:**
- Requires Umbraco v14+ backoffice extension (Lit Web Components, not AngularJS)
- Custom property editor UI or dashboard section
- Needs permission checks (not all editors should spam members)
- Requires robust rate limiting to prevent accidental mass notifications

**Recommended v1 Scope:** No backoffice UI — notifications triggered automatically via content publish hooks or API endpoints

**Recommended v2 Design:**

```typescript
// umbraco-package.json manifest entry
{
  "type": "dashboard",
  "alias": "Prism.NotificationCenter",
  "name": "Notification Center",
  "element": "/App_Plugins/UmbracoPrism/backoffice/notification-center.js",
  "meta": {
    "label": "Notifications",
    "pathname": "prism-notifications"
  },
  "conditions": [
    {
      "alias": "Umb.Condition.SectionAlias",
      "match": "Umb.Section.Members"
    }
  ]
}
```

```typescript
// notification-center.ts (Lit Web Component)
import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js';

@customElement('prism-notification-center')
export class PrismNotificationCenter extends LitElement {
  @state()
  private title = '';
  
  @state()
  private body = '';
  
  @state()
  private selectedGroups: string[] = [];
  
  async sendNotification() {
    await fetch('/umbraco/backoffice/prism/notification/send', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: this.title,
        body: this.body,
        memberGroups: this.selectedGroups
      })
    });
  }
  
  render() {
    return html`
      <uui-form>
        <uui-input label="Title" .value=${this.title}></uui-input>
        <uui-textarea label="Message" .value=${this.body}></uui-textarea>
        <prism-member-group-picker 
          @change=${this.onGroupsChanged}>
        </prism-member-group-picker>
        <uui-button @click=${this.sendNotification}>
          Send Notification
        </uui-button>
      </uui-form>
    `;
  }
}
```

**Permission Model:**
- Require user to be in `PrismConfiguration.AdminGroups` (reuse existing admin check pattern)
- Or create new `PrismConfiguration.NotificationSenderGroups` for granular control

**Decision:** Defer to v2. Keep v1 focused on developer-triggered notifications via content hooks and API endpoints.

---

### 1.4 Scheduled Task Pattern in Umbraco

#### Cleanest Pattern: `IHostedService` with Umbraco-aware scheduling

**Background:**

Umbraco v13+ removed `IRecurringBackgroundTask` (pre-v13 pattern). Current options:

1. **`IHostedService`** — ASP.NET Core standard pattern (runs in background)
2. **Umbraco Runtime Levels** — Ensure task only runs when Umbraco is ready (not during install/upgrade)
3. **Hangfire/Quartz** — Third-party schedulers (overkill for simple tasks)

**Recommended Pattern:**

```csharp
// Consumer code: Create a hosted service that respects Umbraco runtime state
public class MembershipExpiryNotificationTask : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<MembershipExpiryNotificationTask> _logger;
    private Timer? _timer;
    
    public MembershipExpiryNotificationTask(
        IServiceProvider serviceProvider,
        IRuntimeState runtimeState,
        ILogger<MembershipExpiryNotificationTask> logger)
    {
        _serviceProvider = serviceProvider;
        _runtimeState = runtimeState;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogInformation("Skipping notification task — Umbraco not running");
            return Task.CompletedTask;
        }
        
        // Run daily at 9 AM
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(9);
        if (nextRun < now) nextRun = nextRun.AddDays(1);
        
        var delay = nextRun - now;
        
        _timer = new Timer(
            DoWork, 
            null, 
            delay, 
            TimeSpan.FromDays(1));
            
        return Task.CompletedTask;
    }
    
    private void DoWork(object? state)
    {
        // Use scoped services (e.g., IMemberService, IPrismNotificationService)
        using var scope = _serviceProvider.CreateScope();
        
        var memberService = scope.ServiceProvider.GetRequiredService<IMemberService>();
        var notificationService = scope.ServiceProvider
            .GetRequiredService<IPrismNotificationService>();
        
        // Find members expiring in 7 days
        var expiringMembers = memberService.GetAll()
            .Where(m => m.GetValue<DateTime?>("membershipExpiry") is DateTime expiry
                && expiry.Date == DateTime.UtcNow.Date.AddDays(7))
            .ToList();
        
        foreach (var member in expiringMembers)
        {
            notificationService.SendToMemberAsync(
                "Membership Expiring Soon",
                "Your membership expires in 7 days. Renew now to keep access.",
                member.Id,
                CancellationToken.None);
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }
    
    public void Dispose() => _timer?.Dispose();
}

// Registration in Composer
public class NotificationTaskComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHostedService<MembershipExpiryNotificationTask>();
    }
}
```

**Consumer Code Sketch:**

```csharp
// Step 1: Add member property for expiry tracking
public class MemberSchemaSetup : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, 
        CancellationToken cancellationToken)
    {
        var memberType = _memberTypeService.Get("Member");
        
        if (!memberType.PropertyTypes.Any(p => p.Alias == "membershipExpiry"))
        {
            memberType.AddPropertyType(new PropertyType(
                _dataTypeService.GetDataType("Umbraco.DateTime"),
                "membershipExpiry")
            {
                Name = "Membership Expiry"
            });
            
            _memberTypeService.Save(memberType);
        }
    }
}

// Step 2: Register hosted service
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHostedService<MembershipExpiryNotificationTask>();
    }
}
```

**Why this pattern?**
- **Umbraco-aware:** Checks `RuntimeLevel` to avoid running during install/upgrade
- **ASP.NET Core standard:** No custom Umbraco abstractions needed
- **Scoped service access:** Correctly uses `IServiceProvider.CreateScope()` for scoped dependencies like `IMemberService`
- **Testable:** Can inject mock `IRuntimeState` and `IServiceProvider` for unit tests

**Alternative:** Hangfire for more complex scheduling (cron expressions, retries, backoff) — but adds dependency weight. Recommend `IHostedService` for simple daily/hourly tasks.

---

## Part 2: Demo Site Design — "Vinyl Vault"

### 2.1 Demo Concept

**Name:** Vinyl Vault  
**Tagline:** "Your notification-powered vinyl record shop"

**Pitch:**

Vinyl Vault is a vintage record shop built into the UmbracoPrism.TestSite. It demonstrates push notifications in a fun, relatable context that immediately makes sense to developers evaluating the package.

**Why vinyl records?**
- **Instantly relatable:** Everyone understands the concept of "new stock arriving" or "limited edition drops"
- **Content-driven:** Each vinyl is a content node with rich metadata (artist, genre, cover art, release year)
- **Natural subscription model:** Genre-based subscriptions mirror real-world music preferences
- **Visual appeal:** Album cover art makes notifications more engaging than plain text
- **Multiple notification triggers:** New arrivals (content publish), back-in-stock alerts (API trigger), limited edition drops (scheduled task)

---

### 2.2 Use Case 1: Content Subscription Notifications

#### Scenario

**Member journey:**
1. Member browses the Vinyl Vault catalog by genre (Jazz, Rock, Electronic, Hip-Hop, Classical, Techno, Nose Flute Jazz, etc.)
2. Member clicks "Subscribe to Jazz" (or "Subscribe to All New Stock")
3. When an editor publishes a new vinyl record in the Jazz genre, subscribed members receive a push notification:
   - **Title:** "🎵 New arrival in Jazz"
   - **Body:** "Miles Davis 'Kind of Blue' just landed at Vinyl Vault!"
   - **Image:** Album cover art URL
   - **Action:** Tapping opens the vinyl's content page in the member portal

**Editor workflow:**
1. Editor creates a new `VinylRecord` content node under `/vinyl-vault/jazz/`
2. Fills in: Title, Artist, Cover Art (media picker), Release Year, Description
3. Ensures `notifyOnPublish` toggle is **ON** (inherited from `notifiableContent` composition)
4. Clicks **Publish**
5. `ContentPublishedNotification` fires → handler finds members subscribed to "Jazz Subscribers" group → FCM push sent

---

### 2.3 Use Case 2: Backend-Triggered Notifications

#### **Scenario 2A: "Back in Stock" Alert (API-Triggered)**

**Setup:**
- A vinyl record has an `inStock` boolean property (default: true)
- When stock runs out, editor toggles `inStock` to `false`
- Members can join a "waitlist" for out-of-stock items (button on vinyl detail page)
- When editor toggles `inStock` back to `true`, an **API endpoint** is called automatically (or manually via backoffice button)
- The API endpoint sends a push notification to waitlisted members

**Technical flow:**
1. Editor marks `VinylRecord` node as `inStock: true` (or clicks "Notify Waitlist" button in backoffice)
2. Backoffice dashboard extension (or property editor event) calls:  
   `POST /umbraco/api/vinylvault/notify-back-in-stock/{contentId}`
3. Controller:
   - Fetches vinyl record content
   - Queries waitlist (stored in `PrismMemberSubscriptions` table or custom `VinylWaitlist` table)
   - Sends push notification:
     - **Title:** "🎉 Back in Stock!"
     - **Body:** "Daft Punk 'Random Access Memories' is available again at Vinyl Vault!"
     - **Image:** Album cover art
4. Waitlist members receive notification immediately

**Demo value:** Shows how **API-triggered notifications** work when business logic (not just content publish) drives the notification.

---

#### **Scenario 2B: "Limited Edition Drop" Alert (Scheduled Task)**

**Setup:**
- A vinyl record has two additional properties:
  - `limitedEdition` (boolean) — marks this as a limited drop
  - `limitedDropTime` (DateTime) — when the limited edition becomes available
- A **recurring background task** (Umbraco `IRecurringBackgroundTask` or `IHostedService`) runs every 5 minutes
- It checks for limited edition drops happening in the next 30 minutes
- If a drop is upcoming, it sends a notification to all "VIP Members" or "New Stock Subscribers"

**Technical flow:**
1. Editor creates a new vinyl record node with:
   - `limitedEdition: true`
   - `limitedDropTime: 2026-04-03T18:00:00Z` (6pm today)
2. Background task runs at 5:30pm, detects drop at 6:00pm (30 minutes away)
3. Task sends push notification:
   - **Title:** "⏰ Limited Edition Drop in 30 minutes!"
   - **Body:** "Daft Punk 'Random Access Memories' drops at 6:00 PM at Vinyl Vault — don't miss out!"
   - **Image:** Album cover art
4. Members receive advance warning
5. At 6:00pm, the vinyl's `inStock` property is auto-toggled to `true` (or editor manually publishes it)

**Demo value:** Shows how **scheduled background tasks** can drive notifications based on time-based business rules.

---

### 2.4 Document Types Design

#### **1. `vinylRecord` (Document Type)**

**Purpose:** Individual vinyl record content node  
**Icon:** `icon-vinyl`  
**Template:** `VinylRecord.cshtml`  
**Allowed at root:** No  
**Allowed child node types:** None (leaf page)

**Properties:**

| Property Alias      | Label                  | Data Type                  | Tab        | Description                                      |
|---------------------|------------------------|----------------------------|------------|--------------------------------------------------|
| `artist`            | Artist                 | Textstring                 | Content    | Artist or band name                              |
| `albumTitle`        | Album Title            | Textstring                 | Content    | Full album title (distinct from node name)       |
| `genre`             | Genre                  | Multi-Node Tree Picker     | Content    | Link to Genre node(s) — XPath: `$site//genre`    |
| `coverArt`          | Cover Art              | Media Picker (Single)      | Content    | Album cover image                                |
| `releaseYear`       | Release Year           | Numeric (year)             | Content    | Original release year                            |
| `description`       | Description            | Rich Text Editor           | Content    | Album description, track listing, history        |
| `inStock`           | In Stock               | Toggle (Boolean)           | Inventory  | Is this vinyl currently available?               |
| `limitedEdition`    | Limited Edition        | Toggle (Boolean)           | Inventory  | Is this a limited edition drop?                  |
| `limitedDropTime`   | Limited Drop Time      | Date Picker with Time      | Inventory  | When does this limited edition become available? |
| `price`             | Price                  | Decimal                    | Inventory  | Price in USD (e.g., 24.99)                       |
| `catalogNumber`     | Catalog Number         | Textstring                 | Metadata   | Vinyl catalog/SKU reference                      |

**Compositions:**
- `notifiableContent` — Adds `notifyOnPublish`, `notificationTitle`, `notificationBody`, `notificationGroups`
- `seoBase` (if exists) — SEO fields

**Route:**
- `/vinyl-vault/{genre}/{vinyl-name}`
- Example: `/vinyl-vault/jazz/miles-davis-kind-of-blue`

---

#### **2. `genre` (Document Type)**

**Purpose:** Genre category node (Jazz, Rock, Electronic, etc.)  
**Icon:** `icon-folder-music`  
**Template:** `Genre.cshtml`  
**Allowed at root:** No  
**Allowed child node types:** `vinylRecord`

**Properties:**

| Property Alias  | Label           | Data Type                | Tab     | Description                          |
|-----------------|-----------------|--------------------------|---------|--------------------------------------|
| `genreName`     | Genre Name      | Textstring               | Content | Display name (e.g., "Jazz")          |
| `description`   | Description     | Textarea                 | Content | Genre description for landing page   |
| `genreIcon`     | Genre Icon      | Media Picker (Single)    | Content | Optional icon/image for genre        |

**Route:**
- `/vinyl-vault/{genre}`
- Example: `/vinyl-vault/jazz`

**Template:**
- Lists all child `vinylRecord` nodes
- Shows "Subscribe to Jazz" button if member is authenticated

---

#### **3. `vinylVaultHub` (Document Type)**

**Purpose:** Vinyl Vault shop landing page  
**Icon:** `icon-store`  
**Template:** `VinylVaultHub.cshtml`  
**Allowed at root:** No  
**Allowed child node types:** `genre`, `notificationSubscriptions`

**Properties:**

| Property Alias      | Label                  | Data Type                | Tab     | Description                              |
|---------------------|------------------------|--------------------------|---------|------------------------------------------|
| `heroTitle`         | Hero Title             | Textstring               | Content | "Welcome to Vinyl Vault"                 |
| `heroSubtitle`      | Hero Subtitle          | Textarea                 | Content | Tagline or intro text                    |
| `featuredVinyls`    | Featured Vinyl Records | Multi-Node Tree Picker   | Content | Manually curated featured records        |

**Route:**
- `/vinyl-vault`

**Template:**
- Shows genre tiles (Jazz, Rock, Electronic, etc.)
- Shows featured vinyl records carousel
- "Manage Notifications" link to `/vinyl-vault/notifications`

---

#### **4. `notificationSubscriptions` (Document Type)**

**Purpose:** Member's subscription management page  
**Icon:** `icon-bell`  
**Template:** `NotificationSubscriptions.cshtml`  
**Allowed at root:** No  
**Allowed child node types:** None

**Properties:**

| Property Alias      | Label                  | Data Type   | Tab     | Description                          |
|---------------------|------------------------|-------------|---------|--------------------------------------|
| `pageTitle`         | Page Title             | Textstring  | Content | "Manage Your Vinyl Vault Alerts"     |
| `instructions`      | Instructions           | Textarea    | Content | How to subscribe/unsubscribe         |

**Route:**
- `/vinyl-vault/notifications`

**Template:**
- Protected route (requires `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`)
- Lists all available genres with subscribe/unsubscribe toggles
- "Subscribe to All New Stock" master toggle
- Saves preferences by adding/removing member from genre-specific member groups

---

#### **5. `notifiableContent` (Composition)**

**Purpose:** Add notification capability to any content type (same as Part 1 design)  
**Icon:** `icon-paper-plane`

**Properties:**

| Property Alias          | Label                     | Data Type                | Description                                  |
|-------------------------|---------------------------|--------------------------|----------------------------------------------|
| `notifyOnPublish`       | Notify on Publish         | Toggle (Boolean)         | Send notification when this content is published |
| `notificationTitle`     | Notification Title        | Textstring               | Override notification title (default: node name) |
| `notificationBody`      | Notification Body         | Textarea (max 200 chars) | Custom notification message                  |
| `notificationGroups`    | Target Member Groups      | Member Group Picker      | Which member groups should receive notification |
| `notificationImageUrl`  | Notification Image URL    | Textstring               | Override image URL (default: uses coverArt)  |

**Applied to:**
- `vinylRecord`

---

### 2.5 Content Tree Structure

```
Home
└── Vinyl Vault [vinylVaultHub]
    ├── Notifications [notificationSubscriptions]
    ├── Jazz [genre]
    │   ├── Miles Davis - Kind of Blue [vinylRecord]
    │   ├── John Coltrane - A Love Supreme [vinylRecord]
    │   └── Bill Evans - Portrait in Jazz [vinylRecord]
    ├── Rock [genre]
    │   ├── Pink Floyd - The Dark Side of the Moon [vinylRecord]
    │   ├── Led Zeppelin - IV [vinylRecord]
    │   └── The Beatles - Abbey Road [vinylRecord]
    ├── Electronic [genre]
    │   ├── Daft Punk - Random Access Memories [vinylRecord]
    │   ├── Boards of Canada - Music Has the Right to Children [vinylRecord]
    │   └── Aphex Twin - Selected Ambient Works 85-92 [vinylRecord]
    ├── Hip-Hop [genre]
    │   ├── Kendrick Lamar - To Pimp a Butterfly [vinylRecord]
    │   └── A Tribe Called Quest - The Low End Theory [vinylRecord]
    ├── Classical [genre]
    │   └── Beethoven - Symphony No. 9 (Glenn Gould) [vinylRecord]
    ├── Techno [genre]
    │   ├── Kraftwerk - The Man-Machine [vinylRecord]
    │   └── Jeff Mills - Exhibitionist [vinylRecord]
    └── Nose Flute Jazz [genre]
        └── Various Artists - Nasal Passages: A Nose Flute Jazz Collection [vinylRecord]
```

**Recommendation:** This structure avoids an extra `/catalog/` URL segment and keeps genre landing pages at `/vinyl-vault/jazz` instead of `/vinyl-vault/catalog/jazz`.

---

### 2.6 Demo Script

**Goal:** Provide a step-by-step walkthrough for developers evaluating the package.

#### Prerequisites

1. Umbraco.Prism installed on local dev environment
2. Test site running with MockBackOffice authentication
3. Mobile app or web app with FCM notification permissions enabled
4. Demo member logged in: `demo@vinylvault.local` / `Demo123!`

---

#### **Demo Part 1: Content Subscription Notifications**

**Duration:** 5 minutes

1. **Open member portal in mobile app/browser:**
   - Navigate to `/vinyl-vault`
   - See Vinyl Vault landing page with genre tiles

2. **Subscribe to Jazz notifications:**
   - Click "Jazz" genre tile
   - Click "🔔 Subscribe to Jazz Alerts" button
   - Confirm: Button changes to "🔕 Unsubscribe"

3. **Open Umbraco backoffice (in separate browser/tab):**
   - Navigate to Vinyl Vault → Jazz in content tree
   - Create new vinyl record:
     - Name: "Herbie Hancock - Head Hunters"
     - Artist: "Herbie Hancock"
     - Album Title: "Head Hunters"
     - Release Year: 1973
     - Price: 26.99
     - In Stock: true
     - Cover Art: (upload album cover image)
     - **Ensure `notifyOnPublish` is toggled ON**
   - Click **Save and Publish**

4. **Check mobile device:**
   - **Expected:** Push notification appears within 2-3 seconds:
     - Title: "🎵 New arrival in Jazz"
     - Body: "Herbie Hancock 'Head Hunters' just landed at Vinyl Vault!"
     - Image: Album cover art
   - Tap notification → opens vinyl detail page in member portal

5. **Verify non-subscribers don't receive notification:**
   - Log in as `rock@vinylvault.local` (subscribed to Rock only)
   - Publish a new Rock vinyl
   - **Expected:** `demo@vinylvault.local` does NOT receive notification (not subscribed to Rock)
   - **Expected:** `rock@vinylvault.local` DOES receive notification

---

#### **Demo Part 2: Back-in-Stock API Notification**

**Duration:** 3 minutes

1. **Navigate to out-of-stock vinyl:**
   - In member portal, go to `/vinyl-vault/jazz/john-coltrane-a-love-supreme`
   - **Expected:** "Out of Stock" badge displayed
   - Click "Join Waitlist" button
   - **Expected:** Button changes to "On Waitlist"

2. **Mark vinyl as back in stock (backoffice):**
   - Open John Coltrane vinyl in backoffice
   - Toggle `inStock` to **true**
   - Click **Save and Publish**
   - (If using manual trigger) Click "Notify Waitlist" button in dashboard

3. **Check mobile device:**
   - **Expected:** Push notification appears:
     - Title: "🎉 Back in Stock!"
     - Body: "John Coltrane 'A Love Supreme' is available again at Vinyl Vault!"
     - Image: Album cover art
   - Tap notification → opens vinyl detail page (now showing "In Stock")

---

#### **Demo Part 3: Limited Edition Drop Scheduled Notification**

**Duration:** 5 minutes (requires waiting for scheduled time)

**Note:** For demo purposes, set `limitedDropTime` to 5 minutes in the future so you don't have to wait long.

1. **Verify limited edition vinyl exists:**
   - Navigate to `/vinyl-vault/electronic/daft-punk-random-access-memories`
   - **Expected:** "Limited Edition Drop" badge displayed
   - **Expected:** Countdown timer showing time until drop

2. **Wait for background task to run:**
   - Background task runs every 5 minutes
   - When drop time is within 30 minutes, notification is sent

3. **Check mobile device (as VIP or All New Stock subscriber):**
   - **Expected:** Push notification appears 30 minutes before drop:
     - Title: "⏰ Limited Edition Drop in 30 minutes!"
     - Body: "Daft Punk 'Random Access Memories' drops soon at Vinyl Vault — don't miss out!"
     - Image: Album cover art
   - Tap notification → opens vinyl detail page with countdown timer

4. **At drop time:**
   - (Manual step for demo) Editor toggles `inStock` to true
   - Vinyl becomes available for purchase

---

#### **Quick Demo (2-Minute Version)**

If evaluator has limited time:

1. **Subscribe to genre** (20 seconds)
2. **Publish new vinyl in backoffice** (60 seconds)
3. **Show push notification on device** (10 seconds)
4. **Tap notification → navigate to content** (10 seconds)
5. **Explain back-in-stock and limited drop scenarios verbally** (20 seconds)

---

### 2.7 Seeder Design

**Goal:** Pre-populate test site with realistic demo content so developers can immediately see the notification system in action.

#### Seeded Member Groups

```
- "Jazz Subscribers"
- "Rock Subscribers"
- "Electronic Subscribers"
- "Hip-Hop Subscribers"
- "Classical Subscribers"
- "All New Stock Subscribers"
- "VIP Members"
```

#### Seeded Members

```
1. demo@vinylvault.local
   Password: Demo123!
   Groups: Jazz Subscribers, All New Stock Subscribers
   
2. vip@vinylvault.local
   Password: Demo123!
   Groups: VIP Members, Electronic Subscribers, Hip-Hop Subscribers
   
3. rock@vinylvault.local
   Password: Demo123!
   Groups: Rock Subscribers
```

#### Seeded Content

**Genres:**

1. **Jazz**
   - Description: "From bebop to fusion, explore the finest jazz vinyl at Vinyl Vault."
   
2. **Rock**
   - Description: "Classic rock albums that defined generations."
   
3. **Electronic**
   - Description: "Cutting-edge electronic music on vinyl."
   
4. **Hip-Hop**
   - Description: "The golden age of hip-hop, pressed on wax."
   
5. **Classical**
   - Description: "Timeless classical masterpieces."

**Vinyl Records (12 total):**

**Jazz:**
1. Miles Davis - Kind of Blue (1959, $24.99, In Stock)
2. John Coltrane - A Love Supreme (1965, $29.99, **Out of Stock**)
3. Bill Evans - Portrait in Jazz (1960, $22.99, In Stock)

**Rock:**
4. Pink Floyd - The Dark Side of the Moon (1973, $34.99, In Stock)
5. Led Zeppelin - IV (1971, $27.99, In Stock)
6. The Beatles - Abbey Road (1969, $32.99, **Out of Stock**)

**Electronic:**
7. Daft Punk - Random Access Memories (2013, $39.99, **Limited Edition**, Drop Time: +2 hours from seed time)
8. Boards of Canada - Music Has the Right to Children (1998, $29.99, In Stock)
9. Aphex Twin - Selected Ambient Works 85-92 (1992, $26.99, In Stock)

**Hip-Hop:**
10. Kendrick Lamar - To Pimp a Butterfly (2015, $31.99, In Stock)
11. A Tribe Called Quest - The Low End Theory (1991, $28.99, In Stock)

**Classical:**
12. Beethoven - Symphony No. 9 (Glenn Gould) (1824, $19.99, In Stock)

---

### 2.8 Coordination Notes for Blathers

**Backend Requirements:**

1. **Core Notification Service (`IPrismNotificationService`):**
   - `SendToMemberAsync(int memberId, PushNotification notification, CancellationToken ct)`
   - `SendToMemberGroupsAsync(IEnumerable<string> groupNames, PushNotification notification, CancellationToken ct)`
   - `SendToMemberGroupAsync(string groupName, PushNotification notification, CancellationToken ct)`
   - Handles FCM device token resolution from `PrismDeviceTokens` table
   - Handles FCM API calls with retry logic

2. **Content Notification Handler:**
   - `PrismContentPublishedHandler` : `INotificationAsyncHandler<ContentPublishedNotification>`
   - Checks for `notifiableContent` composition
   - Resolves target member groups from `notificationGroups` property
   - Builds notification payload from content properties
   - Delegates sending to `IPrismNotificationService`

3. **API Controllers:**
   - `VinylVaultApiController` with endpoints:
     - `POST /umbraco/api/vinylvault/toggle-subscription`
     - `POST /umbraco/api/vinylvault/join-waitlist`
     - `POST /umbraco/api/vinylvault/notify-back-in-stock/{contentId}`
   - All protected with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`

4. **Background Task:**
   - `LimitedEditionDropNotifier` : `IRecurringBackgroundTask`
   - Runs every 5 minutes
   - Queries for limited edition vinyl with drop time within 30-minute window
   - Sends notifications to configured member groups
   - Marks vinyl with `_limitedDropNotificationSent` flag to avoid duplicates

5. **Database Extensions:**
   - **Option 1 (simpler):** Use Umbraco member properties for waitlist storage
     - Add `vinylWaitlist` property to Member type (comma-separated vinyl IDs)
   - **Option 2 (cleaner):** Custom table `VinylWaitlist`:
     ```sql
     CREATE TABLE VinylWaitlist (
         Id INT PRIMARY KEY IDENTITY,
         MemberId INT NOT NULL,
         VinylContentId INT NOT NULL,
         CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
         UNIQUE (MemberId, VinylContentId)
     )
     ```

6. **PushNotification Model:**
   ```csharp
   public class PushNotification
   {
       public string Title { get; set; }
       public string Body { get; set; }
       public string? ImageUrl { get; set; }
       public Dictionary<string, string>? Data { get; set; }
   }
   ```

**Brewster Dependencies on Blathers:**

- **Must have before starting:** 
  - `IPrismNotificationService` interface + implementation
  - `PushNotification` model
  - FCM device token registration (already implemented?)
  
- **Can work in parallel:**
  - Brewster builds document types, templates, and route-hijacking controllers
  - Blathers builds notification service and API endpoints
  
- **Integration point:**
  - Brewster calls `IPrismNotificationService.SendToMemberGroupsAsync()` from content published handler
  - Brewster's API controllers call `IPrismNotificationService` for waitlist/limited drop scenarios

**Recommended Handoff:**

1. Blathers provides `IPrismNotificationService` interface definition (can be empty stub implementation initially)
2. Brewster builds demo content types and templates against the interface
3. Blathers implements FCM sending logic
4. Both test integration together

---

## Part 3: Implementation Guidance

### Phase 1: Core Notification Service (Blathers)

**Tasks:**
1. Define `IPrismNotificationService` interface
2. Implement FCM sending logic
3. Create `PushNotification` model
4. Add device token storage/retrieval
5. Add member group resolution logic

**Estimated Effort:** 2-3 days

---

### Phase 2: Content Notification Hooks (Brewster)

**Tasks:**
1. Create `PrismContentPublishedHandler` 
2. Implement notification composition checking
3. Add notification payload building logic
4. Register handler in Composer

**Estimated Effort:** 1 day

---

### Phase 3: Demo Site Updates (Brewster)

**Tasks:**
1. Create Vinyl Vault document types in backoffice or code-first
2. Build Razor templates:
   - `VinylVaultHub.cshtml`
   - `Genre.cshtml`
   - `VinylRecord.cshtml`
   - `NotificationSubscriptions.cshtml`
3. Create route-hijacking controllers:
   - `VinylVaultHubController`
   - `GenreController`
   - `VinylRecordController`
   - `NotificationSubscriptionsController`
4. Create `VinylVaultApiController` with endpoints
5. Implement `LimitedEditionDropNotifier` background task
6. Build seeder for demo content
7. Add JavaScript for subscribe/waitlist toggles

**Estimated Effort:** 3-4 days

---

### Phase 4: Testing & Documentation

**Tasks:**
1. Test content subscription flow end-to-end
2. Test back-in-stock notification flow
3. Test limited edition drop notification flow
4. Verify non-subscribers don't receive notifications
5. Update README with demo walkthrough
6. Record demo video/screenshots

**Estimated Effort:** 1-2 days

---

**Total Estimated Effort:** 7-10 days (Brewster: 5-7 days, Blathers: 2-3 days)

---

## Summary

**Vinyl Vault** replaces the previous demo design with a fun, relatable vinyl record shop theme that showcases all three notification use cases:

1. **Content subscription notifications** — Genre-based subscriptions + publish trigger
2. **API-triggered notifications** — Back-in-stock waitlist alerts
3. **Scheduled notifications** — Limited edition drop advance warnings

The demo provides:
- Realistic, content-driven scenarios
- Visual appeal (album cover art in notifications)
- Clear member subscription UX
- Both automatic (content publish) and manual (API endpoint) notification triggers
- Background task integration for time-based notifications

**Developer Experience:**
- Pre-seeded demo content (12 vinyl records, 5 genres, 3 members)
- Simple 2-minute walkthrough script
- All three use cases demonstrated in under 15 minutes
- Authentic feel with real artist/album names

**Coordination with Blathers:**
- Clear interface contracts (`IPrismNotificationService`)
- Parallel development possible (Brewster: content, Blathers: services)
- Well-defined integration points
