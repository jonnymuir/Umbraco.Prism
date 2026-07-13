---
name: "narrated-single-take-demo-recording"
description: "Produce a polished, narrated product demo video as one continuous Playwright recording — no post-production stitching, no fixed sleeps, no scraping terminal text for completion"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context

Use this when the ask is a presentation-quality demo video (not a CI test) that must read as one
continuous take: on-screen captions telling the story, a visible cursor, human-paced typing, and —
critically — no ffmpeg stitching of separately-recorded clips afterward. This applies whether the
demo is entirely UI-driven (garden-waste-permit-demo) or hands part of the story to a live AI agent
over MCP through a terminal (pension-bereavement-demo).

The load-bearing constraint that shapes everything below: Playwright records **one video per
Page**, not per test. As long as every act navigates the *same* page instead of opening a new one,
the whole multi-act recording is naturally one file — there is no stitching step to get right, or
to get wrong.

## Patterns

### One shared page across every act, saved explicitly in `afterAll`

- Create the browser context and its one `page` in `test.beforeAll`, share it via `test.describe.serial`,
  close/save in `test.afterAll`.
- Playwright's automatic video-to-test-result attachment only works for pages scoped to a single
  test — a page shared across tests silently loses its video unless you save it yourself:
  `const video = page.video(); await page.close(); await video.saveAs(finalPath); await video.delete();`
  (`saveAs` copies, so delete the auto-hash-named original after or every run leaves a duplicate
  multi-MB file behind).
- `recordVideo.size` is **not optional** if you want real viewport-resolution footage — omitting it
  scales the recording down to fit inside 800×800 (for a 1920×1080 viewport, ~800×450). This was the
  actual cause of "grainy" footage on a first attempt, not an under-tuned ffmpeg encode.
- `recordVideo` isn't inherited from a config's `use.video` for a manually-created context the way
  `baseURL`/`ignoreHTTPSErrors` are — confirmed live: `page.video()` came back `null` without passing
  it explicitly to `browser.newContext(...)`.

### Reference implementation

- `src/UmbracoPrism.Client/tests/demo/garden-waste-demo.spec.ts` (UI-only, agent extends a hand-built
  stage) and `pension-bereavement-demo.spec.ts` (agent designs and builds the whole thing in one turn).
- Shared helpers: `tests/demo/support/narration.ts` (`beat`/`showSlate`/`clearSlate`/`moveNarrationTo`
  — reading-paced hold times computed from word count, not a fixed flash) and
  `tests/demo/support/human-interactions.ts` (`humanClick`/`humanType` — an animated visible cursor
  and character-by-character typing, since `locator.fill()`/`.click()` teleport instantly and read as
  robotic on a recording).
- `playwright.demo-recording.config.ts` — deliberately excluded from CI (no npm script/workflow
  references it, and its spec filenames don't match any CI `testMatch`).

### Narration position must dodge the actual on-screen action

- The narration bar defaults to the bottom; call `moveNarrationTo(page, 'top')` only where the
  bottom would genuinely cover something the audience needs to read (a CLI about to stream typed
  text, a workflow-graph canvas widened by a panel collapse).
- Don't hardcode "always top from here on" for a whole act — let it settle back to the default by
  just not overriding `position` on the next `beat()` call.

### Never use terminal text, or a fixed sleep, as the "the agent is done" signal

- ttyd's xterm.js renders to canvas layers, not DOM text — there is no reliable way to read the
  terminal's content from the browser side.
- The real completion signal is the state change itself: poll the plain REST authoring API (not
  MCP, and never the terminal) for the actual fact that can only become true via a real
  `save_workflow` call reaching the live engine — e.g. `expect.poll(() => GET
  /prism/workflow-authoring/workflows/{key} has states.length > N && queues.length >= 2, {
  timeout: 35 * 60_000, intervals: [10_000] })`. Set the per-test timeout to match (`test.setTimeout(...)`)
  — a real agent call doing iterative validate→fix→re-validate has been observed to need well over
  an initial short poll budget.

### `headless: false` for any recording with a long unattended wait

- Headless Chromium throttles `requestAnimationFrame`/rendering on a backgrounded tab. A recording
  left unattended for ~10 minutes (waiting on a real agent call) visually **froze at a fixed frame**
  even though the underlying process kept working the whole time — confirmed by extracting real
  frames directly from the `.webm` via `ffmpeg -ss <t> -frames:v 1`, not just periodic screenshots
  (a screenshot taken via CDP can still succeed while the *recorded* video stream itself is stalled).
  Run headed for any take with more than a couple of minutes of passive waiting; a small periodic
  `page.mouse.move` nudge is cheap extra insurance but headed mode is the actual fix.

### ttyd: `--writable`, restrict `--tools` to the exact toolset, `bypassPermissions`

- `--writable` — ttyd defaults read-only; without it the whole hand-off act is inert.
- `--tools "mcp__yourserver__*,..."` restricts the *entire available toolset*, not an allow-list on
  top of the default one. Without this, Claude Code's own `Agent`/Task tool stays available and has
  been observed to spontaneously delegate a call to a background sub-agent fork instead of calling
  the intended MCP tool directly — that fork call failed and left the session hung waiting on a fork
  that never returns. This also makes for a more honest demo: the terminal shows the model calling
  the real tools, not delegating through an opaque sub-agent.
- `--permission-mode bypassPermissions` — an `--allowedTools` allow-list alone was tried first and
  looked sufficient, but in practice only read-only calls went through silently; mutating calls
  (validate/save/simulate) still stopped on an unanswered approval prompt with no human there to
  answer it. Reasonable to bypass entirely *only* because `--tools` has already narrowed the session
  to exactly the intended calls against a local dev stack — no broader capability is being unlocked.
- If the story needs to return to the *same* agent conversation more than once (browser navigates
  away to show the editor, then comes back for a follow-up turn), wrap the command in
  `tmux new-session -A -s <name> -- ...`: ttyd spawns a brand-new child process per browser
  connection, so without tmux, navigating away and back kills the `claude` process and starts a
  fresh one with no memory of the earlier turn. A single-turn hand-off (agent designs and saves
  everything in one pass, never revisited) doesn't need this.
- Detecting the one-time BypassPermissions consent gate: it can only appear on the very first
  connection (or on some Claude Code versions, never). Check the tail of the `script`-piped session
  log (`/tmp/claude-session.log`) for the gate's own text (`/Yes, I accept|No, exit/i`) before
  answering it — blindly sending "2"+Enter on a visit where the gate isn't showing types a literal
  "2" into a live empty prompt and visibly confuses the agent.
- Bigger terminal text: neither ttyd's own `-t fontSize=` flag nor CSS `zoom` actually works.
  `-t fontSize=` destabilizes xterm.js's column/reflow math once real streamed content arrives
  (reproduced reliably as corrupted, overlapping text). CSS `zoom` applied after `page.goto` is too
  late (ttyd/xterm negotiate a column count with the server once, at connection); applied earlier via
  `addInitScript` it either throws (`document.documentElement` doesn't exist yet at that point — the
  assignment is silently swallowed) or, once fixed to run on `DOMContentLoaded`, shrinks the terminal
  into a small corner rather than enlarging it. What actually works: ttyd assigns its live xterm.js
  `Terminal` instance to the global `window.term` synchronously, before calling `fitAddon.fit()` —
  hook that one assignment with a property setter to bump `term.options.fontSize` in between, so both
  xterm's renderer and its own column/reflow math read the same (new) value.

### Generic form-filling for agent-authored schemas

- When part of the demo is a form an AI agent designed at build time (not a fixed schema you can
  hardcode selectors against), DOM-introspect the actual rendered fields at runtime and fill them
  with a small label/type heuristic (name → a plausible name, `email`-typed or label matching
  `/email/` → an email address, `textarea` → free text, etc.) rather than hardcoding exact field
  keys or label text that may differ between agent runs.
- `CSS.escape` is a **browser** API — do not call it from the Node-side Playwright test body (only
  inside a `page.evaluate` callback). It also isn't needed for a plain attribute-selector value
  unless the value itself could contain a `"` character, which field keys/names never do here.

### Aspire's stable proxy ports vs. a process's real dynamic port

- Aspire's DCP layer exposes the fixed, `AppHost`-declared ports (e.g. `44345`/`7245`/`8443` in this
  repo) as a stable reverse proxy in front of the actual app processes, which bind dynamic internal
  ports underneath. `lsof -p <the actual process PID>` shows the dynamic backend port, not the stable
  one — checking a specific PID's own listening sockets and the documented fixed port can legitimately
  disagree, and that's expected, not a misconfiguration.
- Playwright's `baseURL`/`businessAppOrigin` should target the **stable** proxy ports — they survive
  an Aspire restart. Only an out-of-process client that needs the plain-HTTP MCP endpoint directly
  (e.g. a separately-invoked `claude` CLI, since self-signed dev certs aren't trusted) needs the real
  per-process dynamic port re-derived after every restart.

### Rehearse against fake data before an unrepeatable real take

- A live-agent act can't be cheaply re-run for rehearsal — burning 30+ minutes of real agent time
  just to check a CSS selector is wasteful, and the whole point of "one take" is that you don't get
  a free do-over.
- Fake the end state instead: copy an existing real, valid definition (e.g. `GET` a seeded workflow),
  rename its `definitionKey` to the target key, force `version: 0`, and `PUT` it directly via the
  plain REST authoring API. This satisfies the same completion-poll condition the real act waits on,
  letting every *other* act (UI wiring, editor inspection, the end-to-end run) be validated against
  the live stack for real, without touching the agent.
- Before the real take, fully reset: restart Aspire (with this repo's `PRISM_TESTSITE_RESET_RUNTIME=true`
  flag) rather than just calling `resetWorkflows()` — that helper only clears running workflow
  *instances*, not the authored *definition* (memory-only, only wiped by a restart) or any Umbraco
  content/nav entries an earlier dry run's UI-wiring act actually created for real (persisted to the
  real SQLite DB, not touched by `resetWorkflows()`). Skipping this risks the real take's Act 1
  polling succeeding instantly against the leftover fake definition instead of waiting for the real
  agent, and a UI-wiring act creating a silently-duplicated "(1)" content item.

## Examples

- Confirmed-frozen video, confirmed via direct frame extraction:
  `ffmpeg -ss 900 -frames:v 1 -y frame.png act-1-agent-build.webm` returned the same visual frame at
  600s, 900s, and 1800s despite the underlying `claude` process and its session log continuing to
  grow the whole time — the actual evidence that pinned this on headless rAF throttling rather than
  a hung agent.
- `tests/demo/pension-bereavement-demo.spec.ts`'s `fillGdsFormGenerically()` — the generic
  label/type-heuristic form filler for a schema only known once the agent has actually saved it.
- `tests/demo/README.md` — the full ttyd/tmux operator setup this skill's ttyd section summarizes.

## Anti-Patterns

- **Stitching separately-recorded clips together afterward** — defeats the entire "one continuous
  take" premise even if each individual clip looks fine; use the one-shared-page technique instead.
- **A fixed `page.waitForTimeout(N minutes)` as the "agent finished" signal** — either too short
  (flaky) or wastes real recording time; poll the actual state change instead.
- **Recording a long unattended wait in headless mode** — silently freezes the video while the real
  work continues underneath, producing a broken deliverable that looks fine in a quick screenshot
  check.
- **Hardcoding an agent-authored form's exact field names/labels** — breaks the moment the agent
  designs the schema even slightly differently on a different run.
- **Calling `CSS.escape` outside a `page.evaluate` callback** — it's a browser global, not a Node one.
- **Skipping the fake-data rehearsal pass** — the first attempt at wiring several UI acts together
  against a live stack reliably surfaces selector/timing issues that are cheap to fix in rehearsal
  and expensive to discover mid-take.
