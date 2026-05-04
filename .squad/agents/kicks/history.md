# Kicks — History

## Project Context

- **Project:** Umbraco.Prism — multi-tenant Umbraco (v17+) with dynamic branding and stateless identity
- **Stack:** .NET 10.0.x, Capacitor 7.x, TypeScript, Web Components, Lit, Node.js 22.17.1
- **User:** Jonny Muir
- **Joined:** 2026-03-28


## 📋 Recent History

Previous history archived to reduce file size. Recent entries below.

---

     - `unsubscribeFromGenre(apiBaseUrl, authToken, genre)` — DELETEs via `/umbraco/prism/push/unsubscribe`
     - `addForegroundListener(callback)` — listens for notifications received while app is open
     - `addNotificationActionListener(callback)` — listens for notification tap events
     - `removeAllListeners()` — cleanup method
   - **Web Degradation:** All methods check `Capacitor.isNativePlatform()` and resolve silently on web/simulator
   - **Internal Listeners:** Automatically hooks `registrationError` event to log failures

3. **Bundle Request Payload Update:**
   - Added `_pushNotificationsEnabled` state to `prism-create-tenant-modal.ts`
   - Added `pushNotificationsEnabled` field to the bundle payload sent to `POST /umbraco/management/api/v1/prism/tenants/{id}/produce-mobile`
   - Defaults to `false` (opt-in approach)

4. **UI Integration:**
   - Added "Push Notifications" toggle to the Mobile tab in the tenant modal
   - Toggle appears after "Show technical diagnostics" checkbox
   - Includes explanatory hint: "Enable push notifications support in the mobile bundle. Users will be prompted to allow notifications after their first biometric login."
   - Toggle value controls `pushNotificationsEnabled` field in bundle request

5. **Documentation:**
   - Created `docs/PUSH_SETUP.md` — comprehensive iOS and Android native setup guide
   - Covers:
     - iOS: Push Notifications capability, `aps-environment` entitlements, APNs p8/p12 configuration
     - Android: Firebase project setup, `google-services.json` placement, Gradle verification
     - Backend requirements for both platforms
     - Troubleshooting common issues
     - Testing on device/emulator

**Technical Decisions:**

- **Permission Timing:** Push permission request is NOT automatically triggered. The bundle generator UI hint suggests requesting "after first biometric login", but the actual hook must be implemented by the bundle consumer or in future Prism versions. The `PrismPushNotifications.registerDevice()` method handles permission → registration flow atomically.

- **API Endpoint Alignment:** Used the backend API endpoints defined in `docs/design/notifications-backend.md`:
  - `POST /umbraco/prism/push/register` with body `{ "token": "..." }` and Bearer auth
  - `DELETE /umbraco/prism/push/register` for unregistration
  - `POST /umbraco/prism/push/subscribe` with body `{ "genre": "..." }` for genre subscriptions
  - `DELETE /umbraco/prism/push/unsubscribe` for genre unsubscriptions

- **Type Import Fix:** `PluginListenerHandle` is exported from `@capacitor/core`, not `@capacitor/push-notifications` (discovered via TypeScript compilation errors)

- **Error Handling:** Token registration uses a Promise-based pattern with a 10-second timeout. Errors are logged and bubbled to callers. Network failures during registration do not crash the app.

**Build Verification:**
- Ran `npm install` — successfully added `@capacitor/push-notifications@7.0.0`
- Ran `npm run build` — TypeScript compilation succeeded with no errors

**Files Created/Modified:**

*Created:*
- `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts` (352 lines)
- `docs/PUSH_SETUP.md` (comprehensive setup guide)

*Modified:*
- `src/UmbracoPrism.Client/package.json` — added dependency
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — added `_pushNotificationsEnabled` state, UI toggle, payload field
- `src/UmbracoPrism.Client/src/backoffice/index.ts` — exported `PrismPushNotifications` and `PushPermissionState` type

**Not Yet Implemented:**

- Automatic permission request hook after biometric login (left for bundle generator to implement or future Prism enhancement)
- Backend endpoints (`/umbraco/prism/push/register`, etc.) — scope is backend Phase (Blathers)
- Android notification channel setup code (documented in design spec, not yet generated in bundle)
- iOS/Android native project configuration automation (documented in `PUSH_SETUP.md` as manual steps)

**Next Steps for Team:**

1. Backend team (Blathers) must implement the `/umbraco/prism/push/*` endpoints per `docs/design/notifications-backend.md`
2. `MobileBundleService.cs` should conditionally include push notification scaffolding when `pushNotificationsEnabled: true` in the bundle request
3. Consider auto-injecting `PrismPushNotifications.registerDevice()` call into the bundle's post-biometric-login flow (or document as consumer responsibility)
4. Test end-to-end flow: enable toggle → generate bundle → configure native projects per `PUSH_SETUP.md` → verify token registration → send test notification

**Status:** ✅ Phase 3 TypeScript integration complete. Awaiting backend endpoint implementation and bundle generator C# updates.



---

## 2026-04-03: Phase 3 Capacitor Push Notifications Completed

**Status:** ✅ Completed & Merged (awaiting backend Phase 4)

**Deliverables:**
- TypeScript API: `PrismPushNotifications` (8 public methods)
- Bundle integration: `pushNotificationsEnabled` toggle in tenant modal
- Package: `@capacitor/push-notifications@^7.0.0` added
- Documentation: `docs/PUSH_SETUP.md` (iOS/Android setup guide)
- Exports: PrismPushNotifications class, PushPermissionState enum

**Key Decisions:**
1. Plugin choice: `@capacitor/push-notifications` (not Firebase)
2. Opt-in design: `pushNotificationsEnabled: false` by default
3. Deferred permission timing: Left to consumers (recommended post-biometric-login)
4. Manual native setup: Documented in PUSH_SETUP.md (cannot automate APNs/Firebase)
5. API alignment: Endpoints per `docs/design/notifications-backend.md`

**Build Status:** ✅ TypeScript 0 errors, `npm run build` passes

**Documentation:**
- `docs/PUSH_SETUP.md` — complete iOS/Android native setup guide
- Decision notes in `.squad/decisions.md`
- Orchestration log: `.squad/orchestration-log/2026-04-03T12:23:47Z-kicks.md`
- Session log: `.squad/log/2026-04-03T12:23:47Z-phase2-phase3-notifications.md`

**Blocker:** ⚠️ Backend endpoints not yet implemented (Blathers Phase 4 prerequisite)
- `/umbraco/prism/push/register` (POST, DELETE)
- `/umbraco/prism/push/subscribe` (POST, DELETE)

**End-to-End Functional:** Not yet (awaiting backend). TypeScript implementation is production-ready.

**Team Dependencies:**
- Blathers (Backend): Implement 4 push endpoints
- Tom Nook (Services): Conditionally scaffold push code in `MobileBundleService.cs`

**Future Enhancements:**
1. Auto-inject permission request into biometric login flow
2. Generate Android notification channel setup code in bundle
3. Interactive CLI setup wizard (`npx prism-setup-push`)
4. Test Push button in tenant modal
5. Optional Firebase Messaging toggle


### 2026-06-21: Android Bootstrap Script Bug Fixes

**Task:** Fixed two bugs in `BuildBootstrapAndroidScript` in `MobileBundleService.cs` that caused `bootstrap-android.sh` to fail on macOS/Java 25 environments.

**Bug 1 — BSD sed INSERT syntax (macOS crash):**
- The generated script used `sed -i.bak '/<application/i\...'` which is GNU sed syntax. BSD sed (macOS) requires a newline after `\i`, not inline text.
- **Fix:** Replaced with `perl -i -pe 's|(<application)|    <uses-permission.../>\n$1|'` which works identically on macOS and Linux. Removed the now-unnecessary `.bak` cleanup line.

**Bug 2 — Gradle 8.11.1 / Java 25 incompatibility:**
- `@capacitor/android@7.0.0` ships Gradle 8.11.1, which only supports up to Java 23. Class file major version 69 (Java 25) causes a fatal Groovy compilation error during `npx cap sync android`.
- **Fix:** Added a Gradle wrapper upgrade step after `npx cap add android` and before `npx cap sync android`. Upgrades `gradle-wrapper.properties` to Gradle 8.14 (supports Java 25). Uses `sed -i.bak 's/.../.../'` substitution (safe on both platforms — only INSERT was problematic).

**Note on doctor-mobile.sh:** Checked `BuildDoctorScript` — no sed usage, no BSD-specific issues.

**Files changed:**
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — `BuildBootstrapAndroidScript` method only


---

## 2026-05-01: Prism Reflection — Mobile Architecture Review

**Task:** Deep Rams-grade review of the mobile story, commissioned by Jonny Muir.

**Output:** `.squad/reviews/2026-05-01-prism-reflection/05-kicks-mobile.md`

**Verdict:** Mobile is genuinely part of Prism. The thin-Capacitor-shell-over-web-workflow model is sound and the biometric stack is production-grade end-to-end. One critical honesty failure identified.

**Key findings:**

1. **The push bundle gap (❌ Rams #6):** `prism-create-tenant-modal.ts` sends `pushNotificationsEnabled` to `POST .../produce-mobile`. `PrismMobileBundleRequest.cs` has no such field. The backend silently discards it. `MobileBundleService.BuildBundleAsync` never scaffolds push code regardless of the operator's toggle choice. The UI lies.

2. **Biometric UI tokens (⚠️ Rams #10):** `prism-biometric-register.ts` and `prism-biometric-settings.ts` use hardcoded hex colors and `--uui-color-*` tokens (Umbraco's backoffice design system) instead of `--prism-primary`. This breaks tenant branding at the enrollment screen — the highest-trust moment in the mobile UX.

3. **Design system inheritance is opportunistic, not formal:** Mobile inherits branding via `PrismBrandingMiddleware` CSS injection — this works and is coherent. But there is no token contract file. The `⚠️ MOBILE BOUNDARY` comment in `prism-mobile-nav.ts` is not replicated in the biometric components.

4. **What works today:** Biometric auth is fully wired (client + server). Bundle generation produces a deployable ZIP. Bootstrap scripts are platform-correct (Gradle 8.14 after June fix). `prism-mobile-nav` correctly consumes `--prism-primary`. Mobile workflow rendering reuses `PrismComponentRenderPayload` without modification — the WebView is the renderer.

5. **Push backend is implemented:** `PrismNotificationController.cs` has all four push endpoints. The gap is purely in the bundle generator — not the backend.

**Three prioritised improvements identified:**
1. Wire `PushNotificationsEnabled` through `PrismMobileBundleRequest.cs` → `MobileBundleService.cs`
2. Replace hardcoded hex in `prism-biometric-register.ts` + `prism-biometric-settings.ts` with `--prism-*` tokens
3. Add post-download checklist panel in `prism-create-tenant-modal.ts`

**Decisions inbox:** `.squad/decisions/inbox/kicks-mobile-reflection.md`

---

**2026-05-01 — Prism Reflection Review (Rams 10 Principles)**

Delivered mobile coherence with design system review applying Rams principles. Three mobile findings recorded:
1. Push bundle gap is a bug not gap (UI sends field, backend drops it silently)
2. Mobile-facing components must use --prism-* tokens only, not --uui-* or hex
3. Mobile architecture verdict confirmed: thin-shell model, no separate renderer

No code changes — review-only. Decisions merged to decisions.md by Scribe. Orchestration log written to 2026-05-01T07:57:29Z-kicks.md.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.
