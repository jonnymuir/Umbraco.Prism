### 2026-05-31T10:15:00+01:00: User decisions on Tom Nook's three open questions
**By:** Jonny Muir (via Copilot)

**Q1 — Reference workflows location:** **MockBusinessApp owns all four** (planning, leave request, community enquiry, information request, payment demo). Prism's editor package ships with no reference workflows; empty state when no `WorkflowSource` is provided. All tests and docs reference MockBusinessApp's set as the canonical example. Rationale: Prism is opinion-free about which workflows are interesting; MockBusinessApp is the reference business app.

**Q2 — `UmbracoPrism.WorkflowRuntime` location:** **Stays as its own assembly**, labelled as a reference business-domain runtime (integrators are free to ignore it). Defer any rename to a later arc.

**Q3 — Persistence semantics for MockBusinessApp's `WorkflowSource`:** **Server-side in-memory in the MockBusinessApp ASP.NET process** — not browser-page-lifetime. Edits survive browser reloads; they die when MockBusinessApp restarts. Implementation pattern:
  - MockBusinessApp has its own singleton in-memory store of authored workflows (seeded with the four reference workflows at startup).
  - MockBusinessApp exposes its own minimal HTTP endpoints (in its own namespace, e.g. `/mockapp/workflows/*` — NOT under any Prism-owned path) to read/write that store.
  - MockBusinessApp ships its own `WorkflowSource` implementation (in MockBusinessApp's frontend code, not in the Prism editor package) that calls those endpoints. The editor host page bootstraps it and assigns it to `<prism-workflow-editor>`.
  - End-to-end tests must work against this full stack. Document the pattern explicitly — a real business app would replace the in-memory store with a database/blob/whatever it likes; the `WorkflowSource` implementation on top is unchanged.

**Why this matters:** Q3 turns out to make the DDD story **better**, not worse. The integrator-facing example now realistically shows "your business app has its own backend; you implement `WorkflowSource` on top of it; Prism doesn't care what's underneath." That's exactly the boundary we want to demonstrate.
