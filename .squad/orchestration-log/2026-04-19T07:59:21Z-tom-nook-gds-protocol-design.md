# Orchestration Log: Tom Nook — GDS Step Descriptor Protocol Refined Design

**Date:** 2026-04-19T07:59:21Z  
**Agent:** Tom Nook (Lead)  
**Status:** ✅ Completed

## Task Summary

Refine and formalize the Step Descriptor Protocol for the GDS workflow engine, establishing the JSON contract between Business App and UI rendering layer.

## Protocol Scope

**Step Descriptor Envelope** — Complete JSON response structure:
- Session management (workflowId, instanceId, sessionToken, stateVersion)
- Step identity (stepId, stepType, progress)
- Step content (varies by stepType)
- Actions (dynamic button/link set)

**Content Variants:**
- **QuestionContent:** fieldId, fieldType, label, hint, validation, defaultValue, required
- **TaskListContent:** tasks array with status (todo, in-progress, completed), descriptions, links
- **CheckAnswersContent:** sections with question-answer pairs for review before submission
- **ConfirmationContent:** title, message, referenceNumber, nextSteps
- **ErrorContent:** errorCode, message, userMessage, recoveryPath

**Action Schema:**
- key (string): "continue", "save-and-return", "change", "start-section"
- label (string): button/link text
- validation (optional): client-side pre-submit checks
- target (optional): navigation target for multi-step journeys

## Design Decisions

1. **Single response contract** — Every interaction returns a complete StepDescriptor; UI never requests sub-resources
2. **Opaque session token** — Replaces nonce; client returns unchanged on next submission
3. **Optimistic concurrency via stateVersion** — Detect conflicting edits without transaction locks
4. **Optional progress** — Journeys with sections track position; single-step flows omit progress
5. **Extensible action set** — New actions added via enum expansion, not new API endpoints

## Extensibility Pattern (Element Types)

Step types are fixed (question, task-list, etc.), but **fieldType** within questions is extensible:

```
fieldType: "short-text" | "long-text" | "radio" | "checkbox" | "dropdown" | "date" | "file-upload" | "custom-widget"
```

New field types added by:
1. BA returns new fieldType in StepDescriptor
2. Umbraco element type system renders fieldType via registered handler
3. No BA/Umbraco coordination required; rendering decouples from workflow logic

Brewster will formalize element type registration and Umbraco 17 integration patterns.

## Validation & Error Handling

- **Field-level validation:** Rules embedded in StepDescriptor (required, min-length, regex, custom)
- **Submission errors:** BA returns ErrorContent step with recovery path
- **Session conflicts:** stateVersion mismatch triggers refresh-and-retry loop
- **Workflow state errors:** Unexpected state transitions returned as error steps (not HTTP 400/500)

## Handoff Notes

- Protocol ready for backend API implementation
- Element type extensibility spec delegated to Brewster
- Component rendering layer (Isabelle/Tangy) can begin prototype based on this schema
- Test fixtures can be generated from schema samples

---

**Session Log:** `.squad/log/2026-04-19T07:59:21Z-gds-workflow-engine-design.md`
