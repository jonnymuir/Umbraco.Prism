### 2026-05-31T09:40:00+01:00: User directive — DDD boundary between service-design and business domain
**By:** Jonny Muir (via Copilot)

**What:**

1. **Delete `/api/workflow-authoring/*` HTTP endpoints.** No in-tree consumer after the `WorkflowSource` abstraction lands. We do not maintain "could be useful one day" code. Integrators who want HTTP-backed workflow storage implement their own `WorkflowSource`. Tom Nook's open question 3 — resolved option (c).

2. **`WorkflowSource` must be documented well.** Integrator-facing recipe explaining what it is, how to implement it, where the reference in-memory impl lives, what the four reference workflows look like. The "how to expose your workflow store to the Prism editor" story needs to be unmistakeable.

3. **DDD boundary review across all workflow code.** This is the framing that the abstraction belongs to. Two domains:
   - **Service-design domain (Prism)** — describing and building workflows. The editor, the authored model, the schema, the canonical JSON, the validator, the simulation. This is what Prism *is*.
   - **Business domain (per-app)** — running workflows for actual business cases. Persistence (store me a workflow JSON), instances (this customer is at stage 3), roles (who can advance what), notifications, the actual UI presented to end users completing forms, etc. MockBusinessApp is a reference **business domain**, not a reference editor.
   
   Anything that *really* belongs in the business domain must live in the business domain (with the reference implementation shipping as MockBusinessApp). Anything that belongs in service design stays there. The boundary between them is a small number of clean interfaces (`WorkflowSource` is one; there are probably more).

4. **Concrete deliverable for the boundary review:** Tom Nook produces an audit of every workflow-touching file (server + client + docs), labels each as "Prism (service-design)", "Business domain (reference impl)", or "Boundary contract (interface)", and proposes the slice plan that moves anything mis-located to its correct home. The current three-slice plan (legacy purge → abstraction → gateway collapse) is **provisional** and may grow / reshape based on this audit.

**Why:** The 401 was a symptom of a deeper architectural issue — service-design code was reaching into what should be business-domain responsibility (workflow persistence + auth). Fixing the symptom alone leaves other crossed wires in place. Tom Nook is to re-baseline the architecture against DDD principles before we cut any more slices.

**Standing preferences (carry-over):** plain product language, one slice at a time, behavioural tests green, no IoC, explicit construction, editor never in backoffice, no legacy, Opus 4.7 for serious design work this session.
