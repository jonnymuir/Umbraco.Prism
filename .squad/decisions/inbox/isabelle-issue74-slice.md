# Decision: Issue #74 first slice keeps confidence tools as supporting surfaces around the role-first workspace

**Date:** 2026-05-22T19:18:38.794+01:00  
**Author:** Isabelle  
**Status:** Proposed  

For the first implementation slice of issue #74, ship the role-first change in the main authoring workspace first: horizontal role bands in the graph canvas, inspector-only detail space on the right, and no embedded conversation pane. Keep list view, validation, preview, and simulation available as supporting surfaces around that workspace rather than turning them into the primary framing for this slice.

## Why

- The locked #74 direction is about changing the editor's mental model from generic graphing to role-owned work and handoffs, so the first visible slice needs to make the canvas itself role-first.
- Removing the embedded conversation pane at the same time clarifies that the right side is for editing details, not mixed authoring and chat.
- Keeping the confidence tools in place below the workspace preserves already-green seams and avoids reopening multiple interaction models in the same slice.

## Consequences

- Follow-up #74 slices can deepen drawer behaviour, lane navigation, and supporting-surface layout without reintroducing the old conversation split.
- Validation, preview, simulation, and list mode remain required contracts during the swim-lane rollout even though they are not the headline framing in slice one.
- Documentation and stories for this slice should describe the graph canvas as role-first while still showing the supporting confidence surfaces as part of the same editor.
