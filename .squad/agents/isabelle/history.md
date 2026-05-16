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

### 2026-05-16T13:20:33.659+01:00 — Workflow Editor V1: Authoring UX & Accessibility

- **Dual-mode graph navigation** (visual graph + linear list, toggled by `L`) is the correct accessibility pattern for graph canvases — gives AT users full operability while preserving visual-first design intent.
- **Conversation Pane as primary agent surface** — persistent pane for NL requests, proposals, and provenance keeps author in context; collapsible but never hidden.
- **Agent proposal diff is hunk-level** — per-hunk accept/reject controls; bulk-accept is convenience, not primary.
- **Provenance on every field** — authors trace field origins back to introducing agent turn, weeks later.
- **Focus management non-disruptive** — proposals announced via ARIA live region only; focus stays; author-driven review.
- **Explicit save (not autosave)** — dirty state + beforeunload warning is safe baseline for in-flight proposals.

### 2026-05-16T10:59:37.438+01:00 — Workflow Editor UX Direction

- **Split authoring surface:** definition library → editor workspace → simulation/validation, not card-to-JSON jump.
- **Runtime shell visibility:** show inferred shell as feedback; authors edit model (components, transitions, roles, pages); shell inference remains runtime-driven.
- **Preserve narrative pattern:** citizen-facing + reviewer handoff; editor surfaces both lanes.
- **Progressive routing:** compact "Experience & Routing" step with optional advanced drill-down.
- **Safe co-authoring:** proposals → diffs → preview → approval → apply; never silent mutations.

### 2026-05-04 | Recent Sessions

- **Screenshot-Mode Control:** Implemented `prism-screenshot-mode` cookie to suppress mobile helper during walkthroughs.
- **Walkthrough Coverage Audit:** Identified gaps (back/edit flows, validation tests, mobile viewports); closure path documented.
- **Walkthrough Discovery:** All findings merged to decisions.md; implementation phase ready.

---

**Archive:** Pre-2026-04-22 history archived to `.squad/agents/isabelle/archive/` for traceability. SEC-005 closed. Component system migration (4-22) complete. GDS integration and accessibility patterns established (4-13, 4-19, 4-22).
