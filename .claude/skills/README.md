# Engineering skills

Earned, project-specific lessons for whichever coding agent is working on this repo — not
Prism's own product documentation, and not a set of user-invocable slash commands. Each
`<name>/SKILL.md` captures a pattern, gotcha, or convention discovered while building something,
written so a future agent doesn't have to relearn it from scratch. See `TEMPLATE.md` for the
format when adding a new one.

## Provenance

These were migrated from `.squad/skills/` (2026-07-13), which belonged to a retired
Copilot-Squad multi-agent orchestration setup kept around only as historical reference — new
skills should never be written back there. Of ~104 skills found there:

- **89 migrated here**, either because current code/docs still actively reference them by path,
  or because a deliberate check confirmed the pattern they describe is still accurate today.
- **13 left behind as superseded** — they described an architecture this repo has since replaced
  (e.g. direct state-to-state transitions, before routes were required to target a gateway; a
  publish/persistence layer since replaced by `UmbracoPrism.WorkflowRuntime`'s
  `WorkflowAuthoringService`/`IWorkflowSourceStore`).
- **2 left behind flagged unclear** (`third-party-case-step-up-assurance`,
  `third-party-notifier-case-access`) — their source design docs still exist, but this repo's
  current direction (see the AI-ready workflow authoring work) took a different approach to
  third-party/bereavement-style case journeys. Worth a deliberate call on whether to revive,
  update, or formally retire them, rather than guessing.
