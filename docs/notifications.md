# Notifications

Prism sends push notifications to the native mobile apps it generates, through Firebase Cloud
Messaging (FCM). There is no browser web push. This page describes what the package does; to
set up Firebase and the generated app, follow [Push notification setup](PUSH_SETUP.md).

## How a device gets notifications

1. A member signs in to a generated mobile app. The app asks for notification permission, gets
   an FCM token, and registers it with `POST /umbraco/prism/push/register`.
2. Prism stores the token against the member and tenant, replacing any earlier token for that
   pair.
3. The member can subscribe to named **genres** (topics your site defines, such as
   `jazz` or `planning-updates`) with `POST /umbraco/prism/push/subscribe`.
4. On sign-out the app calls `DELETE /umbraco/prism/push/register`, so that device stops
   receiving notifications.

| Endpoint | Body | Purpose |
|---|---|---|
| `POST /umbraco/prism/push/register` | `{ "pushToken": "..." }` | Register or replace this member's device token. |
| `DELETE /umbraco/prism/push/register` | | Stop notifications to this member's device. |
| `POST /umbraco/prism/push/subscribe` | `{ "genre": "..." }` | Subscribe to a genre. Repeating it is safe. |
| `DELETE /umbraco/prism/push/subscribe` | `{ "genre": "..." }` | Unsubscribe from a genre. |

Every endpoint requires a signed-in member (the `PrismMemberCookie` scheme). Registering and
subscribing also require the `PrismStrictIsolation` policy, so a member can only act within their
own tenant. Each member is limited to 10 token registrations and 20 subscription changes per
tenant per hour; past that the endpoint returns `429` with a `Retry-After` header.

## Sending notifications

There are three ways to send, all through `IPrismNotificationService`:

- **When content is published.** List the document type aliases that should notify under
  `Prism:Notifications:NotifiableContentTypes` (comma-separated). A published item of one of
  those types needs a `prismTenantId` property naming its tenant. If it also has a
  `notificationGenre` value, the genre's subscribers are notified; otherwise every member of the
  tenant with a registered device is. The notification's title is the content's name. A failure
  is logged and never blocks the publish.
- **From an Umbraco Automate automation.** The **Send Prism push notification** action notifies
  one member, optionally with a deep link that opens a page in the app when the notification is
  tapped. It lives in this repo's `UmbracoPrism.Automate` project, which the reference site uses
  but which isn't published as a package yet; copy
  [`PrismSendPushNotificationAction.cs`](../src/UmbracoPrism.Automate/PrismSendPushNotificationAction.cs)
  into your own site to use it.
- **From your own code.** Inject `IPrismNotificationService`:

  ```csharp
  // One member, opening /apply-for-a-licence when tapped. Returns false if nothing was sent.
  var sent = await notifications.SendNotificationToUserAsync(
      userId, tenantId, "Your application", "We've made a decision.", "/apply-for-a-licence");

  await notifications.SendNotificationToGenreSubscribersAsync(tenantId, "jazz", "New arrivals", "Three new jazz records are in.");
  await notifications.SendNotificationToAllMembersAsync(tenantId, "Planned downtime", "The site is down for maintenance at 9pm.");
  ```

  `SendNotificationToUserAsync` returns `false` rather than throwing when there was nothing to send
  to: no registered device, Firebase not configured, or every send rejected. Check the result
  before reporting success.

FCM sends go out in batches of 500 tokens. A token Firebase reports as no longer valid is cleared
automatically, so a member who uninstalls the app stops being sent to.

## When Firebase isn't configured

If `Prism:Firebase:CredentialJson` is not set, sending is disabled: the service logs that push notifications are disabled, and
every send returns without delivering. Registration and subscriptions still work, so a site can
run locally without Firebase. See [Push notification setup](PUSH_SETUP.md#part-2--server-side-configuration).

## Worked example

The reference site's record shop sends a "back in stock" notification to a genre's subscribers.
See `PrismVinylNotificationController` in `src/UmbracoPrism.TestSite` and the
[Extending Prism](guides/extending-prism.md) guide, which walks through it.
