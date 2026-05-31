# Slice A — Legacy purge (decision summary)

Branch: `squad/82-named-lanes-editor-slice`
Date: 2026-05-31
Personas: Blathers (backend) + Isabelle (frontend) — bundled into a single PR per directive.

## What landed

- All `Legacy*` properties, `[Obsolete]` getters, and the `LegacyKindRaw` /
  `HasLegacyWaitingPayload` shims are gone from `AuthoredStage` and
  `AuthoredTransition`.
- Unknown stage kinds are no longer silently rewritten to `Question`; the
  schema validator emits **PROJ005** ("Unknown stage kind '<x>'. Allowed
  kinds: Question, CheckAnswers, Confirmation, TaskList.").
  Empty / missing `type` still defaults to `Question` on both sides
  (mirrors C# `Enum.TryParse` early-return behaviour — required for
  back-compat with workflows authored without `type`).
- PROJ140 is retired; `WaitingMetadata`'s "Legacy stage-level…" doc-comment
  cleaned up.
- Frontend dual-key fallbacks (`stageKey`/`displayName`/`kind`/`fromStage`/…)
  are gone from `normaliseStage`/`normaliseField`/`normaliseGateway`/
  `normaliseTransition`. Only canonical wire names are read.
- `legacyKindRewrittenFrom` removed from the TS `AuthoredStage`;
  `stage-legacy-kind-rewritten` removed from the validation issue union.

## Conventions for downstream slices (pin these)

1. **TS shape ≠ wire shape until Slice C.** TS objects still carry
   `stageKey`/`displayName`/`kind` and `fromStage`/`toStage`/`action`. The
   `serialiseWorkflow` exported from `workflow-authoring-client.ts` is the
   only sanctioned TS→wire mapper. **Every Storybook stub that returns a
   workflow JSON must round-trip via `serialiseWorkflow` first** —
   otherwise normalise reads undefined for every canonical key and the
   editor renders empty stage cards.
2. **`AuthoredHandoff.FromStage` / `.ToStage` are canonical** on that
   record (different type from `AuthoredTransition`). Do not delete or
   rename them.
3. **PROJ005 is the new home for "unknown stage kind".** Validators in
   the frontend (`workflow-definition-lint.ts`) and backend
   (`AuthoredWorkflowSchemaValidator.cs`) both speak this code now; do
   not reintroduce a silent rewrite.
4. **`MockBusinessApp/workflow-seeds/planning.json`** is the runtime
   projected shape (different file class from
   `workflow-editor/fixtures/planning.workflow.json`). Slice A only
   migrated the editor fixture; do not edit the runtime seed without
   coordinating with whoever owns runtime projection.

## Deferrals (flagged for Tom Nook)

- No endpoint-level 400 conversion. Tom's plan suggested
  `/api/workflow-authoring/workflows/{key}/publish` should return 400
  when JSON contains retired aliases like `fromStage`. Current behaviour
  is 200 + diagnostics. Coverage is at the projector level
  (PROJ005/PROJ106) — fix this in a later slice if the API contract is
  formalised.
- No dedicated unit test for `mapStageKind` throwing on an unknown
  explicit kind: vitest is not present and a Playwright test for a
  one-line throw is high-scaffolding. Relying on the four-workflow
  contract for non-regression.

## Test results

- Backend: 860 / 860 Core tests green; `dotnet build` clean.
- Frontend: `npm run build` ✅; `npm run build-storybook` ✅;
  `npx playwright test tests/workflow-editor/` = 87 passed + 1 flaky-pass-on-retry +
  11 skipped (= 88-pass baseline restored). The four pre-existing
  failures (3× `workflow-editor-simulation.spec.ts`, 1×
  `workflow-stage-type-options.spec.ts`) are unrelated to Slice A —
  confirmed by re-running the same suite at HEAD `6d84e39` with my
  changes stashed.
