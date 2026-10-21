# Prism Mobile Shell Spec (v2)

## 1) Product goal

A generated Prism app should feel like a native app and pass App Store review on its own merits,
while editors keep maintaining it from the Umbraco backoffice. Navigation, branding, content and
notifications are authored in Umbraco. Store resubmission is only needed when the native shell
itself changes.

Two problems with a remote-URL webview drive this spec:

1. **Store risk.** A webview whose content is the tenant's website is the textbook rejection
   under App Review Guideline 4.2 (Minimum Functionality).
2. **Latency.** Every tap is a full server round trip for HTML, CSS, JS, fonts and the tab bar. On
   a slow or flaky connection even switching tabs is slow, and offline the app shows nothing.

## 2) Rules

### R1. The shell ships in the app
The app's own UI (tab bar, navigation stack, offline screen, design-system assets) is bundled
in the binary. The server supplies configuration and content, never the app's chrome.

### R2. Backoffice is the source of truth
Anything an editor can reasonably change (tabs, icons, labels, branding tokens, which screens
open as modals, content) is served from Umbraco as data and cached by the app. Changing it never
needs a new build.

### R3. Every screen has a cached state
Tab roots and recently viewed screens render from cache first and refresh in the background. No
screen shows a blank page or a raw connection error while a copy exists on the device. Server-rendered
pages are covered first, in [mobile-offline-pages.md](mobile-offline-pages.md).

### R4. Sign-in is standards-aligned
Interactive sign-in uses the system authentication session (`ASWebAuthenticationSession` on iOS,
Custom Tabs on Android) with PKCE. The identity provider's tokens stay on the server; the device
holds only Prism-issued credentials, in the Keychain / Keystore, and using them is gated by
biometrics when enabled. Hosted IdP pages never render inside the app's webviews. Detailed in
[mobile-sign-in.md](mobile-sign-in.md).

### R5. Server-authoritative journeys stay server-rendered until Wayfinder offers otherwise
Wayfinder journeys are rendered by the server inside a native navigation stack. Rendering
journey stages natively from blueprint data is Wayfinder work and is out of scope here.

### R6. The tenant submits the app
App Review Guideline 4.2.6 rejects apps produced by a template or generation service unless the
content owner submits them. Each tenant publishes under its own developer account, and the
generated README says so.

## 3) Architecture

```
┌──────────────────────── Native shell (bundled) ────────────────────────┐
│  Tab bar / navigation stack per tab (UIKit, Material)                   │
│   ├─ Content screens: bundled Lit renderer + cached Delivery API JSON   │
│   ├─ Web screens: server-rendered pages (Wayfinder journeys, forms)     │
│   └─ Native screens: settings, offline, biometrics, notifications       │
│  Manifest cache · Content cache · Token store (Keychain/Keystore)       │
└──────────────────────────────────┬──────────────────────────────────────┘
                                   │ HTTPS (ETag, bearer token)
┌──────────────────────────────────┴──────────────────────────────────────┐
│  Umbraco + Prism                                                        │
│   /api/prism/mobile/manifest   tabs, branding, path rules, versions     │
│   Delivery API (+ Prism access policy enforcement)   content as JSON    │
│   Server-rendered pages        journeys and anything not yet native     │
│   Push (Automate action)       deep links into tabs and screens         │
└─────────────────────────────────────────────────────────────────────────┘
```

### D1. Mobile manifest
`GET /api/prism/mobile/manifest` returns, per tenant:

- `tabs`: label, icon (built-in key or media URL), root path. Authored on the mobile nav node.
- `branding`: the tenant's resolved design tokens, so native chrome matches the site.
- `pathRules`: ordered URL patterns with a presentation (`push`, `modal`, `replace`, `external`)
  and a renderer (`content`, `web`, `native:<screen>`). Hotwire Native's path configuration is
  the model.
- `contentVersion`: bumped on publish, so the app knows when its content cache is stale.
- `minShellVersion`: lets the server tell an old build to update.

Served with a strong ETag. The app ships the manifest from generation time as its first-launch
fallback, so it starts offline.

### D2. Native navigation
One navigation stack per tab. Each stack keeps its own history and its own webview for web
screens, so switching tabs never reloads anything. Back swipe, large titles and the "More"
overflow come from the platform. Implemented as a Capacitor plugin so the existing biometric and
push plugins keep working.

### D3. Cacheable assets
Web screens reference every CSS and JS file by a content-hashed URL (`asp-append-version`), served
`immutable` for a year. After the first visit a web screen only fetches its HTML. Any other CSS or
JS revalidates on every use, so nothing is served stale after a deploy. The native shell's own
screens bundle their assets in the app.

### D4. Content rendering
Content pages are fetched from Umbraco's Content Delivery API and rendered by a bundled Lit
renderer that shares components with the site's design system. Responses are cached on the
device (stale-while-revalidate). When `contentVersion` moves, the app refreshes tab roots and
the pages they link to in the background. Prism's page access policies are enforced on these
JSON endpoints using the member's bearer token.

### D5. Native capabilities
Each of these is configured per tenant in the backoffice:

- Push notifications with deep links (exists).
- Biometric sign-in (exists, moves onto R4's token store).
- Live Activities and widgets for journey status.
- Spotlight indexing of cached content.
- Native document scanning for file uploads.
- Native share sheet, haptics, app badge counts.
- A native settings screen (notifications, biometrics, sign out, delete account).

## 4) Phases

Each phase ends with the reference app built for the iOS simulator and the Android emulator,
screenshots compared against the previous phase, and the flows in section 5 passing. Every phase
ships on iOS and Android together.

| Phase | Scope | Acceptance |
|---|---|---|
| 1. Store hygiene and quick wins | Privacy manifest (`PrivacyInfo.xcprivacy`), R6 in the generated README, a branded bundled offline screen with retry, D3 on the reference site | App launches to the same first screen as before; a page that fails to load shows the branded offline screen; privacy manifest present in the built `.app`; a repeat page view fetches no CSS or JS |
| 2. Manifest and native chrome | D1, D2, R4. Remove the web tab bar from app requests. Keep the iOS navigation hold, which covers the gap WKWebView leaves between pages within a tab | Native tab bar matches the backoffice nav; switching tabs is instant and keeps scroll position; sign-in completes in the system auth session; editing a tab in the backoffice changes the app without a rebuild |
| 3. Native content rendering | D4, behind a per-tenant flag | Tab roots render offline from cache; a publish in the backoffice reaches the app on next foreground; protected pages are refused without a valid member token |
| 4. Flagship capabilities | Live Activity for journey status, then others from D5 as needed | A journey status change updates the Live Activity without opening the app |

The phases are breaking changes to generated apps: each tenant regenerates and resubmits.

## 5) Verification

- **Smoke**: build for the simulator, launch, and check the content area is not blank (the
  existing pipeline check).
- **Flows**: scripted taps and screenshots on iOS and Android for launch, each tab, a content
  page, sign-in, a Wayfinder journey stage, offline launch and a push deep link.
- **Comparison**: screenshots from each phase are compared with the previous phase's, so visual
  regressions are deliberate.
- **Latency**: the same flows under a throttled network profile, timing tap-to-content for a tab
  switch and a content page.

## 6) Out of scope

- Native rendering of Wayfinder journey stages (Wayfinder repo).
- Offline submission of journey forms.
- Replacing Capacitor with another shell framework.
