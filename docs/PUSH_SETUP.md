# Prism Mobile: Push Notifications Setup

Prism sends push notifications via Firebase Cloud Messaging (FCM) only —
`PrismNotificationService` (`src/UmbracoPrism.Core/Services/PrismNotificationService.cs`) is built
on `FirebaseAdmin.Messaging`, with no separate APNs-only path. On the client, the generated mobile
bundle uses the **`@capacitor-firebase/messaging`** plugin, not `@capacitor/push-notifications` —
it bridges the raw APNs token iOS hands it through Firebase's own SDK into an FCM token, so both
iOS and Android register a token the backend can actually send to. There is nothing to install or
configure by hand in a generated bundle's native project: `--push-notifications true` (the
`MobileBundleCli` flag / `PushNotificationsEnabled` bundle request field) wires all of it in
automatically. This doc is in two parts: what you (the Prism site operator) set up in Firebase and
on the server, and what the generated bundle already does for you.

---

## Part 1 — Firebase Console setup (you do this once, per app)

1. Go to the [Firebase Console](https://console.firebase.google.com/) and create a project (or
   use an existing one) — any name, it's just a container for your app registrations.
2. Add your **iOS app** to the project: **Project settings → Add app → iOS**.
   - **Bundle ID** must exactly match your Prism app's `--app-id` (e.g. `com.jonnymuir.prismreference`).
   - Download the generated **`GoogleService-Info.plist`** — you'll need it in Part 3.
3. Add your **Android app** to the same project: **Project settings → Add app → Android**.
   - **Package name** must exactly match the same `--app-id`.
   - Download the generated **`google-services.json`** — you'll need it in Part 3.
4. **Create the APNs key on Apple Developer** (do this before the Firebase upload step below):
   - Go to [developer.apple.com/account](https://developer.apple.com/account) →
     **Certificates, Identifiers & Profiles → Keys → +** (create a new key).
   - Name it anything, tick **Apple Push Notifications service (APNs)**, continue.
   - **Environment: choose "Sandbox & Production"**, not either one alone — Firebase (and most
     other senders) rejects a key scoped to only one environment. This is the whole point of the
     newer key-based approach over the old certificates: one key covers both.
   - Register, then **download the `.p8` file** — Apple only lets you download it once, so keep it
     safe. Note the **Key ID** shown on this same screen.
   - Find your **Team ID**: **Account** (top nav) → select your team top-right if you have more
     than one → scroll down to **Membership details**.
   - Full walkthrough: [OneSignal's guide to this exact flow](https://documentation.onesignal.com/docs/en/ios-p8-token-based-connection-to-apns).
5. **Upload it to Firebase**: **Project settings → Cloud Messaging → Apple app configuration →
   APNs Authentication Key → Upload**.
   - You'll see two separate slots, "development" and "production" — these are leftovers from the
     old certificate system, where you genuinely needed two files. With a single Sandbox &
     Production key, upload the **same `.p8` file, Key ID, and Team ID into both slots**.
6. Generate the backend credential: **Project settings → Service accounts → Generate new private
   key**. This downloads a JSON file — this is what `PrismNotificationService` authenticates with
   server-side. Keep it secret (unlike `GoogleService-Info.plist`/`google-services.json`, which
   are safe to ship inside a compiled app, this file grants send-as-your-project access).

That's the whole Firebase-side setup. No separate iOS/Android tracks, no manual entitlements or
Gradle edits — the generated bundle's bootstrap scripts (Part 3) handle the native wiring, and
Firebase's own SDK bridges the APNs key you uploaded in step 5 automatically once a device
registers.

---

## Part 2 — Server-side configuration

Set `Prism:Firebase:CredentialJson` to the full contents of the service-account JSON from step 6
above (or a file path to it — `PrismNotificationService.TryInitFirebase` accepts either: a raw
JSON string starting with `{`, or a path to a file containing it).

On the reference app's VPS (a systemd `EnvironmentFile`, same convention as
`Prism__SeedTenant__Hostname` etc. — see `docs/umbraco-setup.md`), that's:

```
Prism__Firebase__CredentialJson={"type":"service_account","project_id":"...", ...}
```

Without this set, `PrismNotificationService` logs "push notifications disabled" and every send is
a silent no-op — safe to leave unset while you're still doing the Firebase setup above; nothing
else breaks.

---

## Part 3 — What the generated bundle does for you

Generate the bundle with the flag on:

```bash
dotnet run --project src/UmbracoPrism.MobileBundleCli -- \
  --hostname your-tenant.example.com \
  --push-notifications true \
  --output mobile-bundle.zip
```

(Or, in CI, `deploy-testflight.yml` turns this on automatically once the
`GOOGLE_SERVICE_INFO_PLIST_BASE64` repo secret is set — see below.)

Before running `npm run bootstrap:ios` / `npm run bootstrap:android`, place the two files you
downloaded in Part 1 here:

- `resources/GoogleService-Info.plist`
- `resources/google-services.json`

Then:

- **`bootstrap-ios.sh`** copies `GoogleService-Info.plist` into `ios/App/App/`, registers it as a
  Copy Bundle Resources entry in `project.pbxproj`, injects `UIBackgroundModes`
  (`remote-notification`) into `Info.plist`, writes `ios/App/App/App.entitlements` with
  `aps-environment: production` (correct for a TestFlight/App Store build, not a plain Xcode debug
  run), wires that entitlements file into `CODE_SIGN_ENTITLEMENTS`, and patches
  `AppDelegate.swift` with the delegate methods `@capacitor-firebase/messaging` needs to bridge
  the APNs token into FCM.
- **`bootstrap-android.sh`** copies `google-services.json` into `android/app/` — the Firebase
  Gradle plugin Capacitor already applies picks it up automatically, no manual `build.gradle` edit
  needed.
- **`Master.cshtml`** (the reference host's own wiring — see that file for the pattern) loads
  `prism-push-register.js` on every authenticated mobile page, which requests notification
  permission, calls `FirebaseMessaging.getToken()`, and registers the token with
  `POST /umbraco/prism/push/register`. `prism-biometric-signout.js` clears the locally-cached
  token fingerprint and calls `DELETE /umbraco/prism/push/register` on sign-out.

None of this is gated on a per-tenant database flag (unlike `AllowBiometricLogin`) — whether the
native plugin is compiled in at all is a bundle-generation-time decision, not something a runtime
toggle could change after the fact.

### CI (TestFlight)

`deploy-testflight.yml` reads a `GOOGLE_SERVICE_INFO_PLIST` repo secret
(**Settings → Secrets and variables → Actions → New repository secret**) — open your downloaded
`GoogleService-Info.plist` in a text editor, copy its entire contents, and paste them as the
secret value (it's plain XML text, no encoding needed). When that secret is set, the workflow
automatically passes `--push-notifications true` and writes the file into the bundle's
`resources/` folder before bootstrapping. When it's unset, the pipeline still runs — it just
produces a build without push notifications compiled in, same as today.

---

## Testing

- **iOS:** push notifications don't work on the Simulator (no APNs). Test on a physical device or
  a real TestFlight build.
- **Android:** works on an emulator with Google Play Services installed.
- Confirm registration worked by checking the app's own console log for `[Prism Push] device token
  registered`, or checking that a device token now exists for the signed-in member
  server-side.
- Send a test notification via `IPrismNotificationService.SendNotificationToUserAsync` (or trigger
  the juggling-licence automation demo — `JugglingLicenceDecisionAutomationSeeder` — which sends
  one automatically via `PrismSendPushNotificationAction`).
