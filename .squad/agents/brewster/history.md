# Brewster — History



## 📋 Summary

File has been trimmed to recent entries for readability. Complete history available in git history.

---

## Learnings (2026-04-14 — Umbraco v17 solution review)

- **Prism is strongest when Umbraco owns the route and authored page shell while the business app owns workflow state.** The repo's best-aligned path is the `workflowPage`/`workflowHub` pattern: route-hijacked pages, protected member access, server-rendered Razor, and content-resolved navigation (`src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs`, `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs`, `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`).
- **The current workflow/member journey is close to idiomatic v17, but its document-type design is still thinner than a reference Umbraco site.** `PrismContentTypeSeeder` creates minimal types and templates, yet does not model site structure/editor affordances such as richer page fields or explicit child relationships; `workflowPage` is allowed as root and is seeded as its own root node, while `workflowHub` is intended under Home (`src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`, `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs`).
- **The main Umbraco-specific risks are drift away from strongly typed route hijacking and content-owned routing.** The route-hijack controllers currently omit `[ModelType("alias")]`, `MemberDashboardController` hardcodes `/dashboard` and bypasses `CurrentTemplate()` with a direct view path, and `HomePage.cshtml` is untyped while reading a raw `cardImage` alias that is not present on the generated model (`src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs`, `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`, `src/UmbracoPrism.TestSite/umbraco/models/HomePage.generated.cs`).
- **The backoffice dashboard uses the correct Umbraco 17 extension stack, but its information architecture is muddled.** The package manifest is v17-native (manifest JSON + Lit/UUI web components), yet it registers a custom `Prism.Section` while the actual dashboard is conditioned into `Umb.Section.Content`, so the custom section is effectively unused (`src/UmbracoPrism.Core/wwwroot/umbraco-package.json`, `src/UmbracoPrism.Client/src/backoffice/index.ts`).
- **There is unfinished workflow-demo surface area that should not be treated as the canonical Umbraco pattern.** `workflowDemoPage` and `Views/WorkflowDemoPage.cshtml` still point at a placeholder `prism-workflow-shell` bundle that does not exist in the client assets, and the instance-picker UI is present in Razor but never activated by controller logic (`src/UmbracoPrism.TestSite/Views/WorkflowDemoPage.cshtml`, `src/UmbracoPrism.TestSite/Views/Partials/_WorkflowHub-InstancePicker.cshtml`).


### Follow-up

Blathers spawned to fix restart-only downstream API failure; Tangy to validate after fix.


## Tasks — 2026-04-13 — Dashboard Route Contract Validation (parallel spawn batch)

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T23:42:20Z-brewster.md`

**Spawned:** Brewster, Blathers, Tangy for parallel investigation of dashboard redirect behavior

**Task Summary:**
- Brewster: Confirm `/dashboard` route validity and auth challenge behavior ✅


## Learnings (2026-04-14 — Dashboard home-bounce diagnosis)

- **A signed-in bounce back to `/` is more likely a home-owned auth entry point than broken Umbraco dashboard routing.** In this repo the dashboard CTA already resolves from published content to `/dashboard`, and the known `/ -> login -> /` loop comes from the unauthenticated home-page `Sign In` link omitting a `returnUrl`, which makes `AccountController.Login` and the OIDC callback fall back to `/`.
- **For member-area CTAs on public TestSite pages, carry the authored target into the login link.** `Views/HomePage.cshtml` should build `/auth/login?returnUrl={dashboardUrl}` from the same content-resolved dashboard URL it shows after sign-in, so the first successful login lands on the intended member page instead of the ambiguous signed-in home page.
- Blathers: Inspect auth/session redirect flow ⏳
- Tangy: Complete dashboard navigation trace and identify test readiness signals ✅

**Brewster Findings:**
- `/dashboard` is a valid published route with correct auth challenge behavior
- Unauthenticated requests correctly redirect to `/auth/login?ReturnUrl=%2Fdashboard`
- App-side route wiring is sound
- Route contract is valid; redirect behavior is login flow specific

**Decision Merged:** Consolidated Brewster and Tangy findings into `.squad/decisions.md` section "📌 2026-04-13: Brewster — Dashboard Route Contract" with sub-section "Tangy — Dashboard navigation trace"


## Learnings (2026-04-14 — Classifying transient seeded child routes)

- **A seeded child briefly resolving to `/` on first boot is not normal steady-state Umbraco behaviour; it is mainly a cold-start convergence artefact of this app's runtime pattern.** In this repo we intentionally boot against a reset isolated runtime DB, run unattended install, then publish the demo tree in `WorkflowPageSeeder` on `UmbracoApplicationStartedNotification` while Razor immediately resolves links from `ContentAtRoot()` + published `Url()`. That combination can expose a short window where the node exists in published discovery before Umbraco has finished computing the final hierarchical child path.
- **So the right classification is "Umbraco can transiently do this during startup, but our seeding/runtime design is what makes it visible and user-facing."** A warm, already-settled Umbraco site should not keep returning `/` for a valid child page; our development-only reset/seeding flow and eager route consumption are the primary reasons the wrong-route symptom shows up here.


## Learnings (2026-04-14 — Route-readiness strategy for cold boots)

- **The test harness should wait for the seeded route contract, not for page copy.** In this repo the authoritative startup signal is `GET /api/prism/downstream-demo/seed-contract-ready` returning `ready: true` / `routeContractReady: true`, with the home-page `data-prism-home-ready="true"` marker acting only as a smoke check that the real Razor site is serving.
- **Behaviour tests should never absorb cold-start convergence quirks into their assertions.** Once readiness says the contract is settled, tests should require the authored URLs and expected auth challenge targets (`/dashboard`, `/get-in-touch`, `/my-workflows`), rather than tolerating a transient `/` fallback that only exists during fresh-runtime bootstrapping.


## Learnings (2026-04-14 — Auth cookie redirect leakage and seeded routes)

- **Do not persist the one-off OIDC post-login `RedirectUri` inside `PrismMemberCookie`.** In this repo, storing `/dashboard` on the auth ticket let later protected requests such as `/my-workflows` collapse back to the previous login target even after `seed-contract-ready` reported the authored route contract as settled. Capture the return target for the immediate `/signin-oidc` redirect, then clear `AuthenticationProperties.RedirectUri` before issuing the long-lived member cookie.
- **A seeded-route readiness probe can be truly correct while a persisted auth redirect still falsifies later browser navigation.** `GET /api/prism/downstream-demo/seed-contract-ready` remained authoritative for Umbraco route convergence, but the browser could still be bounced from `/my-workflows` to `/dashboard` until the auth cookie stopped carrying stale redirect state. Treat that as a separate auth-session leak layered on top of the startup contract, not as proof the seed probe is wrong.


## Learnings (2026-04-14 — Restart auth recovery and offline_access scope strategy)

- **The restart auth recovery was already implemented correctly in working-tree changes.** The fix required three coordinated pieces: (1) PrismContext.ShouldRefreshForRuntimeRestart() detects when IssuedUtc < ProcessStartedUtc and forces a token refresh, (2) PrismOidcConfiguration.GetRefreshScope() returns null for the localhost demo tenant (signaling "omit scope parameter, use original scopes from initial login"), and (3) PrismOidcConfiguration.OnAuthorizationCodeReceived sets IssuedUtc = DateTimeOffset.UtcNow on the auth properties before persisting the cookie, ensuring future runtimes can detect the pre-restart session.
- **Generic OIDC tenants should NOT request offline_access by default.** The repo-owned localhost demo (localhost:8443/realms/prism-dev) is special-cased to request "openid profile offline_access" for restart-tolerant demos, but other generic OIDC tenants default to "openid profile" only (standard browser session scopes). This prevents production tenants from accidentally requesting long-lived refresh tokens without explicit product requirements and provider-side authorization.
- **Keycloak refresh token calls should omit the scope parameter entirely when using tokens issued with offline_access.** When the initial login included offline_access, the refresh_token grant should not restate scopes — Keycloak uses the original scopes bound to that refresh token. Sending scope=openid profile on refresh (without offline_access) can cause Keycloak to reject the call. The correct fix is GetRefreshScope() returning null for localhost demo, which PrismContext converts to an empty string, which then skips adding scope to the form parameters.
- **Pre-existing Phase1SecurityRegressionTests failures are unrelated to this work.** The AccountController_Login_RejectsExternalRedirect tests expect an InvalidOperationException to be thrown when calling LocalRedirect() with an external URL, but the test setup creates an unauthenticated principal, so the controller returns Challenge() instead of entering the LocalRedirect() branch. These tests were failing before the working-tree changes and remain failing after — they need separate investigation/correction.
- **The full localhost auth suite (8 tests) now passes, including the restart test.** All Playwright contracts pass: sign-in flow, API call, My Workflows navigation, seeded workflow page, dashboard navigation, restart + API call, sign-out, and restart + sign-out. The Core unit tests (PrismContextTests, PrismOidcConfigurationTests) also pass (26 tests total).



## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Restart auth recovery: runtime restart detection combined with refresh-token scope strategy
- Umbraco startup contract: established dashboard route contract and verified seeded TestSite availability
- Auth redirect state management: stripped transient post-login redirect targets from persisted member cookie

**Key Outcomes:**
- PrismContext.ShouldRefreshForRuntimeRestart() detects pre-restart sessions via AuthenticationProperties.IssuedUtc
- Localhost demo tenant requests openid profile offline_access for long-lived refresh tokens
- Other OIDC tenants request only openid profile unless explicitly configured
- IssuedUtc = DateTimeOffset.UtcNow on cookie creation ensures fresh restart detection timestamp
- All 8 Playwright localhost auth tests pass; all 26 Core unit tests pass

**Route Contract Improvements:**
- Keep published Umbraco routes as single source of truth
- Do not persist transient post-login AuthenticationProperties.RedirectUri into long-lived member cookie
- Treat route readiness and persisted auth redirect state as separate debugging layers

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-brewster.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Separation of concerns between cold-start route readiness and authenticated session redirect state improves maintainability.

## 2026-04-14T20:24:57Z: Umbraco 17 Specialist Review — Architecture Assessment

**Session:** Umbraco 17 specialist review of workflow pages, supporting components, and dashboard (parallel with Tom Nook)

**Work Performed:**
1. Assessed route-hijacked controller pattern against Umbraco 17 idioms
2. Identified missing `[ModelType]` attributes and untyped models
3. Found hardcoded dashboard routing and direct view-path workarounds
4. Reviewed document type design and backoffice extension architecture
5. Classified unfinished demo surfaces
6. Ranked follow-up technical debt

**Key Findings:**
- ✅ Route-hijacked pattern is correct (strong Umbraco 17 fit)
- ⚠️ Missing typed models and ModelType attributes
- ⚠️ Hardcoded `/dashboard` route needs CurrentTemplate() refactor
- ⚠️ Document types are skeletal; content IA needs enrichment
- ⚠️ Backoffice dashboard has unused custom section registration
- ⚠️ Demo surface (`workflowDemoPage`) unfinished

**Highest-Value Follow-ups (Ranked):**
1. Add `[ModelType]` to route hijackers
2. Fix `HomePage.cshtml` type mapping and unmapped aliases
3. Refactor `MemberDashboardController` to use `CurrentTemplate()`
4. Complete or mark `workflowDemoPage` as in-progress
5. Hide unused backoffice section
6. Model richer page fields in content seeder

**Outcome:**
- Findings synthesized for team consensus
- Pattern documented for future workflow work
- Debt prioritized by impact
- ✅ Review complete: `.squad/orchestration-log/2026-04-14T20:24:57Z-brewster.md`

**Status:** Specialist review phase complete; awaiting Tom Nook architectural fit decision and team prioritization
