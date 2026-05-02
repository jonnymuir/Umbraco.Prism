# Tom Nook — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Isabelle: Web Components, Storybook, Playwright UI tests
- Blathers: C# backend, services architecture, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory

## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

## 🔴 2026-04-30: SECURITY TRIAGE QUEUE (Copper Audit) — 6 OPEN FINDINGS

**Assigned to Tom Nook for triage and pre-production resolution.**

| Finding | Severity | Category | Owner | Decision Status | ETA |
|---------|----------|----------|-------|-----------------|-----|
| SEC-002 | CRITICAL | DataProtection CVE (external) | tom-nook | Investigate Umbraco.Cms bump | TBD |
| SEC-003 | HIGH | CookieSecurePolicy (SameAsRequest → Always) | tom-nook | Pre-production gate | Pre-prod |
| SEC-004 | HIGH | @Html.Raw sanitization (IWorkflowContentSanitizer) | tom-nook | Design + implement pre-editor-ship | Pre-prod |
| SEC-005 | HIGH | Proxy-aware IP (ForwardedHeadersMiddleware) | tom-nook | Required pre-cloud-deployment | Pre-prod |
| SEC-006 | MEDIUM | HMACSecretKey committed + compromised | tom-nook | Rotate key; enable secret scan | Post-review |
| SEC-007 | MEDIUM | Missing CI secret scanning step | tom-nook | Add to CI pipeline | Post-review |

**Context:**
- Full security audit by Copper (2026-04-30) following v2.0 polymorphic component model rollout
- 3 patches already applied and committed: SEC-001 (WorkflowPollController auth), SEC-009 (log injection), SEC-011 (HTML encoding)
- Details: `.squad/decisions.md` → "Security — 2026-04-30" section
- Full review report: `.squad/security-review-2026-04-30.md`

**Locked Decisions:**
- `IWorkflowContentSanitizer` (HtmlSanitizer + GDS allowlist) is pre-condition for shipping definition editor to non-dev
- `CookieSecurePolicy.Always` + `ForwardedHeadersMiddleware` required pre-production
- Secrets policy: no real keys in version-controlled appsettings.json; use `dotnet user-secrets` (local) / env vars (CI/CD)

---

## 📌 2026-04-30: Cross-Agent Note — V2 Code Identifiers Naming Review

**Alert:** Mabel's documentation cleanup (2026-04-30) flagged that source code identifiers like `WorkflowDefinitionFileV2.cs` and `ComponentPolymorphismTests.cs` retain "V2" suffixes. Docs cleanup removed all "v2.0" versioning language from public documentation.

**Question:** Should internal code identifiers be renamed as part of future cleanup? (Joint decision with Blathers; no immediate action required.)

**Update (2026-04-30):** Blathers completed V2 naming debt clearance — `WorkflowDefinitionFileV2` deleted, `StepDefinitionV2` removed, test folder renamed. Commit `290a18c`, 547 tests passing. Code identifiers now consistent with documentation terminology.

---

## 📌 2026-04-26: DIRECTIVE UPDATE — Solo Project, Main-Only Workflow

**Captured by:** Scribe  

> *"This is a solo project. Work directly on `main` — no feature branches, no PR ceremony, no merge overhead."*

**Implications:**
- Commit directly to `main` for architectural planning
- No PR gate or Coordinator merge step
- Branching only for issue-driven work explicitly requested

---

## 📌 2026-04-26: Workflow Schema v2.0 Rollout Plan Approved

**Status:** ✅ Approved for execution

**6-Phase Rollout (P1–P6):**
- P1: Abstract `PrismComponent` base + sealed types (additive only)
- P2–P6: Migrator → engine v2 → builder → views → release

**Design:** `[JsonPolymorphic]` with `"type"` discriminator (author vocab)  
**Target:** ≤610 tests at v2.0  
**Risk Flag:** `SummaryListComponent.FieldRefs` needs P3 prototype validation  

---

## Session: v2.0 Design Audit & Scope Refinement (2026-04-26)

**Status:** ✅ Complete

**Work:** Audited 9 design docs against v2 component model

**Key Findings:**
- 7 of 9 docs require rewrite (conditional fields + schema vocabulary)
- workflow-hub-and-conditional-fields.md most v1-coupled (field-level conditionals)
- workflow-forms-engine-redesign.md obsolete (superseded by implementation)
- **No showstoppers** — v2 design is sound

**Newly-Surfaced Gaps:**
1. Component-tree validation traversal (P3 work)
2. Generic conditional visibility on non-input components (defer to v2.1)
3. Summary-list + conditionally-hidden fields (P3 blocker)
4. Component-tree authorization checks (consider `AuthorizedRoles` on inputs)
5. Fieldset-level validation rules (defer to v2.1)
6. Conditional children depth limit (warn in builder)
7. Umbraco doc JSON examples (rewrite P5/P6)
8. Redesign doc obsolescence marker

---

## Prior Work Summary

**2026-04-14 (Pre-v2.0):**
- Release v1.8.0 Semver Recommendation (MINOR bump, 19 features + 6 security hardening)
- Architecture Review (Umbraco 17 fit; route-hijacked pattern approved)
- Workflow schema cleanup design review (v1 → v2 migration path identified)

**Key Learnings from Prior Work:**
- Reference-based secrets is the unifying pattern for multi-provider auth systems
- Umbraco owns authored routes/page shell; Prism owns tenant/auth/session plumbing
- Workflow UI stack strongest where server components stay thin
- stepType redundant — shell should be derived from component tree structure
- Design docs age better when describing *protocols* rather than *schemas*

---

---

## 2026-04-30 — SEC-003 Design Proposal: `@Html.Raw(Content)` Sanitization

**Status:** ✅ Proposal delivered — `.squad/decisions/inbox/tom-nook-sec-003-proposal.md`

**Scoping outcome:**
- Inventoried every `@Html.Raw` in `src/`. **7 in-scope call sites** in workflow display partials (Body, InsetText, WarningText, NotificationBanner, Panel, Details, Accordion). Copper's report cited 4; broadened to 7. Field-attribute Html.Raw and TestSite RTE paths flagged out-of-scope (already-encoded / separate audit).
- Trust today: author-controlled, design-time JSON seeds (`workflow-seeds/*.json` → `BusinessAppWorkflowEngine.BuildComponents` → `PrismComponentRenderPayload.Content` → view). Drifting toward mixed once the definition editor leaves Dev — locked precondition in `decisions.md` already commits to sanitizer-before-non-Dev-editor.
- Existing sanitization: **none wired** for workflow content (only id/file sluggers and Umbraco's RTE TinyMCE setting).

**Recommended pattern:** `IWorkflowContentSanitizer` (Ganss.Xss-backed, GDS-aligned allowlist) injected into `BusinessAppWorkflowEngine`, applied at the engine→payload boundary in `BuildComponents`. Payload becomes the contractual trust boundary; xmldoc states "pre-sanitized, safe for `@Html.Raw`".

**Rejected:** drop-Html.Raw / encode-everything (breaks GDS rich text); trust-source-only (brittle, decision already committed against it); per-partial sanitize call (sprawl); TagHelper sanitize (TagHelper doesn't process content fields); JSON-deserializer hook (action-at-a-distance); AntiXSS encoder (escapes, doesn't allowlist).

**Allowlist (frozen v1):** block-level p/ul/ol/li/blockquote/br/h2-h4; inline strong/em/b/i/code/abbr/span/a; href schemes http(s)/mailto/tel; auto-`rel="noopener noreferrer"`+`target="_blank"` on external; no class/style/id/data-*/event handlers; `<script><style><iframe><svg>` etc. stripped.

**Plan:** 10 discrete tasks routed — Copper owns allowlist + unit tests (T2, T8); Blathers owns Ganss.Xss package, DI wiring, engine seam, seed regression test, Phase1 regression cases (T1, T3, T4, T5, T6, T7, T9); Mabel docs T10 post-merge.

**Test strategy:** `WorkflowContentSanitizerTests` (16 unit cases — `<script>`, `javascript:`, `onerror`, `data:text/html`, `<style>`, mailto/tel preservation, idempotency); 6 new `Phase1SecurityRegressionTests` end-to-end through the engine; seed round-trip diff test.

**Rollout:** no feature flag (per 2026-04-26 directive). Direct cutover on `main`; expected baseline 547 → 569+ tests post-merge. Allowlist tightening is backward-compatible to current seeds (T7 verifies).

**Did not implement** — design doc only, per directive. Awaiting sign-off from Copper (allowlist), Blathers (seam), Jonny (scope).

## 2026-04-30: SEC-003 Design Proposal — @Html.Raw Content Sanitization

**Status:** ✅ DESIGN COMPLETE — Proposal in active review

**Artifact:** `.squad/decisions/inbox/tom-nook-sec-003-proposal.md`

**Scope:** 7 in-scope Html.Raw() sites in workflow display partials (Body, InsetText, WarningText, NotificationBanner, Panel, Details, Accordion).

**Proposal:** IWorkflowContentSanitizer (Ganss.Xss + GDS allowlist) at engine→payload boundary in BusinessAppWorkflowEngine.BuildComponents. Pre-sanitized content becomes contract; TagHelpers emit safely via @Html.Raw.

**Allowlist (GDS-aligned):** Block-level (p/ul/ol/li/blockquote/br/h2-h4); inline (strong/em/b/i/code/abbr/span/a); href (http/https/mailto/tel); auto rel/target on external links; no class/style/id/data-*/events; script/style/iframe/svg stripped.

**Implementation Plan:** 10 discrete tasks routed — Copper allowlist+tests, Blathers package/DI/engine/seeds/regressions, Mabel docs.

**Test Strategy:** 16 unit cases (XSS vectors); 6 Phase1 e2e; seed roundtrip validation. Expected 547 → 569+ tests post-merge.

**Awaiting:** Copper allowlist sign-off, Blathers seam review, Jonny scope final approval.

---

**Batch Consolidation (2026-04-30):** Security audit findings 2/5/6/7/8/10 closed by Blathers and Isabelle; SEC-003 design ready for implementation queue. SEC-001/9/11 closed pre-batch. Decision registry and orchestration logs updated; tom-nook-sec-003-proposal.md retained in inbox (active proposal, not yet merged to decisions).

---

## 2026-05-01 — Prism Vision Reflection (Dieter Rams Review)

**Requested by:** Jonny Muir  
**Output:** `.squad/reviews/2026-05-01-prism-reflection/01-tom-nook-vision.md`

**Learning:** Prism's three pillars (tenant/auth/branding, GDS workflow, mobile bundle) are architecturally sound in isolation, but `PrismComposer.cs` registers all of them unconditionally — no feature gates, no opt-in surface. This is the single largest violation of "as little design as possible" in the codebase and the highest-priority structural debt ahead of any v3 or marketplace listing.

**Key finding:** `MockBusinessApp` is simultaneously demo and reference implementation. It shadows `Shared.WorkflowDefinitionFile` with its own type (`MockBusinessApp/Services/WorkflowDefinitionFile.cs`), creating a hidden coupling that any real BusinessApp implementor will trip over. The dual identity of MockBusinessApp is the most strategic question Jonny needs to answer before the workflow feature can be called production-grade.

**Decision dropped to inbox:** `.squad/decisions/inbox/tom-nook-prism-reflection.md` — recommends decomposing `PrismComposer` into feature extension methods and resolving MockBusinessApp identity.

---

**2026-05-01 — Prism Reflection Review (Rams 10 Principles)**

Delivered holistic vision review of Prism architecture applying Rams principles. Three architectural decisions recorded:
1. Decompose PrismComposer into feature extension methods (AddPrismCore, AddPrismWorkflow, AddPrismMobile, AddPrismNotifications)
2. Resolve MockBusinessApp dual identity (Path A: lean demo, Path B: lean reference — Jonny to choose)
3. Remove OidcClientSecret legacy column + migration

No code changes — review-only. Decisions merged to decisions.md by Scribe. Orchestration log written to 2026-05-01T07:57:29Z-tom-nook.md.

---

**2026-05-02 — Landing Verification: Codespaces BusinessApp URL Fix**

Verified successful landing of Blathers' Codespaces BusinessApp URL discovery fix (commit `6205bd4`) on both main and origin/main branches.

**Verification performed:**
- Branch reconciliation: main → `6205bd4`, origin/main → `6205bd4`
- Build status: clean, 0 errors
- Test status: 650 Core tests passing, 650 TypeScript tests passing
- Regression check: none detected

**Status:** CONFIRMED — Both branches aligned; downstream demo ready for validation in Codespaces.

Orchestration log written to 2026-05-02T13:14:45Z-tom-nook.md.

