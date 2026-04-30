# Project Context

- **Project:** Umbraco.Prism
- **Created:** 2026-03-22

## Core Context

Agent Scribe initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-03-22
📝 2026-03-22: Copilot instructions file completed and verified. Orchestration logs written. Decisions consolidated. Ready for deployment.

## Learnings

- **Multi-project structure:** Core is a NuGet package; Client is web components (Lit/Storybook); TestSite and MockBackOffice are reference implementations.
- **Testing split:** .NET Core uses XUnit (with Moq, FluentAssertions); Client uses Playwright with Storybook test runner and axe for WCAG compliance.
- **Mobile feature complexity:** MobileBundleService (27KB) generates full Capacitor starter bundles with tenant-specific config; requires Node.js 22.17.1 and npm.
- **Middleware-first architecture:** PrismTenantMiddleware resolves tenant from hostname; PrismBrandingMiddleware injects CSS variable overrides dynamically.
- **Stateless identity:** No local Members; all auth deferred to Entra ID (CIAM) with secrets stored in Azure Key Vault per tenant.
- **Admin policy enforcement:** Tenant management restricted to Umbraco users in configured groups (default: admin); enforced via PrismAdminHandler.
- **CI/CD:** Separate workflows for testing (ci-tests.yml) and packaging (package-release.yml); client build must complete before Core pack.

---

## 2026-04-30T14:50:00Z — Security Review Closeout & Branching Policy Directive

**Task:** SEC-003 implementation verification, security review completion, feature-branch policy directive merge, and session bookkeeping.

### Deliverables

1. ✅ **Security Review Closure:** `.squad/security-review-2026-04-30.md` updated
   - SEC-003 status changed from 🟡 DESIGNED to ✅ CLOSED
   - Executive summary updated: 11/11 findings processed (9 CLOSED, 2 PATCHED, 1 NOW CLOSED)
   - Recommended Next Actions refreshed (SEC-003 removed from backlog)
   - SEC-003 detail section expanded with full implementation + test results (601 passing tests)

2. ✅ **Decisions Merged:** Four inbox files merged into `.squad/decisions.md` in chronological order
   - SEC-003 wire-up (Blathers, commits `4223861` + `97491d5`)
   - SEC-003 implementation (Copper, commits `ae616a2` + `55978f5`)
   - Feature-branch + PR policy directive (user policy, effective immediately)
   - SEC-003 design proposal (Tom Nook, archived for reference)

3. ✅ **Inbox Cleared:** Four files deleted after merging
   - `blathers-sec-003-wireup.md`
   - `copper-sec-003-impl.md`
   - `copilot-directive-20260430-feature-branch-policy.md`
   - `tom-nook-sec-003-proposal.md`

4. ✅ **Routing Policy Updated:** `.squad/routing.md` refreshed
   - Feature-branch workflow codified as the default
   - Exception noted for trivial `.squad/`-only bookkeeping
   - Branch naming convention documented
   - Rules and rationale added

5. ✅ **Session Log:** `.squad/log/2026-04-30-security-review-closeout.md` created
   - Closeout summary and sign-off checklist
   - Final security review state (11/11 findings closed)
   - Branching policy impact and rollout guidance

### Key Decisions Codified

**Feature-Branch + PR Workflow:**
- One work item per branch; whole team may collaborate on same branch
- CI gates all merges to main (restores regression prevention)
- Scribe bookkeeping travels with work on feature branches
- Exception: pure `.squad/`-only changes may go direct to main (coordinator's call)
- Effective immediately for new work; existing in-flight work (Copper SEC-003) continues on main

**SEC-003 Production Gate:**
- Precondition for definition editor non-Dev rollout is now SATISFIED
- All 11 security findings processed; 10 closed, 1 patched (SEC-001)
- Test suite: 601 passing, 0 skipped, 0 failed

### Basis

- Copper's security review (2026-04-30)
- Blathers & Copper implementations (commits 4223861, 97491d5, ae616a2, 55978f5)
- Jonny Muir's user directive on feature-branch workflow (2026-04-30)
- Tom Nook's SEC-003 design proposal (2026-04-30, implemented)

---

## 2026-04-03T11:42:28Z — Phase 1 Notifications Backend Documentation & Handoff

**Task:** Post-delivery documentation and team history update

### Deliverables

1. ✅ **Orchestration Log:** `.squad/orchestration-log/2026-04-03T11:42:28Z-blathers.md`
   - Complete Phase 1 deliverables summary
   - Build status (0 errors, 0 warnings)
   - 8 key implementation decisions
   - Handoff notes for next phases

2. ✅ **Session Log:** `.squad/log/2026-04-03T11:42:28Z-phase1-notifications-backend.md`
   - Brief summary of Phase 1 completion
   - All deliverables (FirebaseAdmin, 2 migrations, service, controller, handler)
   - Key decisions documented

3. ✅ **Decision Merge:** `.squad/decisions/inbox/blathers-phase1-notifications.md` → `.squad/decisions/decisions.md`
   - 8 decisions merged into main decisions file
   - Inbox file deleted
   - Deduplication: No pre-existing conflicts

4. ✅ **Agent History Updates:**
   - Blathers: Appended Phase 1 completion record with full technical details
   - Scribe: This entry (documentation and handoff coordination)

### Coordination Notes

**Copied to Blathers History:**
- Deliverables (code, migrations, service, API, handler, composer)
- Build status and technical gotchas solved
- 8 key decisions and rationale
- Ready for next phases (testing, UI, rate limiting, analytics)

**Decision Inbox:** Fully cleared. No pending decisions awaiting scribe coordination.

**Team Impact Summary:**
- Blathers: Phase 1 shipped, ready for testing phase
- Isabelle (Frontend): May need components for subscription UI (Phase 2+)
- Tangy (Testing): Unit tests, integration tests ready to start
- Copper (Security): API auth model, FCM credential storage review deferred to implementation phase
- Celeste (Documentation): XML docs and Firebase setup docs deferred to Phase 2+
- Mabel (Release Management): v1.0.0 notation pending (Phase 2+ release cycle)

---

## Documentation Restructure Session (2026-04-03T15:41:14Z)

### Summary

Processed Mabel's documentation restructuring work. Merged 3 pending decisions from inbox → decisions.md. Cleaned up decision tracking.

### Actions Completed

1. ✅ **Decision Inbox Merge:** 3 files processed
   - `mabel-cloudflare-maintenance.md` → decisions.md (Cloudflare-only maintenance handling)
   - `blathers-di-scopefactory.md` → decisions.md (IServiceScopeFactory pattern for BackgroundService)
   - `kicks-android-bootstrap-fix.md` → decisions.md (perl for manifest injection, Gradle 8.14 upgrade)
   - No duplicates found
   - Inbox files deleted

2. ✅ **Logs Written:**
   - Session log: `/squad/log/2026-04-03T15:41:14Z-docs-restructure.md`
   - Orchestration log: `/squad/orchestration-log/2026-04-03T15:41:14Z-mabel.md`

3. ✅ **Agent Histories Updated:**
   - Mabel: Appended README restructure and docs index work with context
   - Scribe: This entry

### Decision Impact

These three decisions span different domains:
- **Blathers (Backend):** DI ScopeFactory pattern applies to BackgroundService usage
- **Kicks (Mobile):** Android bootstrap improvements for cross-platform scripting
- **Jonny/Cloudflare:** Clarifies maintenance responsibility boundary (no app code changes)

All decisions are orthogonal and improve long-term project hygiene.

---

## Post-Agent Documentation Session (2026-04-12T01:29:29Z)

### Summary

Processed background session output from Blathers (Aspire workload permissions investigation). Created orchestration and session logs, merged decision from inbox to main decisions file, and archived old entries.

### Actions Completed

1. ✅ **Orchestration Log:** `2026-04-12T01:29:29Z-blathers.md`
   - Summary of Blathers' Aspire workload investigation
   - Root cause: .NET SDK at `/usr/local/share/dotnet` owned by `root:wheel` requires elevated privileges
   - Implementation: Updated README.md, ASPIRE_DEV.md, scripts/validate-aspire-prereqs.mjs
   - Validation status and impact documented

2. ✅ **Session Log:** `2026-04-12T01:29:29Z-aspire-workload-permissions.md`
   - Brief summary of Aspire workload permissions fix
   - Outcome: elevated command path documented and tested

3. ✅ **Decision Merge:** `blathers-workload-permissions.md` → `decisions.md`
   - Decision: Document elevated Aspire workload install on macOS protected SDK paths
   - Conventions established for preflight validation messaging
   - Standing effect: Developers now receive clear elevated-command guidance
   - Inbox file deleted

4. ✅ **Archive Maintenance:** `decisions-archive.md` created
   - Archived two entries older than 30 days (2025-07-22, 2025-07-15)
   - decisions.md reduced by 2 old entries to improve size management
   - Archive file preserves historical record

5. ✅ **Team Notifications:**
   - Blathers: Existing handoff already in history from background session
   - No cross-agent updates needed; this is dev-experience only

### Decision Impact

The archived decisions remain valid but are now in historical record:
- **2025-07-22:** uui-input accessibility label pattern (continues to apply)
- **2025-07-15:** Test philosophy as behavioral contracts (continues to apply)

These principles remain standing even after archiving.

---

## 2026-04-14T21:34:14Z — CI Failure Investigation Session Orchestration

### Session Context
- **Topic:** Latest CI failure still failing after readiness fix (Ubuntu cert-trust + workflow_dispatch)
- **Run ID:** 24420087047
- **Agents:** Tangy (classification), Blathers (trace + smallest fix)
- **User:** Jonnymuir

### Orchestration Tasks Completed

1. **Session Log Entry:**
   - Created orchestration log: `.squad/orchestration-log/2026-04-14T21:34:14Z-scribe-ci-failure-session.md`
   - Captured session context, agent roles, key findings, and proposed action

2. **Decision Inbox Merge:**
   - Read both inbox entries (blathers, tangy)
   - Both teams converged on same root cause: Keycloak HTTP readiness gap
   - Merged into unified decision record in `decisions.md`
   - Deleted merged inbox files

3. **Decisions Ledger Update:**
   - Appended new decision: "2026-04-14: Tangy & Blathers — Latest CI Failure Root Cause Classification"
   - Consolidated evidence, root cause analysis, and smallest fix recommendation
   - Added canonical references to AppHost, readiness probes, and CI workflow files

4. **Session History:**
   - Created log entry: `.squad/log/2026-04-14T21:34:14Z-scribe-session-log.md`
   - Documented agent findings, consensus, and action alignment

### Key Decision Consensus
- **Root Cause:** AppHost dependency contract incomplete; Keycloak marked ready before HTTP endpoints available
- **Classification:** Not cert bootstrap, not auth logic—container/HTTP readiness mismatch
- **Next Action:** Add HTTP health gate to Keycloak in `src/UmbracoPrism.AppHost/Program.cs`

### Files Created/Modified
- Created: `.squad/orchestration-log/2026-04-14T21:34:14Z-scribe-ci-failure-session.md`
- Created: `.squad/log/2026-04-14T21:34:14Z-scribe-session-log.md`
- Modified: `.squad/decisions.md` (appended new consolidated decision)
- Deleted: `.squad/decisions/inbox/blathers-latest-ci-failure.md`
- Deleted: `.squad/decisions/inbox/tangy-latest-ci-failure.md`

### Status
✅ Session complete—ready for AppHost hardening phase

---

---

## 2026-04-21T20:58:11Z: Workflow DX Sprint Orchestration

**Scope:** Orchestration of multi-agent workflow DX improvements and server-side validation bug fix

**Agents Coordinated:**
- Blathers (Backend): PrismWorkflowPageController, builders, Archetype→StepType rename
- Mabel (Technical Writer): Documentation rewrite with correct terminology
- Tangy (QA): 62 new builder tests, 493 total passing
- Celeste (Developer Experience): XML documentation, zero warnings
- Coordinator: Server-side validation bug fix

**Deliverables:**
- ✅ 5 Orchestration logs (one per agent) in .squad/orchestration-log/
- ✅ Session log in .squad/log/ with complete context
- ✅ Decision inbox files merged to .squad/decisions.md
- ✅ Agent history files updated
- ✅ Git commit prepared with comprehensive message
- ✅ Build green, 493 tests passing

**Key Achievements:**
- Workflow controller boilerplate reduced 70% (300→90 lines)
- 62 new tests for builder classes
- All documentation terminology corrected
- Server-side validation errors now display correctly on form re-render
- Zero doc warnings
- Breaking changes clearly documented

**Reference:** `.squad/log/2026-04-21T20:58:11Z-workflow-dx-and-validation-fix.md`, `.squad/decisions.md` (both Blathers and Mabel decisions merged)


---

## 2026-04-22T00:10:00Z: Waiting State Feature Commit Orchestration

**Scope:** Final orchestration of waiting state feature implementation across all four agents (Tom Nook, Blathers, Isabelle, Mabel, Tangy).

**Deliverables:**
- ✅ Decision inbox merged (5 files: Tom Nook design, Blathers backend, Isabelle UI, Mabel builder, Tangy tests)
- ✅ Inbox cleared
- ✅ Git commit prepared with comprehensive message
- ✅ Build green: 0 errors, 543 tests passing

**Files Modified:**
- `.squad/decisions.md` — 5 new decision entries appended (waiting state design, backend impl, UI pattern, builder API, test patterns)
- `.squad/agents/scribe/history.md` — This entry

**Agents Coordinated:**
- Tom Nook (Lead): Design decisions on waiting state semantics, polling architecture, builder API
- Blathers (Backend): WaitingConfig models, WorkflowPollController, BuildEnvelope integration
- Isabelle (Frontend): `_WorkflowStep-Waiting.cshtml` with ARIA accessibility, progressive enhancement
- Mabel (Technical Writer): `WaitWith()` builder method, comprehensive documentation
- Tangy (QA): 31 new tests with three-layer pattern (serialization, builder, engine integration)

**Key Achievement:**
Implemented waiting state as a first-class feature with full workflow engine support, accessible polling UI, fluent builder API, and comprehensive documentation. Pattern established for future workflow engine extensions.

**Git Status:**
- Build: 0 errors, 0 warnings (7 pre-existing)
- Tests: 543 passing (31 new waiting state tests)
- Ready for commit to current branch


---

## 2026-04-26 — v2.0 Rollout Ledger Closure

**Role:** Documentation specialist; decision inbox merging, session logging, team history coordination.

**Deliverables:**
- Decision inbox merge: 6 files consolidated into `.squad/decisions.md`
  - copilot-3commit-replan-20260426.md → 3-commit atomic collapse decision
  - copilot-directive-20260426-094531.md → Direct schema replacement directive
  - tom-nook-direct-replacement-sequencing.md → 8-commit sequencing plan (superseded)
  - v2-followthrough-progress.md → Follow-through phases 1–2 summary
  - v2-followthrough-finish-progress.md → Final progress report (blockers, recommendations)
  - v2-followthrough-task2-blocker.md → ModelsBuilder views conflict resolution
  - Plus: tom-nook-v2-design-doc-audit.md (design audit findings)
- Session log: Comprehensive session-close ledger entry in `.squad/decisions.md`
  - 9-commit rollout summary
  - Key decisions locked in
  - Test results + coverage status
  - Open follow-ups + deferred work (Generic ConditionalOn → v2.1, walkthrough expansion scope, 5 active biometric branches)
- Agent history updates:
  - Blathers: Schema replacement + seed fix + ModelsBuilder config
  - Tom Nook: Sequencing plan + design audit
  - Isabelle: E2E tests + screenshot walkthroughs
  - Scribe: This entry (ledger coordination)
- Inbox files deleted after merge

**Status:** ✅ v2.0 rollout CLOSED; ready for v2.1 planning phase.

**Basis:** Charter pattern (append-only ledger, merge inbox newest-at-top, dated, attributed), session close directive (Jonny 2026-04-26).

