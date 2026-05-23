# History: Tangy (Tester)

**Summary:** Layout professionalisation and browser-surface testing. See `history-archive.md` for full session-by-session record.

---

## 2026-05-23T07:42:49Z — Session Orchestration & Decisions Integration

**Scribe orchestration completed:** All decisions merged to `.squad/decisions.md`; orchestration logs created.

**Team status:**
- Isabelle: Tabbed layout redesign delivered; Canvas tab primary, confidence tools secondary
- Tangy: Layout professionalization behavioral proof delivered (22 tests, semantic hooks documented)
- Mabel: Host philosophy documentation moved to guides; thin-host philosophy established

**Current test state:**
- Existing tests (keyboard 7/7, planning walkthrough) remain GREEN
- Layout professionalization tests (22 tests) FAILING until Isabelle's implementation integrates
- Expected state after integration: all tests GREEN

**Decisions merged:**
1. User directive (host minimalism)
2. Workflow editor tabbed layout redesign
3. Host philosophy (keep reference shell minimal)
4. Layout professionalisation behavioral proof

**Next phase:** Integration testing; run 5-command validation gate when shell implementation lands.

---

## 2026-05-23T08:30:10.563+01:00 — Layout Professionalization Behavioral Proof

**Status:** Behavioral proof landed, awaiting Isabelle's shell refactor implementation

**What I delivered:**

1. **New dedicated test file:** `tests/workflow-editor/layout-professionalization.spec.ts`  
   19 tests proving the five layout professionalization dimensions:
   - Host chrome minimization (3 tests) — hero ≤15% viewport, explanatory prose removed, integration rail hidden
   - Simplified launch flow (3 tests) — API base not mainline, launch card streamlined, workflow selection utility-first
   - Editor surface prioritization (3 tests) — editor ≥80% viewport, editor as page not widget, section chrome removed
   - Keyboard/screen reader accessibility (4 tests) — skip link, tab order, shortcuts preserved, landmark semantics
   - Editor functionality preservation (6 tests) — outline, graph/list toggle, inspector, confidence tabs, swim lanes, pointer-unblocked stages

2. **Decision document:** `.squad/decisions/inbox/tangy-layout-professionalisation-proof.md`  
   Documents exact behavioral expectations, semantic hooks for Isabelle, and validation gate commands.

3. **Baseline validation:**  
   - ✅ Client build (npm run build) — green
   - ✅ Backend tests (810 tests) — green
   - ✅ Keyboard tests (7 tests) — green
   - ⏳ Shell tests (26 tests) — not yet run (long-running)
   - ⏳ New layout tests (19 tests) — expected to fail until Isabelle implements

**Working in parallel with Isabelle:** All behavioral expectations documented inline with `BEHAVIORAL HOOK REQUEST FOR ISABELLE` comments. Tests provide concrete acceptance criteria (viewport ratios, element visibility, semantic structure).

**Validation commands (5-step gate):**
```bash
cd src/UmbracoPrism.Client && npm run build
npm run test-storybook:ci:all
npx playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line
npm run test:playwright:planning-smoke
npx playwright test tests/workflow-editor/layout-professionalization.spec.ts --reporter=line
```

**Expected test states:**
- Until Isabelle's refactor: layout tests FAIL (expected), existing tests GREEN
- After Isabelle's refactor: all tests GREEN

**Plain-language verdict:**  
The behavioral proof is complete and landed. It proves the five layout improvements that make the reference host professional and usable. Tests document exact acceptance criteria for Isabelle's implementation with concrete thresholds (viewport ratios, tab counts, semantic structure).

---

**Full history:** See `.squad/agents/tangy/history-archive.md` for complete session record (2026-05-18 onward).
