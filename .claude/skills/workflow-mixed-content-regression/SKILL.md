---
name: "workflow-mixed-content-regression"
description: "Protect mixed workflow forms where content-only explanatory copy sits beside real inputs"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context

Use this when a workflow form mixes explanatory content fields (`inset-text`, `details`, `warning-text`) with real inputs and a change could accidentally treat copy as a fillable field.

## Pattern

### 1. Test authoring inference at the engine boundary

- Seed minimal workflow definitions directly in Core tests.
- Omit authored `stepType` and assert `GetCurrent` / `Advance` still infer the expected shells from components.
- For waiting flows, assert derived waiting config, `ResponseState = "render"`, and `PollAfterMs`.

### 2. Treat content-only fields as server-owned copy

- In tag-helper tests, assert `details` / `inset-text` render semantic content and do **not** emit named form controls for their `fieldKey`.
- In validator tests, assert content-only fields never appear in error dictionaries, even when adjacent real inputs fail.

### 3. Keep real inputs on the GDS error path

- Build contexts or render payloads for neighbouring select / radio / textarea fields with errors.
- Assert the resulting metadata still carries GDS error wiring (`govuk-form-group--error`, `aria-invalid`, `*-error` IDs / described-by references).

## Why it helps

This catches the exact regression where explanatory copy suddenly behaves like an input while preserving confidence that real fields still validate and render errors correctly.
