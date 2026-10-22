# Journey status

A generated mobile app can show where one of a member's journeys has got to on the phone's lock
screen while they wait on it: a Live Activity on iPhone (on the lock screen and in the Dynamic
Island) and a Live Update on Android 16 and later, which is an ongoing notification on earlier
versions. It updates in place with the app closed, and tapping it opens the journey's page in the
app. This page describes what the package does; to switch it on in an app, see
[Building a Mobile App, Part 7](walkthroughs/building-a-mobile-app.md#part-7-journey-status).

## When to use it

A status is for a wait measured in minutes or hours: a check that runs now, a same-day decision.
iOS ends a Live Activity within 12 hours whatever the app does, and Android's Live Updates are
meant for the same kind of short, ongoing activity. A change that comes days later is a push
notification instead, sent through [`IPrismNotificationService`](notifications.md), which opens
the journey's page when tapped.

## Publishing a status

Prism knows nothing about journeys, so the host publishes a status whenever one of its journeys
changes, through `IPrismJourneyStatusService`:

```csharp
journeyStatus.Publish(memberEmail, tenantHostname, new PrismJourneyStatus(
    Key: request.InstanceId,                // one per journey instance
    ServiceName: "Apply for a juggling licence",
    Status: "Deciding your application",
    Step: 2, Steps: 3,                      // optional, shown as "Step 2 of 3"
    Path: "/apply-for-a-juggling-licence",  // the page a tap opens
    Kind: PrismJourneyStatusKind.Waiting));
```

- **`Waiting`** starts the status on the member's phone, or updates it if one is showing for the
  same `Key`.
- **`Done`** ends it. Its last status stays on screen for 15 minutes, then goes.
- The member is identified as push notifications identify them, by email address and the tenant's
  host name, and only their own registered device receives the status.

`Publish` checks the status and throws `ArgumentException` if it is not valid: `Key` must be 1 to
255 characters, `ServiceName` and `Status` 1 to 120, `Step` between 0 and `Steps`, and `Path` a
path on the tenant's own site, such as `/orders`. Otherwise it queues the status and returns at
once. A background sender delivers it through Firebase, so a slow or failed delivery never holds
up whatever caused the change; a failure is logged, and the next status for the same journey
supersedes it. The queue is held in memory, so a status queued as the site stops is lost. That
costs the member one lock screen update, never the journey's own state.

The **Update journey status** action in
[`UmbracoPrism.Automate`](../src/UmbracoPrism.Automate/README.md) publishes a status from an
Umbraco Automate automation, without code.

## What to put on a lock screen

Anyone holding the phone can read a lock screen. Say what kind of thing is happening, never the
outcome or anything personal: "A decision has been made", not "Your licence was refused". The
detail belongs on the page behind sign-in, which the status opens, and in a push notification that
opens that page. Write each status line in the site's own words, and never copy into it anything
the member entered.

## How a device receives statuses

Apps built with the Journey Status bundle option register with Prism natively, using the
member's session cookie, when the member signs in, when the app opens and when it comes back to
the front. An iPhone also reports each Live Activity's own update token once iOS starts one, which
can happen with the app in the background and no page open.

| Endpoint | Body | Purpose |
|---|---|---|
| `POST /umbraco/prism/journey-status/device` | `{ "platform": "ios" \| "android", "startToken": "..." }` | Register this device for statuses. `startToken` is the iPhone's Live Activity push-to-start token. |
| `POST /umbraco/prism/journey-status/activity` | `{ "key": "...", "token": "..." }` | A Live Activity iOS started for journey `key`, and its update token. |

Both require a signed-in member (the `PrismMemberCookie` scheme) and the `PrismStrictIsolation`
policy, and accept JSON only. Registering answers `409` until the device has registered for push
notifications, since statuses go to the device's push token: Journey Status needs the push
notifications bundle option and a configured Firebase project
([Push notification setup](PUSH_SETUP.md)).

Delivery differs by platform:

- **iPhone:** Prism sends Live Activity pushes through Firebase. The first `Waiting` status starts
  the activity with the push-to-start token; later ones update it through its own token, and
  `Done` ends it. No separate APNs key is needed beyond the one Firebase already holds.
- **Android:** each status is a high-priority data message, which the app turns into its ongoing
  notification, tagged by the journey's key so an update replaces it.

A member has one registered device per tenant, the one they signed in on most recently, as for
push notifications. Signing out ends every status showing on the device, and Prism forgets the
device's registration and stored statuses along with its push token.

## Worked example

The reference site publishes from Wayfinder.Umbraco's `ServiceRequestTransitionedNotification`,
which fires each time a service request moves on. `JourneyStatuses` in `UmbracoPrism.TestSite`
maps the juggling licence journey's stages to statuses: "Deciding your application" (`Waiting`)
while a competitive or professional licence waits for approval in the backoffice, and "A decision
has been made" (`Done`) when it settles, alongside the decision automation's own push
notification.

The design is in `Design/mobile-journey-status.md`.
