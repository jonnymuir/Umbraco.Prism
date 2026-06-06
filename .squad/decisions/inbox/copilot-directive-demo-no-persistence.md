### 2026-06-06T15:28:05Z: No disk persistence for demo

**By:** jonnymuir (via Copilot)

**What:** For demo/testing: Keep workflow saves in memory-only. Do NOT add disk persistence. This prevents accidentally overwriting existing workflows that tests depend on.

**Why:** User request — demo needs clean isolation from filesystem side effects. Workflows should sync to runtime in-memory but never write to disk in this phase.
