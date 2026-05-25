---
name: "design-source-of-truth"
description: "Lock a sliced redesign into one plain-language source-of-truth doc and explicitly map older docs to partial/current-state status"
domain: "documentation"
confidence: "high"
source: "observed (2026-05-25T12:01:09.927+01:00 multi-lane workflow design lock)"
---

## Context

Use this when the team has broken a redesign into several backlog slices, but the repo does not yet contain one clear document that explains the full target behaviour end to end.

## Patterns

- Create one canonical design document in the main design docs area, not scattered issue text.
- Write the behaviour in plain product language first, then mention engine internals only where needed to make guarantees clear.
- Include the slice/issue sequence in the same document so the backlog and target design stay tied together.
- Explicitly say which older docs remain current-state background and which should be treated as partial for the redesign.
- Add links from the main design index and the nearest related design set so readers naturally land on the canonical doc.

## Anti-Patterns

- Letting issue bodies become the only place where the full redesign exists.
- Mixing current shipped behaviour and target behaviour without saying which is canonical.
- Forcing implementers to reconstruct the whole model from scattered notes, PRs, and conversations.
