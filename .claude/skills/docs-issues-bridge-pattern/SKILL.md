---
id: docs-issues-bridge-pattern
title: Design Docs ↔ Issues ↔ Decisions Bridge Pattern
author: Mabel (Technical Writer & Release Manager)
date_created: 2026-05-17
last_updated: 2026-05-17
scope: team
---

# Design Docs ↔ Issues ↔ Decisions Bridge Pattern

## What

A lightweight three-layer pattern for keeping design documentation, execution issues, and team decisions in sync across multi-specialist, multi-month features.

**Layers:**

1. **Design Docs** (`/docs/design/`) — narrative spines and specialist sections. Source of truth for logic, contracts, and rationale.
2. **Execution Issues** — discrete 2–5 day tasks, owned by squad members, linked to docs sections.
3. **decisions.md** — durable log of decisions, bumped at PR merge time; acts as the bridge that ties doc sections to issue tracking.

## Why

The Workflow Editor V1 project showed:

- Without bridging, docs live in a bubble; issues live in another. Developers chase doc updates; reviewers miss context. Decisions get embedded in PR comments instead of team memory.
- Cross-linking overhead is real but minimal (5 min per issue, 2 min per doc update) if conventions are clear.
- A three-layer bridge lets each layer keep its purpose without becoming a maintenance tax.

## When to Use

- **Multi-specialist features** (3+ squad members, 2+ month rollout). Workflow Editor V1, notifications, biometric auth.
- **Features with cross-cutting contracts** (planes, handoffs, APIs that don't fit a single codebase file).
- **Public-facing design decisions** that must stay accessible and durable.

Keep it simple for smaller work: a single `/docs/design/feature.md` is fine; skip the issue granularity if the feature is 1 person + 1–2 weeks.

## How

### Layer 1: Design Docs

**Structure:** Spine + specialist sections.

```
/docs/design/{feature}/
├── README.md (spine — shared goals, architecture, contracts)
├── 01-{specialist}-{focus}.md (Isabelle's section)
├── 02-{specialist}-{focus}.md (Blathers's section)
└── ...
```

**Spine responsibilities:**

- Fix shared vocabulary and non-negotiables.
- Diagram the architecture (flows, data models, planes).
- State the end-to-end reference walkthrough so every specialist knows the throughline.
- Define contracts between layers/planes/components.
- Link to specialist sections by section number and author.
- Add "Implementation Tracking" at the end, listing epics and key issues (added after issues are created).

**Specialist section responsibilities:**

- Deep dive into one discipline's concerns (UX, backend, integration, security, testing).
- Reference the spine's shared vocabulary; don't re-explain.
- Answer "how do I implement this part of the architecture?" — not "what should we build?"
- Include open questions and V2 deferred items.

**Maintenance rule:** Specialist sections are updated iteratively by their owner. Spine changes only when contracts change or clarifications are needed for all specialists.

### Layer 2: Execution Issues

**Granularity:** One issue per 2–5 day task.

**Nesting:** Parent epic per squad member + feature; child issues for discrete work.

**Issue template headers:**

```markdown
## Relates to

- **Design spine:** `/docs/design/{feature}/README.md`
- **Specialist section:** `/docs/design/{feature}/NN-{section}.md`
- **Parent epic:** #{epic_number}

## Task

[Title from specialist section or spine; one sentence.]

## What (context from design doc)

> [Copy 2–3 sentences from the relevant doc section. This is a cache; avoids context switch.]

## Acceptance Criteria

- [ ] [Criterion 1]
- [ ] [Criterion 2]
```

**Maintenance rule:** Every issue body must reference its doc section. Copy the relevant snippet; no paraphrasing. If the snippet changes, update the issue body during the next PR touching that area.

### Layer 3: decisions.md Bridge

**When to bump decisions.md:**

- At PR merge time, if the PR touches a design doc, issues change scope, or a cross-cutting decision is made.
- When a specialist updates their section mid-flight (e.g., "projected diff validation rules clarified").
- When a blocker is resolved or scope is deferred to V2.

**Entry format:**

```markdown
## [Date] | [Feature] — [Change Type]

**Decision:** [1–2 sentence summary.]

**Rationale:** [Why now? What alternatives were considered?]

**Artifacts:**
- Design: `/docs/design/...`
- Tracking: [Issue #XX](https://github.com/...)
- Implementation: [PR #YY](https://github.com/...) or "pending"

**Impact:** [What does this affect? What is NOT affected?]
```

**Maintenance rule:** Entries are 3–5 minutes to write. Keep them tight; full rationale lives in PRs or doc sections, not decisions.md.

## Example: Workflow Editor V1

**Docs:**
- `/docs/design/workflow-editor-v1/README.md` — spine (goals, three-plane architecture, planning reference, contracts, section index, V2 open questions)
- `/docs/design/workflow-editor-v1/01-authoring-ux.md` — Isabelle's UX spec
- `/docs/design/workflow-editor-v1/02-runtime-projection.md` — Blathers's backend + projection logic
- `/docs/design/workflow-editor-v1/03-umbraco-integration.md` — Brewster's topological integration
- `/docs/design/workflow-editor-v1/04-agentic-surfaces.md` — Tangy's proposal-first agent loop

**Issues example:**

- Epic: Workflow Editor V1 (parent)
  - [Blathers] Implement projector (refs `/docs/design/workflow-editor-v1/02-runtime-projection.md`)
    - Issue #A: Authored schema + JSON Schema validator
    - Issue #B: Projector function (authored → WorkflowDefinitionFile)
    - Issue #C: Projection tests + edge cases
  - [Isabelle] Authoring UX (refs `/docs/design/workflow-editor-v1/01-authoring-ux.md`)
    - Issue #D: Canvas component rendering
    - Issue #E: Inspector property panel
    - Issue #F: Validation rail

**decisions.md entries example:**

```markdown
## 2026-05-17 | Workflow Editor V1 — Agentic Surfaces Update

**Decision:** Proposal validation layers clarified — V1 ships `authored` + `projection` validators only; `simulation` and `journeyTests` deferred to V2.

**Rationale:** Keeps V1 scope tight. Simulation requires live Aspire stack; journeyTests require playwright walkthrough infra. Both are achievable in V2 with better ROI post-MVP.

**Artifacts:**
- Design: `/docs/design/workflow-editor-v1/04-agentic-surfaces.md` (lines 210–240)
- Tracking: [MCP tools implementation](https://github.com/jonnymuir/Umbraco.Prism/issues/XX)
- Implementation: pending

**Impact:** Tangy's MCP tool surface is simplified. Validation UX shows only two status lanes instead of four. No impact on authoring plane or projection logic.
```

## Anti-Patterns to Avoid

- **Docs without issues:** Design exists but no one knows what to build. Issue template references the doc; doc doesn't know about issues.
- **Issues without doc context:** Developers read issue title and guess at requirements. Copy a snippet from the doc into the issue.
- **decisions.md as a diary:** "Fixed bug X, wrote test Y" — too granular. Reserve decisions.md for cross-cutting decisions, scope changes, and blockers.
- **Broken links:** If a doc section moves, update issue bodies and decisions.md links. Use link checker in CI (optional; not critical for a small squad).
- **Over-linking:** Not every issue needs a decisions.md entry. Entries are for decisions; implementation details stay in the issue.

## Maintenance Overhead

- **Writing a specialist section:** 6–8 hours. One-time per feature.
- **Creating an issue:** 5 min (copy snippet, fill template, set labels).
- **Updating decisions.md:** 2–3 min per entry; done at PR merge or decision time.
- **Updating a doc section:** Bump decisions.md with a one-liner (2 min).
- **Breaking a contract:** Full entry in decisions.md (5 min) + likely a design review comment.

**Total overhead for a feature like Workflow Editor V1 (50+ issues, 5 specialists, 2 months):** ~6–8 hours front-loaded in design phase; ~15 min per week during execution.

## Scaling Considerations

- **One feature per /docs/design/ directory.** Keeps navigation clear.
- **Specialists own their sections.** No approval bottleneck; merge-in specialist PRs directly if docs change.
- **decisions.md is the team's shared buffer.** Anyone can write entries; Scribe or lead curates periodically.
- **Issues can cross specialists if needed.** Use labels or epic nesting to clarify ownership.

For a 5-person squad on a 6-month roadmap, expect 3–5 major design docs, 100–150 execution issues, and ~50–60 decisions.md entries. The pattern scales horizontally without rewriting.

## References

- Current exemplar: `/docs/design/workflow-editor-v1/` and `.squad/decisions.md` entries for Workflow Editor V1.
- Recommendation: `.squad/decisions/inbox/mabel-design-artifact-structure.md` — full design structure guidance.
