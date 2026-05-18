---
name: "workflow-editor-human-ai-coauthoring"
description: "Design workflow editors that stay easy for humans while allowing safe AI co-authoring"
domain: "workflow-ui"
confidence: "medium"
source: "observed"
---

## Context

Use this skill when designing or reviewing a workflow editor that must serve both human operators and AI agents without falling back to raw JSON as the only serious interface.

## Patterns

- Split the editor into three clear layers:
  1. **Definition library** — find/open/clone/compare
  2. **Workspace editor** — canvas/list + inspector + validation
  3. **Simulation/publish** — replay, diff, apply, publish
- Show **front-stage** and **back-stage** work as connected lanes:
  - front-stage = public/member pages, user-visible copy, forms, confirmations, waiting states
  - back-stage = reviewer approvals, system actions, business-app processing, SLAs
- Keep authored intent and runtime inference separate:
  - authors edit components, routes, actions, actors, mappings
  - the editor surfaces inferred shells/status (`question`, `waiting`, `task-list`, etc.) as feedback, not duplicated inputs
- Author page mappings through progressive disclosure:
  - first answer “where does this experience live?” (public/member/business-app)
  - only then reveal advanced content/page ownership and route controls
- AI collaboration must be **proposal-first**:
  - chat or command palette creates a scoped change proposal
  - proposal renders as a human-readable diff
  - human applies all/part/rejects
  - resulting change is replayable in history
- Prefer **Copilot as the conversational shell**, with workflow-specific MCP tools behind it:
  - Copilot handles intent capture, clarification, capability discovery, and orchestration
  - workflow MCP tools handle semantic draft/diff/validate/preview/apply behaviour
  - the editor stays the authority for review, approval, and publish
- Reuse one validation engine for both human and AI edits so trust is anchored in the same rules and messages.
- Give agents a **machine-facing bundle**, not just raw JSON:
  - authored workflow source
  - projected Prism runtime definition
  - structured diff + provenance
  - validate/simulate/preview/test commands
- Treat natural-language generation as **draft → clarify → diff → validate → replay**, especially for research-led service designs.
- The north-star conversational flow is:
  1. ask in service-design language
  2. draft a proposal against named workflow concepts
  3. inspect semantic diff + validation + preview
  4. accept/reject partially or fully
  5. publish explicitly from the editor
- Anchor targeted edits to named workflow concepts (`stageKey`, actor, handoff, rule, route), not vague UI positions like "somewhere after this page".
- Make one complex service journey the **golden executable spec** across layers so AI changes are checked against the same behavioural story in unit, projection, and E2E tests.

## Examples

- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — sticky-tab progressive disclosure, explicit dialog/focus contract
- `src/UmbracoPrism.MockBusinessApp/Program.cs` — current workflow admin surfaces states/transitions/diagram/JSON, proving the underlying workflow artifacts authors need to reason about
- `src/UmbracoPrism.MockBusinessApp/Services/WorkflowTuiService.cs` — current command-oriented harness showing workflow operations are already expressible as machine-friendly verbs
- `src/UmbracoPrism.Core/Views/workflowPage.cshtml` — runtime shell selection derived from component tree
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/community-enquiry.json` — front-stage submission plus reviewer back-stage handoff
- `src/UmbracoPrism.Client/tests/walkthroughs/planning-notification.walkthrough.spec.ts` — example of a walkthrough that can become a stronger golden spec for agent-authored changes

## Anti-Patterns

- Making raw JSON the only complete authoring surface
- Letting AI mutate workflows invisibly without diff/apply review
- Letting agents publish without running the same validation and replay checks humans see
- Mixing page mapping, actor assignment, and component editing into one giant undifferentiated form
- Hiding reviewer/system work so the authored flow looks simpler than the real service
