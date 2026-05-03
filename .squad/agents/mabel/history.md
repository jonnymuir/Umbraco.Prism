# Mabel — History (Technical Writer)

**Agent:** Documentation specialist shipping Codespaces and product-facing guidance, walkthrough architectures, diagnostic flows, and onboarding surface improvements.

**Recent focus:** Diagnostics script landing, runtime troubleshooting docs, port clarification, dashboard accessibility, and scope discipline (product vs bookkeeping separation).

---

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **Scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs, Codespaces guides

---

## 2026-05-03: Diagnostics Script Runtime Fix — Product Commit to Main

**Status:** ✅ Complete (product commit fb1b324 pushed to origin/main)

**Team Context:** Orchestrated with Blathers (runtime implementation) and Tangy (test contract validation)

**Decisions Generated:**

1. **Diagnostics Script Landing: Scope Discipline** (implemented)
   - Pattern: land product deliverables on main; keep .squad bookkeeping separate
   - Clean separation between user-facing artifacts and agent documentation

2. **Diagnostics Script Runtime Isolation — Commitment to Main** (implemented)
   - Product commit fb1b324 now live on origin/main
   - Files: `scripts/codespaces/diagnose-downstream.sh`, `CODESPACES.md`, test contract integration
   - Deferred agent skill extraction to separate bookkeeping merge

**Work Product:**
- Commit fb1b324: Complete, releasable product commit
- CODESPACES.md: Added diagnostics guidance and recovery steps
- Test integration: DashboardLocalEndpointsValidationTests.cs with new contract
- Decision trail: Clear scope discipline for future product vs bookkeeping merges

**Implementation Notes:**
- Followed git hygiene: one commit = one complete, releasable unit
- Users can immediately pull and use diagnostics script in Codespaces
- .squad/ bookkeeping deferred to separate session to maintain clean history

**Outcome:** Product live. Users can pull and use immediately. Scope discipline model established for team.

---

## 2026-05-03: No-Python Diagnostics Landing — Clean Scope Boundary

**Status:** ✅ Complete (product commit 22843a2 pushed to origin/main)

**Context:** The diagnostics script rewrite eliminated Python import path errors. Blathers completed the shell-only implementation, test contracts validated by Tangy. This session: Landing only product files cleanly, keeping .squad bookkeeping separate.

**Files Committed to Main:**
- `scripts/codespaces/diagnose-downstream.sh` — complete shell-only probe logic
- `CODESPACES.md` — updated diagnostics guidance (no Python requirement)
- `MANUAL_DIAGNOSIS_FLOW.md` — clarified assumptions for manual troubleshooting
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — updated test contract verifying shell-only behavior

**Scope Discipline Applied:**
- Staged only product files (`git add` excluding `.squad/` artifacts)
- Left `.squad/agents/{blathers,tangy}/history.md` untracked (deferred to bookkeeping session)
- Did not commit `.playwright-cli/` or `.squad/skills/browser-devtools-api-diagnosis/`
- Clean separation: users pull commit 22843a2, get working diagnostics tool immediately

**User Outcome:** Can now `git pull origin main` and run `bash scripts/codespaces/diagnose-downstream.sh` without Python installation. No Codespace runtime errors, no import path noise.

**Decision Trail:** Continues scope discipline model from commit fb1b324 (product commit + deferred bookkeeping). Single-unit release boundary maintained.

---

## Recent Session Archive

Detailed session histories (2026-04-30, 2026-05-01, and earlier) available in `history-archive.md`. Quick reference:

- **2026-05-01:** Rams-Grade documentation review (onboarding surface audit)
- **2026-04-30:** PR #38 CI green (CHANGELOG + workflow regex fix)
- **2026-05-03 (multiple sessions):** Codespaces port docs, PR #49 merge, branch reconciliation, diagnostics script landing, runtime fix

**Access full chronology:** Git history and `.squad/decisions.md` for decision trail.

## 2026-05-03: Diagnostics Script No-Python Rewrite (SESSION COMPLETION)

**Orchestration log:** `.squad/orchestration-log/2026-05-03T21:00:48Z-mabel.md`

### Work Summary
- Staged only product deliverables for main branch commit
- Created clean product commit 22843a2: "Fix: Rewrite diagnostics script to eliminate Python runtime dependency"
- Implemented product/bookkeeping separation workflow
- Left .squad/ files unstaged for separate Scribe bookkeeping merge
- Pushed to origin/main with clean git history

### Decision Established
- **Diagnostics Script Landing: Product vs. Bookkeeping Separation** (IMPLEMENTED)
  - Main branch hygiene: only shipping artifacts, no .squad/ noise
  - Release boundaries: one commit = one releasable unit (22843a2 is production-ready)
  - User clarity: commit shows only user-facing deliverables, not agent coordination artifacts

### Cross-Agent Context
- **Blathers (Backend Dev):** Rewrote diagnostics script, shell-native; updated docs/tests
- **Tangy (Tester):** Reviewed no-Python path; strengthened regression coverage

### Workflow Established
**For future multi-agent product commits:**
1. Implementation agents complete work
2. Technical Writer stages only product files
3. Create clean product commit with single concern
4. Leave .squad/ files unstaged
5. Separate bookkeeping session updates agent histories and merges without product files

### Convention Update Recommendation
- Scribe should consider updating `.squad/conventions.md` with this landing workflow pattern
- Future technical writers should use this pattern for all multi-agent product handoffs

## 2026-05-03: Transport Diagnostics Landing — Clean Product Commit to Main

**Status:** ✅ Complete (product commit 17edf9c pushed to origin/main)

**Work:** Landed transport and backchannel diagnostics feature for downstream demo.

**Scope Applied:**
- Staged only product files: DownstreamDemoController.cs and DashboardLocalEndpointsValidationTests.cs
- Left unrelated changes unstaged (.playwright-cli/, .squad agent artifacts)
- Created single, releasable product commit

**Commit:** `17edf9c` — feat(diagnostics): expose transport and backchannel diagnostics in downstream demo
- Adds transport type, backchannel presence, base URL, and scheme diagnostics to all downstream responses
- Enhances test coverage with three new contract tests
- Users now have actionable context for Codespaces troubleshooting

**Push Result:** origin/main now points to 17edf9c. Product changes live and ready for users.

**Pattern Reaffirmed:** One commit = one releasable unit. Product/bookkeeping separation workflow continues.

---

## 2026-05-03: Deeper Downstream Timeout Diagnostics — Product Commit to Main

**Status:** ✅ Complete (product commit 442c5e9 pushed to origin/main)

**Work:** Landed enhanced timeout diagnostics with deeper context for downstream demo controller.

**Scope Applied:**
- Staged only product files: DownstreamDemoController.cs and DashboardLocalEndpointsValidationTests.cs
- Left .squad/ bookkeeping files unstaged
- Created single, releasable product commit

**Commit:** `442c5e9` — feat(diagnostics): enhance downstream timeout diagnostics with deeper context
- Adds explicit backchannel usage indicators (usingBackchannel field)
- Surface target path diagnostics for timeout scenarios
- Include timeout window and cancellation source metadata
- Improves timeout guidance pointing operators to wiring checks
- Extended test coverage for both backchannel and public-tunnel timeout paths

**Push Result:** origin/main now points to 442c5e9. Product changes live and ready for users.

**Pattern:** Continues established product/bookkeeping separation workflow. Three-agent coordination (Blathers implementation, Tangy testing, Mabel landing) maintains clean git history.

---

## Learnings

- Transport diagnostics feature scope: Core responsibility is surfacing enough diagnostic context to help users disambiguate between backchannel and public tunnel failures without exposing internal URLs.
- Release hygiene: Confirmed that downstream-demo diagnostics follow the established product commit pattern (clean staging, conventional commits, single unit delivery).
- Team coordination: Three-agent workflow (Blathers implementation, Tangy testing, Mabel landing) continues to yield clean history.
- Timeout diagnostics deepening: When extending diagnostic surface for timeouts, critical to expose backchannel state, target path, and cancellation reason to enable operators to triage public-tunnel vs. backchannel wiring issues.

## Cross-Agent Update: 2026-05-03T22:46:14Z Scribe Consolidation

**Inbox decision merged:** Deeper Downstream Timeout Diagnostics Landing decision logged to decisions.md (commit 442c5e9).

**Orchestration record logged:**
- `.squad/log/2026-05-03T22-46-14Z-timeout-diagnostics.md`

**Decisions merged to main registry:**
- Deeper Downstream Timeout Diagnostics Landing (status: IMPLEMENTED, commit 442c5e9)
- Team approval chain: Tom Nook (Lead), Blathers (Implementation), Tangy (Test coverage)

**Clean-up:** Deleted inbox file `mabel-timeout-diagnostics-landing.md`.

**Team bookkeeping:** Complete. Product commits 17edf9c and 442c5e9 now linked to decision history.

---

## 2026-05-03: Business API Arrival Instrumentation — Product Commit to Main

**Status:** ✅ Complete (product commit 8e1cd68 pushed to origin/main)

**Work:** Landed arrival instrumentation for Business API with safe caller trace ID forwarding.

**Scope Applied:**
- Staged only three product files: MockBusinessApp/Program.cs, DownstreamDemoController.cs, DashboardLocalEndpointsValidationTests.cs
- Left unrelated .squad/ bookkeeping changes unstaged
- Created single, releasable product commit

**Commit:** `8e1cd68` — feat(diagnostics): add Business API arrival instrumentation with safe caller trace ID forwarding
- MockBusinessApp arrival middleware logs before authentication (method, path, trace ID, auth header, caller trace ID)
- Handler entry point logs authentication status alongside request context
- TestSite safely forwards caller trace ID header (X-Prism-Caller-TraceId) for cross-service correlation
- Test validates contract: caller trace ID is captured from request and forwarded to Business App
- Enables operators to correlate dashboard request traces with Business API arrival diagnostics

**Push Result:** origin/main now points to 8e1cd68. Product changes live and ready for users.

**Pattern:** Continues established product/bookkeeping separation workflow. Three-agent coordination (Blathers implementation, Tangy testing, Mabel landing) maintains clean git history.

---

## 2026-05-04T00:01:43.530+01:00: Business API Arrival Instrumentation — Bookkeeping Merge Complete

**Status:** ✅ Documented

**Decision Recorded:** "Business API Arrival Instrumentation Landing" (2026-05-03T23:46:52.875+01:00, IMPLEMENTED, commit 8e1cd68)

**Summary:** Business API arrival instrumentation is live on main. Operators can correlate TestSite requests with Business App diagnostics through safe trace ID forwarding via `X-Prism-Caller-TraceId` header. Middleware logs at pre-auth and handler-entry points without exposing credentials or internal URLs.

**Scope Achieved:** Clean separation of product deliverables (commit 8e1cd68) from bookkeeping (decisions.md consolidation).
