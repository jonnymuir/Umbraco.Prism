# Session Log: GDS Workflow Engine Design — BA-as-Brain & Protocol Finalization

**Date:** 2026-04-19T07:59:21Z  
**Phase:** Protocol Specification & Architecture  
**Status:** ✅ Complete

## Session Summary

Formalized the GDS workflow engine architecture and Step Descriptor Protocol from two completed background design sessions (Tom Nook). Established the BA-as-brain pattern, step descriptor contract, and extensibility model via Umbraco element types.

## Deliverables

### 1. Workflow Engine Architecture

**Core Principle:** Business App owns workflow logic; Umbraco is the component renderer.

- Business App: workflow state machines, routing logic, validation rules, multi-step journeys
- Umbraco/UI Layer: renders descriptors, collects user input, submits to BA
- Zero workflow knowledge in UI; UI is protocol consumer only

**Benefits:**
- BA logic stays decoupled from UI framework changes
- Multiple UI consumers (web, mobile, backoffice) consume same BA contract
- Workflow changes don't require UI redeploy; descriptor contract remains stable

### 2. Step Descriptor Protocol

JSON contract returned by BA for every workflow interaction. Contains all rendering requirements for one page.

**Envelope:**
```typescript
{
  workflowId, instanceId, sessionToken, stateVersion,  // Session management
  stepId, stepType, progress?,                          // Step identity
  content: QuestionContent | TaskListContent | ...,     // Rendering data
  actions: Action[]                                      // Button/link set
}
```

**Step Types:** question, task-list, check-answers, confirmation, error

**Actions:** Dynamic set (continue, save-and-return, change, start-section, etc.)

**Extensibility:** fieldType within questions is extensible (short-text, radio, checkbox, dropdown, date, file-upload, custom-widget)

### 3. Extensibility via Element Types

New question types, task list variants, and confirmation patterns added via pluggable element type system:

- BA returns new fieldType in descriptor
- Umbraco element type system renders fieldType via registered handler
- No BA/Umbraco coordination required

**Delegate work:** Brewster assigned to formalize element type registration and Umbraco 17 integration patterns in `brewster-gds-extensibility`.

## Session Artifacts

- **Protocol Definition:** `.squad/decisions/inbox/tom-nook-gds-workflow-design.md` (1189 lines, comprehensive)
- **Orchestration Logs:**
  - `.squad/orchestration-log/2026-04-19T07:59:21Z-tom-nook-gds-workflow-design.md`
  - `.squad/orchestration-log/2026-04-19T07:59:21Z-tom-nook-gds-protocol-design.md`

## Team Impact

| Role | Next Action |
|------|-------------|
| **Tom Nook** | Hand off to implementation layer; available for protocol clarifications |
| **Brewster** | Design element type extensibility spec; formalize Umbraco 17 integration |
| **Blathers** | Review BA API contract alignment; design backend serialization/validation |
| **Isabelle** | Prototype GDS component rendering from descriptor; build component library |
| **Tangy** | Design test contract and descriptor fixtures; validate rendering behavior |

## Key Decisions Merged

- ✅ BA-as-brain pattern established as canonical for workflow architecture
- ✅ Step Descriptor Protocol finalized as single JSON contract
- ✅ Element type extensibility pattern approved for field type additions
- ✅ Opaque session token replaces nonce; stateVersion gates concurrency

## Next Gates

1. **Element Type Spec** (Brewster): Formalize element type registration, validation, and Umbraco 17 component binding
2. **Backend API Contract** (Blathers): Design BA endpoint contracts, serialization, and HTTP error mapping
3. **Component Library Prototype** (Isabelle): Build GDS component rendering from descriptor schema
4. **Test Fixtures** (Tangy): Generate descriptor samples and test contract for rendering validation

## Handoff Notes

- Protocol is stable and ready for implementation
- Extensibility pattern proven across multiple field type families
- No breaking changes expected; all new types via enum expansion
- Ready for concurrent backend/frontend implementation

---
