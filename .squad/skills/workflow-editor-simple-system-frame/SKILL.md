---
name: "workflow-editor-simple-system-frame"
description: "Reframe workflow-tooling design around editor, engine, and forms engine without losing important seams"
domain: "workflow-architecture"
confidence: "high"
source: "observed (2026-05-17T22:05:30.472+01:00 workflow editor simplification review)"
---

## Context

Use this skill when workflow architecture docs or proposals feel over-modelled and stakeholders need a simpler product-level framing before they can evaluate detailed internals.

## Patterns

- Lead with only **three top-level concepts**:
  1. **Workflow editor** — design-time authoring
  2. **Workflow engine** — runtime execution
  3. **Forms engine** — reusable form/component system
- Treat projection, AI proposal loops, backoffice hosting, and similar seams as **supporting mechanisms**, not extra product nouns in the headline narrative.
- Open the top-level doc with a plain-language statement that there is a workflow editor, a workflow engine, and a forms engine, then make it explicit that the editor is the current design focus.
- Define the editor by what it must own to completely describe the authored workflow:
  - stages
  - transitions
  - actions
  - action parameters
  - validation
  - preview/history
  - editor ergonomics such as undo/redo, copy/paste, and help
- Split actions into two responsibilities:
  - **Design-time action catalog** — available action types, labels, parameter schemas, defaults, validation hints, implemented/not-implemented status
  - **Runtime action handlers** — named implementations resolved by the workflow engine
- Prefer a **handler registry/strategy pattern** over ad-hoc lambdas as the runtime contract in .NET reference apps:
  - authored JSON stores `action.type` + `parameters`
  - DI registers handlers per action type
  - engine resolves and executes handlers with runtime context
- Keep the doc structure product-first:
  1. overview
  2. workflow editor
  3. action model
  4. workflow engine
  5. forms engine integration
  6. supporting seams
  7. delivery slices

## Examples

- `docs/design/workflow-editor-v1/README.md` — useful detail, but too seam-first for a stakeholder who wants a simpler product mental model
- `docs/design/workflow-editor-v1/01-authoring-ux.md` — good source for what the editor must own
- `docs/design/workflow-editor-v1/02-runtime-projection.md` — useful source for authored/runtime separation and runtime constraints
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs` — current runtime is simple enough that a handler registry can extend it without introducing a large orchestration layer

## Anti-patterns

- Presenting internal planes/mechanisms as if they are the primary product concepts
- Leading with architecture slogans before the reader understands the editor-first product story
- Making runtime JSON the editor's primary mental model
- Mixing "what actions can be authored?" with "how does this action execute?" into one vague abstraction
- Using anonymous callbacks as the long-term authored/runtime contract
- Letting supporting AI seams dominate the main workflow-editor story
