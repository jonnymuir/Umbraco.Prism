---
name: "mobile-biometric-security-review"
description: "How to audit Prism-style Capacitor biometric login flows for release readiness"
domain: "security"
confidence: "high"
source: "manual"
---

## Context

Use this skill when reviewing mobile biometric login in a Capacitor/WebView architecture, especially when a server-issued device credential is exchanged for a cookie-backed web session.

## Patterns

- Trace the credential lifecycle end-to-end: registration, storage, retrieval, exchange, revocation, logout, reinstall, and enrollment-change handling.
- Verify whether the biometric credential ever becomes reachable from WebView JavaScript. Native-only handling is materially safer than `window.Capacitor`/`nativePromise` access from page scripts.
- Distinguish **encrypted-at-rest** storage from **biometric-bound** storage. `whenUnlocked` or equivalent is not the same as Secure Enclave / Keystore user-auth-bound access.
- Treat client-generated `deviceId` values as labels, not proof. For serious assurance, require non-exportable key material plus signed challenge/nonce verification.
- Check whether transport guarantees are enforced or merely configurable: HTTPS-only startup URL, secure cookies, ATS/network security posture, CORS scope, and any cleartext allowances.
- Review assurance policy separately from raw capability detection: `deviceIsSecure`, `strongBiometryIsAvailable`, passcode/PIN fallback, and compromised-device controls all change the effective auth strength.

## Examples

- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — generated startup shell currently performs credential retrieval/exchange from WebView JS.
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs` — injected enrollment/autologin scripts show whether tenant pages can reach native storage APIs.
- `src/UmbracoPrism.Client/src/backoffice/biometric-bridge.ts` — app-side bridge shows storage keying, capability detection, and revoke semantics.
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs` — server-side exchange validates token, tenant, expiry, and revocation; review whether “device binding” is truly enforced.

## Anti-Patterns

- Calling a bearer JWT “device-bound” when the device proves nothing beyond possession.
- Storing a long-lived mobile credential with generic secure storage access and assuming the separate biometric prompt makes extraction impossible.
- Letting WebView JS perform token retrieval or exchange in high-assurance scenarios.
- Allowing release bundles to run against `http://` origins or request cookies with request-dependent security settings.
