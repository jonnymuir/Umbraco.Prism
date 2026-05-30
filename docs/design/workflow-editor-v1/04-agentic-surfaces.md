# 04 — Agentic support and test seam

> **Status: Historical** — paused 2026-05-30 per scope-reset directive. The proposal-diff modal, conversation pane, and chat drafter described here were retired. Kept for design archaeology only.

**Date:** 2026-05-16  
**Author:** Tangy (Tester)  
**Status:** Historical (paused 2026-05-30)

---

## 1. Purpose & Scope

This document specifies the supporting agent-facing surfaces around the V1 workflow editor: the structured, machine-facing capabilities that let agents, including GitHub Copilot, propose, validate, preview, and apply workflow changes without ever mutating live runtime state directly. It also defines the **test seam** — how the proposal-first support layer stays observably testable end to end, anchored on the planning application reference workflow. Overall editor, workflow-engine, and publish-model decisions are covered in the sibling design documents; this document is deliberately secondary to that editor-first framing.

---

## 2. Operating Model

The workflow editor can optionally use a **proposal-first** agent loop. No agent may write directly to a live `WorkflowDefinitionFile` or a running workflow instance. Every agent-initiated change flows through six ordered supporting surfaces:

### Surface 1 — Authored Workflow Source (Blathers' Authored Model)

The human-editable source of truth. Stages are modelled at service-design level (`kind`, `serviceZone`, `views`, `handoffs`, `deadlines`) rather than raw Prism states. This is the document agents read to understand current workflow intent and the document they modify via patch operations in a proposal. It is never projected directly; the projector owns that step.

### Surface 2 — Deterministic published runtime file (`WorkflowDefinitionFile`)

The editor's publish step compiles the authored source into a Prism-compatible `WorkflowDefinitionFile`. Publishing is deterministic and idempotent: the same authored source always produces the same runtime file. Agents never edit this file directly. It exists as the output of publishing and the input to runtime validation. Re-running publish after an applied proposal is how the runtime contract is updated.

### Surface 3 — Structured Diff + Provenance Artifact (Proposal Envelope)

Every agent change is packaged as a **proposal envelope** (see Section 4): a machine-readable JSON document capturing the intent, patch operations, placement context, rationale, validation results, and preview reference. The envelope is the unit of review — humans approve or reject it. It is stored as a file alongside the authored source before application and written to the audit log on apply.

### Surface 4 — Validate Command

A fast, synchronous command that checks a proposal envelope (or authored source directly) across four layers:

1. **Schema validity** — authored model structure conforms to the current authored schema version.
2. **Graph integrity** — every stage reachable, every handoff resolvable, no orphan states post-projection.
3. **Role/action legality** — every actor reference resolves to a defined role; every transition action is permitted for that actor in that service zone.
4. **Component rules** — component shapes are compatible with their inferred shell (`question`, `check-answers`, `confirmation`, `waiting`, `task-list`, `status-timeline`); fieldsets contain only `InputComponent` children.

Latency budget: **< 250 ms**. Validation must not trigger a full end-to-end test suite run; that stays outside the authoring loop.

### Surface 5 — Preview / Simulate Command

Accepts a proposal envelope (pre-apply) or a projected `WorkflowDefinitionFile` and produces two artefacts:

- **State graph render** — a Mermaid or structured JSON representation of all states and transitions post-proposal.
- **Journey render** — a simulated step-by-step trace of one or more named actor paths (e.g. "public applicant completes planning journey with ID&V step enabled").

Latency budget: **< 1 s**. The preview is stored by reference (`previewArtifactRef`) in the proposal envelope and surfaced in the editor UI for human review before approval.

### Surface 6 — Focused Test Hooks (Planning Workflow as Executable Spec)

Narrow entry points that allow a CI pipeline or an agent orchestrator to run targeted behavioural contract tests against the current authored workflow state without starting the full application. See Section 9 for the complete planning workflow executable spec.

---

## 3. Tool Boundary — Reuse vs Build

The key principle is simple: **use the appropriate tools for the appropriate jobs and avoid reinventing the wheel**. Agentic support is only valuable where it helps the workflow editor safely draft, review, and publish changes.

| Capability | Owner | Rationale |
|---|---|---|
| Natural-language intent capture ("add identity verification before reviewer") | **Reuse — GitHub Copilot / general LLM** | General NL understanding; no workflow-domain semantics required at this layer |
| Drafting proposal rationale and NL summary | **Reuse — GitHub Copilot / general LLM** | Text generation; workflow context injected via structured authored model, not inferred from raw JSON |
| Repo file edits (creating authored workflow files, updating seeds) | **Reuse — GitHub Copilot / general LLM** | Standard file-edit operations well within Copilot's capability profile |
| Orchestration (calling validate → preview → apply in sequence) | **Reuse — GitHub Copilot / general LLM** | MCP tool invocation; no workflow-domain knowledge required in the orchestrator |
| Calling validate and preview hooks | **Reuse — GitHub Copilot / general LLM** | Tool invocation; the hooks themselves are workflow-aware |
| Safe projection (authored source → `WorkflowDefinitionFile`) | **Build — workflow-aware capability** | Projection requires understanding of shell inference, component rules, and Prism runtime contracts |
| Semantic diffing on the Authored Model | **Build — workflow-aware capability** | Diff must operate on stage/handoff/actor semantics, not on raw JSON key order or shape |
| Insertion-point resolution (e.g. "before reviewer handoff") | **Build — workflow-aware capability** | Requires understanding stage graph topology and named handoff points |
| Placement of inserted steps (e.g. external ID&V at correct handoff) | **Build — workflow-aware capability** | Must respect actor ownership, service zone, and transition action legality |
| Preview rendering (state graph + journey trace) | **Build — workflow-aware capability** | Requires traversing the projected graph and simulating actor paths |
| Structural validation (graph, schema, component rules) | **Build — workflow-aware capability** | Domain rules; must not be inferred by a general agent from raw JSON shape |
| Test hook entry points (focused Playwright/XUnit seams) | **Build — workflow-aware capability** | Requires test infrastructure wired to the authored model and planning spec |

### Anti-patterns (never do these)

- **General agent inferring workflow graph semantics from raw JSON** — a general LLM must not try to understand graph reachability or component shell inference by reading `WorkflowDefinitionFile` JSON directly. It must use the validate and preview tools.
- **UI-only automation as primary authoring API** — agents must not drive the admin UI as their primary mutation mechanism. The proposal envelope + MCP command surface is the contract.
- **Hidden mutations without structured diff/provenance** — every agent-initiated change must produce a proposal envelope. Silent edits to authored sources or runtime files are not permitted.

---

## 4. Proposal Envelope Schema

```json
{
  "id": "string (UUID)",
  "createdAt": "ISO 8601 datetime",
  "agent": {
    "kind": "github-copilot | custom-agent | human-assisted",
    "identity": "string (e.g. 'github-copilot/gpt-4o', 'tangy/squad')",
    "sessionRef": "string (optional session or conversation ID)"
  },
  "targetWorkflowId": "string (definitionKey of the authored workflow)",
  "rationale": "string (natural-language summary of the change and why)",
  "ops": [
    {
      "op": "insert-stage | remove-stage | update-stage | insert-handoff | update-transition",
      "path": "string (JSON Pointer into the authored model, e.g. '/stages/2')",
      "value": { /* the new or replacement authored stage/handoff/transition object */ },
      "before": "string (optional stageKey — insert before this stage)",
      "after": "string (optional stageKey — insert after this stage)"
    }
  ],
  "placement": {
    "insertAfterStageKey": "string (stageKey after which the new stage is inserted)",
    "insertBeforeStageKey": "string (stageKey before which the new stage is inserted)",
    "handoffId": "string (optional — the specific handoff point being modified)",
    "transitionId": "string (optional — the transition being extended or replaced)"
  },
  "validationResult": {
    "status": "pass | fail | not-run",
    "checkedAt": "ISO 8601 datetime | null",
    "errors": ["string"]
  },
  "previewArtifactRef": "string (path or URI to rendered state graph + journey trace | null)"
}
```

### Worked Example — "Insert external ID&V before final review" on the planning workflow

This is a complete, concrete envelope for the V1 demo insertion: adding an external identity-and-verification stage before the `reviewer-assessment` stage in the planning application workflow.

```json
{
  "id": "a3f7c221-8b14-4e02-9d61-f23a10b5e7c9",
  "createdAt": "2026-05-16T13:20:33.659+01:00",
  "agent": {
    "kind": "github-copilot",
    "identity": "github-copilot/gpt-4o",
    "sessionRef": "copilot-session-2026-05-16-planning-idv"
  },
  "targetWorkflowId": "planning-permission",
  "rationale": "Insert a mandatory external identity-and-verification (ID&V) stage between application submission and reviewer assessment. The ID&V step validates the applicant's identity with an external provider before the case is assigned to a planning officer. This addresses the requirement for verified identity before a discretionary planning decision is made.",
  "ops": [
    {
      "op": "insert-stage",
      "path": "/stages/4",
      "value": {
        "stageKey": "identity-verification",
        "displayName": "Identity Verification",
        "kind": "waiting",
        "serviceZone": "frontstage",
        "route": "/apply-for-planning-permission/identity-verification",
        "views": [
          {
            "audience": "public",
            "components": [
              {
                "type": "waiting",
                "content": "We are verifying your identity with our trusted provider. This usually takes a few minutes.",
                "expectedWaitSeconds": 120,
                "pollIntervalMs": 5000,
                "allowDefer": true,
                "deferMessage": "You can leave this page and return. We will notify you when verification is complete."
              }
            ]
          }
        ],
        "handoffs": [
          {
            "handoffId": "idv-complete",
            "toStageKey": "reviewer-assessment",
            "trigger": "system",
            "condition": "idv.result == 'verified'"
          },
          {
            "handoffId": "idv-failed",
            "toStageKey": "identity-verification-failed",
            "trigger": "system",
            "condition": "idv.result == 'failed'"
          }
        ],
        "permissions": {
          "canView": ["public", "member", "caseworker"],
          "canAdvance": ["system"]
        }
      },
      "before": "reviewer-assessment"
    }
  ],
  "placement": {
    "insertAfterStageKey": "check-answers",
    "insertBeforeStageKey": "reviewer-assessment",
    "handoffId": null,
    "transitionId": null
  },
  "validationResult": {
    "status": "pass",
    "checkedAt": "2026-05-16T13:20:33.659+01:00",
    "errors": []
  },
  "previewArtifactRef": "previews/planning-permission/a3f7c221-state-graph.json"
}
```

---

## 5. Command Surface

Commands are exposed in two equivalent forms: as MCP tools (for agents) and as CLI/HTTP endpoints (for humans, CI, and scripts).

### 5.1 MCP Tools

| Tool | One-line Contract |
|---|---|
| `workflow.draft-proposal` | Given a target workflow ID and a natural-language change description, produce a proposal envelope with ops, placement, and rationale populated. Does not apply or validate. |
| `workflow.validate` | Given a proposal envelope or authored workflow source, run schema, graph, role, and component validation. Returns `validationResult`. Latency: < 250 ms. |
| `workflow.preview` | Given a proposal envelope, project the authored model with ops applied and render a state graph + actor journey trace. Populates `previewArtifactRef`. Latency: < 1 s. |
| `workflow.apply` | Given a validated, previewed proposal envelope (human-approved), apply ops to the authored source, re-project to `WorkflowDefinitionFile`, write audit log entry, and return the updated projection. Synchronous on success. |
| `workflow.diff` | Given a proposal envelope, return a semantic diff of the authored model before and after ops: stages added/removed/modified, handoffs changed, transitions added/removed. Does not apply or validate. |

**MCP Request/Response shapes (representative):**

```jsonc
// workflow.validate — request
{
  "tool": "workflow.validate",
  "input": {
    "envelope": { /* proposal envelope */ }
    // OR:
    "workflowId": "planning-permission"  // validates current authored source without a proposal
  }
}

// workflow.validate — response
{
  "status": "pass | fail",
  "checkedAt": "ISO 8601",
  "errors": ["string"],
  "durationMs": 87
}

// workflow.apply — request
{
  "tool": "workflow.apply",
  "input": {
    "envelopeId": "a3f7c221-8b14-4e02-9d61-f23a10b5e7c9",
    "approvedBy": "jonny.muir",
    "approvedAt": "2026-05-16T13:20:33.659+01:00"
  }
}

// workflow.apply — response
{
  "status": "applied",
  "auditRef": "audit/planning-permission/a3f7c221.json",
  "projectedWorkflowDefinitionPath": "src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-permission.json"
}
```

### 5.2 CLI / HTTP Equivalents

```bash
# Validate current authored source
dotnet run --project src/UmbracoPrism.Cli -- workflow validate --workflow planning-permission

# Validate a proposal envelope file
dotnet run --project src/UmbracoPrism.Cli -- workflow validate --envelope proposals/a3f7c221.json

# Preview a proposal (renders graph + journey trace to ./previews/)
dotnet run --project src/UmbracoPrism.Cli -- workflow preview --envelope proposals/a3f7c221.json

# Apply an approved proposal
dotnet run --project src/UmbracoPrism.Cli -- workflow apply --envelope proposals/a3f7c221.json --approved-by jonny.muir

# Semantic diff (prints stage-level diff to stdout)
dotnet run --project src/UmbracoPrism.Cli -- workflow diff --envelope proposals/a3f7c221.json
```

HTTP equivalents (for CI):

```
POST /api/workflow-editor/validate       body: { envelopeId } or { workflowId }
POST /api/workflow-editor/preview        body: { envelopeId }
POST /api/workflow-editor/apply          body: { envelopeId, approvedBy, approvedAt }
GET  /api/workflow-editor/diff/{envelopeId}
```

**Latency budgets:**

| Command | Budget |
|---|---|
| `validate` | < 250 ms |
| `preview` | < 1 s |
| `apply` | Synchronous, no timeout constraint (blocks until projection completes) |
| `diff` | < 100 ms |

---

## 6. Collaboration Loop

The canonical six-step loop for a human-initiated agent-assisted workflow change:

```
Step 1: Human NL request
  "Add identity verification before the reviewer step in the planning workflow."
  Surface: Editor UI / Copilot chat
  Who: Human → GitHub Copilot

Step 2: Agent produces proposal envelope
  Copilot calls workflow.draft-proposal with targetWorkflowId and change description.
  The workflow-aware capability resolves the insertion point and produces the ops + placement.
  Output: proposal envelope (id, ops, placement, rationale) — not yet validated.
  Surface: Surface 3 (proposal envelope)

Step 3: Editor previews the resulting journey/graph
  Copilot calls workflow.preview with the envelope.
  State graph + actor journey trace rendered and stored as previewArtifactRef.
  Human sees the proposed graph change and the simulated applicant journey.
  Surface: Surface 5 (preview/simulate)

Step 4: Validation runs on the proposal
  Copilot calls workflow.validate with the envelope.
  Validation result written into envelope.validationResult.
  If fail: Copilot reports errors; human can request revision or abandon.
  Surface: Surface 4 (validate command)

Step 5: Human approves
  Human reviews diff (workflow.diff), preview, rationale, and validation status.
  Human confirms approval in the editor UI (or via CLI --approved-by flag).
  Surface: Surface 3 (proposal envelope review)

Step 6: Apply / regenerate
  Copilot calls workflow.apply with envelopeId + approver identity.
  Ops applied to authored source; projector re-runs; WorkflowDefinitionFile updated.
  Audit log entry written.
  If any post-apply validation fails: rollback authored source; report; proposal moves to "failed" status.
  Surface: Surface 2 (projected runtime file) + Surface 6 (focused test hooks trigger on apply)
```

---

## 7. Guardrails

These are non-negotiable constraints on all agent-initiated workflow changes (carried forward from the restart proposal):

1. **No direct live-instance writes.** Agents must never write to a running workflow instance or to a live runtime `WorkflowDefinitionFile` without going through proposal → validate → human-approve → apply.
2. **No UI-only automation as primary authoring API.** The editor UI may be the human's view of proposals, but an agent's primary authoring surface is the proposal envelope + MCP command surface. Driving the admin UI via Playwright as the change mechanism is not permitted.
3. **No hidden mutations.** Every agent-initiated change must produce a proposal envelope before any file is modified. Silent edits (direct file writes outside the apply command) are not permitted.
4. **Fast, targeted validation only in the authoring loop.** The inner loop runs schema/graph/role/component validation (< 250 ms). Full end-to-end Playwright or XUnit test suites run outside the inner loop — in CI or on explicit request after apply.
5. **No apply without passing validation.** The apply command must reject any envelope whose `validationResult.status` is `fail` or `not-run`.
6. **No ambiguous insertions.** If the insertion point specified in natural language is ambiguous (e.g. multiple handoff candidates), the draft-proposal command must return an error listing the candidates rather than guessing.

---

## 8. Test Seam

The test seam maps the four levels of the testing pyramid onto the proposal-first system.

### 8.1 Unit Level — C# / XUnit

These tests verify the internal correctness of the workflow-aware capabilities without starting the application.

**Projection determinism:**
- `ProjectionIsDeterministic_ForSameAuthoredSource` — projecting the same authored source twice produces byte-identical `WorkflowDefinitionFile` JSON.
- `ProjectionPreservesShellInference_AfterInsertStage` — inserting a stage via a proposal and re-projecting does not change the inferred shell types of unmodified states.

**Patch apply:**
- `ApplyOps_InsertStage_AddsStageAtCorrectPosition` — `insert-stage` op places the new stage before/after the specified anchor stage key.
- `ApplyOps_RemoveStage_RemovesStageAndOrphanedHandoffs` — `remove-stage` op removes the stage and any handoffs that referenced it.
- `ApplyOps_WithInvalidPlacement_ThrowsProposalException` — an op referencing a non-existent `before` or `after` stage key throws a typed exception.

**Validate:**
- `Validate_PassesForValidEnvelope` — a well-formed proposal on the planning workflow passes all four validation layers.
- `Validate_FailsWhenInsertedStageHasUnresolvableHandoff` — a stage with a `toStageKey` that does not exist in the post-apply graph fails graph integrity check.
- `Validate_FailsWhenFieldsetContainsNonInputComponent` — a stage whose fieldset contains a content component (e.g. `InsetTextComponent`) fails component rule check.
- `Validate_CompletesWithinLatencyBudget` — validate call on planning-permission authored source completes in under 250 ms (benchmark test, skipped in CI, used for perf regression locally).

**Semantic diff:**
- `Diff_IdentifiesInsertedStage_ByStageKey` — a proposal inserting a stage produces a diff entry of type `stage-added` with the correct `stageKey`.
- `Diff_IdentifiesModifiedHandoff_ByHandoffId` — a proposal modifying a handoff produces a diff entry of type `handoff-modified`.

**Existing anchors to preserve:**
- `WorkflowDefinitionInferenceTests` — all four existing tests remain green after projection layer is introduced.
- `SeedFileRoundtripTests` — all demo seed files survive serialise/deserialise roundtrip without loss.

### 8.2 Component Level — Storybook

New editor components (Isabelle's domain) each get an accessibility and visual contract story. Tangy owns the axe-core assertions; Isabelle owns the component inventory.

Key components with test seams:

| Component | Storybook story names |
|---|---|
| `<prism-workflow-graph>` | `renders planning workflow state graph with all stages`, `highlights inserted stage in proposal preview` |
| `<prism-proposal-diff>` | `shows inserted stage in green with rationale`, `shows removed stage in red`, `shows no diff for empty ops` |
| `<prism-proposal-panel>` | `shows validation pass state`, `shows validation errors inline`, `shows approve and reject actions when human-review-pending` |
| `<prism-journey-trace>` | `renders public applicant journey for planning with ID&V step`, `renders reviewer journey for planning` |

Each story runs `axe-core` accessibility audit. Any violation fails the story.

### 8.3 Journey Level — Playwright

The existing journey contract (`workflow-gds-journey.spec.ts`) covers the core planning applicant journey and must remain green. The following additions are V1 scope:

**Member continuation (new):**
- `Member can return to a saved planning application and continue from the correct step`
- `Member sees identity-verification waiting state when ID&V step is active`

**Reviewer / role-gated back-stage (new):**
- `Reviewer sees planning application in under-review queue after submission`
- `Reviewer can approve a planning application and applicant sees confirmed status`
- `Reviewer cannot see applicant identity data before ID&V step completes`

### 8.4 Agent-Loop Level — Playwright + MCP (new)

These tests exercise the complete collaboration loop as an observable, repeatable behaviour. They require the MCP server to be running and the planning workflow authored source to be in a known reset state.

Test suite: `src/UmbracoPrism.Client/tests/agent-loop/planning-workflow-agent-loop.spec.ts`

```
test.describe('Planning workflow agent-loop behavioural contracts')

  'NL request produces a valid proposal envelope with insertion before reviewer-assessment'
  'Proposal envelope for ID&V insertion passes validation without errors'
  'Preview renders state graph containing identity-verification stage between check-answers and reviewer-assessment'
  'Human-approved proposal is applied and projected workflow seed is updated'
  'Applied proposal for ID&V insertion makes identity-verification stage visible in workflow graph view'
  'Rejected proposal leaves authored source and projected seed unchanged'
  'Proposal with ambiguous insertion point returns candidate list rather than guessing'
  'Apply is rejected when validationResult is not-run'
  'Apply is rejected when validationResult is fail'
  'Audit log entry is written on successful apply with agent identity and rationale'
```

Each test calls MCP tools directly via a thin TypeScript harness (not via the editor UI), validates the response shape, and (for apply tests) checks that the authored source and seed file were mutated correctly. The `beforeEach` resets the planning workflow authored source to the baseline via the test-reset endpoint pattern already used in `workflow-gds-journey.spec.ts`.

---

## 9. Planning Workflow as Executable Spec

The planning application journey is the V1 behavioural contract reference. It exercises every surface in one realistic service flow.

### State-machine transitions and test names

| Transition | Playwright test name |
|---|---|
| Unauthenticated user arrives at public initiation route | `Unauthenticated user can begin a planning application without signing in` |
| Applicant completes project-details step and advances | `Applicant advances past project details when all required fields are filled` |
| Applicant submits with missing required fields | `Applicant cannot advance past project details when required fields are empty` |
| Applicant selects conditional radio that reveals sub-fields | `Work type conditional field is revealed when applicant selects Other category` |
| Applicant enters invalid date and sees error | `Applicant sees date validation error when proposed start date is not a real date` |
| Applicant reaches check-answers and changes a prior answer | `Applicant can change project name from check-answers and return to check-answers with updated value` |
| Applicant submits and sees confirmation | `Applicant sees application-received confirmation with reference number after submission` |
| Member returns to saved application and continues | `Member can return to a saved planning application and continue from the correct step` |
| ID&V step is active: applicant sees waiting state | `Applicant cannot submit without identity verification when ID&V step is enabled` |
| ID&V completes: applicant progresses to reviewer queue | `Applicant progresses to reviewer queue after identity verification completes` |
| Reviewer sees submitted application | `Reviewer sees planning application in under-review queue after applicant submission` |
| Reviewer approves application | `Reviewer approval moves application to approved state and applicant sees confirmed status` |
| Reviewer requests changes | `Reviewer request-for-changes returns application to applicant for amendment` |
| Applicant resubmits after reviewer changes | `Applicant can resubmit amended application and reviewer sees updated submission` |

### ID&V insertion-point V1 demo

The insertion point demonstrated in V1 is: **insert external ID&V before `reviewer-assessment`**. The above transition table includes this as `ID&V step is active: applicant sees waiting state` and `ID&V completes: applicant progresses to reviewer queue`. The test that proves the insertion is behavioural, not structural:

> **"Applicant cannot submit without identity verification when ID&V step is enabled"**

This test applies the ID&V proposal envelope to the planning workflow, navigates through the applicant journey to submission, and asserts that the waiting/identity-verification step is present in the rendered journey before the reviewer queue.

---

## 10. Provenance & Audit

Every applied proposal leaves a durable audit trail. The audit log is append-only and stored as structured JSON files alongside the authored source (e.g. `workflow-audit/planning-permission/<envelopeId>.audit.json`).

### Audit log entry shape

```json
{
  "envelopeId": "a3f7c221-8b14-4e02-9d61-f23a10b5e7c9",
  "appliedAt": "2026-05-16T13:20:33.659+01:00",
  "agent": {
    "kind": "github-copilot",
    "identity": "github-copilot/gpt-4o",
    "sessionRef": "copilot-session-2026-05-16-planning-idv"
  },
  "approvedBy": "jonny.muir",
  "approvedAt": "2026-05-16T13:20:33.659+01:00",
  "targetWorkflowId": "planning-permission",
  "rationale": "Insert mandatory external ID&V stage before reviewer-assessment.",
  "opsSummary": [
    { "op": "insert-stage", "stageKey": "identity-verification", "before": "reviewer-assessment" }
  ],
  "validationResult": {
    "status": "pass",
    "checkedAt": "2026-05-16T13:20:33.659+01:00",
    "errors": []
  },
  "previewArtifactRef": "previews/planning-permission/a3f7c221-state-graph.json",
  "projectedSeedPath": "src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-permission.json",
  "projectedSeedSha": "sha256:abc123..."
}
```

**What the audit log enables:**
- Full agent identity and session traceability for every applied change.
- Diff reconstruction from `opsSummary` — reviewers can see exactly what was added/removed/modified.
- Rollback readiness — the `projectedSeedSha` before apply can be stored to enable seed-level rollback.
- Compliance review — the `approvedBy` + `approvedAt` fields provide human sign-off evidence.

The audit log is not a git substitute; it complements the git history of the authored source and seed files. The combination of git commit (automated on apply) + audit JSON + proposal envelope gives full end-to-end provenance.

---

## 11. Open Questions

1. **Sandboxed preview environment** — should preview rendering spin up a sandboxed in-process projection and journey simulation, or should it require a running application instance? An in-process simulation is faster (< 1 s budget is achievable) but may miss runtime-specific rendering edge cases. A full app instance is slower but higher fidelity.

2. **Multi-step agent plans** — the current model assumes a single proposal envelope per change. A multi-step plan (e.g. "add ID&V, then add a deadline clock, then add a caseworker assignment stage") requires either a sequence of individual proposals applied in order, or a compound proposal envelope with ordered op lists. The compound envelope approach risks making validation and preview too coarse; the sequential approach requires the authored source to be in a consistent state between steps.

3. **Rollback semantics** — the current model relies on git history for rollback (revert the authored source commit, re-project). Should the system also support a first-class `workflow.rollback --to-envelope <id>` command that re-applies the state at a given audit log point? This is desirable but adds complexity to the apply/audit contract.

4. **Operator backstage views** — Blathers' open question: should backstage operator views stay inside Prism payloads or move to a separate operator UI contract? The agent-support layer is agnostic, but the preview and simulate command needs to know which actor paths to trace. Resolution needed before V1 preview command is implemented.

5. **Multi-actor concurrent proposals** — if two agents (or a human + agent) produce proposals simultaneously targeting the same workflow, which one wins? The apply command should acquire a file lock on the authored source; concurrent applies must queue or fail with a conflict error.

---

## 12. Acceptance Tests V1 Ships

The following tests are the explicit V1 acceptance gate, listed in priority order.

### C# / XUnit (run via `dotnet test src/UmbracoPrism.Core.Tests/`)

| Priority | Test | What it proves |
|---|---|---|
| P0 | `ProjectionIsDeterministic_ForSameAuthoredSource` | Projection is reproducible — foundational for all agent ops |
| P0 | `ApplyOps_InsertStage_AddsStageAtCorrectPosition` | Insert op works correctly |
| P0 | `Validate_PassesForValidEnvelope` | Validation passes for a well-formed proposal |
| P0 | `Validate_FailsWhenInsertedStageHasUnresolvableHandoff` | Validation catches graph errors |
| P1 | `ProjectionPreservesShellInference_AfterInsertStage` | Shell inference not broken by insertion |
| P1 | `Validate_FailsWhenFieldsetContainsNonInputComponent` | Component rule validation works |
| P1 | `ApplyOps_RemoveStage_RemovesStageAndOrphanedHandoffs` | Remove op cleans up handoffs |
| P1 | `Diff_IdentifiesInsertedStage_ByStageKey` | Semantic diff surface works |
| P2 | `ApplyOps_WithInvalidPlacement_ThrowsProposalException` | Invalid placement is rejected with typed error |
| P2 | `Diff_IdentifiesModifiedHandoff_ByHandoffId` | Handoff diff works |
| P2 | All existing `WorkflowDefinitionInferenceTests` | Shell inference contract preserved |
| P2 | All existing `SeedFileRoundtripTests` | Seed schema not broken |

### Playwright (run via `node node_modules/.bin/playwright test --reporter=line`)

| Priority | Test | What it proves |
|---|---|---|
| P0 | `Applicant sees application-received confirmation with reference number after submission` | Core planning journey still works after projection changes |
| P0 | `Applicant cannot submit without identity verification when ID&V step is enabled` | ID&V insertion demo works end-to-end |
| P0 | `NL request produces a valid proposal envelope with insertion before reviewer-assessment` | Agent-loop entry point works |
| P0 | `Proposal envelope for ID&V insertion passes validation without errors` | Validate command works on real proposal |
| P0 | `Human-approved proposal is applied and projected workflow seed is updated` | Full apply loop works |
| P1 | `Reviewer sees planning application in under-review queue after applicant submission` | Reviewer surface works |
| P1 | `Preview renders state graph containing identity-verification stage between check-answers and reviewer-assessment` | Preview command works |
| P1 | `Audit log entry is written on successful apply with agent identity and rationale` | Provenance is durable |
| P1 | `Rejected proposal leaves authored source and projected seed unchanged` | Rejection guard works |
| P2 | `Apply is rejected when validationResult is not-run` | Guardrail 5 enforced |
| P2 | `Proposal with ambiguous insertion point returns candidate list rather than guessing` | Guardrail 6 enforced |
| P2 | `Member can return to a saved planning application and continue from the correct step` | Member continuation works |
| P2 | `Applicant can change project name from check-answers and return to check-answers with updated value` | Existing check-answers contract preserved |
