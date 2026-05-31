### 2026-05-31T09:13:00+01:00: User directive — three architectural corrections (post scope-reset)
**By:** Jonny Muir (via Copilot)
**What:**

1. **There is no legacy.** Remove all uses of "legacy" / [Obsolete] shims / `HasLegacyWaitingPayload` / `LegacyWaitingPayload` / `LegacyKindRaw` / legacy wire field aliases from the codebase. We are not maintaining backwards compatibility with anything — this is pre-1.0 work. Clean it out, don't keep dead JSON-boundary normalisers around.

2. **The editor must consume workflows through an abstraction, not a hardcoded API.** The current 401 (`Failed to fetch workflow "planning": 401`) is a symptom: `<prism-workflow-editor>` is calling `/api/workflow-authoring/...` directly. That's wrong. The editor should depend on an interface / callback / host-supplied service ("expose your workflow store by implementing this interface"). Squad's reference implementation is in-memory, seeded with the four reference workflows. This makes:
   - Tests simple (no HTTP, no disk).
   - Integrator story clear ("I have my own business app — I implement this interface and provide it to the editor").
   - Future flexibility — Squad may later ship a fully-fledged workflow case-management system with its own implementation, but for now we provide the tooling, not the runtime store.
   This decision must be **documented prominently** so consumers of Prism understand the integration pattern.

3. **Gateways ARE transitions.** A stage cannot transition to another stage except through a gateway. The current model still treats "transition" as a separate first-class concept (`AuthoredTransition`). Collapse it: a gateway *is* the transition (carrying routing rules — conditions, triggers, role gates, target stages). Every part of the system must reflect this: server model, validators, frontend types, graph rendering, JSON canonical form, simulation, docs, walkthroughs. This includes simplifying the MockBusinessApp workflow admin page — since the editor shows the state diagram and detail, the admin page only needs the high-level description and a link to the editor.

**Why:** User architectural correction — captured for team memory and slice planning. Together these three directives complete the "gateway-only" simplification we started in the scope-reset arc.

**Scope of the cleanup pass:** full review of all workflow code AND documentation. No half-measures.
