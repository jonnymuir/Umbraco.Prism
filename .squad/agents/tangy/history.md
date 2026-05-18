# History: Tangy (Tester)

## 2026-05-18: Issues #70–#71 Quality Gates — Recent Summary

### Issue #71 Workflow Runtime in Umbraco Surfaces
**Status:** ✅ APPROVED (Acceptance-complete)

**Acceptance Criteria Met:**
- Workflow start page loads in Umbraco ✅
- Forms render for first stage ✅
- Submit creates instance and advances stage ✅
- Back-stage visibility enforced (reviewer-only) ✅
- Instance state persisted correctly ✅
- Resume/dashboard works ✅
- Tests for planning workflow through Umbraco ✅

**Evidence:**
- Backend: 782/782 tests passing
- Controllers: PrismWorkflowPageController base + WorkflowPageController (TestSite) + WorkflowHubController
- Auth: PrismMemberCookie enforcement with POST-Redirect-GET pattern
- State: StateVersion tracking for concurrency
- Playwright: Infrastructure timing noted (not blocker); structural test coverage present

**Verdict:** Production-ready for merge.

---

### Issue #70 Workflow Runtime Action-Handler Registry
**Status:** Quality gate established

**Required Evidence:**
1. Runtime contracts: `IWorkflowActionHandler`, `IWorkflowActionRegistry`, execution context/result types
2. DI registration in MockBusinessApp with 5+ concrete handlers
3. Catalog endpoint resolves from runtime registry (not editor-only)
4. Focused .NET tests: `GetCatalog()`, `Resolve(actionType)`, `ExecuteAsync(...)`
5. Reference-host smoke test

**Decision:** Keep handler registration in MockBusinessApp boundary; reuse BuiltInActionCatalogProvider to avoid catalog drift.

**Design Principle:** Generic WorkflowRuntime stays orchestration-focused; host-specific handler implementations (forms, case, notification) live in reference app.

---

## Earlier Issues #64–#69: Archive Reference

Previous work on issues #64 (copy/paste), #65 (validation), #66 (help/shortcuts), #67 (preview), #68 (simulation), #69 (editor hosting).

**Status:** All acceptance-complete. See `history-archive.md` for gate details and learnings.

---

## Learnings

### Quality Gate Pattern (Refined across #64–#71)
- Each slice defines 5–7 seams including .NET tests, client build, Storybook CI, keyboard contract, slice-specific Playwright, and planning smoke
- Infrastructure noise (cold-start timing, route convergence, seed data) must be explicitly separated from feature gaps
- Retry-only flakes in unrelated specs do not invalidate acceptance unless they propagate to the slice itself

### Honest Acceptance Boundaries
- Distinguish acceptance evidence from surrounding health
- Document missing seams clearly if not yet implemented
- Call out environment vs. feature blockers with equal weight
- Shared surfaces (catalog, validation, simulation) reduce future drift by design

### Auth Patterns in Umbraco Context
- Framework-level `[Authorize]` attributes establish challenge point
- Nonce filtering must happen before nonce creation (not after) to prevent stale tokens
- TempData + POST-Redirect-GET preserves validation state across round-trips
- Claims-based pre-population works reliably for reader scenarios; reviewer role checks need explicit auth context guards

