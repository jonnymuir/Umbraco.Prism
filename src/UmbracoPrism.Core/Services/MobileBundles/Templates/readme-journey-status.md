
## Journey status

This bundle shows where a member's journeys have got to on the lock screen while they wait on
them: a Live Activity on iPhone (the lock screen and the Dynamic Island) and a Live Update on
Android 16 and later, an ongoing notification on earlier versions. The site publishes each status
through `IPrismJourneyStatusService` or the "Update journey status" Umbraco Automate action, and it
travels through Firebase, as push notifications do. Tapping a status opens its page in the app.

- **iOS:** `bootstrap-ios.sh` adds the `PrismJourneyStatus` widget extension target, which draws the
  Live Activity, and sets `NSSupportsLiveActivities`. The extension is signed as its own app
  identity, `{{appId}}.JourneyStatus`. Automatic signing with an App Store Connect API key registers
  it on the first archive when the key's role (Admin or App Manager) may register identifiers;
  otherwise register it once in the Apple Developer account. The app sends the site its Live
  Activity tokens itself, with the member's session, since iOS can start an activity while the app
  has no page open.
- **Android:** `bootstrap-android.sh` puts the app's own Firebase messaging service,
  `PrismJourneyStatusService`, in place of the push plugin's. It shows journey statuses and passes
  every other message to the plugin.

Each platform logs its journey status messages under the shell's `PrismShell` tag or category (see
"Diagnosing a live app"): `Journey status device sent: 204` means the site will send this device
statuses. Until the page has registered for push notifications the site answers 409, and the app
tries again when next opened. Members can turn Live Activities off in iOS Settings, and the
"Journey status" notification channel off on Android.
