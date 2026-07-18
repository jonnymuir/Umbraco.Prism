---
name: "workflow-editor-validation-rail"
description: "Host-owned validation rail pattern for workflow editor save confidence"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #65)"
---

## Context

Use this when a workflow editor needs one visible place to summarise structural errors and configuration warnings without losing the in-context inspector and graph feedback authors already rely on.

## Pattern

1. Run a shared validation pass in the host editor so rail messages, save blocking, and jump-to-item links all read from the same issue list.
2. Treat disconnected or unreachable workflow structure as **blocking errors**.
3. Treat incomplete action configuration as **warnings** unless the authored model is structurally broken.
4. Render the rail as real buttons, not static text, so authors can jump directly to the affected stage, transition, or action.
5. Preserve inline inspector errors and move focus into the affected control after a rail jump.
6. Keep all messages workflow-friendly; name stages, transitions, and actions instead of leaking JSON paths.

## Why this works

- Host ownership prevents graph warnings, inspector errors, and save-state rules from drifting apart.
- Button-based jump links keep the rail accessible to keyboard and screen-reader users.
- Separating blocking structural issues from non-blocking action warnings matches how authors fix workflows in practice.

## Anti-Patterns

- Separate validators for the graph, inspector, and save button
- Blocking save for every missing action detail, even when the workflow structure is still being authored
- Showing rail messages as static text with no way to jump to the fix
- Using JSON-oriented errors like `params.formDefinitionId is required`
