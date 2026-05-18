# Isabelle — History Archive

Summarized entries from earlier periods preserved for reference.

## Pre-2026-05-16 Summary

**Project foundation (2026-03 through 2026-05-14):**
- Component system migration completed
- GDS integration and accessibility patterns established
- SEC-005 workflow shell security closed
- V1 workflow editor UX requirements refined through design cycle
- Reference editor shell v1 hosted

**Key learnings consolidated:**
- Dual-mode graph navigation (visual + linear) is the accessibility pattern for graph canvases
- Conversation pane is the primary agent surface
- Explicit save (not autosave) is safe baseline
- Split authoring surface (library → editor → validation) not JSON-first
- axe-core shadow DOM quirks: no `<header>` in shadow DOM, every overflow region needs tabindex, `role="alert"` breaks `<ul>` structure
- Mock drafters belong next to components, not shared test folders
- Story page state can bleed; reset at play() start

---

**Archive created:** 2026-05-18 for history summarization gate
