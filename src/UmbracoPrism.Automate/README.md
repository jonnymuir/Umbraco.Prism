# UmbracoPrism.Automate

[Umbraco Automate](https://www.nuget.org/packages/Umbraco.Automate) actions for
[Umbraco Prism](https://github.com/jonnymuir/Umbraco.Prism): send a push notification to a member's
Prism mobile app, or show where one of their journeys has got to on their lock screen, from an
automation.

## Install

```bash
dotnet add package UmbracoPrism.Automate
```

It needs `UmbracoPrism` and Umbraco Automate 17, and registers itself: there is no code to add.
The actions appear in the automation editor under **Prism**.

## Send Prism push notification

Sends a notification to one member's registered devices.

| Setting | Required | What it is |
|---|---|---|
| User id | Yes | The member to notify: the id their device registered under. |
| Tenant id | Yes | The Prism tenant the member belongs to. |
| Title | Yes | The notification's title. |
| Body | Yes | The notification's text. |
| Deep link path | No | A page on the tenant's site to open when the notification is tapped, such as `/orders`. |

Every setting accepts bindings, so you can fill it from the trigger or an earlier step.

The step's output has one value, `sent`. It is `false` when nothing was sent because the member
has no registered device or Firebase isn't configured. That isn't treated as a failure: a member
who never installed the app is a normal case. Branch on `sent` if the automation should do
something else for those members, such as send an email.

The deep link must be a path on the tenant's own site. Anything else, such as another site's URL
or a `javascript:` link, fails the step with a validation error, because the app would otherwise
follow it.

Delivery goes through Prism's notification service, so it needs Firebase set up as described in
[Notifications](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/notifications.md).

## Versions

Released together with `UmbracoPrism`, with the same version number. Install the same version of
both.

## Licence

MIT. See the [Umbraco Prism repository](https://github.com/jonnymuir/Umbraco.Prism) for source,
issues and the [security policy](https://github.com/jonnymuir/Umbraco.Prism/blob/main/SECURITY.md).

## Update journey status

Shows where one of a member's journeys has got to on their phone's lock screen, in an app built
with the **Journey Status** option: a Live Activity on iPhone and a Live Update on Android. A
later status with the same journey key updates it in place.

| Setting | Required | What it is |
|---|---|---|
| User id | Yes | The member, as for push notifications. |
| Tenant id | Yes | The Prism tenant the member belongs to. |
| Journey key | Yes | One per journey, such as the service request's ID. |
| Service name | Yes | The service, such as `Apply for a juggling licence`. |
| Status | Yes | One short line, up to 120 characters, such as `Deciding your application`. |
| Kind | Yes | `Waiting` while the member waits on something now, or `Done` when that wait is over. |
| Path | Yes | The page on the tenant's site that tapping the status opens. |
| Step, Steps | No | Whole numbers, shown as "Step 2 of 3". |

The status is queued and delivered in the background, so the step never waits on it. Write the
status as what kind of thing is happening, never the outcome or personal detail: anyone holding
the phone can read a lock screen. Send the outcome as a push notification instead, which opens the
page behind sign-in.
