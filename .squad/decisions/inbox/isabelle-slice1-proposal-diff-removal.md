### 2026-05-30T11:15:00+01:00: Slice 1 (frontend) — proposal-diff overlay & chat-drafter scaffolding removed
**By:** Isabelle (Frontend Dev & Accessibility Lead)
**What:**
- Deleted Web Components: `prism-proposal-diff.ts`, `prism-proposal-diff.stories.ts`, `workflow-authoring-mock-drafter.ts`.
- `prism-workflow-editor.ts`: dropped `prism-proposal-diff` import, modal doc comment, `_proposal` + `_modalOpen` state fields, `_handleProposalAccept` / `_handleProposalReject` / `_applyProposalLocally` / `_closeModal`, the `<prism-proposal-diff>` modal markup, the `this._modalOpen ||` branch from `_handleEditorKeydown`, and the `prism-proposal-diff { … }` CSS selector. **Preserved `.modal-backdrop` and its `/* ---- Modal overlay ---- */` comment** — still used by the F1 shortcut/help dialog rendered by `_renderShortcutGuide`.
- `prism-workflow-editor.stories.ts`: removed `draftProposal` import and the `ModalOpen` ("Proposal Modal Open") story which poked private `_proposal`/`_modalOpen` state.
- `workflow-authoring-client.ts`: removed `previewProposal` and `applyProposal` exports plus the `ProposalEnvelope` type import. `publishWorkflow` is **untouched** — save protocol still posts to `/publish`.
- `types.ts`: removed `ProposalEnvelope`, `ProposalAgent`, `ProposalOp`, `ProposalPlacement`, `STUB_PROPOSAL`.
- `fixtures/index.ts`: no change required — no proposal stubs were in the file.

**Why:** Workflow editor scope reset (per Jonny's 2026-05-30T11:05 directive). Proposal-diff modal and chat-drafter scaffolding are being torn out so stages + gateways can be the only authoring model in subsequent slices.

**Residual surfaces to watch in later slices:**
1. `ValidationResult` interface in `types.ts` is now an unused export (was only consumed by `ProposalEnvelope`). Left in place because it was outside the explicit deletion list — fold it into a follow-up types-tidy pass.
2. `.modal-backdrop` CSS is now shared by exactly one consumer (the F1 shortcut/help dialog). If that dialog is restyled, the class can move into `prism-help-panel` scope.
3. Storybook still has a "Workflow Authoring" addon-controls section that referenced agent-driven story flows in narration only — no code change needed but copy should be reviewed when the agentic-surfaces doc is marked historical.
4. Backend twin (Blathers, Slice 1 backend half): preview endpoint deletion, `WorkflowPreviewService` removal, `WorkflowPatchService.ApplyAsync` reshape. Per Jonny's directive `ProposalEnvelope` survives as the save protocol on the backend (publish only).

**Verification:**
- `npm run build` ✅ (tsc + 2 vite builds clean).
- `npm run build-storybook` ✅.
- Targeted Playwright run — `workflow-graph-visual`, `workflow-graph-keyboard`, `workflow-editor-shell`, `workflow-editor-help`, `workflow-editor-stage-preview` all green.
- `workflow-editor-validation.spec.ts:8` and three `workflow-editor-simulation.spec.ts` tests fail **pre-existing** on HEAD (reproduced with my changes stashed) — failures are unrelated to proposal-diff removal and belong to other in-flight work in the squad/82 branch.
- Grep across `src/` for `prism-proposal-diff`, `draftProposal`, `ProposalEnvelope`, `STUB_PROPOSAL`, `_modalOpen`, `_proposal`, `previewProposal`, `applyProposal`, `workflow-authoring-mock-drafter` returned **zero** matches.

**Branch:** `squad/82-named-lanes-editor-slice`. No PR opened — Blathers is delivering the backend half in parallel on the same branch.
