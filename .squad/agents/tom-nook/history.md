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

## 2026-05-17: Workflow Editor Foundation & Reframe

### 2026-05-17T12:32:29.455640Z

Reviewed overall CI and E2E architecture; provided architectural recommendations for faster CI

### 2026-05-17T16:56:41.297+01:00 | Workflow separation review

- The extraction branch is **partial split**: authoring in `UmbracoPrism.WorkflowEditor`, but MockBusinessApp still owns too much composition.
- Clearest remaining coupling is host/composition, not model. RuntimeBackstage abstraction is the next gap.

### 2026-05-17T17:33:13.797+01:00 | Merge-readiness review for reference split

- PR #53 not merge-ready yet: GitHub shows `core-tests` failing despite local fixes. Uncommitted worktree changes block state validation.
- Walkthrough doc is partially documented (PNGs present but not embedded); doesn't reflect new reference-shell flow.

### 2026-05-17T20:02:23.686+01:00 | Workflow editor state audit

- Main contains **foundation/reference slice**, not full V1: shell page, graph/inspector/conversation Lit components, filesystem-backed store, deterministic projector, preview/apply endpoints.
- Natural-language path is demo scaffolding (one canned prompt, local fabrication); no workflow MCP server, no Copilot wiring.
- Projection/apply only partially wired: persists authored JSON but doesn't regenerate runtime seeds.
- Backoffice presence is thin iframe host in TestSite; implementation doesn't match final V1 topology yet.
- Clear summary: editor foundation + authoring model + projector + mocked proposal loop exist; real agent loop, MCP, Copilot, runtime publish, full authoring UX not yet present.

### 2026-05-17T22:05:30.472+01:00 | Workflow editor doc reframe

- Reframed `docs/design/workflow-editor-v1/README.md` around **three product concepts only**: workflow editor, workflow engine, forms engine (editor is V1 focus).
- Demoted publishing, Umbraco hosting, Copilot/MCP to secondary seams.
- Updated integration and agentic surfaces docs to match simpler framing.

### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch — Workflow editor doc restructuring

- Completed full rewrite of `docs/design/workflow-editor-v1/` to establish three-product frame as primary narrative.
- Produced three decisions merged to `.squad/decisions.md`:
  1. **tom-nook-workflow-editor-doc-reframe.md** — README reframed around editor responsibilities and action-model split.
  2. **tom-nook-workflow-editor-simplification.md** — Doc restructuring (7-section pattern) + 6 follow-on implementation phases.
  3. **tom-nook-workflow-editor-state-audit.md** — Foundation/reference slice classification; sequenced: real agent plane → Copilot → runtime publish → UX completion.
- Pattern: three products first, implementation seams (projection, MCP, Umbraco, validation) demoted. Scope guardrails documented.

## Learnings

### 2026-05-17T22:05:30.472+01:00 | Design rewrite learnings

- **Three-product frame is clearer** than plane-heavy story. Lead with workflow editor, workflow engine, forms engine; tuck implementation seams behind.
- **Editor owns full authored definition**: stage catalogue, graph transitions, action attachments, parameter editing, validation, preview, history, undo/redo, copy/paste, help. Raw runtime JSON stays as generated/debug artifact.
- **Action split**: design-time action descriptors vs runtime action handlers. Editor asks catalog what exists; engine resolves named handler at runtime. Reference app uses DI-registered handler registry (not lambdas) for testability and portability.
- **V1 demonstration shape**: MockBusinessApp stays as reference showing how to compose pieces; reusable logic moves to dedicated libraries. "Right tool for right job" remains explicit: Copilot CLI for NL drafting/orchestration, workflow-specific MCP tools for semantic ops, runtime never writes AI directly.

---

See history-archive.md for pre-2026-05-16 history.
