# Decision — Stages carry the GDS component tree directly

**Author:** Blathers (Coding Agent, working as backend dev)
**Date:** 2026-06-01
**Issue:** #82 (named-lanes editor — Slice A consolidation)

## What changed

`AuthoredStage` no longer carries a flat `Fields: List<AuthoredField>`. Instead
it carries `Components: IReadOnlyList<PrismComponent>` — the same polymorphic
GDS hierarchy (`fieldset`, `accordion`, `panel`, `summary-list`, `task-list`,
input variants, body/inset-text/warning-text, …) that the runtime already
consumes. The TypeScript editor's `AuthoredStage.components` mirrors the C#
shape exactly.

`AuthoredField` and `FieldType` (C#) and `AuthoredField` / `FieldKind` (TS)
have been removed. There is no transitional cohabitation: stages declare
components and only components.

`WorkflowProjector.EmitComponents` is now a near-pass-through:

- If `stage.Components.Count > 0`, emit them verbatim.
- Otherwise emit a kind-appropriate default
  (`Question` → empty fieldset; `CheckAnswers` → harvested summary list;
   `Confirmation` → panel + optional body; `TaskList` → empty task list).

The gateway projector (`EmitTransitions`, commit 23b34c2) is **untouched**.

## Why

The April 2026 component-hierarchy decision (`tom-nook-component-hierarchy-feasibility.md`)
landed the polymorphic tree on the runtime side, but authoring kept a flat
field list that the projector translated into a single fieldset. That
translation was the only thing standing between authors and the full GDS
vocabulary (panels, accordions, warning-text, summary-list rows, …) that
real workflows already need. Removing it lets stages express GDS directly
and removes a class of "the runtime can render this but the editor can't
author it" bugs.

## Editor UX implication

The Inspector's stage panel now shows a **read-only Components summary**
(count + per-component label/kind) and a hint pointing authors at the
**Definition tab** for detailed editing via the JSON editor. There is no
component tree editor or palette — that is deliberately out of scope for
this slice; the Definition tab covers complex setup.

## Reference workflows

The four MockBusinessApp reference workflows (planning, information request,
payment demo, community enquiry) have been re-authored with real GDS
components: fieldsets with meaningful legends, body content, inset-text /
warning-text where appropriate.

## Tests

- `dotnet test UmbracoPrism.Core.Tests` → 814/814 passing.
- `npm run build` (Client + WorkflowEditor) → green, 0 type errors.
- C# fixture JSON files and TS planning fixture migrated to the components
  shape.

## Follow-ups for other squad members

- **Isabelle (designer):** the Inspector now nudges authors to the Definition
  tab for component editing. Consider whether the summary view needs richer
  affordances (inline JSON snippet preview? per-component "open in JSON
  editor at this path" link?).
- **Tom Nook (architect):** the projector pass-through means the C# wire
  output for stages now contains `components: [...]` exactly as runtime
  expects — confirm any downstream consumers (state-machine importer,
  audit log) cope with the richer shape.
