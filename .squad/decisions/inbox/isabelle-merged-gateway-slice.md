# Decision: Merged Gateway Slice — Editor-Only fromGateway/toGateway Fields

**Date:** 2026-05-26  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Issues:** #83, #84, #85 (merged into one implementation slice)

## Context

Issues #83 (gateway read-only scaffolding), #84 (editable gateway metadata), and #85 (join gateway waiting
information) were merged into a single frontend-only authoring slice. The implementation adds full gateway
editing to the inspector and a create-gateway dialog to the graph workspace, without touching backend
execution semantics.

## Decision: `fromGateway` and `toGateway` are editor-only annotations

`AuthoredTransition` now carries two optional fields:

```typescript
fromGateway?: string; // gateway key when this transition departs from a gateway
toGateway?: string;   // gateway key when this transition arrives at a gateway
```

**These fields are NOT sent to the backend runtime today.** The C# `AuthoredTransition` model does not
yet include them. They are consumed only by the graph layout renderer to compute explicit gateway routing
(instead of the anchor-stage heuristic) and by the inspector when updating gateway key references.

## What must happen before these fields become load-bearing

1. **Backend contract alignment:** Add `FromGateway` and `ToGateway` nullable string fields to the C#
   `AuthoredTransition` record and all serialisation/deserialisation paths.
2. **Validation:** Backend validation should check that `fromGateway`/`toGateway` values reference real
   gateway keys in the same workflow.
3. **Preview/simulation alignment:** Preview and simulation engines may need to be gateway-aware if
   routing semantics change. Current runtime remains stage-driven; these fields are purely cosmetic to it.
4. **Publish pipeline:** Strip or preserve the fields on publish — decision deferred to the backend team.

## What is safe to ship now

- All gateway editing UI (inspector form, create dialog, delete action)
- Key rename propagation across `fromGateway`/`toGateway` references within the editor
- Visual routing in the graph using explicit gateway fields when present
- Join gateway waiting information editing

## Affected files

- `src/UmbracoPrism.Client/src/workflow-editor/types.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
