# Orchestration Log: Tom Nook — GDS Workflow Engine Initial Architecture

**Date:** 2026-04-19T07:59:21Z  
**Agent:** Tom Nook (Lead)  
**Status:** ✅ Completed

## Task Summary

Formalize the GDS workflow engine architecture and Step Descriptor Protocol from two background design sessions into shareable team decisions.

## Scope

**Completed Background Work:**
- tom-nook-gds-workflow-design: Workflow engine initial architecture with BA-as-brain pattern
- tom-nook-gds-protocol-design: Step Descriptor Protocol refined design

**Key Artifacts:**
- `.squad/decisions/inbox/tom-nook-gds-workflow-design.md` (1189 lines, comprehensive protocol definition)

## Protocol Definition

The Step Descriptor Protocol defines the JSON contract the Business App returns for every workflow interaction:

- **Stateless rendering contract:** UI renders exactly what descriptor specifies; zero workflow routing knowledge
- **Session management:** Opaque tokens, state versions, instance/workflow IDs for tamper-proofing and concurrency
- **Step types:** Question, task-list, check-answers, confirmation, error
- **Actions:** Dynamic button set (continue, save-and-return, change, start-section, etc.)
- **Progress:** Optional section/step tracking for multi-page journeys
- **Content variants:** Flexible schema per step type (questions, task lists, answer summaries, confirmations)

## Workflow Engine Design Principles

1. **BA owns workflow logic** — routing, validation, state machines
2. **Umbraco is the component renderer** — consumes descriptors, renders GDS/UI components
3. **No UI-side orchestration** — eliminates coupling between UI framework and workflow state
4. **Extensibility via element types** — New question types, task list variants, confirmation patterns added via pluggable element type system

## Extensibility Model (Preview)

Element type pattern allows workflow UIs to expand without BA/Umbraco coordination:
- Question element types: short-text, long-text, radio, checkbox, dropdown, date, file-upload, etc.
- Task list variants: progress indicators, completion checkers, custom fields
- Confirmation patterns: summary tables, review steps, legal confirmations

Brewster assigned to design formal element type extensibility spec in `brewster-gds-extensibility`.

## Handoff Status

- ✅ Protocol definition complete and documented
- ✅ Workflow engine architecture finalized
- ✅ Ready for implementation layer design and element type extensibility spec
- 🔄 Brewster to refine extensibility model and element type contracts

## Next Actions

1. Scribe merges protocol decision to shared decisions.md
2. Blathers reviews for backend API contract alignment
3. Brewster expands extensibility model for Umbraco 17 element types
4. Isabelle prototypes GDS component rendering from descriptor

---

**Session Log:** `.squad/log/2026-04-19T07:59:21Z-gds-workflow-engine-design.md`
