# Service Design Principles

Prism's business is good service design — the general discipline, not any one
industry's rulebook. This document grounds whoever is authoring a workflow (human or
AI agent) in three widely-recognised, public frameworks before they draft a single
`WorkflowDefinitionFile`, so the result is judged against "is this a good service?"
and not just "is this valid JSON?"

It is deliberately industry-agnostic. It does not cover sector regulation or
domain best practice — FCA Consumer Duty, PASA pensions administration standards,
NHS clinical safety, and the like. That knowledge belongs to whoever is authoring
the service, not to Prism: bring it yourself, alongside this document, as your own
reference material — a skill document, a style guide, a compliance handbook,
whatever your AI tooling supports as supplementary context. Prism's job is the
general discipline of service design; the domain expertise is yours.

This document is also exposed as an MCP resource
(`workflow-docs://service-design-principles`) so an agent authoring workflows
through the MCP toolkit can fetch it directly — see
[AI-Ready Workflow Authoring](./ai-workflow-authoring.md).

---

## 1. The Double Diamond (Design Council) — the process

A four-phase framework for how to get to a good answer, not a waterfall to tick off:

- **Discover** — understand the problem through research and the people affected
  by it, rather than assuming you already know it.
- **Define** — use what Discover surfaced to reframe the challenge in sharper,
  sometimes different, terms than you started with.
- **Develop** — generate more than one candidate solution; look outside the
  obvious first answer.
- **Deliver** — test candidate solutions at small scale, drop what doesn't work,
  refine what does.

Discovering a deeper problem partway through is normal and can send you back a
phase — it's a loop, not a line. Nothing is ever permanently "finished"; contexts
change and services need to change with them.

**In Prism terms:** Discover and Define happen before you touch the editor or
`save_workflow` at all — they're conversations and research, not JSON. Develop is
where you sketch competing shapes for the queues/states/gateways, ideally more than
one. Deliver is `validate_workflow` and `simulate_workflow`'s job: dry-run a
candidate through the real engine before anything reaches a live user.

## 2. The GOV.UK Service Standard — the bar

Fourteen points a service should meet. Not all of them are Prism's to enforce —
some are organisational, not structural — but each should shape how a workflow
gets authored:

1. **Understand users and their needs** — design queues and states around what the
   people in them are trying to do, not around an internal team's process.
2. **Solve a whole problem for users** — model the whole journey across every
   queue involved (applicant *and* admin, say), not one team's slice of it.
3. **Provide a joined-up experience across all channels** — the engine is
   channel-agnostic; design one journey, don't let each channel drift into its
   own variant.
4. **Make the service simple to use** — fewer states and routes wins; push
   decision logic into gateways and calculations rather than showing it to users.
5. **Make sure everyone can use the service** — see
   [Using GDS Design System Components](./workflow-gds-components.md) for the
   accessible component catalogue.
6. **Have a multidisciplinary team** — outside Prism's scope; an organisational
   commitment, not a workflow property.
7. **Use agile ways of working** — outside Prism's scope; a team practice.
8. **Iterate and improve frequently** — the author loop (`list_workflows` →
   `read_workflow` → draft → `validate_workflow` → `simulate_workflow` →
   `save_workflow`) exists precisely so a workflow can be cheaply revised, not
   just cheaply built once.
9. **Create a secure service which protects users' privacy** — Prism doesn't ship
   an auth story for the authoring surface; see
   [AI-Ready Workflow Authoring](./ai-workflow-authoring.md#auth) — the host's
   responsibility.
10. **Define what success looks like and publish performance data** — outside
    Prism's scope today; worth naming explicitly in a workflow's own
    documentation as you author it.
11. **Choose the right tools and technology** — not structurally applicable to
    workflow authoring itself.
12. **Make new source code open** — an organisational choice, not a workflow
    property.
13. **Use and contribute to open standards, common components and patterns** —
    this is what Prism's component catalog *is*: reach for an existing generic
    component (`stat-group`, `summary-list`, `chart`, `showWhen`) before inventing
    a bespoke one — see the
    [Money Modeller pattern](../../CLAUDE.md#declarative-calculations--live-stages-money-modeller-pattern).
14. **Operate a reliable service** — `validate_workflow` and `simulate_workflow`
    exist to catch a broken journey before it ever reaches a real user.

## 3. Good Services (Lou Downe / School of Good Services) — the outcome checklist

Fifteen properties a *finished* service should have, useful as a checklist against
a drafted workflow rather than a process to follow:

1. Enable a user to complete the outcome they set out to do
2. Be easy to find
3. Clearly explain its purpose
4. Set the expectations a user has of it
5. Be agnostic of organisational structures
6. Require the minimum possible steps to complete
7. Be consistent throughout
8. Have no dead ends
9. Be usable by everyone, equally
10. Respond to change quickly
11. Work in a way that is familiar
12. Encourage the right behaviours from users and staff
13. Clearly explain why a decision has been made
14. Make it easy to get human assistance
15. Require no prior knowledge to use

A few map onto concrete authoring decisions worth calling out directly:

- **Have no dead ends** — already structurally enforced: every state route must
  resolve through a gateway (`ValidateGatewayRouting()`), so a workflow can't be
  saved with a step that leads nowhere.
- **Clearly explain why a decision has been made** — any state that represents a
  decision (an approval, a discretionary outcome, an eligibility result) should
  render the *reason*, not just the outcome. If a decision can't be explained in
  the UI, that's a sign the underlying `calculations` block needs a field for it.
- **Make it easy to get human assistance** — consider whether the workflow needs
  an explicit escalation route to a human queue, rather than trusting an
  automated path to cover every case.
- **Require the minimum possible steps to complete** — before adding a state, ask
  whether it needs to be shown to a user at all, or whether a gateway/calculation
  can resolve it silently.

## Using this while authoring

Read this before drafting, not after. Discover/Define against the real problem;
sketch more than one Develop candidate against these fourteen and fifteen points
before committing to one; use `validate_workflow`/`simulate_workflow` as your
Deliver-phase small-scale test, cheaply and repeatedly, before `save_workflow`
makes anything live. Then bring your own domain expertise on top — this document
never will.
