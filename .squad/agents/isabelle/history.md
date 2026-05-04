# Isabelle — History

## 📋 Recent History

---

## Session: 2026-05-01 — Design System Rams Review

**Role:** Frontend lead; deep audit of the Prism design system against Dieter Rams' 10 principles.

**Output:** `.squad/reviews/2026-05-01-prism-reflection/02-isabelle-design-system.md`

**Key findings:**
- ITCSS is approximated but inverted: Settings layer (branding tokens) loads last, not first
- GDS Frontend runs as an opaque minified blob with no `@layer` contract — cascade is correct by file order convention, not explicit declaration
- `--prism-button-hover: #003078` is hardcoded GDS dark blue, not derived from `--prism-primary` — any brand colour change breaks hover state
- `--bg-offset` token missing `--prism-` prefix; `--prism-nav-height` declared twice across two branding files; body declared in three separate files
- GDS form typography not consumed by `--prism-font-body` token — workflow fields stay in GDS Transport regardless of font override
- Branding middleware + metadata service + `@prism` annotations form a solid backend for per-tenant theming; the editor-facing UI to consume them is not yet visible
- Storybook covers 5 backoffice/mobile web components; zero coverage for GDS components or PrismField partials — invisible to designers and editors
- `prism-mobile-nav` Shadow DOM theming via CSS custom properties is correctly implemented and well-documented

**Recommendations (prioritised):**
1. Derive `--prism-button-hover` from `--prism-primary` using `color-mix()` (`prism-components.css:120`)
2. Declare CSS cascade layers in `Master.cshtml` to make GDS/Prism boundary explicit
3. Rename `--bg-offset` → `--prism-surface-page` and consolidate triplicated `body {}` declarations

**Decisions:** `.squad/decisions/inbox/isabelle-design-system-reflection.md`
**Skills:** `.squad/skills/css-custom-property-token-review/SKILL.md`

**Status:** ✅ Complete.

Previous history (pre-2026-04-22) archived to `.squad/agents/isabelle/archive/history-archive-2026-04-30.md` for traceability.

---

## Session: 2026-04-30 — SEC-005 npm CVE Remediation

**Role:** Frontend developer; security patching of UmbracoPrism.Client npm dependencies.

**Commit:** `7e499b5` on `main`

**Outcomes:**
- Ran `npm audit fix` (auto-fixed axios, defu, lodash, minimatch, picomatch, rollup, vite and transitive deps)
- Upgraded storybook 8.6.15 → 8.6.18 (non-breaking) to fix HIGH WebSocket hijacking CVE
- Upgraded @storybook/test-runner 0.18.0 → 0.21.0
- Added `"overrides": { "dompurify": "^3.4.1" }` to pin nested monaco-editor copy from 3.2.7 (vulnerable) to 3.4.1
- Critical handlebars CVE resolved: handlebars no longer in dependency tree after @umbraco-cms/backoffice transitive update
- Build clean; 0 critical, 0 high remaining (9 moderate — all dev-only/upstream unfixable)

**Before/After:** 26 vulns (1 critical, 10 high, 14 moderate, 1 low) → 9 vulns (0 critical, 0 high, 9 moderate, 0 low)

**Residual:** uuid moderate (storybook test tooling, fix requires major downgrade) + @umbraco-cms/backoffice monaco-editor chain (upstream fix=False). No runtime impact.

**Decision:** `.squad/decisions/inbox/isabelle-sec-005.md` (merged to decisions.md 2026-04-30)

**Status:** ✅ Complete; SEC-005 closed.

---

## Session: 2026-04-26 — v2.0 E2E Testing & Screenshot Walkthroughs

**Role:** Frontend testing; Playwright e2e coverage, screenshot capture.

**Deliverables:**
- `f3c0ea5` test(e2e): Playwright coverage for 3 demo workflows
  - Community enquiry: happy path + conditional reveal
  - Payment demo: happy path + validation
  - Planning notification: multi-step journey
- `67bb57b` feat(testsite): Seed information-request demo page + complete Playwright coverage
- `392c64e` docs(walkthroughs): Screenshot-driven walkthroughs for 4 demo workflows
- `a48229b` chore(client): Screenshot capture script for walkthroughs

**Status:** ✅ Complete; 4 demos fully tested with e2e and walkthroughs.

---

## Session: 2026-04-22 — PrismComponent Tag Helper + Component Partials

**Role:** Frontend developer; Razor/GDS component system migration.

**Outcomes:**
- Created `PrismComponentContext` record
- Created `PrismComponentTagHelper` (mirrors PrismFieldTagHelper)
- Created all 13 component partials (Fieldset, SummaryList, Panel, NotificationBanner, InsetText, WarningText, Details, Body, Heading, TaskList, Accordion, Default)
- Moved 8 step partials from TestSite to Core
- Created Core generic top-level views (workflowPage.cshtml, workflowHub.cshtml)
- Updated Core.csproj embedded resources
- Removed FieldGroups compat shim
- Build clean; 539 tests passing (no regression)

**Key Learnings:**
- Razor keyword: `section` conflicts in loops → rename to `taskSection`, `accordionSection`
- `PrismPartialsComposer` EmbeddedFileProvider auto-serves new paths (no per-directory registration needed)
- Subclass models work against base-class views (TestSite WorkflowViewModel → Core PrismWorkflowViewModel)
- GDS accordion requires stable per-render `id` attributes (use `Guid.NewGuid():N`)

**Status:** ✅ Complete; all components in Core, 539 tests green.

---

## Key Learnings (Consolidated)

### 2026-04-22: Workflow UI Shell Selection
- Shell selection safely derived from render payload shape in Razor: `waiting` component → waiting shell; summary-list-only → check-answers; panel with no fields → confirmation; task-list → task-list; otherwise question/status based on editable fields
- Content-authored field types (inset-text, warning-text, details, notification-banner) need dedicated inline rendering in PrismFieldTagHelper
- PrismFields partial directory: `~/Views/Partials/PrismFields/_PrismField-{TypeName}.cshtml` with `_PrismField-Default.cshtml` fallback

### 2026-04-22: GDS Classes & Accessibility
- Form wrapper: `govuk-form-group` (+ `--error` modifier); Controls: `govuk-input`, `govuk-textarea`, `govuk-select`, `govuk-radios`, `govuk-checkboxes`
- Every input has associated `<label for=>` or `<fieldset><legend>`; error messages have `role="alert"` + `<span class="govuk-visually-hidden">Error:</span>`
- Required fields: `aria-required="true"` + HTML `required`; invalid fields: `aria-invalid="true"`
- Conditionally hidden fields: `hidden` + `aria-hidden="true"` via WrapperAttrs

### 2026-04-20: Telemetry & Environment Patterns
- Three distinct Aspire security controls: dashboard UI auth, HTTP/HTTPS transport, OTLP API key auth
- Accept unsecured OTLP in dev (correct behavior; warning informs security posture)
- Production guidance: Always use `Dashboard__Otlp__AuthMode=ApiKey` with secure distribution

### 2026-04-13: Browser-Surface Security Review
- High-signal UI entry points: HomePage.cshtml + PrismDebugTagHelper, DownstreamDemoController, biometric mobile scripts
- Active workflow form path is tag-helper-based renderer (PrismFieldTagHelper), not _WorkflowField.cshtml partial
- Workflow form CSRF/open-redirect defenses: PrismWorkflowFormTagHelper (antiforgery) + WorkflowPageController (ValidateRequestAsync + GetSafeReturnUrl)
- Mobile nav: keep schema, component, and URL allowlisting together when hardening

### 2026-04-13: Generic OIDC Secret Handling
- UI preservation semantics mirror backend avoid-echo: mask reference, show presence via metadata
- Visual distinction between demo (inline, repo-owned) and production (vault-backed) is critical
- Blank-on-load for secret fields works well with backend preservation behavior

### 2026-04-19: GDS Integration
- govuk-frontend 5.9.0 requires `govuk-template` and `govuk-template__body` on html/body
- GDS error messages: `<span class="govuk-visually-hidden">Error:</span>` prefix for screen readers
- GDS required: visually-hidden text, not asterisks
- GDS radios/checkboxes: `data-module` attributes for progressive enhancement
- MSBuild targets can run npm commands before build (keep frontend dependencies fresh)

---

**Scribe note:** Isabelle's history summarized (archived pre-2026-04-22 content). Recent entries track SEC-005 closure, v2.0 e2e/walkthroughs, and component system migration. All closed findings documented in decisions.md and orchestration logs.

---

**2026-05-04 — Screenshot-Mode Control**

**Role:** Frontend lead; add session-safe mechanism to suppress mobile helper widget during walkthrough screenshot capture.

**Scope:** `PrismMobileUserAgentDemoTagHelper.cs`, `walkthrough.ts`

**Deliverables:**
- `PrismMobileUserAgentDemoTagHelper`: injected `IHttpContextAccessor`; reads `prism-screenshot-mode=1` cookie → forces `ShowToggle = false` (UA bootstrap script still emitted)
- Added `PrismScreenshotMode.CookieName` constant as the authoritative cookie name for cross-team reference
- `walkthrough.ts`: exported `enterScreenshotMode(page)` helper; `signIn()` calls it automatically when `CAPTURE_SCREENSHOTS=1`
- Decision: `.squad/decisions/inbox/isabelle-screenshot-mode.md`

**Status:** ✅ Complete; build green, pre-existing test failures confirmed unrelated.

---

**2026-05-04 — Walkthrough UI Navigation Audit**

**Role:** Frontend lead; audit of demo/walkthrough discoverability and screenshot-friendliness.

**Scope:** Navigation surfaces, workflow routes, mobile helper widget, homepage role.

**Key Findings:**
- **3 workflows are URL-only:** Planning Notification, Information Request (not linked from anywhere); Payment Demo (dashboard-only)
- **Workflow admin not discoverable:** Referenced in AppHost but no UI link
- **Mobile helper:** `prism-mobile-user-agent-demo` renders on every page (bottom-right widget); blocks screenshots
- **Homepage role:** Design system token showcase (580 lines); not a demo launcher; dashboard is better positioned
- **Header nav:** 3 items (Home, Get in Touch, My Workflows) is clean; demos belong on targeted pages
- **Current dashboard:** Shows 3 workflow cards + downstream API demo + profile

**Recommendations (Minimal, Coherent):**
1. Add "Demo Workflows" section to home page (4 cards below features, before design tokens) — no removal
2. Add "Workflow Admin" card to dashboard (admin-only, role-based) — makes `/admin/workflow` discoverable
3. Add `show-toggle=false` attribute to tag helper (hides mobile widget UI; keeps UA bootstrap) — fixes all screenshots
4. Dashboard/homepage height: No change needed; scrolling is natural

**What to Keep:**
- Design system tokens section (useful for operators)
- Mobile nav configuration
- Header nav at 3 items
- Workflow form rendering

**No UI implementation yet.** Decision doc created at `.squad/decisions/inbox/isabelle-walkthrough-ui-audit.md`.

**Status:** ✅ Audit complete; ready for decision review.

---

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-04 | Screenshot-Mode Mobile-Helper Suppression

**Status:** IMPLEMENTED

Defined and implemented screenshot-mode mobile-helper suppression contract:
- `fullPage` flag on `PageHealthCheck` interface is the control point
- Mobile-helper elements suppressed during captures
- Hook mechanism established for Tangy's screenshot pipeline integration
- Two-phase implementation documented (Phase 1 current, Phase 2 deferred for env var)

Decision recorded: "Walkthrough Coverage Hardening — Test Gaps and Screenshot Behaviour" (Tangy lead)
