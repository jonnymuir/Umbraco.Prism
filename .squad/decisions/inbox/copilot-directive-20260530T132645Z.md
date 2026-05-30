### 2026-05-30T13:26:45+01:00: User directive — JSON twin-pane + visual regression coverage
**By:** Jonny Muir (via Copilot)
**What:**
1. Add a fourth top-level editor tab containing an editable JSON editor for the AuthoredWorkflow document. Visual editor and JSON tab must stay in sync bidirectionally — changes in either propagate to the other; validation diagnostics surface when JSON is invalid or contradicts the schema.
2. Before declaring the editor done, plan and implement simple, high-signal visual tests covering: (a) items fit inside their lane, (b) stages and gateways render cleanly without text crashing or nodes overlapping, (c) horizontal and vertical scrolling behave well, (d) arrows between stages/gateways are intuitive and legible, (e) add/maintain ergonomics for the author.
3. Continue using Opus 4.7 for serious design/implementation work this session.
**Why:** User request — captured for team memory and slice planning.
