# Tom Nook decision — issue #81 landing and push

- Date: 2026-05-25T11:48:05.065+01:00
- Issue: #81
- Scope: landing procedure, docs alignment, and branch hygiene

## Decision

Land issue #81 on a dedicated `squad/81-clean-up-duplicate-workflow-surface-rules` branch, not directly on `main`. Ship the assignment source-of-truth cleanup with the updated design/docs notes and behavioural proof so CI and review see the contract change as one slice.

## Why

The repository branch policy now requires feature branches for substantive code changes, and this slice changes both editor behaviour and the authored/runtime contract story. Keeping code, tests, and documentation together prevents later lane work from reintroducing duplicate surface rules by accident.

## Consequence

The pushed branch is ready for coordinator review and CI as a single issue-focused unit. Future lane work can branch from a clean contract instead of inheriting stale editor-only surface metadata.
