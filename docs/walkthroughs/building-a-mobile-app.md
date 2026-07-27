# Walkthrough — Building a Mobile App from a Service Blueprint

A guide to taking a Prism service blueprint and shipping it as a native iOS or Android app using Capacitor — covering the shell structure, biometric authentication, deep link handling, and the build pipeline.

> **Prerequisites:** The Prism stack runs locally (or in Codespaces). You have completed at least one service blueprint walkthrough (e.g., [Planning Notification](planning-notification.md)) and are familiar with how service blueprints render in the browser. For iOS builds, Xcode 15+ is required. For Android, Android Studio.

---

## Overview

Prism mobile apps are **thin Capacitor shells** around the existing web-based service blueprint UI. The same Razor views, Lit web components, and service blueprint engine that power the browser experience are reused unchanged inside a native WebView. What Capacitor adds:

- **Native push notifications** (via `@capacitor/push-notifications`)
- **Biometric authentication** (via `@aparajita/capacitor-biometric-auth`)
- **Deep link handling** (URL schemes / Universal Links / App Links)
- **Secure credential storage** (via `@aparajita/capacitor-secure-storage`)
- **Native navigation chrome** (`prism-mobile-nav` web component)

This means you get a genuinely native-feeling app without maintaining a separate codebase.

---

## Part 1: The Capacitor Shell Structure

The Capacitor configuration and generated native projects live alongside the client source in `src/UmbracoPrism.Client/`:

```
src/UmbracoPrism.Client/
  capacitor.config.ts         ← Capacitor configuration
  android/                    ← Generated Android Studio project
  ios/                        ← Generated Xcode project
  src/
    mobile/
      prism-mobile-nav.ts     ← Native-feeling bottom navigation bar
  public/
    sw.js                     ← Service worker (web push + offline caching)
```

### The `capacitor.config.ts`

```typescript
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.prism.portal',
  appName: 'Prism Portal',
  webDir: 'dist',
  server: {
    // In dev: point at the live Vite dev server
    url: 'https://localhost:44345',
    cleartext: false,
  },
  plugins: {
    PushNotifications: {
      presentationOptions: ['badge', 'sound', 'alert'],
    },
    BiometricAuth: {
      androidTitle: 'Verify your identity',
      androidSubtitle: 'Use your biometric to sign in',
    },
  },
};

export default config;
```

💡 **What's happening:** In a production build, `webDir: 'dist'` tells Capacitor to bundle the compiled Vite output into the native app. In development, `server.url` overrides this with a live server URL — so the native shell makes network requests to the Prism TestSite directly, which means you can iterate on the web UI without rebuilding the native project each time.

---

## Part 2: Preparing the Web Build

### Step 1: Build the Client Bundle

```bash
cd src/UmbracoPrism.Client
npm install
npm run build
```

This produces a `dist/` folder containing the compiled web components, service worker, and Storybook-ready component bundles.

💡 **What's happening:** Vite bundles the TypeScript source into ES modules. The Capacitor CLI will copy this `dist/` folder verbatim into the native app's `WebView` asset folder during the sync step.

### Step 2: Sync Capacitor

```bash
npx cap sync
```

This copies `dist/` into `ios/App/App/public/` and `android/app/src/main/assets/public/`, and installs any native plugin dependencies.

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
- **SigningKey** — HMAC-SHA256, signs biometric JWTs
- **EncryptionKey** — AES-256, encrypts refresh tokens at rest

For key generation steps and configuration (local dev via user secrets, production via Key Vault), see **[docs/biometric-setup.md](../biometric-setup.md)** — that document is the authoritative configuration guide.

<!-- manual capture: Biometric enrollment prompt requires physical device or simulator native UI interaction -->

### Configuring Biometric Auth in the Backoffice

1. Log into the Umbraco backoffice (`https://localhost:44345/umbraco`).
2. Navigate to **Settings → Prism Dashboard → [your tenant]**.
3. Find the **Biometric Auth** toggle and enable it.
4. Click **Save**.

When the mobile app next connects, it checks this tenant setting and offers biometric enrollment after successful OIDC login.

<!-- manual capture: Prism Dashboard biometric toggle requires Umbraco backoffice authentication -->

💡 **What's happening:** The `prism-biometric-settings` web component (in `src/UmbracoPrism.Client/src/backoffice/prism-biometric-settings.ts`) reads the tenant's `biometricEnabled` flag from `GET /umbraco/api/prism/tenants/{id}` and saves it via `PATCH /umbraco/api/prism/tenants/{id}`. The mobile app reads the same flag on startup to decide whether to show the enrollment prompt.

---

## Part 4: Deep Link Handling

Deep links let external sources (emails, SMS, other apps) open your Prism mobile app at a specific service blueprint or content page.

### URL Scheme (Universal/Custom)

In `capacitor.config.ts` you can configure a custom URL scheme:

```typescript
const config: CapacitorConfig = {
  appId: 'com.prism.portal',
  // ...
  plugins: {
    App: {
      // Custom URL scheme (e.g., prism-portal://service-blueprint/planning-notification)
      appUrlScheme: 'prism-portal',
    },
  },
};
```

For Universal Links (iOS) and App Links (Android), you need:
- An HTTPS association file at `/.well-known/apple-app-site-association` (iOS) or `/.well-known/assetlinks.json` (Android)
- These files served by your Prism TestSite

### Handling Deep Links in the Shell

```typescript
import { App, type URLOpenListenerEvent } from '@capacitor/app';

App.addListener('appUrlOpen', (event: URLOpenListenerEvent) => {
  const url = new URL(event.url);
  // e.g., prism-portal://service-blueprint/planning-notification → navigate to /apply-for-planning-permission
  const serviceBlueprintSlug = url.pathname.replace('/service-blueprint/', '');
  window.location.href = `/${serviceBlueprintSlug}`;
});
```

💡 **What's happening:** Capacitor's `App` plugin listens for URL events at the native level. When the OS hands a URL to your app (from a tap on a link in Safari or an email), `appUrlOpen` fires with the full URL. Your handler can then navigate the `WebView` to the appropriate page in the Prism TestSite.

---

## Part 5: The Mobile Navigation Component

The `prism-mobile-nav` web component (`src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.ts`) provides a native-feeling bottom tab bar for mobile layouts. It:

- Renders below the service blueprint content area
- Uses CSS variables for theming (respects the tenant's `--prism-nav-bg`, `--prism-primary`, etc.)
- Integrates with `@capacitor/app` to detect the active route and highlight the correct tab
- Shows push notification badge counts on the notifications tab

```html
<prism-mobile-nav
  active-route="/dashboard"
  .tabs="${[
    { label: 'Home',          icon: 'home',  href: '/' },
    { label: 'My Service-Blueprints',  icon: 'list',  href: '/dashboard' },
    { label: 'Notifications', icon: 'bell',  href: '/notifications', badge: 3 },
    { label: 'Profile',       icon: 'user',  href: '/profile' },
  ]}"
></prism-mobile-nav>
```

<!-- manual capture: Prism Mobile Nav requires physical device, simulator, or Storybook component story -->

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
4. If using push notifications, add the **Push Notifications** capability (see [PUSH_SETUP.md](../PUSH_SETUP.md#ios-setup-apns)).

### Step 4: Build and Run (iOS)

1. Select a physical device or simulator in the Xcode target dropdown.
2. Press **⌘R** to build and run.
3. The app launches with the Prism TestSite loaded in a full-screen WebView.

<!-- manual capture: iOS app screenshot requires physical iOS device or Xcode simulator -->

💡 **What's happening:** The native Xcode project (`ios/App/`) embeds your compiled web assets in a `WKWebView`. Capacitor bridges JavaScript calls from the web layer to native Swift/Obj-C plugin implementations — this is how `@aparajita/capacitor-biometric-auth` calls Face ID/Touch ID, and how `@capacitor/push-notifications` registers with APNs.

For App Store distribution builds, change the `aps-environment` entitlement from `development` to `production` (see [PUSH_SETUP.md](../PUSH_SETUP.md#ios-setup-apns)).

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

See [PUSH_SETUP.md](../PUSH_SETUP.md#android-setup-fcm) for the full Firebase project setup steps.

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

Point `server.url` in `capacitor.config.ts` at your running Prism stack, then in Xcode/Android Studio simply re-run the app. The WebView loads fresh from the live server on every launch — no Capacitor sync needed.

```typescript
server: {
  url: 'https://localhost:44345',
  cleartext: false,
},
```

> ⚠️ Remove `server.url` before App Store or Play Store submission — production builds should use the bundled assets in `webDir`.

### Testing Push Notifications

- **iOS:** Push notifications require a physical device — they do not work on the iOS Simulator.
- **Android:** Emulators with Google Play Services installed support push notifications.
- For troubleshooting token registration failures and delivery issues, see [Push Notifications walkthrough](push-notifications.md#troubleshooting).

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
| Push notifications walkthrough | [push-notifications.md](push-notifications.md) |
| Capacitor documentation | [capacitorjs.com/docs](https://capacitorjs.com/docs) |
| Storybook: Mobile Nav story | `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.stories.ts` |

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`building-a-mobile-app.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/building-a-mobile-app.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
