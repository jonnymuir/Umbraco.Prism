---
date: 2026-05-25T11:48:05.065+01:00
author: Mabel
related: Issue #81
status: Complete
---

# Issue #81 — Documentation Updates for Assignment-Driven Lane Logic

## Summary

Issue #81 removes duplicate front-stage/back-stage surface logic from the workflow editor and makes lane assignment driven entirely by `actor` and `roleGates` fields. The `editorSurface` field is stripped before publishing.

This decision documents the documentation updates made to reflect the shipped behaviour.

## Changes Made

### 1. `docs/design/workflow-editor-v1/01-authoring-ux.md` — Section 7.4

**Before:**
```
Graph view shows these as role-first horizontal bands, 
with front-stage and back-stage placement still expressed 
through the owning role and supporting styling.
```

**After:**
```
Lane placement (front vs back stage) is **derived from the stage's 
actor and role-gate assignment**, not a separate editable field. 
Authors set the actor and role gates, and the editor displays stages 
in the appropriate lane visually.
```

**Rationale:** Clarifies that front/back-stage is a **derived visual grouping**, not an authored field. Authors interact only with `actor` and `roleGates`.

### 2. `docs/design/workflow-editor-v1/README.md` — Section 4.1

**Added paragraph after authoring model definition:**
```
**Stage assignment and lane grouping:** Each stage has an assigned 
actor (e.g. "applicant", "reviewer") and optional role gates 
(e.g. "admin-approval"). The editor derives visual lane grouping 
automatically: stages with public-facing actors (applicant, resident, 
member) appear in the front-stage lane; stages with reviewer/officer/system 
actors or role gates appear in the back-stage lane. Authors do not 
manage a separate surface field; the lanes are determined by the 
assignment data.
```

**Rationale:** Explicitly documents the lane-derivation logic so future developers understand the system is assignment-driven, not surface-driven.

### 3. `docs/design/workflow-editor-v1/02-runtime-projection.md` — Section 7

**Added to projection rules:**
```
- UI-only fields (such as temporary editor surface hints) are 
  stripped before projection, leaving only the authored assignment 
  data (actor, roleGates) that drives runtime behaviour
```

**Rationale:** Documents the published contract: the runtime receives only `actor` and `roleGates`, not temporary UI fields.

## What Was NOT Changed

- **Walkthrough docs** — Already refer to "back-stage surfaces" and "back-stage actors" in the runtime context, which is correct and unchanged
- **Umbraco integration doc** — References to "authoring surface" and "editor surface" refer to the authoring environment as a whole, not to a field; correct as written
- **Reference workflow contract** — Similarly correct; no changes needed

## Verification

1. No contradictions remain between design docs and shipped code
2. Lane assignment logic is now clearly documented as `actor` + `roleGates` → lane placement
3. Authors understand they do not manage a surface field
4. The projection contract is clear: UI-only fields are stripped

## Key Principle Reinforced

**Assignment-driven lane meaning:** 
- Authors edit `actor` and `roleGates`
- The editor derives visual lane placement from that assignment
- The runtime receives only the assignment data
- No separate surface enum leaks into the published definition

This clean separation means lane redesigns (e.g., adding new actor roles or changing role-gate behaviour) only require changes to the assignment interpretation logic, not mutation of published workflows.
