# Blathers — History

## Core Context

This agent manages backend services, authentication infrastructure, and CI/CD workflows.

**Key domains:** Auth/OIDC, Aspire local dev, CI infrastructure, Database services, Security hardening, Playwright/E2E

## 📋 Recent Sessions

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
- Fixed cold-Razor-view first-render race that caused first test per spec file to timeout
- Added 5 HTTP behavioural probes to pre-warm all workflow routes
- Expanded seed-contract gate to validate all 4 workflow pages + dashboard
- Result: ✅ localhost-auth-playwright lane now green

**Test Results:** 10 passed, 2 pre-existing TestSite binding failures (unrelated)

---

## Session: Option 1 Regression Fix (2026-04-26)

**Status:** ✅ Complete — Commit `7e55151` merged

**Bug Fixes:**
1. **Decimal field validation gap:** WorkflowFieldValidator didn't recognize `"decimal"` as numeric type → min/max constraints silently ignored. Added `"decimal"` case to validation logic + unit test.
2. **Planning confirmation incomplete:** Missing reference number body component in planning-notification.json seed. Added reference number body component.

**Root Cause:** Atomic v2.0 schema swap (commit `7423803`) introduced `"decimal"` type but validation wasn't updated; seed migration dropped confirmation body text.

**Test Results:** +1 new decimal validation test; 543/547 passing (same 4 pre-existing TestSite failures)

**Blind Spots:** No compile-time guarantee all field types handled in validator; seed JSON lacks schema enforcement; single 5000+ line atomic commit created spread-out fixes.

---

## Session: Workflow v2.0 Phase 1 — Polymorphic Component Hierarchy (2026-04-26)

**Status:** ✅ Complete — 9-commit atomic rollout concluded

**Scope:** Atomic direct replacement of v1 schema with v2 polymorphic components (no migrator, no dual schema, no feature flags)

**Key Implementation:**
- Abstract `PrismComponent` base + sealed derived types (16 component types)
- `[JsonPolymorphic]` with `"type"` discriminator
- `FieldsetComponent.Children: PrismComponent[]` replaces flat `fields[]`
- `ConditionalChildren` on Radios/Checkboxes only (v2.1 for generic conditionals)
- ModelsBuilder view generation disabled (TestSite uses Core's embedded views)

**Seed Roundtrip Guard:**
- All 4 seeds migrated to v2; added `SeedFileRoundtripTests` to catch schema drift
- Regression guard: all seeds deserialize correctly, no orphaned v1 properties

**E2E + Docs:**
- Playwright tests cover all 4 demos (happy paths + conditionals)
- Screenshot-driven walkthroughs for all 4 demos
- 12 design + guide docs refreshed for v2

**Test Results:** Clean build (0 warnings); 583 tests maintained; +4 seed roundtrip tests (546 total); no regressions

---

## Prior Work Summary (2026-04-22 and earlier)

**2026-04-22:**
- stepType Removal & Component Model Unification (decision made; v1 vs v2 architecture)
- Compound Content Field Types implementation
- GDS Component Model foundation work

**2026-04-21:**
- Instance Policy Implementation (single/multiple/prompt policies)
- Field Group API Endpoints
- Security Hardening Phase 2

**2026-04-20:**
- GDS Workflow Models Phase 1 Completion
- GDS Workflow Models Evolution analysis

**2026-04-14 (Pre-v2.0):**
- Aspire localhost auth CI job (manual rerun + Linux cert trust)
- Phase 1 Security Regression CI Test Fix
- CI workflow infrastructure baseline

**Key Learnings:**
- Server-side validation is source of truth; client-side decorative only
- Atomic breaking changes create spread-out follow-up fixes
- E2E tests catch subtle validator + seed gaps that unit tests miss
- Playwright pre-warming (HTTP probes) essential for cold-route performance
- Decimal as distinct type aligns with GDS component model
- Aspire restarts between test files have high overhead (~1 min per file)

---

## Session: SEC-002/006/007/008/010 — Security Review 2026-04-30 Full Remediation

**Status:** ✅ Complete — 4 commits pushed to main

**Scope:** Close all five security findings from the 2026-04-30 security review in a single session (one commit per logical group, build + test verification after each).

### Commits

| SHA | Findings | Summary |
|-----|----------|---------|
| `2618c54` | SEC-002, SEC-008 | NuGet CVE bumps: DataProtection → 10.0.7, OpenTelemetry.Api → 1.15.3 |
| `df434bf` | SEC-006 | CookieSecurePolicy.Always + regression test |
| `44c476f` | SEC-007 | ForwardedHeadersMiddleware wired; proxy-aware rate-limiting |
| `87900c9` | SEC-010 | PII/GUIDs scrubbed in MockBusinessApp; appsettings.Local.json pattern extended |

### Test progression

548 → 549 (after SEC-006) → 550 (after SEC-007) — 550/550 passing at close.

### Key details

**SEC-002 (CRITICAL):** `Microsoft.AspNetCore.DataProtection` GHSA-9mv3-2cwr-p262 fixed by pinning to 10.0.7 in `UmbracoPrism.Shared.csproj` (uses base SDK, no automatic web-framework override). Required co-bumping `System.Security.Cryptography.Xml` 10.0.6 → 10.0.7 to avoid NU1605 downgrade error.

**SEC-008 (MEDIUM):** `OpenTelemetry.Api` GHSA-g94r-2vxg-569j — 1.12.0 and 1.13.x all vulnerable; pinned to 1.15.3 in both `ServiceDefaults.csproj` and `AppHost.csproj` independently.

**SEC-006 (MEDIUM):** `CookieSecurePolicy.SameAsRequest` → `Always` in `PrismComposer.cs`. Local dev now requires HTTPS (already enforced by Aspire launch profile).

**SEC-007 (MEDIUM):** `ForwardedHeadersMiddleware` wired as first call in `UmbracoPipelineFilter` pre-pipeline. `KnownProxies`/`KnownNetworks` cleared for dev-safe default — production deployment MUST restrict to known proxy CIDRs.

**SEC-010 (LOW):** Real Entra tenant GUIDs + `jonnypmuir@gmail.com` replaced with placeholders. ⚠️ PII (`jonnypmuir@gmail.com`) remains in git history — history rewrite required if repo goes public.

### Artifacts created

- `.squad/decisions/inbox/blathers-sec-002-008.md`
- `.squad/decisions/inbox/blathers-sec-006.md`
- `.squad/decisions/inbox/blathers-sec-007.md`
- `.squad/decisions/inbox/blathers-sec-010.md`
- `.squad/agents/blathers/SKILL-proxy-aware-rate-limiting.md`

---

## 2026-04-30: Security Patch Sprint — SEC-002, SEC-004, SEC-006, SEC-007, SEC-008, SEC-010

**Status:** ✅ COMPLETE — 5 commits, 6 findings closed

### SEC-002 (CRITICAL) + SEC-008 (MEDIUM): NuGet CVE Bumps
**Commit:** `2618c54`
- Microsoft.AspNetCore.DataProtection 10.0.0 → 10.0.7 (GHSA-9mv3-2cwr-p262 fix)
- System.Security.Cryptography.Xml 10.0.6 → 10.0.7 (co-pin for NU1605 compat)
- OpenTelemetry.Api 1.12.0 → 1.15.3 (GHSA-g94r-2vxg-569j fix in ServiceDefaults + AppHost)

### SEC-004 (HIGH): TestSite Secrets Management Pattern
**Commit:** `b6336fd`
- Introduced `appsettings.Local.json` (gitignored) for Umbraco:CMS:Imaging:HMACSecretKey + Prism:VaultUri
- Created `src/UmbracoPrism.TestSite/README.md` with bootstrap docs
- Pattern: Load Local config before CreateUmbracoBuilder() to enable clean first-run HMAC generation

### SEC-006 (HIGH): CookieSecurePolicy.Always
**Commit:** `df434bf`
- PrismMemberCookie `SameAsRequest` → `Always` in PrismComposer (line ~108)
- HTTPS required for authenticated flows (Aspire already enforces via dev-certs)
- Regression test: Phase1SecurityRegressionTests.PrismMemberCookie_SecurePolicy_IsAlways

### SEC-007 (HIGH): Proxy-Aware IP Rate Limiting
**Commit:** `44c476f`
- ForwardedHeadersMiddleware wired (X-Forwarded-For + X-Forwarded-Proto)
- Registered as first call in UmbracoPipelineFilter pre-pipeline
- BiometricController.GetClientIp() now proxy-aware; rate-limit buckets per-client IP
- Regression test: Phase1SecurityRegressionTests.BiometricRateLimit_PartitionKey_UsesRemoteIpAddress_NotRawForwardedForHeader
- ⚠️ CAVEAT: KnownProxies/KnownNetworks left empty (dev-safe default). MUST be hardened before production deployment.

### SEC-010 (MEDIUM): Scrub PII in MockBusinessApp
**Commit:** `87900c9`
- Azure Entra GUIDs: real values → placeholders (00000000-0000-0000-0000-000000000001–4)
- Email addresses: jonnypmuir@gmail.com → alpha-admin@example.com
- Wired appsettings.Local.json pattern (identical to SEC-004)
- Created `src/UmbracoPrism.MockBusinessApp/README.md`
- ⚠️ PII FLAG: Real email remains in git history; owner should notify if repo goes public (GDPR/UK GDPR Art. 17)

### Test Results
548 → 550 tests passing (+2 regression tests)

### Pattern Lock
All test/mock app secrets now follow `appsettings.Local.json` pattern:
- `src/UmbracoPrism.TestSite/appsettings.Local.json` (SEC-004)
- `src/UmbracoPrism.MockBusinessApp/appsettings.Local.json` (SEC-010)
Rule: Placeholder values in tracked JSON; real values in gitignored Local copy.

### Artifacts
- Merged from inbox: blathers-sec-002-008.md, blathers-sec-004-fix.md, blathers-sec-006.md, blathers-sec-007.md, blathers-sec-010.md
- All findings documented in `.squad/decisions.md`

**Scribe note:** Security batch 2 consolidation recorded in `.squad/log/2026-04-30-security-batch-2.md` and orchestration logs.
