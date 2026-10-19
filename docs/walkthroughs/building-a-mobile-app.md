# Walkthrough: Building a Mobile App

A guide to generating a native iOS or Android app for a Prism tenant, covering the generated project, biometric sign-in, notifications, the app's tabs and the build pipeline.

> **Prerequisites:** The Prism stack runs locally (or in Codespaces). You have walked through the reference site at least once (see [Home entry](home-entry.md)). For iOS builds, Xcode 15+ is required. For Android, Android Studio.

---

## Overview

A Prism mobile app is a generated native shell for one tenant. The pages inside it are the tenant's
own site, served by Umbraco, so editors keep maintaining content, navigation and branding in the
backoffice. The shell adds what a website cannot do on its own:

- **Native tabs.** iOS uses a native tab bar; each tab is its own webview, so switching tabs is
  instant and keeps each tab's place. The tabs come from the site (see Part 5).
- **Biometric sign-in** (`@aparajita/capacitor-biometric-auth`, `@aparajita/capacitor-secure-storage`).
- **Push notifications** (`@capacitor-firebase/messaging`) that open a page when tapped.
- **A branded offline screen** when a page cannot be reached.

---

## Part 1: Generating the App

In the backoffice, open **Settings → Advanced → Tenants**, choose the tenant, fill in its mobile
settings (app name, app ID, start URL, the error screen's text and colours, and which features to
include) and select **Produce Mobile**. You download a zip. CI builds the same zip with
`src/UmbracoPrism.MobileBundleCli`, which takes the same settings as command-line flags.

The zip contains a Capacitor project:

```
capacitor.config.ts       ← start URL, allowed hosts, user agent marker
package.json              ← Capacitor and the plugins for the features you chose
www/
  index.html              ← first screen: checks the site is reachable, then opens it
  offline.html            ← shown when a page fails to load
resources/
  icon.svg                ← replace with the app's own icon before bootstrapping
  PrivacyInfo.xcprivacy   ← the iOS privacy manifest
scripts/
  bootstrap-ios.sh        ← creates and patches the Xcode project
  bootstrap-android.sh    ← creates and patches the Android Studio project
README.md                 ← setup, store submission notes and troubleshooting
```

💡 **What's happening:** `server.url` in `capacitor.config.ts` points the app at the tenant's site.
The app appends a user agent marker (`PrismMobile`, plus `PrismNativeTabs` on iOS) so the site
knows it is rendering inside the app.

---

## Part 2: Bootstrapping the Native Projects

```bash
npm install
npm run bootstrap:ios       # or bootstrap:android
```

The iOS script adds the iOS platform, syncs Capacitor, generates icons, and writes the native shell
(`PrismShell.swift`: the tab bar, one Capacitor bridge per tab, and the tab icon loader) into the
Xcode project. Both scripts can be run again safely after changing settings or regenerating the zip.

---

## Part 3: Biometric Authentication

After a user's first OIDC login, Prism can enroll their device for biometric auth (Face ID, Touch ID, fingerprint). Subsequent app launches skip the OIDC redirect and use biometric verification instead, for a faster, more native-feeling login experience.

### How It Works

```
First launch:
  User → OIDC login (Keycloak / Entra)
  Prism → stores encrypted refresh token on device (AES-256)
  User → prompted to enroll biometric

Subsequent launches:
  User → biometric prompt (OS-level)
  Prism → decrypts refresh token, exchanges for new access token
  User → lands on dashboard (no OIDC redirect)
```

The biometric system requires two cryptographic keys configured under `Prism:Biometric`:
- **SigningKey**: HMAC-SHA256, signs biometric JWTs
- **EncryptionKey**: AES-256, encrypts refresh tokens at rest

For key generation steps and configuration (local dev via user secrets, production via Key Vault), see **[docs/biometric-setup.md](../biometric-setup.md)**: that document is the authoritative configuration guide.

<!-- manual capture: Biometric enrollment prompt requires physical device or simulator native UI interaction -->

### Configuring Biometric Auth in the Backoffice

1. Log into the Umbraco backoffice (`https://localhost:44345/umbraco`).
2. Navigate to **Settings → Advanced → Tenants → [your tenant]**.
3. Find the **Allow Biometric Login** toggle and enable it (it is on by default).
4. Click **Save**.

When the mobile app next connects, it checks this tenant setting and offers biometric enrollment after successful OIDC login.

<!-- manual capture: the tenant editor's biometric toggle requires Umbraco backoffice authentication -->

💡 **What's happening:** The toggle is part of the tenant editor (`prism-create-tenant-modal.ts`), which saves the tenant's `AllowBiometricLogin` flag with `PUT /umbraco/management/api/v1/prism/tenants/{id}`. When it is off, mobile users can't register or use biometric sign-in for that tenant.

### Wiring the Mobile Shell and Biometric Scripts into Your Layout

Prism ships the mobile-shell CSS/JS and biometric enroll/auto-login scripts as plain static
files under `wwwroot/mobile-shell/` in `UmbracoPrism.Core` — the same delivery mechanism as
`prism-mobile-nav.js`. **Your host must reference them explicitly** in its own layout;
Prism does not inject them for you (SEC-PT2-004: this used to happen via a response-rewriting
middleware, which was retired in favour of ordinary asset references — see
[branding-design-system.md](../branding-design-system.md#how-overrides-reach-the-live-site) for
the same change applied to branding CSS).

Detect a Prism mobile request with `PrismMobileRequestDetection.IsPrismMobileRequest(Context)`
(`UmbracoPrism.Core.Extensions`) and conditionally include the assets in `<head>` / before
`</body>`:

```cshtml
@using UmbracoPrism.Core.Extensions
@{
    var isPrismMobileRequest = PrismMobileRequestDetection.IsPrismMobileRequest(Context);
    var biometricLoginEnabled = isPrismMobileRequest && (tenant?.AllowBiometricLogin ?? false);
    var isAuthenticated = Context.User.Identity?.IsAuthenticated == true;
}

@if (isPrismMobileRequest)
{
    <link rel="stylesheet" href="/App_Plugins/UmbracoPrism/mobile-shell/prism-mobile-shell.css" />
}

@* … your page content … *@

@if (isPrismMobileRequest)
{
    <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-mobile-shell-guard.js"></script>
}

@if (biometricLoginEnabled)
{
    @if (isAuthenticated)
    {
        <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-enroll.js"></script>
    }
    else
    {
        <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-autologin.js"></script>
    }
}
```

| Asset | Purpose | Include when |
|---|---|---|
| `prism-mobile-shell.css` | Safe-area padding, full-width layout for the WebView | Any Prism mobile request |
| `prism-mobile-shell-guard.js` | Adds the `prism-mobile` CSS class; keeps `target="_blank"` links and `window.open()` inside the WebView instead of spawning a new window | Any Prism mobile request |
| `prism-biometric-enroll.js` | Shows the "Enable Face ID / Touch ID?" banner and registers a device credential | Mobile request, biometric login enabled for the tenant, user already authenticated |
| `prism-biometric-autologin.js` | Silently exchanges a previously-enrolled device's stored credential for a session, before the login page would otherwise render | Mobile request, biometric login enabled for the tenant, user not yet authenticated |

`UmbracoPrism.TestSite`'s own `Views/Shared/Master.cshtml` is the reference implementation —
copy the pattern above from there if you'd rather see it in full context.

None of these scripts need a build-time tenant hostname baked in — by the time any of them run,
the Capacitor WebView has already navigated to your site's real HTTPS origin (see the bootstrap
shell `MobileBundleService` generates), so `window.location.host` and plain relative fetch URLs
are always correct.

---

## Part 4: Opening a page from a notification

A generated app opens a specific page when someone taps a push notification. The sender supplies
a site-relative path (the `deepLinkPath` argument to
`IPrismNotificationService.SendNotificationToUserAsync`, or the **Deep link path** field of the
**Send Prism push notification** Automate action). Prism sends it as the notification's `url`
data, and the app's `prism-push-register.js` navigates the WebView there when the notification
is tapped.

The link must be a path on the tenant's own site, such as `/apply-for-a-juggling-licence`, so a
notification can never send someone to another site or run script in their session:

- `SendNotificationToUserAsync` drops any other value, logs a warning, and sends the notification
  without a link.
- The Automate action fails the step with a validation error, so the automation's author sees it.
- The app itself only follows a link that resolves to its own site, so a link that reaches
  Firebase some other way is ignored too.

`PrismDeepLinkPath.IsSitePath` is the same check, if you want to validate a path before you send.

The generated apps don't register a custom URL scheme or Universal/App Links, so a link in an
email or another app opens the site in the browser rather than in the app. Adding one is a change
to the generated Capacitor project (see Capacitor's
[deep links guide](https://capacitorjs.com/docs/guides/deep-links)).

---

## Part 5: The App's Tabs

The app reads its tabs from `/umbraco/prism/mobile/manifest` when it starts and whenever the member
signs in or out, and keeps the last copy so it can start offline. Prism does not own the site's
navigation, so the host supplies the tabs by implementing `IPrismMobileTabsProvider`:

```csharp
public class MyMobileTabsProvider : IPrismMobileTabsProvider
{
    public Task<IReadOnlyList<PrismMobileTab>> GetTabsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<PrismMobileTab> tabs =
        [
            new("Home", "/", "home"),
            new("Dashboard", "/dashboard", "/media/icons/dashboard.svg"),
        ];
        return Task.FromResult(tabs);
    }
}

// in a composer
builder.Services.AddScoped<IPrismMobileTabsProvider, MyMobileTabsProvider>();
```

`context.User` is the signed-in member, if there is one, so a provider can leave out pages that
member cannot use. A tab's icon is either a built-in name (`home`, `dashboard`, `account`,
`settings`, `transactions`, `notifications`, `more`) or the URL of an image. SVG icons made of
`<path>` elements, the usual single-colour icon sets, are drawn natively and tinted like system
icons. A host that registers no provider gets a single Home tab.

The reference site's provider (`TestSiteMobileTabsProvider`) reads the **Mobile Nav Links** block
list on the Settings node, the same items its web tab bar uses, so editors maintain both in one
place.

Pages rendered for the iOS app leave out their own web tab bar: `PrismMobileRequestDetection.HasNativeTabs`
is true when the user agent carries `PrismNativeTabs`. Android apps still use the
`prism-mobile-nav` web component until they get native tabs.

### Behaviour

- **Switching tabs** shows the tab as you left it, without a network request.
- **Tapping the selected tab** returns it to its first page.
- **Signing in or out** in one tab reloads each other tab the next time it is shown, and fetches
  the tabs again for the new member.
- **A notification tap** is handled by the tab that is showing.

---

## Part 6: Building for iOS

### Step 3: Open the Xcode Project

```bash
npx cap open ios
```

Xcode opens with the generated project. Before building:

1. Select the **App** target.
2. Under **Signing & Capabilities**, set your Team and Bundle Identifier.
3. If using biometric auth, verify the `NSFaceIDUsageDescription` key is set in `Info.plist`:
   ```xml
   <key>NSFaceIDUsageDescription</key>
   <string>Use Face ID to sign in to your Prism account.</string>
   ```
4. If using push notifications, add the **Push Notifications** capability (see [Push notification setup](../PUSH_SETUP.md#part-1--firebase-console-setup-you-do-this-once-per-app)).

### Step 4: Build and Run (iOS)

1. Select a physical device or simulator in the Xcode target dropdown.
2. Press **⌘R** to build and run.
3. The app launches with the Prism TestSite loaded in a full-screen WebView.

<!-- manual capture: iOS app screenshot requires physical iOS device or Xcode simulator -->

💡 **What's happening:** The native Xcode project (`ios/App/`) embeds your compiled web assets in a `WKWebView`. Capacitor bridges JavaScript calls from the web layer to native Swift/Obj-C plugin implementations, this is how `@aparajita/capacitor-biometric-auth` calls Face ID/Touch ID, and how `@capacitor/push-notifications` registers with APNs.

For App Store distribution builds, change the `aps-environment` entitlement from `development` to `production` (see [Push notification setup](../PUSH_SETUP.md#part-1--firebase-console-setup-you-do-this-once-per-app)).

---

## Part 7: Building for Android

### Step 5: Place `google-services.json`

If push notifications are enabled, place the Firebase `google-services.json` in:

```
android/app/google-services.json
```

Then sync:

```bash
npx cap sync android
```

See [Push notification setup](../PUSH_SETUP.md#part-1--firebase-console-setup-you-do-this-once-per-app) for the full Firebase project setup steps.

### Step 6: Open and Build in Android Studio

```bash
npx cap open android
```

Android Studio opens. Select a device or emulator and click **Run** (▶).

<!-- manual capture: Android app screenshot requires Android Studio emulator or physical device -->

💡 **What's happening:** The Android project (`android/`) embeds your compiled web assets in a `WebView` backed by Capacitor's Android bridge. Plugin calls (biometric, push, secure storage) are dispatched to their corresponding Kotlin implementations via the Capacitor plugin registry.

For detailed Android-specific guidance, refer to the [Capacitor Android documentation](https://capacitorjs.com/docs/android).

---

## Part 8: Development Tips

### Iterating Without Rebuilding Native Apps

Point `server.url` in `capacitor.config.ts` at your running Prism stack, then in Xcode/Android Studio simply re-run the app. The WebView loads fresh from the live server on every launch, no Capacitor sync needed.

```typescript
server: {
  url: 'https://localhost:44345',
  cleartext: false,
},
```

> ⚠️ Remove `server.url` before App Store or Play Store submission, production builds should use the bundled assets in `webDir`.

### Testing Push Notifications

- **iOS:** Push notifications require a physical device, they do not work on the iOS Simulator.
- **Android:** Emulators with Google Play Services installed support push notifications.
- For token registration failures and delivery issues, see [Push notification setup](../PUSH_SETUP.md#testing) and [Notifications](../notifications.md#when-firebase-isnt-configured).

### Biometric Auth During Development

To re-trigger the biometric enrollment prompt (useful when testing):

```bash
# iOS: reset all app permissions on simulator
xcrun simctl privacy booted reset all com.prism.portal

# Android: clear app data
adb shell pm clear com.prism.portal
```

---

## Related Resources

| Resource | Location |
|---|---|
| Biometric key configuration | [docs/biometric-setup.md](../biometric-setup.md) |
| Native push setup (APNs, FCM) | [docs/PUSH_SETUP.md](../PUSH_SETUP.md) |
| How Prism sends notifications | [docs/notifications.md](../notifications.md) |
| Capacitor documentation | [capacitorjs.com/docs](https://capacitorjs.com/docs) |
| Storybook: Mobile Nav story | `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.stories.ts` |

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`building-a-mobile-app.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/building-a-mobile-app.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
