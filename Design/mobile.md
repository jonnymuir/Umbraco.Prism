# Prism Mobile Shell Spec (v1)

## 1) Product Goal
Deliver a mobile app experience that feels native while retaining a web delivery model.

This means Prism Mobile must prioritize:

- no unexpected context switching out of the app
- mobile-safe layout behavior (safe areas, full-width content)
- deterministic startup and diagnostics
- clear constraints for Entra authentication

## 2) Non-Negotiable Rules

### R1. Stay in WebView
The app must not intentionally launch Safari/Chrome for normal in-app navigation.

### R2. Mobile-Safe Rendering
When Prism mobile mode is active, pages must:

- honor notch/home-indicator safe areas
- use viewport-fit=cover compatible rendering
- avoid desktop max-width containers unless explicitly overridden

### R3. Deterministic Mobile Signal
Mobile mode must remain sticky across in-app navigation (query -> cookie continuity).

### R4. Auth Decision Must Be Explicit
Entra interactive sign-in policy must be treated as a product decision, not an accidental behavior.

## 3) Architecture Decisions

### D1. Mobile Shell Mode
Produced bundles default to direct top-level WebView startup via `server.url` with `?prismMobile=1`.

### D2. Runtime Mobile Guardrails
On Prism mobile requests, server-rendered HTML should inject:

- a mobile shell base class (`.prism-mobile`)
- safe-area helper CSS primitives
- in-WebView navigation guard behavior (`target="_blank"` and `window.open` should not open external browser by default)

### D3. Layout Contract
Tenant/site CSS should treat `.prism-mobile` as the contract for app-style layout.

### D4. Entra Authentication Contract
There are two supported auth modes:

1. **Strict in-WebView mode (no external browser):**
  - Suitable only when auth can complete without external Entra hosted UI breakouts.
  - May conflict with tenant Conditional Access/security posture.

2. **Compliance mode (recommended for Entra):**
  - Uses system browser / ASWebAuthenticationSession style flow.
  - May visually leave WebView, but is standards-aligned and more reliable for Entra policies.

Prism must document this tradeoff clearly; teams choose mode intentionally.

## 4) Acceptance Criteria

### A. Navigation
- Tapping links with `target="_blank"` keeps navigation inside the same WebView in mobile mode.
- `window.open(...)` in mobile mode does not spawn external browser context.

### B. Safe Area & Width
- On modern iPhones (notch + home indicator), content avoids clipping under system bars.
- Primary page container uses full available width in mobile mode unless tenant explicitly constrains it.

### C. Startup
- Generated `capacitor.config.ts` includes direct `server.url` with `prismMobile=1`.
- Mobile detection source can be observed and remains stable after first navigation.

### D. Auth
- README and design docs explicitly state Entra auth mode tradeoffs.
- No silent fallback to external browser behavior without documentation.

## 5) Out of Scope (v1)

- Full native tab bar/navigation framework.
- Offline-first caching/service worker strategy.
- Native plugin parity for all device capabilities.

## 6) Implementation Notes

- Keep all generated starter assets minimal and editable.
- Prefer middleware/runtime enforcement over per-page manual changes.
- Keep this document as the source of truth for mobile UX behavior changes.