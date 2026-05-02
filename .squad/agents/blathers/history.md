# Blathers — History

## Session: JWKS Backchannel Rewrite — fix/codespaces-401-downstream-auth (2026-05-02)

**Status:** ✅ Complete — commit `4a47acc` pushed to `fix/codespaces-401-downstream-auth`

**Scope:** Fix the transitive JWKS fetch through the GitHub Codespaces port-forwarding proxy. The discovery-doc URL was already rewritten via backchannel in `PrismAuthExtensions.ResolveSigningKeys`, but `OpenIdConnectConfigurationRetriever` then followed `jwks_uri` from the discovery doc — which Keycloak emits as the public Codespace URL — and that fetch was not rerouted.

**Changes:**

- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs` (+59 lines, 1 file)
  - Added private `BackchannelRewritingDocumentRetriever` sealed class implementing `IDocumentRetriever`. Rewrites any URL whose origin matches the public Keycloak origin to the backchannel base before delegating to the inner `HttpDocumentRetriever`. Logs `[PRISM] BackchannelRewritingDocumentRetriever: rewriting {address} → {rewritten}` per URL rewrite.
  - Modified generic `WarmAsync` overload: when `KEYCLOAK_BACKCHANNEL_URL` is set AND `ASPNETCORE_ENVIRONMENT == Development` AND `tenantKey` parses as an HTTPS URI, creates the `ConfigurationManager` with `BackchannelRewritingDocumentRetriever` instead of the injectable factory. Non-Development and non-backchannel path uses factory unchanged — zero behaviour change for production.

**Dual gating:** Same pattern as Copper's `e0e8ee3` (PrismContext.RefreshTokenAsync) — both env vars checked with `string.Equals(..., OrdinalIgnoreCase)`.

**Security bedrock:** No `RequireHttpsMetadata = false`, no `ValidateIssuer = false`, no certificate bypass. `normalizedKey` (the public OidcAuthority URL) remains the issuer trust anchor for JWT validation.

**Test results:** 631 passed, 0 failed, 0 skipped — no regressions.

**Build:** Succeeded, 0 errors, 5 pre-existing warnings (unchanged).

**Commit SHA:** `4a47acc`

## Learnings

- `OpenIdConnectConfigurationRetriever` uses a single `IDocumentRetriever` instance for ALL fetches — both the discovery-document GET and the transitive `jwks_uri` GET. Wrapping the retriever at construction covers both with one interception point, which is cleaner than trying to post-process the `OpenIdConnectConfiguration` after retrieval.
- The injectable `_configurationManagerFactory` (internal constructor) is the right seam for unit tests. The backchannel path creates the `ConfigurationManager` directly (bypassing the factory) — tests don't set the env vars, so they still hit the factory. This keeps test isolation clean with no extra test setup.
- `Uri.GetLeftPart(UriPartial.Authority)` is the correct way to extract `scheme://host:port` from a URI — covers both standard ports and non-standard ports (e.g. `:8443`) without manual string manipulation.
- When `tenantKey` is the full public OidcAuthority URL (e.g. `https://{name}-8443.app.github.dev/realms/prism-dev`), the origin extracted is `https://{name}-8443.app.github.dev` — which correctly matches the prefix of every Keycloak-emitted URL in the discovery doc.

---

## Session: Codespaces 401 Deploy-Delta Diagnosis (2026-05-02)

**Status:** ✅ Complete — diagnosis written to `.squad/diagnosis/2026-05-02-codespaces-401/blathers-deploy-diagnosis.md`

**Scope:** Diagnosis-only (no code changes). Identified the deployment delta causing `/api/prism/downstream-demo` to return 401 in GitHub Codespaces while working locally.

**Key Findings:**

1. **JWKS fetch gap in `KEYCLOAK_BACKCHANNEL_URL` mechanism** — `PrismAuthExtensions.ResolveSigningKeys` correctly substitutes the OIDC discovery document URL with the internal backchannel address (`http://localhost:8080/...`). However, `PrismSigningKeyCache.WarmAsync` uses `ConfigurationManager<OpenIdConnectConfiguration>` + `OpenIdConnectConfigurationRetriever` which then FOLLOWS the `jwks_uri` from the discovery document. That `jwks_uri` still points to the public Codespace URL (`https://{name}-8443.app.github.dev/...`) because Keycloak uses `KC_HOSTNAME` for ALL URLs in its discovery document. This second HTTP call exits through GitHub's port-forwarding proxy (the same proxy the backchannel was introduced to bypass), blocking the JWKS fetch → no signing keys → 401.

2. **Call path confirmed**: Browser → `/api/prism/downstream-demo` (relative, TestSite) → `DownstreamDemoController` → `https://localhost:7245/api/backoffice/me` (hardcoded, server-side localhost → MockBusinessApp) → JWT Bearer validation fails in MockBusinessApp.

3. **`BusinessAppUrl` is hardcoded** — `src/UmbracoPrism.AppHost/Program.cs:31` — `const string BusinessAppUrl = "https://localhost:7245"` is never Codespace-aware. Not the root cause (localhost IS reachable server-side) but worth noting.

4. **Secondary hypothesis**: SSL trust for the `prism-downstream-demo` HttpClient calling `https://localhost:7245` on Ubuntu 24.04. If `dotnet dev-certs https --trust` doesn't add the cert to .NET's trust store, this would surface as Network Error (statusCode:0), not 401 — so secondary.

**Key Learnings:**

- `OpenIdConnectConfigurationRetriever` fetches BOTH the openid-configuration AND the `jwks_uri` it contains using the same `HttpDocumentRetriever`. Substituting the metadata URL (backchannel) is not sufficient — the JWKS URL must also be rerouted through the backchannel.
- Keycloak with `KC_HOSTNAME` set always uses the configured hostname in ALL discovery document URLs, regardless of which URL you use to fetch the document (internal or external).
- The GitHub Codespaces port-forwarding proxy blocks unauthenticated server-side outbound calls — even to ports marked "public" in devcontainer.json (confirmed by the existing backchannel mechanism being necessary).
- `curl -sk` in `on-start.sh` uses `--insecure` which means the readiness check accepts MockBusinessApp returning 401 as "healthy" — a false positive that hides the 401-at-startup issue.

**Artifacts:**
- Diagnosis: `.squad/diagnosis/2026-05-02-codespaces-401/blathers-deploy-diagnosis.md`
- Decision inbox: `.squad/decisions/inbox/blathers-codespaces-401-diagnosis.md`

---

## Session: Workflow Engine Rams-Grade Review (2026-05-01)

**Status:** ✅ Complete — review written to `.squad/reviews/2026-05-01-prism-reflection/03-blathers-workflow.md`

**Scope:** Deep review of the workflow engine and business app integration against Dieter Rams' 10 Principles of Good Design, framed by GDS heritage. Covered: `PrismComponent` hierarchy, `PrismComponentRenderPayload`, `WorkflowDefinitionBuilder`, `BusinessAppWorkflowEngine`, `PrismWorkflowPageController`, `WorkflowFieldValidator`, advance API contract, convention-based partial dispatch.

**Key Findings:**

1. **Hardcoded business rule in generic engine:** Lines 304–336 of `BusinessAppWorkflowEngine.Advance()` embed a regex-based `enquiry-type == "Technical support"` domain rule. It is invisible to service designers, untestable in isolation, and makes the engine non-generic. Must be extracted to a declarative rule mechanism in the workflow definition.

2. **`PrismComponentRenderPayload` is a 20-property flat bag:** Contradicts the clean design-time sealed record hierarchy. All 20 properties are nullable and only 3-4 are relevant per component type. Should be replaced with a typed render hierarchy mirroring `PrismComponent`.

3. **Advance API contract leaks JsonElement:** `Dictionary<string, object?>` round-trips through JSON as `JsonElement`; `GetDisplayValue()` has explicit `JsonElement` special-casing. Root cause: the contract should be `Dictionary<string, string>` or a typed DTO, not `object?`.

4. **Service designer journey is code-first (good via builder, obscure via JSON seed):** No JSON schema for seeds; type discriminator spellings are inconsistent (`checkboxlist` vs `checkboxes`); Umbraco backoffice `workflowKey` linkage is undocumented and invisible from the seed files.

5. **String enums everywhere:** `InstancePolicy`, `ResponseState`, `Style`, `StepType` are all unenforceable string contracts. Should be C# enums or constant holders.

6. **`InferStepType()` is implicit magic:** Step type is inferred from component presence (`PanelComponent` → "confirmation", `SummaryListComponent` → "check-answers"). Works, but is invisible to designers and produces confusing results if components are mixed.

**Rams Scorecard Summary:** 4 × ✅, 5 × ⚠️, 1 × ❌ (Principle 10 — as little design as necessary).

**Artifacts:**
- Review: `.squad/reviews/2026-05-01-prism-reflection/03-blathers-workflow.md`
- Decision inbox: `.squad/decisions/inbox/blathers-workflow-reflection.md`

---

## Core Context

This agent manages backend services, authentication infrastructure, and CI/CD workflows.

**Key domains:** Auth/OIDC, Aspire local dev, CI infrastructure, Database services, Security hardening, Playwright/E2E

## 📋 Recent Sessions

---

## Session: PR #40 PT2 Backend Security Batch — 5 Findings Fixed (2026-04-30)

**Status:** ✅ Complete — 5 commits merged as `83eb30e` on `main`

**Scope:** Close five PT2 security findings (SEC-PT2-003, 004, 006, 009, 010). Backend hardening: logout-CSRF, security headers, DataProtection persistence, Capacitor JSON antiforgery policy, origin restrictions.

**Commits:**

| SHA | Finding | Summary |
|-----|---------|---------|
| `828b5d4` | SEC-PT2-003 | Logout: `[HttpGet]` → `[HttpPost] + [ValidateAntiForgeryToken]`; Razor forms updated |
| `9f1f34e` | SEC-PT2-004 | `PrismSecurityHeadersMiddleware`: CSP Report-Only, HSTS, XFO, XCTO, Referrer-Policy, Permissions-Policy |
| `6c0e8e9` | SEC-PT2-006 | `TestSiteRuntimeLayout.cs`: DataProtection keys → persistent filesystem (fallback: `App_Data/prism-keys/`) |
| `7a3b0ef` | SEC-PT2-009 | Antiforgery exemptions on `BiometricController`, `PrismNotificationController`, `PrismVinylNotificationController` + policy comments |
| `11b8cbb` | SEC-PT2-010 | `IsCapacitorOrigin`: `http://localhost` restricted to Development only (iOS `capacitor://localhost` always allowed) |

**Key Decisions:**

1. **CSP Report-Only pattern:** Ship CSP without breaking Umbraco backoffice (inline scripts). Enforce after nonce/hash audit.
2. **Intentional antiforgery exemptions:** Bearer-token + origin-checked endpoints documented with policy comments to prevent "fix" reverts.
3. **DataProtection persistence at TestSite layer:** Core library cannot double-configure; follow-up: encryption-at-rest + multi-instance sharing left as seam.

**Test Results:**

- Baseline: 601 tests passing
- After batch: 618 tests passing (+17 new)
- New tests: 7 in `PrismSecurityHeadersMiddlewareTests.cs`, 3 reflection-based logout tests, 3 CORS header tests
- Status: All green; no regressions

**Follow-Up Items (Dispatched):**

- **SEC-PT2-005 (Backoffice auth default scheme):** Blathers on `sec/pt2-backoffice-test` — integration test needed
- **SEC-PT2-007 + SEC-PT2-008 (Razor @Html.Raw sanitization):** Isabelle on `sec/pt2-razor-hardening`
- **CSP enforcement:** Post-audit when inline-script audit + nonce deployment locked in
- **DataProtection encryption-at-rest:** `ProtectKeysWith*` for production (DPAPI / Key Vault)
- **Multi-instance DataProtection:** Azure Blob / Redis key ring sharing seam

**Lessons:**

- Report-Only CSP is a legitimate ship-now-tighten-later pattern for defense-in-depth headers
- Bearer-token endpoints need policy comments (antiforgery exemptions) to prevent future "fix" reverts that would introduce regressions
- TestSite-layer security configuration (e.g., DataProtection) is sometimes safer than Core-library defaults

---

## Session: PR #38 CI Green — MockBusinessApp Sanitizer Fix (2026-04-30)

**Status:** ✅ Complete — Commit `6751662` on `fix/ci-green` (merged as `dc316fb` on main)

**Scope:** Fix `localhost-auth-playwright` CI timeout by registering `IWorkflowContentSanitizer` in MockBusinessApp's DI container.

**Changes:**

1. Added `PassthroughSanitizer` (file-scoped) to MockBusinessApp's `Program.cs`
2. Registered as `services.AddSingleton<IWorkflowContentSanitizer, PassthroughSanitizer>()`
3. Verified: HTTP 401 on readiness probe, app starts successfully

**Impact:** Unblocked all three Playwright spec files; 601 Core unit tests pass.

**Note on Round 2:** Blathers diagnosed a concurrent handler race in round 2 and shipped a polling fix (commit `46826fe`). This turned out to be a misdiagnosis — Umbraco's notification handlers are sequential, not concurrent. The polling fix created a deadlock (seeder blocked dispatcher, preventing type-creating seeder from running). Brewster reverted this in round 3.

**Lesson:** In Umbraco, `INotificationAsyncHandler` dispatch is sequential in registration order. Async polling in handlers holds the entire dispatch chain. Use `[ComposeAfter]` for explicit ordering, not polling.

---

## Session: SEC-003 — Sanitizer Wire-Up (T1, T3–T5, T7, T9) (2026-04-30)

**Status:** ✅ Complete — Commit `4223861` pushed to main

**Scope:** Wire the `IWorkflowContentSanitizer` abstraction across Core + MockBusinessApp per Tom Nook's SEC-003 proposal. Copper follows up with the real Ganss.Xss-backed impl (T2 + T8).

**Changes:**

| Task | What |
|------|------|
| T1 | `HtmlSanitizer` 9.0.892 added to `UmbracoPrism.Core.csproj` (0 vulns) |
| T3 | `IWorkflowContentSanitizer` interface in `UmbracoPrism.Shared/Services/Sanitization/` (placed in Shared so MockBusinessApp can reference without dep cycle) |
| T4 | `NoOpWorkflowContentSanitizer` (internal, Core) + singleton DI registration in `WorkflowBuilderExtensions` |
| T5 | `BusinessAppWorkflowEngine` ctor gains `IWorkflowContentSanitizer`; `Sanitize()` applied to Content on Body, InsetText, WarningText, NotificationBanner, Details, Waiting (all 7 Html.Raw sites) |
| T6 | `_PrismComponent-Waiting.cshtml` does not exist — Waiting.Content is covered by T5 engine seam |
| T7 | `SeedContentSanitizationTests` — 4 theory cases (one per seed); spy sanitizer asserts output == input; trivially passes today, becomes real guard when Copper's sanitizer lands |
| T9 | 6 skipped regression tests in `Phase1SecurityRegressionTests` (script, javascript:, onerror, data:, SVG/onload, plain-text); `[Fact(Skip = ...)]`; correctly skipped with NoOp |

**Architectural deviation:** Interface placed in `UmbracoPrism.Shared` (not Core as spec said). Reason: MockBusinessApp only references Shared; putting interface in Shared avoids `MockBusinessApp → Core` inversion.

**Test delta:** 550 → 554 passing + 6 skipped = 560 total. 0 failures.

**Handoff:** Copper owns T2 (real impl) + T8 (unit tests) + un-skipping T9 + re-registering in DI.

**Decision note:** `.squad/decisions/inbox/blathers-sec-003-wireup.md`

---

## Session: SEC-004 — Rotate Leaked HMAC Key & Extract TestSite Secrets (2026-04-30)

**Status:** ✅ Complete — Commit `b6336fd` pushed to main

**Scope:** Remediate SEC-004 from the 2026-04-30 security review: remove committed `Umbraco:CMS:Imaging:HMACSecretKey` from `appsettings.json`; extract `Prism:VaultUri`; prevent re-leak.

**Changes:**
1. Removed `Umbraco:CMS:Imaging:HMACSecretKey` and `Prism:VaultUri` from `src/UmbracoPrism.TestSite/appsettings.json` — the HMAC value is burned (still in git history; user to handle if repo ever goes public)
2. Wired `appsettings.Local.json` into `Program.cs` via `builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)` — loaded before `CreateUmbracoBuilder()`, higher priority than `appsettings.json`
3. Added `src/UmbracoPrism.TestSite/appsettings.Local.json` to root `.gitignore` with explanatory comment
4. Created `src/UmbracoPrism.TestSite/README.md` documenting the local secrets bootstrap pattern

**Chosen secret extraction mechanism:** `appsettings.Local.json` (gitignored file). User-secrets was already wired (`UserSecretsId` in `.csproj`) but the Local.json pattern was preferred because it self-documents the first-run HMAC bootstrap step: Umbraco writes the regenerated key to `appsettings.json` on first run; dev moves it to `appsettings.Local.json`, then reverts `appsettings.json`. Subsequent runs read the key from Local.json and Umbraco does not regenerate.

**Umbraco HmacSecretKeyService write target:** Umbraco's `IJsonSettingsEditor` / `AppSettingsConfigurationFileEditor` writes the auto-generated HMAC key directly to `appsettings.json` in the content root (not to any other provider). It regenerates the key only when the value is missing from all config providers. Once the key is present in `appsettings.Local.json` (which is loaded into the config chain), Umbraco sees a non-null value and does not regenerate — so `appsettings.json` remains clean after the first-run bootstrap.

**bin/** tracked check:** Not tracked in git. No action needed.

**Build/Test:** 547/547 passing — clean build, 0 new failures.

**Verification:** `git grep "dMxHo7"` → empty (key not in tracked files; historical commit `60f7717` still in git history).

---

## 📌 2026-04-30: Cross-Agent Note — V2 Code Identifiers Naming Review

**Alert:** Mabel's documentation cleanup (2026-04-30) flagged that source code identifiers like `WorkflowDefinitionFileV2.cs` and `ComponentPolymorphismTests.cs` retain "V2" suffixes.

**Question:** Should internal code identifiers be renamed as part of future cleanup? (Joint decision with Tom Nook; no immediate action required.)

---

## Session: V2 Suffix Rename — Workflow Definition Types (2026-04-30)

**Status:** ✅ Complete — Commit `290a18c` pushed to main

**Scope:** Drop the meaningless `V2` suffix from workflow code identifiers (decisions.md had already banned V2 class names; this clears the debt).

**Changes:**
1. `WorkflowDefinitionFileV2.cs` deleted — `WorkflowDefinitionFile` already existed as the canonical type in `UmbracoPrism.Shared.Models.Workflow`; the V2 file was a legacy duplicate with only a `SchemaVersion` property extra (no references other than the ComponentPolymorphism test)
2. `StepDefinitionV2` eliminated — canonical `StepDefinition` already existed with identical shape
3. Test folder renamed via `git mv`: `Workflow/V2/` → `Workflow/Components/` (mirrors prod folder structure)
4. Both test files updated: namespace `UmbracoPrism.Core.Tests.Workflow.V2` → `UmbracoPrism.Core.Tests.Workflow.Components`
5. Test method renamed: `WorkflowDefinitionFileV2_RoundtripsCorrectly` → `WorkflowDefinitionFile_RoundtripsCorrectly`
6. Removed `SchemaVersion = "2.0"` init and its JSON assertion from the roundtrip test (canonical `WorkflowDefinitionFile` has no `SchemaVersion` property)

**Build/Test:** 547 passed, 0 failed (same count as previous session baseline)

**Surprises:**
- A canonical `WorkflowDefinitionFile.cs` (no V2) already existed alongside the V2 file — both in `namespace UmbracoPrism.Shared.Models.Workflow`. The V2 file could not simply be renamed in-place; it had to be deleted. `SeedFileRoundtripTests.cs` was already using the canonical type correctly; only `ComponentPolymorphismTests.cs` referenced the V2 types.
- No other production code referenced V2 types — the grep was clean.

---

## Session: Workflow Developer Experience Improvements (2026-04-28)

**Status:** ✅ Complete

**Work:** Client-side validation and Playwright test readiness improvements post-v2.0 rollout

**Key Fixes:**
1. Removed client-side blur validation (caused layout shift → failed form submissions)
2. Removed client-side submit interception (competing DOM mutations with GDS server-side error summary)
3. Fixed checkbox display value formatting (multi-valued checkboxes now render with proper separators)
4. Server is now the only validation source (prism-workflow-validation.js handles only form.noValidate + character counters)

**Playwright Readiness:**

---

## 📦 Archived Sessions (2026-04-28 and earlier)

Complete chronological history available in git. Recent summaries:

**Archived entries include:**
- Phase 1 backend security patches (SEC-001, SEC-004, SEC-006, SEC-007, SEC-010)
- SEC-003 implementation (workflow content sanitization)
- Multiple PR security findings and test fixes
- Seeding and schema validation work

**Access:** Full session details in git history; `.squad/decisions.md` for decisions.
