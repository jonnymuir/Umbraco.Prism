# Mobile review — Kicks

_Reviewed: 2026-05-01T08:57:29+01:00_

---

## Verdict

Mobile is genuinely part of Prism, not a sibling pretending. The core insight — a thin Capacitor shell around the existing web-delivered workflow engine — is architecturally sound and executed with surprising depth. `BiometricController.cs`, `biometric-bridge.ts`, and `MobileBundleService.cs` (1,214 lines) are production-grade. But there is one clear seam failure: the push notification toggle that operators see in the backoffice sends `pushNotificationsEnabled` to the bundle endpoint, and the backend silently ignores it — `PrismMobileBundleRequest.cs` has no such field. The feature is UI-complete and backend-complete in isolation; they are not wired together. That gap is the honest test of Rams' sixth principle, and it fails.

---

## Maturity assessment — what's real, what's stub, what's vapour

**Real (production-grade):**

- `src/UmbracoPrism.Core/Controllers/BiometricController.cs` — biometric register, exchange, revoke, and unenrol endpoints all implemented with rate limiting, antiforgery exemption policy comments, and tests in `BiometricControllerTests.cs`.
- `src/UmbracoPrism.Client/src/backoffice/biometric-bridge.ts` — full Capacitor bridge. Multi-tenant key design using tenant hostname as key suffix is correct, defensive, and portable. Enrollment-change detection (`checkEnrollmentChange`) guards against biometric re-enrolment attacks. Real iOS Keychain / Android Keystore via `@aparajita/capacitor-secure-storage`.
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — generates a deployable ZIP with `capacitor.config.ts`, `package.json`, bootstrap shell scripts for both platforms, `www/index.html`, CSS overrides, and native manifest additions for biometric. Tested.
- `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.ts` — clean, accessible bottom tab bar with full CSS custom property exposure, safe-area padding, media-library icon URLs, and `aria-current="page"`.
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs` — injects `html.prism-mobile`, `env(safe-area-inset-*)` primitives, and the `window.open` WebView guard on every mobile request.
- `src/UmbracoPrism.Core/Controllers/PrismNotificationController.cs` — push register, unregister, subscribe, unsubscribe endpoints exist at `umbraco/prism/push`.

**Stub (designed, partially wired):**

- `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts` — `PrismPushNotifications` class is complete TypeScript (8 methods, web degradation, 10-second token timeout). But `PrismMobileBundleRequest.cs` has no `PushNotificationsEnabled` property, so the bundle generator never conditionally scaffolds push code. The UI toggle fires into a void.
- `prism-biometric-register.ts` / `prism-biometric-settings.ts` — real components, but styled with hardcoded hex values (`#2563eb`, `#c82333`, `#f0fdf4`) rather than CSS custom properties. Design system token discipline breaks down at the auth surface — the most visible mobile-only UI.

**Vapour (documented, not built):**

- Offline-first / service worker caching — explicitly "Out of Scope (v1)" in `Design/mobile.md`. No service worker in the bundle.
- `npx prism-setup-push` CLI wizard — noted in Kicks' history as a future enhancement, no implementation.
- Android notification channel setup code in the bundle — documented as not generated.
- Per-tenant push scaffolding in the ZIP output — the backoffice toggle exists; the bundle wiring does not.

---

## The "ship a tenant's mobile app today" journey

**What works end-to-end today:**

1. Operator creates tenant in backoffice → configures `BiometricAuthEnabled: true`.
2. Clicks "Generate Mobile Bundle" → downloads ZIP.
3. Runs `scripts/bootstrap-ios.sh` or `scripts/bootstrap-android.sh` (platform-correct Gradle 8.14 now, after June bootstrap fix).
4. Opens Xcode / Android Studio → builds → app launches WebView against tenant URL.
5. User does first OIDC login → is prompted to enrol biometric → subsequent launches skip OIDC. ✅

**What is blocked or manual today:**

- Push notifications in the bundle: toggle is visible in the backoffice Mobile tab, but `PrismMobileBundleRequest.cs` lacks `PushNotificationsEnabled`, so `MobileBundleService.BuildBundleAsync` never includes push scaffolding regardless of operator choice.
- The permission request hook after biometric login is documented as "consumer responsibility" — not auto-injected.
- iOS: `NSFaceIDUsageDescription` additions are in the bundle (`resources/ios-info-plist-additions.xml`) but must be manually applied to `Info.plist`; no automated injection in the bootstrap script.
- App Store / Play Store submission requires the operator to know to remove `server.url` from `capacitor.config.ts` and switch `aps-environment` to `production` — this is documented, but not enforced or automated.

**Verdict on "ship today":** A branded mobile app with biometric auth can be shipped today with ~2–3 hours of native project setup. Push requires an additional manual Firebase/APNs configuration step even once the bundle gap is closed.

---

## Design system inheritance (does mobile share tokens, brand, types with web?)

**The good:** `PrismBrandingMiddleware` injects tenant CSS overrides as `:root { --color-primary: ...; }` into every server-rendered response, including those loaded in the Capacitor WebView. Mobile inherits tenant branding without any extra configuration. `prism-mobile-nav` correctly resolves active state colour from `var(--prism-primary, #007aff)` — one CSS variable, tenant-controlled.

**The gap:** `prism-biometric-register.ts` and `prism-biometric-settings.ts` do not consume `--prism-primary`. They use `--uui-color-interactive` (Umbraco's backoffice token set) for the register button, and raw hex `#2563eb` / `#c82333` for confirmations and error states. This means a tenant with a red primary colour gets a blue "Enable Biometric Login" button — a branding inconsistency at exactly the moment the user is establishing trust with the app.

**The boundary:** `prism-mobile-nav.ts` has a comment at line 1: `⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.` This rule is correctly enforced for the nav component. It is NOT enforced in `src/backoffice/prism-biometric-register.ts`, which leaks `--uui-*` tokens into member-facing views.

**Token system maturity:** There is no formal design token file (no `tokens.ts`, no `tokens.json`, no Style Dictionary). The token system is the CSS custom property convention, living in site CSS files and discovered by `PrismBrandingMiddleware`'s scanner. Mobile consumes it opportunistically. Coherent, but not documented as a contract.

---

## Rams scorecard

| # | Principle | Score | Evidence |
|---|---|---|---|
| 1 | Innovative | ✅ | Thin-shell + server-rendered workflows is a genuinely smart architecture. Biometric enrollment-change detection (`checkEnrollmentChange`) is careful engineering. |
| 2 | Useful | ⚠️ | Biometric auth works end-to-end. Push notification bundle wiring is broken (UI toggle → silent drop at API boundary). |
| 3 | Aesthetic | ⚠️ | `prism-mobile-nav` is clean. Biometric UI uses raw hex colors that don't honour tenant brand. Two design languages in one product. |
| 4 | Understandable | ⚠️ | Walkthrough docs (`building-a-mobile-app.md`) are excellent. Push feature incompleteness is not surfaced to operators in the backoffice UI. |
| 5 | Unobtrusive | ✅ | Mobile detection is server-side (`?prismMobile=1` → sticky cookie → `html.prism-mobile`). No JavaScript polling. The app just works. |
| 6 | Honest | ❌ | The push toggle lies. It appears interactive and saveable; the backend ignores it. Operators cannot know the feature doesn't produce a push-ready bundle. |
| 7 | Durable | ✅ | Capacitor 7.x, hostname-keyed SecureStorage, enrollment fingerprinting, gradle 8.14 upgrade in bootstrap — all forward-looking choices. |
| 8 | Thorough | ⚠️ | `MobileBundleService.cs` is meticulous (1,214 lines, tested). Push scaffolding omission is a thoroughness failure at the boundary between two complete subsystems. |
| 9 | Environmentally friendly | ✅ | No native code written per feature. Web assets reused verbatim in WebView. Minimal native footprint by design. |
| 10 | As little design as possible | ⚠️ | `prism-mobile-nav` exemplifies this — just enough chrome, everything else via CSS custom properties. Biometric UI over-designs with bespoke hardcoded styles where one `--prism-primary` reference would suffice. |

---

## Three improvements to bring mobile to parity (prioritized)

### 1. Close the push bundle gap — wire `PushNotificationsEnabled` through to bundle output

**Why first:** This is Rams #6 (honesty). The UI tells operators they can enable push; the API silently discards the setting. It's the clearest broken promise in the mobile story.

**Files to change:**
- `src/UmbracoPrism.Core/Controllers/Models/PrismMobileBundleRequest.cs` — add `public bool? PushNotificationsEnabled { get; set; }`
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — read `request.PushNotificationsEnabled ?? false`; conditionally add `@capacitor/push-notifications` to `package.json`, `PushNotifications` plugin config to `capacitor.config.ts`, and the `PrismPushNotifications.registerDevice()` call hook into the generated `www/index.html` biometric-login-complete listener.
- `src/UmbracoPrism.Core.Tests/MobileBundleServiceTests.cs` — add test cases for push-enabled / push-disabled bundle output.

### 2. Replace hardcoded hex colors in biometric UI with CSS custom properties

**Why second:** Trust is built at the biometric enrollment screen. A misbranded button at that moment is a design system failure at the worst possible time. Also enforces the "mobile boundary" principle consistently across the `src/backoffice/` mobile components.

**Files to change:**
- `src/UmbracoPrism.Client/src/backoffice/prism-biometric-register.ts` — replace `background: var(--uui-color-interactive, #3544b1)` with `background: var(--prism-primary, #2563eb)` and align all state colors to `--prism-*` tokens.
- `src/UmbracoPrism.Client/src/backoffice/prism-biometric-settings.ts` — replace `#2563eb`, `#c82333`, `#fee` / `#c00` with `var(--prism-primary)`, `var(--prism-danger, #c82333)`, `var(--prism-danger-surface, #fef2f2)`.
- Add a comment at the top of each file mirroring the mobile boundary guard in `prism-mobile-nav.ts`: `// TOKEN CONTRACT: Use --prism-* custom properties only. No --uui-* imports.`

### 3. Add push-readiness signal to the backoffice bundle UI

**Why third:** Even before improvements 1 and 2 ship, operators deserve feedback. Once the bundle gap is closed, a post-download checklist panel should surface the manual steps that remain (APNs cert, `google-services.json`, `server.url` removal for production). This is about honesty at the operator surface, not just at the code boundary.

**Files to change:**
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — after successful bundle download, render a contextual checklist: push-specific steps shown only when `pushNotificationsEnabled: true`, biometric steps shown only when `biometricAuthEnabled: true`. Link to `docs/PUSH_SETUP.md` and `docs/biometric-setup.md`.
- `docs/walkthroughs/building-a-mobile-app.md` — add a callout in the push section noting that push requires backend endpoints AND the bundle to have been generated with the toggle enabled.
