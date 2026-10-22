# Walkthrough: Building a Mobile App

A guide to generating a native iOS or Android app for a Prism tenant, covering the generated project, biometric sign-in, notifications, the app's tabs and the build pipeline.

> **Prerequisites:** The Prism stack runs locally (or in Codespaces). You have walked through the reference site at least once (see [Home entry](home-entry.md)). For iOS builds, Xcode 15+ is required. For Android, Android Studio.

---

## Overview

A Prism mobile app is a generated native shell for one tenant. The pages inside it are the tenant's
own site, served by Umbraco, so editors keep maintaining content, navigation and branding in the
backoffice. The shell adds what a website cannot do on its own:

- **Native tabs** on iOS and Android; each tab is its own webview, so switching tabs is instant
  and keeps each tab's place. The tabs come from the site (see Part 5).
- **Sign-in through the system sign-in sheet**, so the identity provider's pages never load
  inside the app (see Part 3).
- **Biometric sign-in** (`@aparajita/capacitor-biometric-auth`, `@aparajita/capacitor-secure-storage`).
- **Push notifications** (`@capacitor-firebase/messaging`) that open a page when tapped.
- **Journey status** on the lock screen while a member waits on something (see Part 7).
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
native/
  ios/                    ← the native tab shell in Swift, one file per responsibility
  android/                ← the same shell in Java
scripts/
  bootstrap-ios.sh        ← creates the Xcode project and adds native/ios to it
  bootstrap-android.sh    ← creates the Android Studio project and adds native/android to it
README.md                 ← setup, the native shell's files and logs, store submission notes
```

💡 **What's happening:** `server.url` in `capacitor.config.ts` points the app at the tenant's site.
The app appends a user agent marker (`PrismMobile`, plus `PrismNativeTabs`, `PrismSystemSignIn`
and, with saved pages, `PrismSavedPages`) so the site knows it is rendering inside the app. The generated README maps each native file to
what it does, and says how to read the shell's log, which is tagged `PrismShell` on both platforms.

---

## Part 2: Bootstrapping the Native Projects

```bash
npm install
npm run bootstrap:ios       # or bootstrap:android
```

Each script adds its platform, syncs Capacitor, generates icons, and writes the native shell into
the project: on iOS the tab bar, one Capacitor bridge per tab and the tab icon loader; on Android
the same in `MainActivity.java` and `PrismTabFragment.java`. It also does the native setup for the
features you chose: the Face ID usage text for biometric sign-in, the push entitlement and Firebase
files for push notifications, and the `PrismJourneyStatus` widget extension for journey status.
Both scripts can be run again safely after changing settings or regenerating the zip.

With push notifications, put the two Firebase files in `resources/` before bootstrapping:
`GoogleService-Info.plist` for iOS and `google-services.json` for Android (see
[Push notification setup](../PUSH_SETUP.md)).

---

## Part 3: Signing In

### The System Sign-In Sheet

The app signs members in through the phone's own sign-in sheet: `ASWebAuthenticationSession` on
iOS, and Chrome's Auth Tab on Android (a Custom Tab where the browser has no Auth Tab). The
identity provider's pages never load in the app's webviews, and the tenant keeps the same
provider registration and `/signin-oidc` redirect URI it uses for the website.

```
Member taps "Sign in" (or opens a page that needs a member)
  → /auth/login serves the app sign-in page (prism-system-sign-in.js)
  → biometric sign-in, if the member has enrolled
  → otherwise the system sheet opens /umbraco/prism/mobile/auth/start
      → the provider's sign-in → /signin-oidc → /umbraco/prism/mobile/auth/complete
      → https://<site>/umbraco/prism/mobile/auth/callback?code=…  (handed to the app only)
  → the page redeems the code with its PKCE verifier and opens the member's page
  → the other tabs pick up the session
```

The callback is an `https` URL on the tenant's own domain. Each platform hands it to the app only
after checking a file the site serves: `/.well-known/apple-app-site-association` on iOS and
`/.well-known/assetlinks.json` on Android. The one-time code is useless without the verifier the
page holds, and the sheet keeps no session after sign-in, so signing out is complete.

Generated apps need iOS 17.4 or later, the first release with `https` callbacks for the sheet, and Android 7.0 (API 24) or later, which Google Play's automatic protection requires.

### Setting Up App Sign-In in the Backoffice

1. Open **Settings → Advanced → Tenants → [your tenant] → Produce Mobile**.
2. Under **App Sign-In**, fill in:
   - **Apple Team ID**: the ten-character ID under Membership in your Apple Developer account.
   - **Android Package Name**: only when it differs from the App ID.
   - **Android Signing Certificate SHA-256 Fingerprints**: for an app on Google Play, the app
     signing key's fingerprint from Play Console, under App signing. Add the fingerprint of any
     other key you install builds signed with, one per line.
3. Click **Save**.

A tenant seeded from configuration can set these as `Prism:SeedTenant:MobileApp` instead (see
[Seeding a tenant from configuration](../umbraco-setup.md#seeding-a-tenant-from-configuration-no-backoffice-no-manual-step)). Until a platform's fields are filled
in, the site serves no file for it and that platform's app signs in inside the app as before. The iOS app's App ID needs the Associated Domains capability,
which Xcode adds when it signs with automatic signing.

The association can only be checked against a real domain from a signed build, so the system
sheet completes on devices, not in the iOS simulator. On an Android emulator the callback can be
handed to the app by hand with `adb shell am start -a android.intent.action.VIEW -d "<callback URL>" <app id>`.

### Biometric Sign-In

After a user's first OIDC login, Prism can enroll their device for biometric auth (Face ID, Touch ID, fingerprint). Subsequent app launches skip the OIDC redirect and use biometric verification instead, for a faster, more native-feeling login experience.

#### How It Works

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

#### Configuring Biometric Auth in the Backoffice

1. Log into the Umbraco backoffice (`https://localhost:44345/umbraco`).
2. Navigate to **Settings → Advanced → Tenants → [your tenant]**.
3. Find the **Allow Biometric Login** toggle and enable it (it is on by default).
4. Click **Save**.

When the mobile app next connects, it checks this tenant setting and offers biometric enrollment after successful OIDC login. In an app that uses the system sign-in sheet, the app sign-in page offers biometric sign-in first, before opening the sheet.

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
    var appSignsInNatively = PrismMobileRequestDetection.SignsInNatively(Context, tenant);
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

@if (PrismMobileRequestDetection.HasNativeTabs(Context))
{
    <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-offline.js"
            data-prism-saved-pages="@(PrismMobileRequestDetection.SavesPages(Context) ? "on" : "off")"
            data-prism-signed-in="@(isAuthenticated ? "true" : "false")"></script>
}

@if (biometricLoginEnabled)
{
    @if (isAuthenticated)
    {
        <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-enroll.js"></script>
    }
    else if (!appSignsInNatively)
    {
        <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-autologin.js"></script>
    }
}

@if (isPrismMobileRequest && isAuthenticated)
{
    <script src="/App_Plugins/UmbracoPrism/mobile-shell/prism-biometric-signout.js"
            data-prism-idp-hosts="@(appSignsInNatively ? "" : string.Join(",", PrismIdentityProviderHosts.Resolve(tenant)))"></script>
}
```

| Asset | Purpose | Include when |
|---|---|---|
| `prism-mobile-shell.css` | Safe-area padding, full-width layout for the WebView | Any Prism mobile request |
| `prism-mobile-shell-guard.js` | Adds the `prism-mobile` CSS class; keeps `target="_blank"` links and `window.open()` inside the WebView instead of spawning a new window | Any Prism mobile request |
| `prism-offline.js` | Saved pages (Part 6): registers the worker that keeps pages on the device and shows the offline line, or removes them in an app built without saved pages | App with native tabs |
| `prism-biometric-enroll.js` | Shows the "Enable Face ID / Touch ID?" banner and registers a device credential | Mobile request, biometric login enabled for the tenant, user already authenticated |
| `prism-biometric-autologin.js` | Silently exchanges a previously-enrolled device's stored credential for a session, before the login page would otherwise render | Mobile request, biometric login enabled for the tenant, user not yet authenticated |
| `prism-biometric-signout.js` | On sign-out, forgets the device's biometric credential and push token, and clears the identity provider's cookies from the app | Mobile request, user authenticated. Pass no hosts when the app signs in through the system sheet, since the provider's pages never load in the app |

`SignsInNatively` is true for an app that uses the system sign-in sheet on a platform the
tenant has set up for app sign-in (see Part 3). Such an app offers biometric sign-in from its
own sign-in page instead, so the layout leaves out `prism-biometric-autologin.js`.

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

Pages rendered for the app leave out their own web tab bar: `PrismMobileRequestDetection.HasNativeTabs`
is true when the user agent carries `PrismNativeTabs`. Android shows the first five tabs, as
Material's bottom navigation allows.

### Behaviour

- **Switching tabs** shows the tab as you left it, without a network request.
- **Tapping the selected tab** returns it to its first page.
- **Signing in or out** in one tab reloads each other tab the next time it is shown, and fetches
  the tabs again for the new member.
- **A notification tap** is handled by the tab that is showing.
- **Back on Android** goes back in the tab's own history, then to the first tab, then leaves the app.

---

## Part 6: Saved Pages

With the **Saved Pages** bundle option (`--saved-pages true` for the CLI), the app keeps copies of
the site's pages on the device. A tab's first page opens at once from its saved copy and refreshes
in the background, and a page the member has seen still opens with no connection.

- **What is saved:** `GET` pages of the site that the site allows to be stored, and the site's own
  stylesheets, scripts, fonts and images. A response sent `Cache-Control: no-store` is never saved.
  The reference site sends every signed-in page that way, and ASP.NET Core does the same for any
  page with an antiforgery token, so member pages and journey forms always need a connection. A
  host that wants a signed-in page available offline sends it `Cache-Control: private` instead.
- **Signing in or out** deletes everything saved, so no copy outlives the session it was saved in.
- **Fresh copies** replace what is on screen only before the member starts using the page, or when
  they come back to the app; never once they have typed something. Publishing content tells the app
  its saved pages are out of date.
- **Offline,** a page shows one line above the tab bar: "You're offline. Some information may be
  out of date."

The worker is served from `/prism-offline-sw.js`, and `prism-offline.js` (Part 3's layout snippet)
registers it. On iOS, saved pages limit the app's webviews to the site and its identity provider's
sign-in hosts (App-Bound Domains, at most 10, no wildcards), which is when iOS allows a service
worker. A tenant that signs in inside the app on Entra needs an authority whose host the generator
can list; App Sign-In avoids the question, since the provider's pages never load in the app.

The design is in `Design/mobile-offline-pages.md`.

---

## Part 7: Journey Status

With the **Journey Status** bundle option (`--journey-status true` for the CLI, alongside push
notifications), a member sees where one of their journeys has got to on the lock screen while they
wait on it: a Live Activity on iPhone, on the lock screen and in the Dynamic Island, and a Live
Update on Android 16 and later (an ongoing notification on earlier versions). It updates in place
with the app closed, and tapping it opens the journey's page in the app.

The site publishes each status. Prism does not know about journeys, so the host decides when:

```csharp
journeyStatus.Publish(memberEmail, tenantHostname, new PrismJourneyStatus(
    Key: request.InstanceId,
    ServiceName: "Apply for a juggling licence",
    Status: "Deciding your application",
    Step: null, Steps: null,
    Path: "/apply-for-a-juggling-licence",
    Kind: PrismJourneyStatusKind.Waiting));
```

- **Waiting** starts the status, or updates it if one is showing. **Done** ends it, with its last
  status showing for 15 minutes.
- **`Publish` returns at once.** A background sender delivers the status through Firebase, so a
  slow or failed delivery never holds up whatever caused the change.
- **The status line says what kind of thing is happening, never the outcome:** "A decision has
  been made", not "Your licence was refused". Anyone holding the phone can read a lock screen.
  The outcome belongs in a push notification that opens the page behind sign-in.
- **A Live Activity is for a wait of hours,** since iOS ends one within 12. A change days later is a
  push notification.
- The member is identified as push notifications identify them, by email address and the tenant's
  host name, and only their own devices receive the status.

The **Update journey status** Umbraco Automate action publishes a status without code. The
reference site publishes from Wayfinder.Umbraco's `ServiceRequestTransitionedNotification`
(`JourneyStatuses` in the TestSite): a signed-in member's juggling licence application shows
"Deciding your application" while the decision runs, and "A decision has been made" when it
settles, alongside the decision's own push notification.

On iOS the Live Activity is drawn by a widget extension, `PrismJourneyStatus`, signed as its own
app identity: the app's ID with `.JourneyStatus` added. The generated README says how to register
it. [Journey status](../journey-status.md) describes the API, the app's endpoints and delivery in
full; the design is in `Design/mobile-journey-status.md`.

---

## Part 8: Building for iOS

```bash
npx cap open ios
```

In Xcode, under **Signing & Capabilities**, choose your team for the **App** target and, with
journey status, the **PrismJourneyStatus** target. Then pick a simulator or device and press
**⌘R**. The app opens on its first tab, showing the tenant's site.

The push entitlement is set to `production`, which is what TestFlight and the App Store need. To
receive notifications in a build run from Xcode on a device, change `aps-environment` in
`ios/App/App/App.entitlements` to `development` for that run.

💡 **What's happening:** each tab is a Capacitor bridge with its own `WKWebView` loading the
tenant's site. Plugin calls from the page (biometric sign-in, push registration, the system
sign-in sheet) reach their native implementations through that tab's bridge.

---

## Part 9: Building for Android

```bash
npx cap open android
```

Android Studio opens. Select a device or emulator and click **Run** (▶).

💡 **What's happening:** the Android project's `MainActivity` is the native tab shell: a Material
bottom navigation bar with one Capacitor bridge and `WebView` per tab (`PrismTabFragment.java`),
all loading the tenant's site.

For detailed Android-specific guidance, refer to the
[Capacitor Android documentation](https://capacitorjs.com/docs/android).

---

## Part 10: Development Tips

### Running Against Your Local Stack

The app always loads its pages from the start URL, so a change to the site shows in the app on its
next page load, with no rebuild. To run an app against the local stack, generate the bundle with
the local site as its start URL (`--start-url https://localhost:44345` for the CLI). The simulator or emulator must trust
the site's development certificate, and the Android emulator reaches your machine's ports through
`adb reverse tcp:44345 tcp:44345` (and `tcp:8443` for the local Keycloak).

### Testing Push Notifications and Journey Status

- **iOS:** push notifications and remotely started Live Activities need a physical device; the
  Simulator can't receive them.
- **Android:** emulators with Google Play Services receive push notifications and journey statuses.
- The shell logs what it does under `PrismShell` on both platforms, including registration and why
  a page failed to load. The generated README says how to read it.
- For token registration failures and delivery issues, see
  [Push notification setup](../PUSH_SETUP.md#testing) and
  [Notifications](../notifications.md#when-firebase-isnt-configured).

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
| Journey status on the lock screen | [docs/journey-status.md](../journey-status.md) |
| Capacitor documentation | [capacitorjs.com/docs](https://capacitorjs.com/docs) |
| Storybook: Mobile Nav story | `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.stories.ts` |

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`building-a-mobile-app.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/building-a-mobile-app.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
