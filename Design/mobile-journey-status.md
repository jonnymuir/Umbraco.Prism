# Prism Mobile: Journey Status (Phase 4)

Detailed design for phase 4 in [mobile.md](mobile.md): a member sees where a journey they started
has got to, outside the app, and it updates without them opening it.

## 1) Goals

- When something the member is waiting on changes, they see it without opening the app.
- While a short wait is running (a check, a same-day decision), it stays on the lock screen and
  updates in place.
- Nothing on the lock screen says more than the member would be happy for someone holding their
  phone to read.
- iOS and Android reach the same level together, and Prism stays free of any Wayfinder
  dependency.

## 2) The constraint that shapes it

A Live Activity is for a wait measured in hours. iOS ends one after at most 8 hours, and it stays
on the lock screen for up to 4 more. Android's Live Updates are meant for the same kind of
activity: ongoing, user-initiated and time-sensitive, like a delivery or a ride. A licence
decision that takes ten working days fits neither.

So the design separates two kinds of status:

| Kind | Example | Where the member sees it |
|---|---|---|
| **A wait now** | "Checking your details", expected within the hour | iOS Live Activity (lock screen and Dynamic Island); Android Live Update (promoted ongoing notification) |
| **A change later** | "A decision has been made", days after submitting | A push notification, as today, that opens the journey |

A journey moves between them: a wait now ends with its outcome, which can itself be a change
later. A home screen widget listing the member's open journeys is the natural next step for
multi-day journeys, and is left for a later phase (section 9).

## 3) What the member sees

A status has a service name, one short status line, an optional step ("Step 2 of 3") and the time
it changed. Tapping it opens the journey's page in the app.

- The lock screen says what kind of thing happened, never the outcome or personal detail: "A
  decision has been made", not "Your licence was refused". The detail is on the page, behind
  sign-in.
- A wait now that finishes shows its last status for a short while (iOS keeps it up to 4 hours;
  the app asks for 15 minutes), then goes.
- Members control both from the system: Live Activities can be turned off per app in iOS
  Settings, and Android's notification settings cover Live Updates.

## 4) Prism's part (no Wayfinder dependency)

Prism gains a small, journey-agnostic API, alongside `IPrismNotificationService`:

```csharp
public sealed record PrismJourneyStatus(
    string Key,            // one per journey instance, e.g. the Wayfinder instance ID
    string ServiceName,    // "Apply for a juggling licence"
    string Status,         // "Checking your details"
    int? Step, int? Steps, // optional "Step 2 of 3"
    string Path,           // the page the status opens, site-relative
    PrismJourneyStatusKind Kind); // Waiting (a wait now) or Done

public interface IPrismJourneyStatusService
{
    void Publish(string userId, string tenantId, PrismJourneyStatus status);
}
```

`Publish` queues the status and returns at once. A background sender delivers it, so a slow or
failed delivery never holds up, or breaks, whatever caused the change. A failed delivery is
logged, and the next status for the same journey supersedes it. The queue is in memory: a status
queued as the site stops is lost, which costs the member one lock screen update, never the
journey's own state.

- **Waiting** starts the journey's Live Activity or Live Update, or updates it if one is running.
- **Done** ends any running one, with its last status showing for a short while.

A change later is a push notification, sent as today through `IPrismNotificationService` or the
Automate action, so a status never sends one itself and a member is never told twice.

Prism keeps, per member device, the tokens needed to reach it (section 6). An Umbraco Automate
action, "Update journey status", sits beside the existing "Send Prism push notification", so a
support-system automation can publish a status without code.

## 5) Where statuses come from in the reference site

Wayfinder.Umbraco publishes a `ServiceRequestTransitionedNotification` each time a request moves
on and the move is saved, whatever caused it: the member, a caseworker, a gateway or a support
system's outcome. The reference site handles it. For a signed-in member's request, it looks up
the new stage's status, if the site has given it one, and publishes it to the request's owner.
The mapping from stage to status lives in the reference site, for its own journeys. The
notification needs Wayfinder.Umbraco 4.1.0 or later, so the reference site moves to the current
release (5.0.0) first.

For the juggling licence: "Deciding your application" (waiting) while the licence decision runs,
which for a competitive or professional licence waits on a person approving it in the
backoffice's Automate section; and "A decision has been made" (done) when it settles. The
decision automation already sends a push notification for each outcome, which is the change
later.

A later home for the mapping is the blueprint itself: a stage could declare its member-facing
status line and kind. That would be a Wayfinder change, and the reference site's mapping would
then read it from the blueprint instead.

## 6) Platforms

### iOS

- **A widget extension target** in the generated Xcode project, `PrismJourneyStatus`, holding the
  Live Activity's lock screen and Dynamic Island views, drawn with SwiftUI in the system's own
  style, which keeps its contrast in light and dark mode. The bootstrap adds the target the same
  way it adds the native shell.
  `NSSupportsLiveActivities` goes in Info.plist.
- **Started remotely.** iOS 17.2 and later can start a Live Activity from a push
  (push-to-start), and generated apps already require iOS 17.4. The app sends Prism its
  push-to-start token, and each running activity's update token, to
  `/umbraco/prism/journey-status`. It posts them natively with the member's session cookie, since
  iOS wakes the app briefly to report a remotely started activity's token, with no page open.
- **Delivered through Firebase.** FCM's HTTP v1 API starts, updates and ends Live Activities via
  `apns.live_activity_token`, which Prism's Firebase Admin SDK already exposes, so no separate
  APNs key is needed.
- **A second app identity.** The extension is signed as its own bundle ID, the app's own with
  `.JourneyStatus` added. The TestFlight workflow signs automatically with the App Store Connect
  API key, which registers that ID and its provisioning profile the first time it archives,
  provided the key's role (Admin or App Manager) may register identifiers. Otherwise, the ID is
  registered once by hand in the Apple Developer account. The generated README says which.

### Android

- **A Live Update:** a promoted ongoing notification using `Notification.ProgressStyle` (steps as
  progress points), on Android 16 and later with the `POST_PROMOTED_NOTIFICATIONS` permission.
  Below Android 16, the same notification is an ordinary ongoing notification.
- **Delivered as a data message,** since only the app can build an ongoing, styled notification.
  `@capacitor-firebase/messaging` owns the app's Firebase messaging service, so the shell
  subclasses it: Prism's journey status messages are handled natively, and every other message
  goes to the plugin as before.
- The notification is tagged with the journey's key, so an update replaces it. When the wait is
  over it shows the last status for 15 minutes, as iOS does.
- The app registers natively at `/umbraco/prism/journey-status`, as on iOS, so Prism sends
  statuses only to apps built to show them.

### Both

- Tapping a status opens its page in the tab whose first page it is, or else in the tab showing.
- Signing out ends every running status on the device, and Prism forgets the device's registration
  and stored statuses along with its push token.

Both need the push notifications bundle option, and Journey Status is its own bundle option
alongside it.

## 7) Security and privacy

- Statuses are published only to the member's own devices, found by the user ID the host passes,
  the same as push notifications today.
- The status line is written by the site, never taken from what the member entered, so nothing
  personal reaches the lock screen unless the site puts it there. The guidance above says not to.
- Signing out unregisters the device's tokens, as it does for push today, and ends any running
  Live Activity or Live Update.

## 8) Decisions

1. **Live Activities only for waits now.** Multi-day changes use push notifications, because iOS
   ends a Live Activity within 12 hours whatever the app does.
2. **Prism's API is journey-agnostic.** The reference site maps Wayfinder.Umbraco's transition
   notification to statuses. Prism queues what it is given and delivers it in the background, so
   no host has to.
3. **Firebase for both platforms.** One delivery path and one set of credentials, as push uses
   today.

## 9) Not in this phase

- A home screen widget listing the member's open journeys (the multi-day surface). It is the
  next phase, and reuses this phase's extension target and status API.
- Statuses for caseworkers (their queue changing), which is a different audience.

## 10) Verification

- Prism tests: publishing each kind sends the right Firebase message per platform (start, update
  and end for iOS; data message for Android), only to the member's own devices, and a failed
  delivery is logged without reaching the caller.
- The reference site: a competitive juggling licence application shows "Deciding your
  application" on the lock screen of both apps, with the app closed. When the approval is given in
  the backoffice, the Live Activity or Live Update ends and the decision's push notification
  arrives.
- On a TestFlight build and the Play internal track, since the simulator and emulator can only
  approximate remote start and promoted notifications.
- Signing out ends a running Live Activity or Live Update.
