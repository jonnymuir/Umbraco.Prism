## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---


## Learnings (Summarized)

### 2026-05-17T12:45:42.676+01:00 — Fast-Fail CI Strategy for Flaky Tests

### 2026-05-17 — Recent Session Summary

- Analyzed CI timing and localhost-auth Playwright strategy
- Documented E2E CI architecture recommendations
- Coordinated with Tom Nook on faster-fail strategy
- Decision entries merged to shared decisions.md

## 2026-05-17T12:32:29.455640Z

Analyzed CI timing and localhost-auth Playwright strategy; wrote decision inbox entry for E2E strategy

## 2026-05-17T13:36:14.940+01:00 — E2E Strategy Implementation

**Task:** Implement complete fast-fail + shared-environment strategy for localhost-auth tests.

**Delivered:**
1. CI fast-fail with `--max-failures=1` on localhost-auth lane
2. Dedicated `planning-workflow-editor-smoke` CI job for early signal
3. Playwright worker fixture (`shared-app-host-fixture.ts`) for shared AppHost across specs
4. Planning walkthrough migrated to use worker fixture (removed per-spec lifecycle)
5. Preserved all diagnostics and isolation guarantees

**Key Learnings:**
- **Worker fixtures need `auto: true`** if tests don't explicitly reference them in signatures
- **globalSetup doesn't share state** with worker processes; worker-scoped fixtures are the correct pattern for shared infrastructure
- **Explicit > implicit for isolation:** resetWorkflows() in beforeEach is the right contract for workflow state, not just relying on runtime reset
- **AppHost.start() is already idempotent** (checks `if (this.child) return`), making it safe for multiple specs to call
- **Test passed in 1.1min** (33s startup + execution) vs previous ~3min+ per-spec cost

**Performance Impact:**
- Before: 12 walkthroughs × ~1min startup = ~12min baseline
- After: 1 startup (~33s) for entire batch
- **Expected CI improvement: ~25-28min → ~10-12min for full suite**

**Validation:**
- Local planning smoke run: **1 passed (1.1m)**
- Worker fixture logs confirm clean startup/teardown lifecycle
- Isolation verified: fresh browser context + resetWorkflows() beforeEach
- Decision written to inbox, history updated
- Committed (`7d7f7b9`) and ready to push

**Next:** Push to trigger CI and validate smoke lane runs before broader suite.
