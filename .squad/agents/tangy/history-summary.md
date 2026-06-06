# Tangy History Summary

**Period:** 2026-05-25 to 2026-05-31

## Active Work

### 2026-05-30 — Slice 7: Visual Regression Strategy + Baseline Suite
✅ DELIVERED: 25 new visual/behavioral tests proving lane fit, overlap, text constraints, scroll behavior, arrow legibility.
- Test files: `tests/workflow-editor/workflow-canvas-lane-fit.spec.ts`, `workflow-canvas-no-overlap.spec.ts`, `workflow-canvas-text-fits.spec.ts`, `workflow-canvas-scroll.spec.ts`, `workflow-canvas-arrows.spec.ts`, `workflow-editor-ergonomics.spec.ts`
- Strategy doc: `docs/testing/workflow-editor-visual-tests.md` (maxDiffPixelRatio 0.02, 0% flake budget, 1440×900 viewport)
- Helpers: `tests/workflow-editor/support/canvas-helpers.ts` with `CANONICAL_SCENARIOS` registry and `measureGraph()` geometry assertions
- Key learning: DOM-geometry assertions far more durable than pixel snapshots for canvas concerns
- Status: 25 passed + 1 fixme; awaiting Isabelle's sticky header implementation

### 2026-05-26 — Multi-Lane Gateway Behavioral Test Slice (#83/#84/#85)
✅ MERGED: 32 backend/frontend contracts covering gateway editor UX, join waiting copy, parallel-lane runtime safety.
- Files: `MultiLaneGatewayContractTests.cs` (17 backend facts, 14 live + 3 skipped), `workflow-parallel-lanes.spec.ts` (9 Playwright, 6 live + 3 skipped), `workflow-editor-gateways.spec.ts` (8 total, 7 live + 1 skipped)
- Final counts: Backend 129 passed + 3 skipped; Gateway editor 7 passed + 1 skipped; Parallel lanes 6 passed + 3 skipped
- Key contract: `[data-prism-gateway]` carries lane attribution but exists as graph siblings, not DOM children of lane columns
- Status: All guardrails in place; awaiting Isabelle/Blathers implementation

### 2026-05-25 — Gateway-Only Behavioral Proof Rewrite
✅ COMPLETE: Rewritten frontend/backend tests to enforce gateway-only routing model (no transitions as primary objects).
- Quality gates now fail: hybrid models (stages + transitions + badges), transition chips as user-facing objects, stage-level waiting constructs
- Frontend contracts: graph reads stage→gateway→node, validation points back to gateways
- Backend contracts: join-gateway waiting language, reject stage-to-stage routes
- Status: Model stability locked while Isabelle/Blathers align frontend/backend

### 2026-05-30 — A11y + Test Quality Audit (Slices 1–3b)
✅ REVIEW COMPLETE: Documented label-leak patterns, arrow glyph accessibility, single-point-of-failure coverage gaps in validators.
- Key findings: inspector display names not synced in outline; `→` glyph needs aria-label; `[Obsolete]` shim path has zero round-trip tests; PROJ140 validator OR'd triggers need isolated test coverage
- Output: `.squad/decisions/inbox/tangy-editor-reset-a11y-test-review.md`
- Status: Accessibility debt documented; awaiting Slice 5+ coverage

### 2026-05-31 — Definition-Tab Find/Scroll Coverage Note
✅ AWARENESS: Isabelle's 5 new Playwright tests for Definition pane Ctrl/Cmd+F and wheel scroll behavior now live. Test portfolio expanded; no action required.

### 2026-05-31 — Triage Flagged: definition-tab-sync.spec.ts (2 Failures)
⚠️ PENDING: Pre-existing bidirectional JSON↔Canvas sync failures in `definition-tab-sync.spec.ts` flagged by Isabelle's Definition-tab UX commit. Candidate for Tangy triage.

---

**Full session-by-session record:** See `history-archive.md`
