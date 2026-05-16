# Decision: Safe deeper downstream timeout diagnostics

## Context

Jonny needed better browser-visible detail for downstream demo timeouts, especially when TestSite calls MockBusinessApp through an internal backchannel in Codespaces or local Aspire wiring.

## Decision

Keep masking internal backchannel ports in `transport.transportBaseUrl` as `http://localhost:****`, but add safe timeout details that do not expose raw internal ports:

- `transport.usingBackchannel`
- `transport.targetPath`
- `timeout.timedOutByUs`
- `timeout.cancellationSource`
- short `summary` / `nextCheck` hints

Also enrich server logs with the masked transport base URL and target path so operators can correlate browser output with backend logs.

## Rationale

The browser already needs to know whether TestSite used the backchannel, which path it targeted, and whether the 10-second timeout came from our own request window. Those details help diagnose stale AppHost wiring and public-tunnel fallbacks, while the raw localhost port still stays hidden from browser-visible JSON.

---
date: 2026-05-03T23:26:29.163+01:00
author: Tangy
status: decision
area: testing, diagnostics, downstream-demo
---

# Decision: Timeout Diagnostics Must Distinguish Deadline vs Cancellation Without Leaking Backchannel Ports

## Context

`DownstreamDemoController` now exposes richer timeout diagnostics for `/api/prism/downstream-demo` so operators can tell whether a failed request used the public tunnel or the internal backchannel. The remaining behavioural risk was ambiguity between a real controller timeout and an externally cancelled request, especially in unit tests that throw `TaskCanceledException` directly.

## Decision

Browser-visible timeout responses should preserve these contracts:

1. **Deadline vs cancellation must be explicit.**
   - Timeout responses expose `statusText`, `timeout.timedOutByUs`, and `timeout.cancellationSource`.
   - Behavioural tests cover both the controller-owned timeout window and a separate external-cancellation path.

2. **Internal-backchannel diagnostics must stay masked.**
   - Responses may identify `internal-backchannel`, the target path, and suggested next checks.
   - `transport.transportBaseUrl` must remain masked (`http://localhost:****`) and raw internal ports must not appear anywhere in browser-visible JSON.

3. **Operator guidance should point to configuration and health checks, not implementation leaks.**
   - `summary` and `nextCheck` should reference the downstream path and wiring checks like `BUSINESSAPP_BACKCHANNEL_URL`.
   - Guidance should avoid exposing raw localhost ports while still telling operators what to verify next.

## Test Coverage

- `DownstreamDemo_IncludesTransportDiagnostics_OnTimeout`
- `DownstreamDemo_IncludesMaskedInternalBackchannelTimeoutDiagnostics`
- `DownstreamDemo_LabelsExternalCancellation_SeparatelyFromTimeoutWindow`
- Existing masking contract in `DownstreamDemo_DoesNotExposeRawBackchannelPortInDiagnostics`

---
date: 2026-05-03T23:38:00.000+01:00
author: Mabel
status: IMPLEMENTED
area: diagnostics, backend, testing
---

# Decision: Tests That Read Env Vars Must Join EnvVarSensitiveTestCollection

## Context

`EnvVarSensitiveTestCollection` was designed to serialise test classes that *mutate* `KEYCLOAK_BACKCHANNEL_URL` and `ASPNETCORE_ENVIRONMENT`. `PrismContextTests` was not in the collection because it does not mutate those variables.

However, `PrismContext.RefreshTokenAsync` **reads** both variables at runtime to conditionally rewrite the token endpoint. When `BackchannelRewriteTests` (in the collection) set those vars while `PrismContextTests` ran in parallel, the token endpoint was rewritten to an `http://localhost` URL. The Moq mock matched the `https` URL only, so Moq returned null, causing `NullReferenceException` at `result.Success`.

The failure was latent but only surfaced in CI at commit beef21c because adding `BusinessAppWorkflowClientTests` to the collection changed execution timing and widened the race window.

## Decision

**Any test class that exercises code paths which _read_ `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must be in `EnvVarSensitiveTestCollection`, even if it does not mutate those variables itself.**

Pattern to use (as in `LocalhostGenericOidcRegressionTests`):
1. Add `[Collection(EnvVarSensitiveTestCollection.Name)]` to the class.
2. Implement `IDisposable` saving both env vars in the constructor and restoring them in `Dispose`.

## Rationale

xUnit parallelism operates at the test-class level. Without collection membership, any class that reads global state (environment variables) is subject to races with any other class that writes that state.

## Files Affected

- `src/UmbracoPrism.Core.Tests/PrismContextTests.cs` — fixed in commit 860c5d3

---
date: 2026-05-04T09:22:01.025+01:00
author: Tangy
status: ACCEPTED
area: testing, ci, moq
---

# Never Use Concrete CancellationToken Values as Moq Matchers for ASP.NET Core Contexts

## Context

CI run 25294216756 (commit `beef21c`) failed with 4 `PrismContextTests` throwing `NullReferenceException` at `PrismContext.cs:212`. The production code was unchanged and correct. The fault was entirely in the test setup.

Mock setups for `IPrismTokenRefreshService.RefreshAsync` used `httpContext.RequestAborted` as a concrete value matcher. On Linux (GitHub Actions, Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If that feature is activated by the authentication stack between setup-time and call-time, Moq's captured token value no longer matches the token in the actual call. Moq's loose mock returns `null` for the unmatched setup, causing `result.Success` to throw. On macOS (arm64) the lazy path is stable and the bug is masked.

## Decision

**When writing Moq setups for methods that accept a `CancellationToken`, always use `It.IsAny<CancellationToken>()` rather than a concrete `HttpContext.RequestAborted` or `httpContext.RequestAborted` value.**

Rationale:
- `DefaultHttpContext.RequestAborted` is lazily initialised through `IHttpRequestLifetimeFeature` and its behaviour can differ between platforms.
- The intent of tests like these is to verify routing logic and return values, not to assert the exact CancellationToken instance.
- Concrete value matching for CancellationToken is always fragile unless you own the token source and can guarantee stability.

## Implementation

Replace:
```csharp
.Setup(t => t.RefreshAsync(..., httpContext.RequestAborted, ...))
.Verify(t => t.RefreshAsync(..., httpContext.RequestAborted, ...), Times.Once)
```

With:
```csharp
.Setup(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...))
.Verify(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...), Times.Once)
```

Applied in commit `1601415` to four `PrismContextTests` methods.

## Blathers Review Note

The fix is entirely in test harness code. `PrismContext.cs` and `IPrismTokenRefreshService` are correct and do not require changes. Blathers does not need to act on this. The CI should pass once this commit is pushed.

# Decision: Screenshot-mode cookie contract

## Context

The `prism-mobile-user-agent-demo` toggle widget renders on every TestSite page
(bottom-right fixed widget).  It clutters automated walkthrough screenshots
without adding documentary value.

## Decision

A single well-known cookie suppresses the widget for a whole browser session.

**Cookie name:** `prism-screenshot-mode`  
**Value:** `"1"` to suppress; absent/`"0"` to leave the widget visible.  
**Scope:** `Path=/; SameSite=Lax; Secure=false` (localhost only).

### Server-side (C#)

`PrismMobileUserAgentDemoTagHelper` reads the cookie via `IHttpContextAccessor`.
If the cookie equals `"1"`, `ShowToggle` is forced to `false` — only the UA
bootstrap `<script>` is emitted, not the widget HTML.  The constant
`PrismScreenshotMode.CookieName` in `UmbracoPrism.Core.TagHelpers` is the
authoritative source for the cookie name.

### Client-side (Playwright)

`enterScreenshotMode(page)` in
`src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts` adds the
cookie to the browser context before any navigation.  `signIn()` calls it
automatically when `CAPTURE_SCREENSHOTS=1` so every walkthrough spec picks it up
without per-spec wiring.

## Tangy hook

Tangy (or any test author) who needs screenshot-clean pages outside the
`signIn()` flow can call `enterScreenshotMode(page)` directly.  No other hook
is required.  The cookie must be set before the first page load that should
suppress the widget.

## What is NOT changed

- Manual browser usage: cookie not set → widget renders as before.
- The UA bootstrap script: always emitted regardless of screenshot mode, so
  tests that drive mobile-UA behaviour (`prismMobile` cookie/localStorage) are
  unaffected.
- `show-toggle="false"` on the tag helper still works and takes precedence in
  any template that needs to permanently hide the widget.
---
decision_id: walkthrough-ui-audit-2026-05-04
author: Isabelle
created_at: 2026-05-04T11:46:55.877+01:00
subject: Audit findings — walkthrough/demo discoverability and screenshot-friendliness
status: draft-for-review
---

# Walkthrough UI Navigation Audit — Decision

## Problem Statement

The walkthrough system includes 4 demo workflows + admin UI, but **manual discoverability is fragmented**:
- 3 workflows (Payment Demo, Planning Notification, Information Request) are unreachable without direct URL knowledge
- Workflow admin UI (`/admin/workflow`) is not linked from any UI surface
- Mobile helper widget (`prism-mobile-user-agent-demo`) appears in all screenshots, blocking viewport and cluttering walkthrough images
- Homepage focuses on design tokens, not demo workflows — misses opportunity to showcase core features

## Current State

### Routes (All Content-Based in Umbraco)
| Route | Discoverable Via |
|-------|------------------|
| `/get-in-touch` | Header nav + Dashboard card |
| `/payment-demo` | Dashboard card only ⚠️ |
| `/apply-for-planning-permission` | URL-only ❌ |
| `/request-information` | URL-only ❌ |
| `/my-workflows` | Header nav + Dashboard card |
| `/admin/workflow` | AppHost reference only ❌ |

### Navigation Surfaces
- **Header:** 3 items (Home, Get in Touch, My Workflows)
- **Dashboard:** 3 workflow cards + downstream API demo
- **Homepage:** Design system token showcase (580 lines); unauthenticated hero with Sign In/Register

### Mobile Helper Widget
- Renders on every page via `prism-mobile-user-agent-demo` tag helper
- Fixed position bottom-right corner
- Shows checkbox + status text + close button
- Persists state in localStorage/sessionStorage
- **Screenshot impact:** Visible in all walkthrough images; blocks content on mobile-width views

## Recommended Changes (Minimal & Coherent)

### 1. Add Demo Workflows Section to Home Page ✅
**What:** Insert "Demo Workflows" section below hero/features, before design tokens  
**Where:** `homePage.cshtml` after `.features` section  
**Content:** 4 card grid showing:
- Community Enquiry (currently linked)
- Payment Demo (currently dashboard-only)
- Planning Notification (currently URL-only)
- Information Request (currently URL-only)

**Why:** Home becomes a natural entry point for trying workflows; design tokens section remains for operators; no removal of existing content.

**Impact:** ~120 lines of HTML; adds ~300px height to authenticated home (acceptable; user goal-driven)

### 2. Add Workflow Admin Link to Dashboard ✅
**What:** Add "Workflow Admin" card/link to dashboard  
**Where:** `memberDashboard.cshtml` in the dash-grid  
**Guard:** Role-based visibility (admin-only; check against `Context.User.IsInRole("admin")` or similar)  
**Link:** Points to `/admin/workflow`

**Why:** Makes admin UI discoverable without URL knowledge; leverages dashboard's existing card pattern.

**Impact:** 1 new card; fits naturally in existing layout.

### 3. Hide Mobile Helper Widget UI (Keep UA Mock) ✅
**What:** Add `show-toggle="false"` attribute option to tag helper  
**Where:** `PrismMobileUserAgentDemoTagHelper.cs`  
**Behavior:**
- Still runs bootstrap script (UA mock remains active)
- **Does not render** the toggle UI widget (no checkbox, status, close button)
- Walkthrough screenshots capture clean page content
- Developers can still test via query param (e.g., `?prismShowMobileToggle=1` to override)

**Alternative (not recommended):** Playwright-native dismissal (click close button before screenshot in each test) — less reusable, requires per-test updates.

**Why:** Decouples mobile testing from screenshot concerns; one tag helper change fixes all walkthrough specs.

**Impact:** Tag helper only; no view changes needed.

### 4. Leave Homepage Height & Design Tokens Unchanged ✅
**Decision:** No removal of design system tokens section.  
**Rationale:** Tokens section is valuable for branding operators; scrolling is natural UX; adding demos above doesn't harm tokens visibility.

---

## What NOT to Change

| Item | Reason |
|------|--------|
| Header nav (3 items) | Clean; demos belong on targeted pages |
| Mobile nav config | Site-wide; not demo-specific |
| Workflow form rendering | Working well; no accessibility/UX issues |
| Dashboard size | Scrolling is natural; no change needed |

---

## Implementation Checklist (No Implementation Yet)

- [ ] **Home page:** Add demo workflows section (4 cards)
- [ ] **Dashboard:** Add admin card with role guard
- [ ] **Tag helper:** Add `show-toggle=false` attribute + query param override
- [ ] **Tests:** Verify no regressions in walkthrough specs
- [ ] **Accessibility:** Ensure demo cards meet WCAG 2.2 AA (focus, labels, contrast)

---

## Decision Rationale

**Why these three changes together?**
1. **Discoverability (1 + 2):** All workflows + admin UI are now reachable without URL knowledge
2. **Screenshot cleanliness (3):** Mobile widget no longer clutters walkthrough images
3. **Coherence:** Each change is independent; can be reviewed separately
4. **Minimal scope:** No removal of existing content; only additions + tag helper tweak

**Why not more aggressive changes?**
- Dashboard already works well (3 cards is clean; 4-5 is acceptable)
- Homepage tokens section has value (for operators)
- Header nav at 3 items is intentional (clarity over clutter)
- Mobile nav stays site-wide (not demo-specific)

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Home page longer on scroll | Low | Document natural scrolling; test at typical viewports |
| Admin card visible to non-admins | Medium | Implement role guard; test with non-admin user |
| UA mock affects other tests | Low | Keep bootstrap active; only hide UI; test mobile-specific features still work |
| Tag helper query param conflicts | Low | Use unique param name; document in code comment |

---

## Next Steps

1. **Review:** Scribe/team review of this audit
2. **Implementation:** If approved, no changes needed for this session (audit-only)
3. **Separate PR:** Recommend addressing each change in focused PR (home → dashboard → tag helper)
4. **Testing:** Update walkthrough specs to verify no mobile widget appears

---

## Related Artifacts

- **Audit document:** /Users/jonnymuir/Documents/Projects/Umbraco.Prism/.squad/agents/isabelle/history.md (2026-05-04 entry)
- **Routes defined in:** `/src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`
- **Tag helper:** `/src/UmbracoPrism.Core/TagHelpers/PrismMobileUserAgentDemoTagHelper.cs`
- **Views:**
  - `/src/UmbracoPrism.TestSite/Views/homePage.cshtml`
  - `/src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml`
  - `/src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`
- **Walkthroughs:** `docs/walkthroughs/*.md` + `src/UmbracoPrism.Client/tests/walkthroughs/*.walkthrough.spec.ts`
# Decision: Testing Standards Going Forward

### What Changes
1. **All new walkthroughs** must include:
   - Happy path test ✓ (already required)
   - At least one edge case test (validation, conditional reveal, or back/edit)
   - Mobile viewport variant (desktop + iPhone 12 or tablet size)
   - Success state assertion (submission confirmation, error message, etc.)

2. **Existing walkthrough gaps** to be closed:
   - Information Request: Add success state assertion (5 min)
   - Community Enquiry: Add validation test (15 min)
   - Community Enquiry: Add back/edit test (15 min)
   - Payment Demo: Add back/edit test (15 min)
   - Information Request: Add back/edit test (15 min)
   - Information Request: Add validation test (15 min)
   - All 4 walkthroughs: Add mobile viewport variant (45 min)

### What Stays the Same
- Manual-only walkthroughs (authoring, tenant creation, design system, mobile build, push notifications) remain acceptable per R6
- Helper patterns (`assertHealthyPage`, `step()`) enforce good practices
- Component tests continue in Storybook (no change)
- Backoffice automation not required (manual captures sufficient)

## Success Metrics

After implementing Priority 1 & 2 recommendations:
- ✓ 100% of walkthrough workflows covered for back/edit flow
- ✓ 100% of walkthrough workflows have validation test
- ✓ 100% of walkthrough tests run on mobile viewport
- ✓ 100% of workflows assert submission success state
- ✓ Home page entry point tested
- → Total: 26+ tests (up from 20)
- → Zero regression risk; improved edge case coverage

## Out of Scope (Not Changing)

The following are acceptable as manual-only or out-of-scope:
- Full backoffice OIDC/tenant creation automation
- Workflow authoring via backoffice (manual captures sufficient)
- Mobile app Xcode/Android Studio builds
- Service worker + push notification full lifecycle (partial automation only)
- Accessibility full audit (basic assertions can start now; full audit separate initiative)

---

**Next step:** Prioritize Tier 1 improvements (back/edit + validation tests) for closure by sprint end.
---
date: 2026-05-04T11:46:55.877+01:00
author: Tom Nook (Discovery & Architecture)
status: proposal
priority: high
category: walkthroughs, documentation, user-experience
---

# Walkthrough & Testing Architecture — Discovery & Recommendations

**Scope:** End-to-end verification of walkthrough/test infrastructure against user request constraints. No code changes in this pass — architecture and sequencing only.

---

## Executive Summary

Walkthroughs are architecturally sound (executable specs ✓, tests gate PRs ✓, spec-markdown lockstep enforced ✓). **Six concrete gaps** block the user's vision:

1. **Navigation hierarchy is incomplete.** Dashboard doesn't list all 4 workflow types; discovery requires visiting TestSite sources.
2. **Workflow types are underexposed.** Only 2 of 4 seeded workflows linked from dashboard; 2 others invisible to end users.
3. **Admin screen is unreachable.** `/admin/workflow` (where operators manage instances, move states, edit definitions) has no link from the dashboard or any user journey. Walkthroughs can't document the ops path.
4. **Screenshot heights are excessive.** `fullPage: true` produces 2500–9400px PNG files. Homepage screenshot is 9447px tall — unreadable in docs.
5. **Mobile nav leaks into workflow screenshots.** `prism-mobile-nav` component renders in walkthrough capture, adding visual clutter to form-focused screenshots.
6. **Workflow movement is undocumented.** No walkthrough shows how operators use admin panel to transition workflow instances between states.

Additionally:
- **Push notifications walkthrough is orphaned** — markdown written, spec exists but skipped, image directory empty.
- **4 workflow seeds exist; 9 walkthroughs reference them.** Mismatch suggests incomplete coverage or intentional deferral.

---

## What Exists Today

### Walkthrough Infrastructure ✓

**Three-artifact lockstep (per SKILL.md):**
- `docs/walkthroughs/{key}.md` — narrative
- `src/UmbracoPrism.Client/tests/walkthroughs/{key}.walkthrough.spec.ts` — executable
- `docs/images/walkthroughs/{key}/*.png` — generated

**9 walkthrough suites defined:**
1. community-enquiry (seeded ✓, spec ✓, images ✓)
2. information-request (seeded ✓, spec ✓, images ✓)
3. payment-demo (seeded ✓, spec ✓, images ✓)
4. planning-notification (seeded ✓, spec ✓, images ✓)
5. authoring-a-workflow (spec manual ✓, images N/A, no seed needed)
6. creating-a-tenant (spec manual ✓, images N/A, backoffice only)
7. design-system (spec exists, narrative exists)
8. building-a-mobile-app (spec manual, images N/A, device biometrics)
9. push-notifications (spec skipped, markdown written, **images empty ✗**)

**Test integration:**
- All 9 specs in `src/UmbracoPrism.Client/tests/walkthroughs/`
- All matched to `.github/workflows/capture-screenshots.yml` (manual `workflow_dispatch`)
- All gated by `localhost-auth-playwright` job in CI

**Screenshot infrastructure:**
- Helper in `tests/walkthroughs/support/walkthrough.ts` exports `step()` and `assertHealthyPage()`
- `step()` calls `page.screenshot({ fullPage: true })`
- `CAPTURE_SCREENSHOTS=1` env var controls write; assertions always run

---

### Navigation & Discoverability ✗

**What's exposed from dashboard (`/dashboard`):**
- Card: "My Workflows" → `/my-workflows` (WorkflowHub)
- Card: "Payment Demo" → `/payment-demo` (payment-demo workflow)
- Card: "Get in Touch" → `/get-in-touch` (community-enquiry workflow)
- No card or link for: information-request, planning-notification

**What's in the content tree (implicit, not dashboard-driven):**
- Home `/`
- Dashboard `/dashboard`
- WorkflowHub `/my-workflows`
- 4 workflow pages (`/get-in-touch`, `/payment-demo`, `/apply-for-planning-permission`, `/request-information`)

**What's hidden from typical user navigation:**
- `/admin/workflow` — ops panel with workflow instances, state transitions, JSON editor
  - Exists in `MockBusinessApp/Program.cs` (lines 276–745)
  - Hardcoded to Development environment only (defence-in-depth at line 49)
  - No link from dashboard, no mention in TestSite views
  - Accessible only if user knows the URL

---

### Workflow Definitions & Seeds

**4 seed files in `MockBusinessApp/workflow-seeds/`:**
1. `community-enquiry.json` — 4 states, form-based, conditional reveals
2. `information-request.json` — 3 states, file upload, address lookup
3. `payment-demo.json` — 3 states, Stripe integration, waiting state
4. `planning-notification.json` — 5 states, complex multi-page, waiting + review

**Workflow types inferred from state component trees:**
- `"question"` — user entry form states
- `"check-answers"` — summary-list component (GDS pattern)
- `"waiting"` — status timeline, no user actions
- `"confirmation"` — final state, congratulations panel
- `"task-list"` — (inferred from future v2 schema, may not be in current seeds)

No `StepType` enum in current code (deprecated from v1). Types are inferred post-render via `stepType()` utility in `BusinessAppWorkflowEngine`.

---

### Screenshots & Visual Capture

**Current state:**
- `step()` uses `page.screenshot({ fullPage: true })`
- Captures entire viewport height, no scroll clipping
- No exclusion for header, nav, or footer

**Real dimensions observed:**
| Walkthrough | File | Dimensions | Size (KB) |
|---|---|---|---|
| community-enquiry/01-initial | 1280×2537 | 185 |
| community-enquiry/02-conditional | 1280×2672 | 200 |
| information-request/01-initial | 1280×2088 | 114 |
| payment-demo/01-initial | 1280×1244 | 59 |
| planning-notification/01-initial | 1280×1957 | 80 |
| **shared/01-homepage** | **1280×9447** | **800** |

The shared homepage screenshot is **9447 pixels tall** — ~13 inch document when viewed at 72dpi. Visual noise in markdown.

**Mobile nav behavior:**
- `prism-mobile-nav` web component rendered in `_MobileShellNav.cshtml`
- Included in Master layout (applies to all views)
- Appears in all walkthrough screenshots (unless hidden via CSS or excluded via viewport)
- Adds ~60–80px visual clutter at top of form-focused screenshots

---

## Gaps & Blockers

### 1. Navigation Hierarchy Not Fully Exposed

**Problem:** A new user arriving at the dashboard sees 3 workflow cards (My Workflows, Payment Demo, Get in Touch). They have no way to discover that `information-request` and `planning-notification` workflows exist without:
- Browsing TestSite source code
- Asking the developer
- Reading the walkthrough index (not reachable from app UI)

**Impact on Walkthroughs:**
- "Information Request" walkthrough can be read, but user cannot reach the workflow unless they know `/request-information`
- "Planning Notification" walkthrough similarly blocked
- Ops cannot verify these workflows are fully functional via normal navigation

**What's needed:**
- Dashboard should list **all 4 workflow types** (or link to a discoverable registry)
- WorkflowHub (`/my-workflows`) could be expanded to show "all available workflows" section
- OR: Create a "Workflows" or "Templates" gallery on the dashboard

---

### 2. Admin Screen Unreachable from Normal Navigation

**Problem:** The `/admin/workflow` screen is the canonical ops interface for:
- Viewing all workflow instances across all users
- Transitioning instances between states (approve, reject, request-changes)
- Editing JSON definitions (hot-reload)
- Inspecting state diagrams and transitions

It exists in development but is completely hidden. No walkthrough can document the ops workflow.

**Current access:**
- Only via direct URL (if you know the path)
- Not linked from any view
- Not mentioned in README or docs (except this discovery)

**Impact on Walkthroughs:**
- Cannot document "Move a workflow instance from Review → Approved" steps
- Cannot show the state diagram or definition editor
- Operators have no UI path to the tool they need

**What's needed:**
- Link on dashboard (dashboard role: admin-only, or dev-environment-only display)
- OR: Document the URL in a "For Operators" section with prerequisite disclosure
- OR: Route it through the Umbraco backoffice instead (higher friction, but more secure)

---

### 3. Screenshot Heights Excessive; Mobile Nav Leaks In

**Problem 1: Height**
- `fullPage: true` captures the entire scrollable document
- Forms with lots of fields or long explanatory text produce 2500–9400px files
- User has to scroll endlessly in markdown; visual fatigue
- 800KB for a single screenshot is disproportionate

**Problem 2: Mobile Nav**
- `prism-mobile-nav` component adds ~60–80px at the top of every screenshot
- In a form-focused walkthrough (e.g., "Community Enquiry"), this is visual noise
- It's useful for mobile context docs, but clutter for desktop workflows

**What's needed:**
- Clip screenshots to viewport height or content bounds (viewport: 1280×800 or similar)
- Either hide `prism-mobile-nav` before capture (e.g., `await page.locator('prism-mobile-nav').hide()`) or exclude it via viewport
- Document the screenshot dimensions in SKILL.md

**Implementation hint:**
```typescript
await page.locator('prism-mobile-nav').evaluate(el => el.style.display = 'none');
// OR use a narrower viewport
page.setViewportSize({ width: 1280, height: 800 });
```

---

### 4. Push Notifications Walkthrough Is Orphaned

**State:**
- Markdown: ✓ (comprehensive, links to architecture docs)
- Spec: ✓ (exists, but `.skip(true, ...)`)
- Images: ✗ (directory is empty, only `.gitkeep`)

**Why skipped:**
- Spec comment says "Manual capture only" — web push subscription UI requires manual browser prompts
- Spec covers automation up to the subscription prompt, then defers to manual capture

**What's needed:**
- Decide: Is this a manual-only walkthrough (accept the `.skip` and document manual capture procedure in .md)?
- OR: Automate the browser's granted push subscription (mock it, or use headless browser grant automation)?
- Either way: Capture the images (manually or via automation) so the markdown has visual support

---

### 5. Workflow Type Discovery in Admin Screen

**Problem:** The `/admin/workflow` HTML shows workflow definitions with state icons and state diagrams, but there's no visual "gallery" of workflow types. It's an instance table + definition cards, not a "workflow template browser."

**What's needed (if exposing admin on dashboard):**
- Consider rearranging the admin HTML so the definition cards are visually prominent and easy to screenshot
- Group by workflow type or category
- Make each card screenshot-friendly (not overly wide, not a dense code dump)

---

### 6. Authoring & Tenant Creation Walkthroughs Are Manual-Only

**State:**
- Both marked `.skip(true, ...)` in specs
- Both require backoffice interaction (Umbraco admin UI)
- Both have TODO comments for manual captures

**What's needed:**
- Clarify scope: Are these walkthroughs expected to be auto-captured, or documented as manual?
- If manual: Document the capture procedure in the markdown (see SKILL.md R1 for example)
- If auto: Implement backoffice auth and content tree navigation in the spec

**Low priority** — these are developer/operator workflows, not end-user. But they should be complete enough that someone can follow them without surprises.

---

## Proposed Implementation Slice

**Goal:** Deliver a coherent end-to-end journey from end-user workflows through admin management, with complete discoverability, properly-sized screenshots, and no hidden paths.

### Phase 1: Dashboard Navigation (Isabelle + Blathers — 1–2 days)

**Objective:** Expose all 4 workflow types from dashboard; link to admin screen (dev-only or admin-only).

**Deliverables:**
- [ ] Add "Request Information" and "Planning Notification" cards to dashboard (or expand to a gallery/list view)
- [ ] Add "Manage Workflows" card that links to `/admin/workflow` (only visible if dev or has admin role)
- [ ] Verify WorkflowHub lists all 4 workflow types (or add a section)
- [ ] Update `memberDashboard.cshtml` and related controllers

**Test Requirement:** Existing dashboard tests still pass; new cards link to correct URLs (no 404s).

**Who owns:** Isabelle (frontend) + Blathers (controller routing/auth checks)

**Dependencies:** None — purely additive to dashboard view.

---

### Phase 2: Screenshot Optimization (Tangy — 2–3 days)

**Objective:** Reduce screenshot heights; remove mobile nav clutter; establish viewport standard.

**Deliverables:**
- [ ] Update `walkthrough.ts` `step()` function:
  - Set viewport to fixed dimensions (e.g., 1280×1024)
  - Hide `prism-mobile-nav` before capture (or exclude via viewport width)
  - Document the standard in SKILL.md
- [ ] Re-capture all walkthrough images via `workflow_dispatch` (automated batch)
- [ ] Verify community-enquiry/01-initial goes from 2537px → ~1024px (or similar)
- [ ] Update all markdown if image filenames or sizes change significantly

**Test Requirement:** All walkthrough specs still pass; images are cleaner and shorter; markdown renders without excessive scrolling.

**Who owns:** Tangy (testing), with Mabel (documentation review)

**Dependencies:** Phase 1 complete (new dashboard cards should be in screenshots)

**File-level changes:**
- `src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts` — `step()` function
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — document viewport standard
- All `docs/images/walkthroughs/**/*.png` — regenerated

---

### Phase 3: Admin Walkthrough & State Movement (Blathers — 2–3 days)

**Objective:** Document the admin screen; show operators how to move workflow instances between states.

**Deliverables:**
- [ ] Create `docs/walkthroughs/workflow-administration.md`
- [ ] Create `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts`
- [ ] Spec covers:
  - Navigate to `/admin/workflow`
  - View workflow instances table
  - View workflow definitions (state diagrams)
  - Execute an action (e.g., "Approve" a pending instance) via the form
  - See instance state change reflected in table
- [ ] Capture screenshots for each step

**Test Requirement:** Spec gates on all PRs; no CI red flags.

**Who owns:** Blathers (backend), with Tangy (test structure)

**Dependencies:** Phase 1 (dashboard link exists), Phase 2 (screenshot config finalized)

**File-level changes:**
- New: `docs/walkthroughs/workflow-administration.md`
- New: `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts`
- New: `docs/images/walkthroughs/workflow-administration/*.png`
- Update: `docs/walkthroughs/README.md` to include new walkthrough

---

### Phase 4: Push Notifications & Manual Capture Walkthroughs (Mabel + Tangy — 2 days)

**Objective:** Complete push-notifications walkthrough; decide on authoring/tenant-creation manual captures.

**Deliverables (Push Notifications):**
- [ ] Clarify: Is this end-to-end automatable, or manual from subscription prompt onward?
- [ ] If automatable: Implement browser grant automation in spec
- [ ] If manual: Document the manual capture procedure in the markdown (see SKILL.md for format)
- [ ] Capture screenshots for all steps
- [ ] Remove `.skip()` or clearly document why it remains skipped

**Deliverables (Authoring & Tenant):**
- [ ] Decide: Full automation, or manual with documented capture procedure?
- [ ] If manual: Add `<!-- manual capture: reason -->` comments in markdown per SKILL.md R1
- [ ] If full automation: Implement backoffice login + navigation in spec

**Test Requirement:** All specs are not skipped OR have documented reasons + manual procedures.

**Who owns:** Mabel (docs clarity) + Tangy (spec implementation)

**Dependencies:** Phases 1–3 complete

---

### Phase 5: Navigation Hierarchy & Discoverability Refinement (Tom Nook — 1 day)

**Objective:** Review final navigation hierarchy; ensure Prism content tree matches documentation; update SKILL.md.

**Deliverables:**
- [ ] Verify all 4 workflow types are navigable from dashboard or hub
- [ ] Verify `/admin/workflow` is accessible via dashboard link or documented URL
- [ ] Update `umbraco-workflow-page-ownership` SKILL.md with final guidance
- [ ] Review all walkthrough READMEs and links for consistency
- [ ] Final check: No broken links, all URLs resolve, navigation feels natural

**Who owns:** Tom Nook (architecture review)

**Dependencies:** All prior phases complete

---

## Sequencing & Team Coordination

**Recommended order:**
1. **Phase 1** (Dashboard) — unblocks Phases 2–3. Start immediately.
2. **Phase 2** (Screenshots) — can run in parallel with Phase 1; unblocks final polish.
3. **Phase 3** (Admin Walkthrough) — depends on Phase 1 link; depends on Phase 2 for screenshot config.
4. **Phase 4** (Push/Manual) — independent; can run in parallel with Phases 2–3.
5. **Phase 5** (Final Review) — only after all prior phases complete.

**Cross-File Dependencies:**

| File | Phase | Owner | Impact | Notes |
|---|---|---|---|---|
| `memberDashboard.cshtml` | 1 | Isabelle | Dashboard cards | Adds links to new workflows + admin |
| `MemberDashboardController.cs` | 1 | Blathers | Controller logic | Auth checks, URL resolution |
| `TestSiteSeedContract.cs` | 1 | Blathers | Routes | Add constants for new workflow URLs if needed |
| `walkthroughs/support/walkthrough.ts` | 2 | Tangy | Screenshot helper | Viewport + mobile-nav-hiding logic |
| `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` | 2 | Tangy | Skill doc | Document viewport standard + height rules |
| `/admin/workflow` (Program.cs) | 1 | Blathers | Ops panel | No code change, but linked from dashboard |
| `docs/images/walkthroughs/**/*.png` | 2 | automated | Screenshots | Regenerated by `workflow_dispatch` |
| `docs/walkthroughs/*.md` | 3–4 | Tangy/Mabel | Narratives | New walkthroughs + updates to existing |

**Potential bottlenecks:**
- **Phase 1 → Phase 2:** Tangy may need Isabelle's final dashboard design before capturing. Sequence so dashboard merge → screenshot capture immediately.
- **Phase 2 → Phase 3:** Screenshot config finalized before starting admin-walkthrough spec.
- **Pull request merges:** No feature branches per 2026-04-26 directive. Each phase commits directly to `main`; recommend squashing logical units into 1–2 commits per phase.

---

## Files to Touch (Summary)

### View/Controller (Phase 1)
- `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml`
- `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` (if auth check needed for admin link)
- `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs` (if new URLs added)

### Test Infrastructure (Phase 2)
- `src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts`

### Walkthrough Specs (Phase 3–4)
- `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts` (NEW)
- `src/UmbracoPrism.Client/tests/walkthroughs/push-notifications.walkthrough.spec.ts` (update)
- `src/UmbracoPrism.Client/tests/walkthroughs/authoring-a-workflow.walkthrough.spec.ts` (decide on manual)
- `src/UmbracoPrism.Client/tests/walkthroughs/creating-a-tenant.walkthrough.spec.ts` (decide on manual)

### Walkthrough Narratives (Phase 3–4)
- `docs/walkthroughs/workflow-administration.md` (NEW)
- `docs/walkthroughs/push-notifications.md` (update/complete)
- `docs/walkthroughs/authoring-a-workflow.md` (update with manual capture procedure)
- `docs/walkthroughs/creating-a-tenant.md` (update with manual capture procedure)
- `docs/walkthroughs/README.md` (index all 9+1 walkthroughs)

### Documentation & Skills (Phase 2–5)
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` (document viewport standard)
- `.squad/skills/umbraco-workflow-page-ownership/SKILL.md` (refine if needed)

### Generated Assets (Phase 2, 3–4)
- `docs/images/walkthroughs/**/*.png` (all regenerated; new workflow-administration dir)

---

## Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Admin screen assumes dev-only access; adding dashboard link exposes it to end users | Medium | Add role-based or env-var gate on the view; display only in Development or if user has admin role. Document this in SKILL.md. |
| Screenshot re-capture changes image dimensions; old docs may reference old sizes | Low | Run capture in CI on a single branch; verify all markdown images load before merging. |
| Push-notifications walkthrough remains manual/incomplete; scope creep on spec automation | Low | Decide early (manual vs. auto); document decision and stick to it. Accept manual for this phase if crypto/browser-grant complexity is high. |
| Workflow types (community, payment, planning, info-request) hardcoded in views; adding a 5th requires code change | Low | Consider data-driven dashboard card list (loop over workflow definition keys returned from Business App API); out of scope for this pass, but note for v2.1. |
| Navigation changes break existing links in external docs or bookmarks | Low | Verify URLs are stable (only *adding* new routes, not moving existing ones). Test `/get-in-touch`, `/payment-demo`, `/my-workflows` remain unchanged. |

---

## Non-Goals & Deferral

**Out of scope for this pass:**
- Rebuilding the admin screen HTML (it's functional; we're just linking to it)
- Automating browser grant prompts (push-notifications spec remains manual-to-capture if infeasible)
- Changing the workflow definition storage (JSON seeds are fine; no schema migration)
- Mobile app screenshots (building-a-mobile-app walkthrough remains manual; device biometrics are not UI-automatable)
- Consolidating duplicate walkthrough docs (doc-walkthrough-consolidation SKILL.md deferred to Mabel's batch)

---

## Acceptance Criteria

- [ ] **Phase 1:** All 4 workflow types are discoverable from dashboard or WorkflowHub; `/admin/workflow` is linked (dev-only or admin-only).
- [ ] **Phase 2:** All walkthrough screenshots are ≤1200px tall; `prism-mobile-nav` is hidden or excluded.
- [ ] **Phase 3:** New `workflow-administration.md` walkthrough documents state transitions via admin screen; spec gates on PR.
- [ ] **Phase 4:** `push-notifications` walkthrough is complete (auto or manual) with images; `authoring-a-workflow` and `creating-a-tenant` have documented manual procedures.
- [ ] **Phase 5:** Navigation hierarchy is documented in SKILL.md; no broken links in any walkthrough; team review sign-off.

---

## Next Steps

1. **Immediate:** Share this document with Isabelle, Blathers, Tangy, Mabel for review.
2. **Day 1:** Isabelle + Blathers start Phase 1 (dashboard cards).
3. **Day 2–3:** Tangy works Phase 2 in parallel (screenshot config) once Phase 1 is visible.
4. **Day 3–5:** Blathers + Tangy start Phase 3 (admin walkthrough); Mabel starts Phase 4 (push/manual).
5. **Day 6:** Tom Nook final architecture review (Phase 5); ready for merge.

**Expected outcome:** End-to-end walkthrough journey is complete, discoverable, visually clean, and documented with executable specs that gate every PR. Operators have a canonical path to the admin screen. All workflow types are reachable from normal navigation.

---

**End of discovery report.**


---
date: 2026-05-04T11:46:55.877+01:00
author: Brewster
status: IMPLEMENTED
area: testsite, walkthroughs, discoverability
---

# Walkthrough Discoverability — All Workflow Types Reachable from Dashboard

## Context

Audit findings showed that some workflow demos (planning-notification, information-request)
were only reachable via direct URL knowledge. The member dashboard linked to just three
workflow types out of four, and there was no route from the Prism dashboard to the
MockBusinessApp workflow admin screen.

Two TestSite stub views (`workflowHub.cshtml`, `workflowPage.cshtml`) contained
`Layout = null` and no content, silently overriding the Core library's fully implemented
embedded views and rendering blank pages.

## Decisions Made

### 1. Delete TestSite stub views — use Core embedded views

`src/UmbracoPrism.TestSite/Views/workflowHub.cshtml` and `workflowPage.cshtml` were
stub files with `Layout = null` that blocked the `PrismEmbeddedViewsStartupFilter`
embedded views from being served. Deleting the stubs lets the Core's implementations
(with `Layout = "~/Views/Shared/Master.cshtml"` and full rendering logic) take over.

**Rule:** The TestSite should not ship stub overrides for Core-embedded views unless
there is a deliberate TestSite-specific customisation. A file that only contains
`Layout = null` is a broken placeholder and must be removed.

### 2. Restructure member dashboard card grid

The existing 6-card flat grid was split into two coherent groups:

- **Overview** (4 cards): My Account, Documents, Support, My Workflows hub
- **Workflow Demos** (4 cards, in a labelled `dash-section`): Get in Touch,
  Apply for Planning Permission, Payment Demo, Request Information

All four seeded workflow types are now directly reachable from one section with
content-tree resolved URLs, not hardcoded route guesses.

### 3. Expose workflow admin URL from `MemberDashboardController`

`IConfiguration` was injected into `MemberDashboardController` to derive
`{PrismBusinessApp:WorkflowApiBaseUrl}/admin/workflow`. This is the same URL pattern
the AppHost annotates as a `Workflow Admin` resource URL. It is passed to the view as
`ViewBag.WorkflowAdminUrl`.

A **Developer Tools** `dash-section` renders conditionally (only when the URL is set),
showing a single card linking to the admin screen in a new tab.

### 4. Environment-aware without extra config

No new configuration keys are introduced. The existing `PrismBusinessApp:WorkflowApiBaseUrl`
already resolves correctly in Codespaces (via AppHost forwarded URL detection) and
locally (`https://localhost:7245`). The admin URL is simply appended as `/admin/workflow`.

## Verification

- `dotnet build` — 0 errors, 2 pre-existing warnings (unrelated)
- `dotnet test` — 690 passed, 0 failed

---
date: 2026-05-04
author: Tangy
status: PROPOSED
area: testing, walkthroughs, screenshots, documentation
---

# Walkthrough Coverage Hardening — Test Gaps and Screenshot Behaviour

## Context

Walkthrough coverage audit (2026-05-04) found five gaps in the executable specs:

1. Back/edit flows absent for `community-enquiry`, `payment-demo`, and `information-request`
2. Form validation tests absent for `community-enquiry` and `information-request`
3. `information-request` happy path lacked an explicit body-content assertion for the under-review success state
4. No home-page entry walkthrough (homepage hero → dashboard → workflow hub path)
5. Screenshot capture used `fullPage: true` unconditionally, producing oversized images for long pages (homepage hero, etc.)

## Decisions

### D1 — Viewport-first screenshots; fullPage is opt-in per step

**Decision:** The `step()` helper in `tests/walkthroughs/support/walkthrough.ts` now defaults to
`fullPage: false` (viewport-sized capture). Individual steps that genuinely need the full scrolled
page (e.g. a check-answers summary list that would be cut off) can pass `fullPage: true` via the
`PageHealthCheck` interface.

**Rationale:** Viewport captures show exactly what the user sees without scrolling, which is the
right documentation-first default. Full-page captures are appropriate for summary/check-answers
pages only.

**Isabelle hook contract:** The `fullPage` flag on `PageHealthCheck` is the per-step control point
intended for the docs pipeline. If the `capture-screenshots.yml` workflow needs a global override
(e.g. always full-page for a particular walkthrough), the recommended mechanism is:

```yaml
# In .github/workflows/capture-screenshots.yml
env:
  CAPTURE_SCREENSHOTS: '1'
  SCREENSHOT_FULL_PAGE: '1'   # <-- add this to request full-page globally
```

Then read `process.env.SCREENSHOT_FULL_PAGE === '1'` in `walkthrough.ts` as the fallback when
`expected.fullPage` is undefined:

```ts
const useFullPage = expected.fullPage ?? process.env.SCREENSHOT_FULL_PAGE === '1' ?? false;
await page.screenshot({ path: file, fullPage: useFullPage });
```

This change is NOT included in the current commit; it is queued for Isabelle to implement when
the docs pipeline requires it. The existing `fullPage?: boolean` field on `PageHealthCheck` is
the stable hook.

### D2 — Persistence tests verify instance-policy contract, not just submit success

**Decision:** For single-page workflows (`community-enquiry`, `information-request`,
`payment-demo`) that have no check-answers step, the "back/edit" behavioral contract is:
*after submission, returning to the workflow URL shows the current state (under-review /
processing), not a fresh form.*

These "persistence" tests are now in the respective walkthrough specs. They navigate away after
submit and navigate back to verify the instance-policy guarantee.

### D3 — `home-entry` is a first-class walkthrough

**Decision:** `home-entry.walkthrough.spec.ts` is a new walkthrough spec covering the full
homepage entry path: signed-out hero → signed-in hero → dashboard → workflow hub. It uses the
same `LiveAppHost` + `step()` pattern as all other walkthrough specs.

The `docs/walkthroughs/home-entry.md` document is the human narrative counterpart; it embeds the
four screenshots generated by the spec.

### D4 — `assertHealthyPage` skipHeading usage for variable-heading pages

**Decision:** The home page's signed-in state and the dashboard may not present their hero text
as a `<h1>` role heading. Where the primary visual identity is a welcome message or layout element
rather than a semantic heading, `skipHeading: true` is used and the test adds an explicit
`expect(...).toBeVisible()` assertion for the relevant content.

This maintains R3 (assert before shoot) without coupling the test to implementation-specific
heading hierarchy.

## Scope not changed

- Admin/backoffice walkthroughs (`authoring-a-workflow`, `creating-a-tenant`, `design-system`)
  remain manual-only per the existing policy. No backoffice automation was added.
- Mobile viewport tests were identified as a gap in the audit but are out of scope for this
  hardening pass (deferred to a future Tangy task).

## Files changed

- `tests/walkthroughs/support/walkthrough.ts` — fullPage default + Isabelle hook comment
- `tests/walkthroughs/community-enquiry.walkthrough.spec.ts` — validation + persistence tests
- `tests/walkthroughs/information-request.walkthrough.spec.ts` — validation + persistence + explicit success assertion
- `tests/walkthroughs/payment-demo.walkthrough.spec.ts` — defer/persistence test
- `tests/walkthroughs/home-entry.walkthrough.spec.ts` — new spec (3 tests)
- `docs/walkthroughs/home-entry.md` — new walkthrough document
- `docs/images/walkthroughs/home-entry/` — new images directory (.gitkeep placeholder)


# Decision: PASA death-process should use verified case access, not mandatory registration

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Blathers  
**Status:** Proposed  

## Summary

For a PASA-style death-notification workflow, the notifier should not be forced through permanent registration before they can report a death, save progress, or resume later.

Instead, the product should use a lightweight verified contact mechanism such as email magic link or SMS OTP to establish a case-scoped notifier identity. Prism then hosts the workflow for that notifier identity, while the business app owns member matching, case persistence, evidence tracking, and reviewer decisions.

## Why

- Bereavement reporting is often a one-off task carried out by someone who is not the member.
- The current Prism workflow model already supports resumable, reviewer-backed journeys once an authenticated actor exists.
- A case-scoped identity gives enough proof to save and resume safely without over-designing account creation.

## Team impact

- Backend and auth work should plan for a notifier-facing session model alongside member-facing auth.
- Workflow design should treat the notifier as the actor and the deceased member as the linked subject.
- Case-management persistence should stay outside Prism workflow field state.


# Decision: PASA Death Process Design Scaffold

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Celeste (Documentation Engineer)  
**Status:** 🚧 Design Phase — Input Requested

## Summary

Authored a comprehensive design document scaffold for a PASA (lifecycle termination) death-process workflow example. The scaffold is intentionally open-ended with explicit decision slots for each discipline (Architecture, Security, Backend, Frontend, Testing) to absorb input from Tom Nook, Copper, Blathers, Isabelle, and Tangy.

## Rationale

**Why a scaffold instead of a complete spec?**

1. **Clarity on unknowns** — Rather than guess at implementation details, the scaffold explicitly flags design decisions that *must* be made upstream (e.g., "Is this single-instance or multi-instance? Who can approve?")
2. **Parallel input** — Each team member can focus on their domain without waiting for others; inputs can be merged later.
3. **Reusable pattern** — The structure itself (decision slots, open questions, narrative sections) can be applied to future workflow designs.
4. **Documentation discipline** — By linking design → backend contract → walkthrough → security audit → specs, the document ensures all artifacts stay in sync.

## Document Structure

The design document includes:

- **Overview & Goals** — Why we're documenting this workflow
- **Open Questions by Discipline** — Explicit slots for Tom Nook (architecture), Copper (security), Blathers (backend), Isabelle (frontend), Tangy (testing)
- **Proposed Workflow Structure** — Tentative state machine with component mapping
- **End-to-End Narrative** — Placeholder walkthrough describing user, admin, and system actions
- **Backend Contracts (Tentative)** — Sample JSON workflow definition + `/advance` response schema
- **Security Considerations** — Threat model & tenant isolation questions
- **Testing Strategy** — Placeholder for executable specs and unit tests
- **Documentation Artifacts** — Links to design → backend spec → walkthrough → security guide → executable specs
- **Decision Timeline** — Four phases from design → implementation → documentation
- **Appendix for Reviewers** — Role-specific guidance for each team member

## Location

Created at: `/docs/design/pasa-death-process.md`

Follows existing design doc conventions:
- Named after the workflow (like `workflow-forms-engine.md`)
- Linked from `docs/design/README.md` (to be added)
- Uses markdown with mermaid flowcharts for clarity
- Includes state machines, contracts, and narratives

## Next Action

Team should review and fill in open questions:

1. **Tom Nook:** Confirm scope, instance policy, state sequence
2. **Copper:** Refine threat model, define audit trail requirements
3. **Blathers:** Finalize backend contract, cleanup orchestration
4. **Tangy:** Define test scenarios and performance SLAs
5. **Celeste:** Merge inputs and advance to walkthrough/implementation phases

## Key Learning

This approach — **design scaffold with explicit decision slots** — is reusable for future complex workflows. Consider extracting as a `.squad/templates/design-doc-scaffold.md` for future use.



# Decision: PASA death-process should use staged assurance and case-scoped access

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Copper (Security Engineer)  
**Status:** Proposed  

## Summary

For the PASA death-notification example, the notifier should not create a permanent member-style account just to report a death, save progress, or return later.

Instead, the design should use:

1. a **public start** with minimum data capture,
2. **verified contact-channel access** via magic link as the primary mechanism, with OTP as a fallback,
3. a **case-scoped notifier identity** plus case reference for save/resume,
4. **reviewer-backed step-up assurance** before any meaningful member-data disclosure or downstream benefit action.

## Security posture

- Treat the **notifier** as the authenticated actor and the **deceased member** as the linked subject.
- Separate **channel proof** from **authority/member-match proof**.
- Keep member matching, reviewer notes, anti-fraud signals, and entitlement decisions in server-side case-domain tables, not in browser-owned workflow payloads.
- Fail closed on data disclosure: before verification, show only generic statuses such as `received`, `under review`, or `more information needed`.

## Save/resume decision

The preferred save/resume pattern is:

- issue a case reference as soon as contact verification succeeds,
- re-establish access through a fresh verified session,
- use a workflow hub to list that notifier's active/completed death cases,
- never treat a raw case URL, `instanceId`, or reference number as sufficient authentication.

## Why this beats the alternatives

- **Full registration** is disproportionate for a one-off bereavement task and increases friction.
- **Magic link alone** is acceptable for bootstrap and low-risk resume, but not for sensitive disclosure without reviewer-backed progression.
- **Case reference + KBA alone** is too weak for online assurance.
- **Delegated representative portals** are a valid future extension, but should come after the simpler case-scoped model.

## Team impact

- Backend design should add `NotifierIdentity` / `NotifierSession` and keep `DeathCase` separate from `WorkflowInstance`.
- Frontend/workflow design should show only generic progress until reviewer-backed verification is complete.
- Documentation and walkthroughs should make the staff-review boundary explicit so the example does not imply that a notifier can self-serve beneficiary or payment outcomes.


# Tom Nook decision — PASA death-process baseline

**Date:** 2026-05-15T06:35:47.013+01:00
**Requested by:** Jonny Muir

## Decision

Use a **case-scoped notifier model** for the PASA death-process example:

1. the notifier is the authenticated workflow actor,
2. the deceased member is the linked subject,
3. the service does **not** require mandatory registration up front,
4. save/resume uses a **hybrid** of passwordless verified-session access plus case-reference recovery,
5. stronger identity checks happen only when the case moves into sensitive disclosure or payment-affecting work.

## Rationale

- PASA public guidance supports **risk-based** identity verification and a frictionless experience where proportionate.
- Broader UK bereavement services show that **no-account or optional-account initiation** is the better front-door pattern for death notification.
- This keeps Prism aligned with existing save/resume and reviewer-loop patterns without pretending the deceased member is the signed-in workflow user.

## Consequences

- The example should add a small pre-workflow bootstrap for notifier contact verification.
- Member matching, duplicate detection, and evidence review stay in the business-app domain layer.
- Progress visibility should stay high level until the case has passed the required proofing threshold.

## Needs sign-off from

- Product owner
- Tom Nook
- Copper
- Blathers
- Celeste


# Decision: Workflow Editor V1 — Projection Determinism & Storage Layout

**Date:** 2026-05-16  
**Author:** Blathers  
**Status:** Proposed

## Context

The workflow editor V1 needs a stable contract for how the Authored Model is compiled into `WorkflowDefinitionFile` and where both artefacts live on disk. Determinism is critical for diff/replay/test reliability.

## Decisions

### 1. Projection Determinism Guarantee

The `IWorkflowProjector.Project(AuthoredWorkflow)` function is a **pure, deterministic function**. Given identical `AuthoredWorkflow` input, it MUST produce byte-identical `WorkflowDefinitionFile` output on every invocation.

Determinism is achieved by:

1. **Normalise before emit:** sort all `Stages[]` by `StageKey`, `Transitions[]` by `(FromStageKey, ToStageKey, Action)`, `Fields[]` by `FieldKey`, `Roles[]` by `RoleKey` — all ordinal. Content blocks within a state are emitted in fixed type order (heading, inset-text, warning-text, details, notification-banner, body), then alphabetically by content within each type.
2. **Fixed serialisation options:** `JsonNamingPolicy.CamelCase`, `WriteIndented = false`, `DefaultIgnoreCondition = Never`, `UnsafeRelaxedJsonEscaping`.
3. **SHA-256 checksum** of the serialised bytes is included in `ProjectionResult.Checksum`.

The checksum enables a CI verify step: re-project all `*.workflow.json` authored files and fail if the checksum differs from the checked-in seed.

### 2. Shell Inference Preservation

The projector emits component trees that satisfy the existing shell inference contracts:

- `WaitingComponent` → `status-timeline`
- `PanelComponent` (no inputs) → `confirmation`
- `SummaryListComponent` → `check-answers`
- `TaskListComponent` → `task-list`
- Default → `question`

These rules are locked by `WorkflowDefinitionInferenceTests` and `SeedFileRoundtripTests`. The projector MUST NOT emit legacy `stepType` or `waitingConfig` properties on any `StepDefinition`.

### 3. Storage Layout

```
src/UmbracoPrism.MockBusinessApp/
  workflow-authored/          ← Authored Model source of truth (*.workflow.json)
  workflow-seeds/             ← Generated WorkflowDefinitionFile (checked in; loaded by runtime)
```

- Authored files: `{definitionKey}.workflow.json` — camelCase JSON, UTF-8 without BOM.
- Generated seed files: existing naming convention (`planning-application.json`, etc.) — unchanged.
- Both artefacts are checked into git. The generated seed is the integration point with the Prism runtime.
- Seed files without a corresponding `.workflow.json` are untouched (backward-compatible with hand-authored seeds).

### 4. Versioning (V1)

- `AuthoredWorkflow.Version` is a monotonically increasing integer, incremented by `ApplyPatch` on every successful patch application.
- Optimistic concurrency: `WorkflowPatchEnvelope.BaseVersion` must match the current version or the patch is rejected.
- `AuthoredWorkflow.SchemaVersion` (string, e.g. `"1.0"`) tracks the authored schema independently of the workflow business version. Migration steps run on load if `SchemaVersion` is older than the current engine version.
- Git is the V1 rollback mechanism. Named draft/published branching is a V2 concern.

## Implications

- CI must include a `workflow-editor project --verify` step for every authored file.
- `IAuthoredWorkflowStore` is designed to be swapped for multi-tenant deployments; V1 ships a file-backed single-tenant implementation.
- The `/admin/workflow` inspector and the Prism runtime are unaffected; they continue to load seed files directly.

## Related

- `docs/design/workflow-editor-v1/02-runtime-projection.md` — full design
- `.squad/decisions/inbox/blathers-workflow-runtime-design.md` — prior authored-model proposal
- `.squad/decisions/inbox/tom-nook-workflow-editor-design.md` — three-plane architecture

# Blathers workflow runtime design

- **Date:** 2026-05-16T10:59:37.438+01:00
- **Author:** Blathers

## Context

Prism already has a stable workflow/forms contract built around `WorkflowDefinitionFile`, component-authored steps, runtime shell inference, `WorkflowResponseEnvelope`, nonce-backed POST validation, and business-app-owned state transitions.

The new editor needs to let authors model front stage and back stage work, multiple actor roles, public/member/business-app experiences, waiting states, deadlines, and handoffs without forcing authors to hand-author lots of low-level Prism states.

## Decision

### 1. Author a stage model, not raw runtime states

Use a higher-level authored shape with:

- workflow identity (`definitionKey`, `displayName`, `version`, `instancePolicy`)
- actor catalogue (`public`, `member`, `agent`, `reviewer`, `caseworker`, `system`, third-party roles)
- case model references (case type, linked subject types, assignment queues, SLA policy names)
- authored stages as the primary unit of design

Each authored stage should describe:

- `stageKey`
- `displayName`
- `kind` (`capture`, `review`, `waiting`, `decision`, `task-list`, `complete`, `backstage`)
- `route` / route intent
- `serviceZone` (`frontstage`, `backstage`, `hybrid`)
- `entryCriteria`
- `views[]` for audience-specific surfaces (`public`, `member`, `business-app`, `operator`)
- `handoffs[]`
- `waiting` metadata
- `deadlines[]`
- `permissions`
- `assignments`
- `outcomes` / `nextStage`

Authors stay focused on service design. Runtime step shells stay derived.

### 2. Project authored stages into Prism-compatible runtime states

Introduce a projection layer that expands each authored stage into one or more runtime states:

- front-stage authored views project to Prism render states with existing components
- waiting stages project to `ResponseState = defer` plus a `waiting` component
- check/review views project to `summary-list`, `task-list`, `panel`, or normal question shells
- backstage-only stages do not need browser-facing fields; they project to status/timeline or operator-only views
- handoffs create runtime transitions/actions rather than extra authored duplication

Recommendation: authored stages own intent; projector owns Prism shell selection and route/state expansion.

### 3. Keep operational truth outside workflow answers

Represent these in case/domain persistence, not generic workflow field payloads:

- case status and lifecycle
- linked member/applicant/representative subjects
- assignment owner/queue
- internal notes
- review decisions
- evidence/document manifests
- deadline clocks and breach state
- third-party proofing and participation records

Prism instance state remains the user journey position plus authored answer data needed to render the next step.

## Compatibility constraints

The following must remain stable:

- `definitionKey`, `initialState`, `instancePolicy`
- component-based authored/rendered field semantics
- transition/action model
- `StateVersion`
- `WorkflowProblem`
- `WorkflowResponseEnvelope` / `StepContent`
- waiting/task-list/check-answers/confirmation shell behaviour
- current nonce, antiforgery, and claim-derived ownership model

The new editor can add authored metadata, but the projector must still emit the same compatible runtime contract Prism expects today.

## Validation and migration

- Validate authored graph before projection: unique stage keys, valid routes, valid view audiences, resolvable handoffs, deadline references, and actor references.
- Validate projected graph after projection: every runtime state reachable, every action resolvable, every form field key unique within a view path, and no incompatible shell/component combinations.
- Treat authored schema version separately from workflow business version.
- Migrate authored definitions through explicit migration steps into the latest authored schema before projection.
- Keep projector backward-compatible so old raw Prism definitions can still run unchanged during migration.

## Open decisions

1. Whether backstage operator views should stay inside Prism payloads or move to a separate operator UI contract.
2. Whether permissions are authored as named policies only, or allow inline role expressions.
3. How much routing logic is authored declaratively versus delegated to business-app policy handlers.
4. Whether task-list progression is purely projected from stage dependencies or optionally hand-authored for editorial control.

# Brewster — Workflow editor topology in Umbraco + Prism

- **Date:** 2026-05-16T10:59:37.438+01:00
- **Author:** Brewster
- **Status:** Proposed

## Summary

The reference implementation should separate concerns by actor:

1. **Public website** in Umbraco content for discovery, explanations, and calls to action.
2. **Member website** in Umbraco content for authenticated workflow entry, resume, and status.
3. **Business-app user/editor surface** in the MockBusinessApp for workflow operations, assignment, review, and definition editing.

## Decision

- Keep Umbraco as the authored shell for public and member journeys.
- Keep Prism `workflowPage` and `workflowHub` as the member-facing integration points.
- Keep authored workflow definitions and operator tooling owned by the Business App.
- If Umbraco needs an editor-facing convenience surface, add it as a **v17 backoffice extension** that links to or embeds the Business App editor, rather than re-implementing workflow authoring inside document templates.
- Do **not** position `workflowDemoPage` as the preferred pattern for this architecture.

## Why

- This preserves existing ownership rules: `workflowHub` and `workflowPage` remain the stable member-facing pages, and instance routing still resolves by `workflowKey` plus optional `instanceId`.
- It matches Umbraco idioms: editors author content structure and page narrative in the tree; Prism bridges auth, tenancy, and rendering; the Business App owns workflow behaviour.
- It creates a clearer product story for the planning-application demo: citizen/member experience in Umbraco, caseworker/editor experience in the business application.

## Implications

- Add dedicated content nodes for public explainer pages and protected member entry pages rather than collapsing everything into one generic demo route.
- Keep workflow definitions discoverable from member pages and dashboard links, but do not let member pages mutate business-user workflow state directly outside the normal Prism flow.
- Any future Umbraco editor integration should use Umbraco 17 backoffice manifests/Lit components and respect that it is an editorial shell over a downstream workflow system, not the source of truth.

# Brewster Decision — Workflow Editor V1 Umbraco Integration

**Date:** 2026-05-16T13:20:33.659+01:00
**Author:** Brewster (Umbraco Platform Specialist)
**Status:** Proposed

## Editor Hosting Decision

**Choice: Option (c) — Hybrid. A v17 backoffice section embeds the editor app.**

A Lit/Web Component (`<prism-workflow-editor-app>`) registered as a v17 backoffice section via the Umbraco package manifest. The component embeds or frames the standalone workflow editor/projection tooling, which remains independently runnable from the CLI and CI pipelines.

## Rationale

- Editors discover the editor through the familiar Umbraco backoffice — no separate URL to remember or bookmark.
- Umbraco backoffice auth (standard login) is reused. No separate authentication flow or session is needed for the editor surface.
- The projection tooling stays host-agnostic: it exposes a clean HTTP API so the Lit component, CLI, and agent-plane tools can all invoke it without Umbraco-specific DI dependencies.
- Pure backoffice Lit/WC (option a) would require rebuilding the full editor UI as web components — a large scope for V1. A separate admin app (option b) loses discoverability and requires a separate login. The hybrid captures both benefits.

## Non-Negotiables Applied

- No AngularJS in the backoffice extension. Manifest declared per v17 package API. Lit elements only.
- No Surface Controllers anywhere in the workflow path.
- The `workflow-publisher` capability check must live in the projection API layer, not solely in the Lit component (client-side enforcement is not sufficient).

## Surface Mapping Confirmed

| Surface | Host | Auth scheme | Entry DocType |
|---|---|---|---|
| Public | Umbraco TestSite | Anonymous | `workflowLanding` (new) |
| Member | Umbraco TestSite | `PrismMemberCookie` | `workflowPage` / `workflowHub` (existing) |
| Back-stage | MockBusinessApp | Business-app role | `/admin/workflow` (existing) |
| Editor | Umbraco backoffice | Umbraco backoffice login | `prism-workflow-editor` section |

## Priority-1 Prerequisite

Remove stub view files `src/UmbracoPrism.TestSite/Views/workflowPage.cshtml` and `src/UmbracoPrism.TestSite/Views/workflowHub.cshtml`. These currently violate `TestSiteViewModelBindingTests`. No other TestSite editor work should land until this is resolved.

## Full Design

See `docs/design/workflow-editor-v1/03-umbraco-integration.md`.

### 2026-05-16T11:04:11.589+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** The workflow editor should support natural-language, research-backed workflow generation and conversational refinement, including inserting new capabilities like external ID&V into the workflow at the appropriate point.
**Why:** User request — captured for team memory

### 2026-05-16T11:06:16.825+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Use the appropriate tools for the appropriate jobs and avoid reinventing the wheel for the agentic capabilities, potentially leaning on tools like GitHub Copilot where they fit best.
**Why:** User request — captured for team memory

# Workflow editor should be split, service-aware, and AI-safe

**Date:** 2026-05-16T10:59:37.438+01:00  
**Author:** Isabelle  
**Status:** Proposed

## Context

Prism currently proves workflow capability through:

- authored Umbraco workflow pages and hubs
- component-shaped workflow seeds
- a lightweight local Workflow Admin that exposes states, transitions, diagrams, and raw JSON
- backoffice web-component patterns such as sticky tabs, modal sidebars, explicit focus handling, and progressive disclosure

The next editor should move beyond raw JSON editing without hiding the underlying workflow model from advanced users or AI agents.

## Decision

Adopt a **three-layer workflow editor UX**:

1. **Definition library** for browsing, filtering, cloning, comparing, and opening workflows
2. **Workspace editor** with a stage/route canvas, properties inspector, and compact right-rail validation
3. **Simulation + publish layer** for journey replay, validation, diff review, and safe apply/publish

Within the workspace, model the workflow as **connected lanes**:

- **Front stage** — what the public/member user sees
- **Back stage** — what reviewer/system/business-app actors do

Page mapping should be authored in a dedicated **Experience & Routing** panel that starts simple (where does this workflow live?) and progressively reveals advanced ownership/mapping details only when needed.

AI assistance must operate through the same workspace as the human author, using proposal diffs, scoped apply, validation, and replayable change history instead of direct hidden edits.

## Why

- Raw JSON remains useful, but it is too steep as the primary authoring experience.
- Prism workflows already encode meaningful handoffs between citizen-facing states and reviewer/system actions; the editor should make that visible.
- Prism's authored/runtime split is a strength: the UI can keep authoring focused on intent while still surfacing inferred runtime shells and validation.
- Shared human/AI tooling reduces trust risk and keeps debugging understandable.

## Consequences

- New UI work should prefer **progressive disclosure** over a single giant form.
- Validation should be persistent and explain issues in workflow terms ("reviewer action has no actor", "public route has no page mapping"), not just schema terms.
- AI features should be treated as **co-authoring aids**, not autonomous editors.
- JSON/code view should remain available as an advanced/debug surface, but not as the default primary workflow editor.

# Decision: Workflow editor V1 — Authoring UX key decisions

**Date:** 2026-05-16T13:20:33.659+01:00  
**Author:** Isabelle  
**Status:** Proposed  
**Relates to:** `docs/design/workflow-editor-v1/01-authoring-ux.md`

## Decisions Made

### 1. Conversation Pane is the primary agentic surface

The Conversation Pane (bottom of the right rail, below the Step Inspector) is the single surface where NL requests are submitted, agent proposals are rendered as diffs, and provenance history is displayed. There is no separate "AI panel" or modal. This keeps the author in context during review.

**Why:** Agents must surface proposals as reviewable diffs (Tangy's proposal-first model). A persistent pane that is always reachable without a mode switch reduces friction and keeps the authoring context visible.

### 2. Dual-mode graph navigation (visual + linear list)

The graph canvas is accompanied by a parallel Linear List View (`L` to toggle) — a semantic table of the same states and transitions. This is the primary AT-facing surface; screen readers should treat it as the authoritative structure.

**Why:** Graph canvases are notoriously inaccessible to AT. Attempting to make the SVG graph itself the primary screen-reader surface in V1 would require disproportionate engineering for an editor-only tool. The dual-mode pattern delivers WCAG 2.1.1 Keyboard compliance and meaningful screen-reader semantics without blocking V1 delivery.

### 3. Agent proposals are hunk-level, not atomic

`<prism-proposal-diff>` exposes per-hunk accept/reject controls. Authors can accept some changes from a proposal and reject others. "Accept all" is a convenience shortcut.

**Why:** Agentic changes to a workflow graph are rarely perfectly scoped. Authors need granular control to maintain trust in the tool.

### 4. Focus does not move on agent proposal arrival

When an agent proposal arrives, only an ARIA live region (role="status", aria-live="polite") announces it. Focus does not teleport to the Conversation Pane. The author chooses when to review.

**Why:** Unexpected focus movement during background agent activity is a common accessibility failure and a significant usability disruption in a graph editor. The author may be mid-edit.

### 5. Explicit save only in V1

V1 uses explicit Save (toolbar button) with a dirty-state indicator. No autosave.

**Why:** Autosave with in-flight agent proposals creates ambiguous state (did the save include the unreviewed diff?). Explicit save is safe and auditable.

### 6. Stable data-* test hook contract

The `data-testid` and `data-*` attributes listed in `01-authoring-ux.md §10` are treated as a stable public contract. Changing them requires coordination with Tangy. No renaming without updating both the component and the test suite.

## Deferred

- Collaborative multi-user cursors (V2)
- Inline comments on graph nodes (V2)
- Autosave (V2, after agent proposal state model is clarified)
- Undo across accepted agent proposals (needs formal spec before implementation)

# Decision: Workflow editor agentic operating model (restart recommendation)

**Date:** 2026-05-16  
**Author:** Tangy  
**Status:** Proposed

## Decision

Adopt a **proposal-first workflow editing model**.

The workflow editor should stay human-first, but expose a small set of machine-facing surfaces that let agents propose, preview, validate, and apply workflow changes without directly mutating live runtime state.

## Recommended machine-facing surfaces (in order)

1. **Authored workflow source** — the human-editable source of truth for intentful authoring.
2. **Deterministic projected runtime file** — the generated/projected `WorkflowDefinitionFile` contract used by Prism runtime.
3. **Structured diff + provenance artifact** — machine-readable change proposal including rationale, target insertion point, and impacted states/transitions.
4. **Validate command** — fast structural/domain validation (schema, graph, role/action legality, component rules).
5. **Preview/simulate command** — render preview of state graph plus selected end-user/reviewer journeys.
6. **Focused test hooks** — narrow executable-spec entry points for demo workflows such as planning.

## Tool split

- **General agents (for example GitHub Copilot):** natural-language interpretation, drafting proposals, repo edits, orchestration, and invoking validation hooks.
- **Workflow-editor capabilities:** workflow-aware transforms, placement of inserted steps (for example external ID&V at the correct handoff), safe projection, semantic diffing, and previews.
- **Do not** expect a general coding agent to infer workflow graph semantics purely from raw JSON shape.

## Collaboration loop

1. Human asks for a change in natural language.
2. Agent produces a structured proposal/diff.
3. Editor previews the resulting journey/graph.
4. Validation hooks run on the proposal.
5. Human approves.
6. Change is applied and committed/regenerated.

## Executable-spec anchor

Use the planning application journey as the first planning-to-application demo.
It already covers the strongest behavioural contract set in one place: multi-step capture, validation, conditional reveal, check-answers, and completion; it is also the clearest seed for later insertion of reviewer or ID&V stages.

## Guardrails

- No direct agent writes to live instances.
- No UI-only automation as the primary authoring API.
- No hidden mutations without a structured diff/provenance record.
- Keep validation fast and targeted; long-running full-suite checks stay outside the inner authoring loop.

## Repo anchors

- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Components/SeedFileRoundtripTests.cs`
- `src/UmbracoPrism.Core.Tests/WorkflowEngine/WorkflowDefinitionInferenceTests.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/WorkflowTuiService.cs`
- `src/UmbracoPrism.Client/tests/workflow-gds-journey.spec.ts`
- `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts`

# Tangy — Workflow editor agentic validation model

- **Date:** 2026-05-16T11:04:11.589+01:00
- **Author:** Tangy
- **Status:** Proposed

## Decision

Design the workflow editor so every agent change is a **proposal bundle** over an authored workflow model, never an opaque live mutation.

The bundle should contain:

1. authored workflow source
2. projected Prism runtime definition
3. human-readable diff
4. provenance and research notes
5. validation results
6. preview/simulation outputs
7. regression-test results

## Machine-facing interfaces

- Expose an MCP server with scoped tools such as `list_workflows`, `open_workflow`, `propose_change`, `apply_change`, `validate_workflow`, `simulate_workflow`, `preview_route`, and `run_workflow_tests`.
- Keep a durable file contract:
  - authored editor format for human/agent co-authoring
  - projected `WorkflowDefinitionFile` for Prism compatibility
  - proposal/provenance artifact capturing prompt, author, rationale, refs, and checks
- Keep command-line/test harness entry points so agents can generate diffs, run projection, run unit contracts, and execute walkthrough journeys non-interactively.
- Package common generation/refinement workflows as skills so agents can follow the same clarification, proposal, and validation loop every time.

## Validation strategy

- Use one validation engine for manual and agent edits.
- Validate in layers:
  1. authored schema and graph integrity
  2. projection compatibility with Prism runtime contracts
  3. simulation/replay of critical transitions and actor handoffs
  4. end-to-end browser journeys
- Treat the planning application flow as the reference executable spec across all layers.

## Collaboration rule

- Human edits and agent edits must meet in the same review surface: side-by-side diff, validation panel, provenance, and selective apply/reject.
- Record whether a change came from a human edit, generated draft, research synthesis, or targeted follow-up request such as inserting external ID&V after a named decision point.

## Guardrail

- Do not allow agent publication when validation, projection, or executable journey checks are red.
- Do not let research-derived generation skip clarification when policy, jurisdiction, actor ownership, or evidence requirements are ambiguous.

# Decision: Workflow editor V1 agentic surfaces — proposal envelope schema + reuse/build boundary

**Date:** 2026-05-16  
**Author:** Tangy  
**Status:** Proposed  
**Extends:** `.squad/decisions/inbox/tangy-workflow-editor-agentic-restart.md` (canonical operating model)

---

## Decision

Adopt the **proposal envelope** as the atomic unit of all agent-initiated workflow changes, and enforce the reuse/build tool boundary described below.

---

## Proposal Envelope Schema (canonical)

```json
{
  "id": "string (UUID)",
  "createdAt": "ISO 8601 datetime",
  "agent": {
    "kind": "github-copilot | custom-agent | human-assisted",
    "identity": "string",
    "sessionRef": "string (optional)"
  },
  "targetWorkflowId": "string (definitionKey)",
  "rationale": "string (NL summary)",
  "ops": [
    {
      "op": "insert-stage | remove-stage | update-stage | insert-handoff | update-transition",
      "path": "string (JSON Pointer into authored model)",
      "value": { /* authored stage/handoff/transition object */ },
      "before": "string (optional stageKey)",
      "after": "string (optional stageKey)"
    }
  ],
  "placement": {
    "insertAfterStageKey": "string | null",
    "insertBeforeStageKey": "string | null",
    "handoffId": "string | null",
    "transitionId": "string | null"
  },
  "validationResult": {
    "status": "pass | fail | not-run",
    "checkedAt": "ISO 8601 | null",
    "errors": ["string"]
  },
  "previewArtifactRef": "string | null"
}
```

---

## Reuse / Build Boundary (authoritative table)

| Capability | Owner | Rationale |
|---|---|---|
| NL intent capture | Reuse — GitHub Copilot / general LLM | No workflow-domain knowledge required |
| Rationale / NL summary drafting | Reuse — GitHub Copilot / general LLM | Text generation; context injected via structured authored model |
| Repo file edits | Reuse — GitHub Copilot / general LLM | Standard file operations |
| Orchestration (call validate → preview → apply) | Reuse — GitHub Copilot / general LLM | MCP tool invocation |
| Projection (authored source → `WorkflowDefinitionFile`) | Build — workflow-aware | Shell inference, component rules, Prism runtime contract |
| Semantic diffing on Authored Model | Build — workflow-aware | Stage/handoff/actor semantics, not JSON shape |
| Insertion-point resolution | Build — workflow-aware | Graph topology + named handoff points |
| Placement of inserted steps | Build — workflow-aware | Actor ownership, service zone, transition action legality |
| Preview rendering (state graph + journey trace) | Build — workflow-aware | Graph traversal + actor path simulation |
| Structural validation | Build — workflow-aware | Schema, graph, role/action, component rules |
| Focused test hooks | Build — workflow-aware | Test infra wired to authored model + planning spec |

---

## Anti-patterns (never do these)

- General agent inferring workflow graph semantics from raw `WorkflowDefinitionFile` JSON.
- UI-only automation as the primary authoring API for agents.
- Hidden mutations without a proposal envelope.
- Applying a proposal whose `validationResult.status` is `fail` or `not-run`.
- Guessing an ambiguous insertion point — return candidate list instead.

---

## MCP Command Surface (summary)

| Tool | Contract | Latency |
|---|---|---|
| `workflow.draft-proposal` | NL + targetWorkflowId → proposal envelope (ops, placement, rationale) | — |
| `workflow.validate` | envelope or workflowId → validationResult | < 250 ms |
| `workflow.preview` | envelope → state graph + journey trace, populates previewArtifactRef | < 1 s |
| `workflow.apply` | envelopeId + approver → apply ops, re-project, write audit log | synchronous |
| `workflow.diff` | envelopeId → semantic diff (stage-added, stage-removed, handoff-modified) | < 100 ms |

---

## Repo Anchors

- `docs/design/workflow-editor-v1/04-agentic-surfaces.md` — full specification (this decision is a summary)
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs` — runtime target contract
- `src/UmbracoPrism.Core.Tests/WorkflowEngine/WorkflowDefinitionInferenceTests.cs` — shell inference contract under test
- `src/UmbracoPrism.Client/tests/workflow-gds-journey.spec.ts` — existing journey contract to preserve
- `src/UmbracoPrism.Client/tests/agent-loop/planning-workflow-agent-loop.spec.ts` — (to be created) agent-loop behavioural tests

# Tom Nook Decision — Workflow Editor Design

**Date:** 2026-05-16T10:59:37.438+01:00
**Requested by:** Jonny Muir
**Status:** Proposed

## Decision

Use a three-plane architecture for the new workflow editor project:

1. **Authoring plane** — an editor-native workflow graph and page/component authoring model optimised for humans.
2. **Projection plane** — a deterministic compiler/projection layer that emits Prism-compatible workflow definitions and runtime metadata without making the editor itself the runtime.
3. **Agent plane** — structured AI surfaces (MCP/skills/API) for generate, inspect, diff, validate, and test operations against the authored model and its Prism projection.

Use a **planning application** as the reference end-to-end demo because it exercises public initiation, optional member continuation, role-based back-stage handling, and richer service-design complexity than the current lightweight demos.

## Why

The current repo already separates concerns in a useful way: Umbraco owns content routes and page shells, Prism owns secure rendering and form handling, and the Mock Business App owns workflow definitions, transitions, and state advancement. The new editor should preserve that separation instead of coupling authoring directly to live runtime execution.

The existing `/admin/workflow` experience is valuable as a developer inspection panel, but it is JSON-first and instance-first. It is not a durable product direction for human-friendly workflow design, collaboration, or AI-assisted change control.

## Architecture guidance

- Treat the Prism-compatible `WorkflowDefinitionFile` shape as a **runtime target contract**, not the editor's primary internal model.
- Preserve current shell inference rules by projecting component shapes that continue to drive `question`, `check-answers`, `confirmation`, `task-list`, `waiting`, and `status-timeline` rendering.
- Keep Prism workflow pages as content-owned shells, with separate public/member/business-app surfaces mapped onto the same underlying case model where appropriate.
- Require agent operations to go through a structured contract (draft, explain, diff, validate, test) rather than direct live mutation of runtime instances.

## Immediate implementation implication

The first implementation wave should define the editor domain model, projection rules, and planning-application reference workflow before building rich UI affordances.

# Tom Nook Decision — Workflow Editor V1 Spine

**Date:** 2026-05-16
**Requested by:** Jonny Muir
**Status:** Proposed
**Artifact:** `docs/design/workflow-editor-v1/README.md`

## Decision

Adopt the V1 spine document as the connective tissue for the four specialist sections (Isabelle, Blathers, Brewster, Tangy). The spine fixes shared vocabulary, the three-plane architecture, the planning-application reference, the end-to-end walkthrough, and the cross-cutting contracts between planes.

## V1 invariants

1. **Three planes** — Authoring (human-first stage model) / Projection (deterministic compiler) / Agent (proposal-first AI surface). Independent products, stable contracts between them.
2. **Runtime contract untouched.** `WorkflowDefinitionFile` is the projection target; shell inference via `PrismComponentExtensions.InferStepType()` is preserved; `WorkflowResponseEnvelope`, nonce, antiforgery and claims behaviour are unchanged.
3. **Authored model ≠ runtime contract.** Authors design stages, actors, handoffs, views, waiting, deadlines. The runtime sees projected states/transitions/components.
4. **Deterministic projection.** Pure function; same input → byte-identical seed; unknown fields rejected; total over stages and handoffs; structured diagnostics (no exceptions across the boundary).
5. **Proposal-first agent loop.** Every AI change is a structured bundle (authored diff + projected diff + rationale + target insertion point + validation + preview + provenance). No live-instance writes.
6. **Reuse, don't reinvent.** General NL/drafting/orchestration lean on GitHub Copilot. Workflow-specific MCP tools exist only for workflow-aware transforms, safe projection, semantic diffing, simulation, and previews.
7. **NL + conversational refinement are first-class.** "Generate a workflow for X" and "insert external ID&V after declaration" both route through the same proposal/validate/preview/approve loop human edits use. Refinements are layered proposals, not hidden mutations.
8. **Planning application** is the V1 reference demo; the external ID&V insertion is the canonical agent scenario.
9. **Topology.** Workflow authoring lives in the Business App; Umbraco keeps public/member shells; a thin v17 backoffice extension links/embeds — it does not re-implement.

## Cross-cutting contracts (normative)

- **Authoring → Projection:** authored JSON shape with `definitionKey`, `actors`, `stages`, `handoffs`, `policies`; pure deterministic projection; structured diagnostics.
- **Projection → Runtime:** must emit valid `WorkflowDefinitionFile`; no authored `stepType`; existing shell families authoritative; operational truth (case status, assignments, evidence, ID&V records) stays in case/domain persistence.
- **Agent ↔ Authoring:** proposal artifact with prompt, author, target insertion point, authored + projected diffs, rationale, references, validation results, preview, timestamp. No agent applies a red proposal. No agent invents workflow semantics from raw JSON.
- **Repo layout:** authored sources under `src/UmbracoPrism.MockBusinessApp/workflow-authoring/<key>/`; projected seeds under `workflow-seeds/<key>.json` (generated artifacts under VCS); projector library under `src/UmbracoPrism.Shared/Workflow/Projection/`; MCP server + proposals under `src/UmbracoPrism.MockBusinessApp/workflow-agent/` and `.proposals/`.

## Deferred to V2

Versioning / lifecycle / rollback; in-flight instance migration; multi-tenant authoring; collaborative real-time editing; operator backstage UI contract; permission expressiveness; routing authoring depth; task-list authoring control; agent autonomy ceiling; cross-workflow refactors.

## Routing

- Isabelle: own `01-authoring-ux.md` within the §6.1 / §6.3 contracts.
- Blathers: own `02-runtime-projection.md`; §6.1 + §6.2 contracts are yours to enforce.
- Brewster: own `03-umbraco-integration.md` within the §6.4 repo-layout contract.
- Tangy: own `04-agentic-surfaces.md` within the §6.3 proposal contract.

Any change that crosses a plane boundary comes back to the spine.

## 2026-05-16

### Planning Workflow Editor Walkthrough — Blockers (Tangy diagnostic)

- **Date:** 2026-05-16
- **Author:** Tangy
- **Status:** BLOCKED — do not remove `test.skip` until all items below are resolved
- **PR:** #52 (`squad/planning-workflow-editor-walkthrough`)

#### Summary

`planning-workflow-editor.walkthrough.spec.ts` cannot be activated. The `test.skip(true, ...)` remains. The following five blockers must be resolved before Tangy can land the spec.

#### Blocker 1 — `workflow-editor.html` not served by MockBusinessApp

The spec navigates to `https://localhost:7245/workflow-editor.html`. `MockBusinessApp/Program.cs` has no `UseStaticFiles()` call and no `MapGet("/workflow-editor.html", ...)` route. The Vite build output lives at `src/UmbracoPrism.Core/wwwroot/dist/workflow-editor.html` but is never mounted.

**Owner: Isabelle or Blathers.** Add `app.UseStaticFiles(...)` to `MockBusinessApp/Program.cs` with a `PhysicalFileProvider` pointing at `UmbracoPrism.Core/wwwroot/dist/`, or add an explicit `MapGet` endpoint.

#### Blocker 2 — TypeScript schema ≠ C# schema (crash-level)

`prism-workflow-graph.ts:128` — `stage.exits.length > 0`  
`prism-step-inspector.ts:36` — `stage.views.some(...)`

Both accesses are unguarded. The C# `AuthoredStage` model has no `exits` and no `views` properties. When the GET endpoint returns C# JSON, the components throw during render.

**Owner: Isabelle.** Add `?.` guards (or `?? []` fallbacks) on every `stage.exits` and `stage.views` access in both components, OR define the GET endpoint to return TypeScript-schema JSON.

#### Blocker 3 — Mock drafter emits C#-incompatible stage shape

`workflow-authoring-mock-drafter.ts` creates the new `id-verification` stage with `kind: 'Capture'` and `views`/`exits` in TypeScript format. The C# `JsonStringEnumConverter` throws on `"Capture"` (not in `StageKind` enum: `Question|CheckAnswers|Confirmation|TaskList|Waiting|StatusTimeline`). `PatchService.ApplyInsertStage` returns diagnostic `PATCH002` and no save occurs. The stage never appears in the graph.

**Owner: Tangy + Isabelle joint.** Align mock drafter output to C# schema: use `kind: 'Question'`, remove `views`/`exits`, use `fromStage`/`toStage` in transition ops. Also fix the `_applyProposalLocally` guard — currently requires `op.before` to be truthy but mock drafter sets it `undefined` when no `submitted` stage exists.

#### Blocker 4 — `applyProposal` client sends wrong body format

`workflow-authoring-client.ts` sends `JSON.stringify(proposal)` (raw `ProposalEnvelope`). The C# `/apply` endpoint expects `ApplyWorkflowRequest { Envelope: ProposalEnvelope, Approver: string }`. The server receives a body where `Envelope` is null → HTTP 400 → component falls back to local apply → re-fetches → reverts.

**Owner: Tangy** (clear client bug, unblocked now).  
Fix: `body: JSON.stringify({ envelope: proposal, approver: 'walkthrough' })`.

#### Blocker 5 — No planning workflow seed in the authoring store

`MockBusinessApp/workflow-authored/` does not exist. `GET /api/workflow-authoring/workflows/planning` returns 404. The component renders an error banner; heading shows "Workflow Editor", not "Planning Permission" → spec health check fails.

**Owner: Blathers.** Create `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json` with `displayName: "Planning Permission Application"` (satisfies `/planning permission/i`) and a stage `applicant-details` (satisfies `[data-prism-stage="applicant-details"]`). Use C# `AuthoredWorkflow` JSON format.

#### Changes Tangy will make once blockers 1–5 are resolved

1. Remove `test.skip(true, ...)`
2. Fix `waitForRequest` for proposals: `POST .../workflows/planning/preview` (not `.../proposals`)
3. Fix `waitForRequest` for accept: `POST .../workflows/planning/apply` (not `PATCH .../planning-permission`)
4. Fix stage key assertion: `[data-prism-stage="id-verification"]` (not `identity-verification`)
5. Fix `applyProposal` body (Blocker 4 above)

#### Resolution order

| Step | Owner | Action |
|------|-------|--------|
| 1 | Blathers or Isabelle | `UseStaticFiles` in MockBusinessApp |
| 2 | Blathers | `workflow-authored/planning.workflow.json` seed |
| 3 | Isabelle | `?.` guards on `stage.exits` / `stage.views` |
| 4 | Isabelle / Tangy | Align mock drafter to C# schema |
| 5 | Tangy | Fix client body, fix spec assertions, remove skip |

---

### Authored Workflow V1 Foundation — Namespace, Fixture Format, and Projection Contract

- **Date:** 2026-05-16T17:47:42.605+01:00
- **Author:** Blathers
- **Status:** IMPLEMENTED
- **Commit:** `24374f2`

#### Context

Implementing the V1 authored workflow model and deterministic projection slice as scoped in the `feat(core)` task. Several team-relevant decisions were made during implementation.

#### Decisions

##### 1. Namespace and Directory Layout

Authored types live in `src/UmbracoPrism.Core/Workflow/Authoring/` under namespace `UmbracoPrism.Core.Workflow.Authoring`. This isolates the authoring plane from the runtime types in `UmbracoPrism.Shared.Models.Workflow` — no cross-contamination.

The store reads from a configurable `basePath`, defaulting to `workflow-authored/*.workflow.json` (as per the decisions.md spine). Tests use the test-project fixture path directly.

##### 2. StageKind Enum Values

`StageKind` uses PascalCase values (`Question`, `CheckAnswers`, `Confirmation`, `TaskList`, `Waiting`, `StatusTimeline`) serialized as strings via `[JsonConverter(typeof(JsonStringEnumConverter))]`. JSON authored files use PascalCase (e.g. `"kind": "Question"`). This keeps C# idiomatic without a custom naming policy.

`StatusTimeline` is an explicit alias for `Waiting` — both emit a `WaitingComponent` and both infer "status-timeline" via `InferStepType`. Agents can use either; the projector normalises both to the same output.

##### 3. FieldType Enum

`FieldType` covers: `Text`, `Number`, `Decimal`, `Email`, `Date`, `Textarea`, `Boolean`, `Select`, `Radios`, `Checkboxes`. Each maps to a concrete `InputComponent` subtype. Unknown types fall back to `TextInputComponent`.

##### 4. Canonical JSON Options (Lock)

`WorkflowProjector.CanonicalOptions` is a public static `JsonSerializerOptions`:
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `WriteIndented = false`
- `DefaultIgnoreCondition = JsonIgnoreCondition.Never` (nulls explicit, no ambiguity)
- `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`

Checksum = SHA-256 of these bytes, hex-encoded lowercase.

This is intentionally different from the round-trip read options (which use `PropertyNameCaseInsensitive = true`). Canonical = write side only.

##### 5. Check-Answers Component Population

When projecting a `CheckAnswers` stage, the `SummaryListComponent.Children` are populated from all `Question`-kind stages in the workflow, sorted by `StageKey` then `FieldKey` (both ordinal). This is V1 behaviour. V2 should allow explicit field refs per check-answers stage.

##### 6. Fixture Format (Source of Truth for Tangy)

`src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json` is the canonical planning workflow fixture. Four stages: `declaration`, `application-form`, `check-answers`, `submitted`. Copied to output via `<None CopyToOutputDirectory="PreserveNewest" />` in the test project `.csproj`. Tangy's tests consume this file from `AppContext.BaseDirectory/Workflow/Authoring/Fixtures/planning.workflow.json`.

#### Impact

- CI can now project and verify `planning.workflow.json` and any future `*.workflow.json` authored files.
- `IAuthoredWorkflowStore` is the extension point for multi-tenant or database-backed stores in future waves.
- Patch service, Preview service, HTTP API, and Umbraco wiring are explicitly out of scope for this slice.
