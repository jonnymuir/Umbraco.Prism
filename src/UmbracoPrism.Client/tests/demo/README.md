# Garden waste permit — demo recording

`garden-waste-demo.spec.ts` captures the "AI-assisted workflow authoring" walkthrough as one
continuous take: a service designer wires up a new civic service in Umbraco (Act 1), adds it as
a service via the mock business app's workflow admin dashboard (Act 2), hand-builds its first
stage in the visual editor (Act 3), hands a second stage (with a real calculation) to an AI agent
connected only through the MCP toolkit (Act 4), reviews what it built (Act 5), runs the result as
a logged-in user (Act 6) — then goes back to the *same* agent conversation for a real follow-up
turn (Act 7: a separate address stage, a proper review screen, the fee shown more clearly), reviews
that refinement in the editor (Act 8), and runs it again as a user, including clicking a summary-list
row's own "Change" link to go back and revise an earlier answer (Act 9). The two agent visits (Acts
4 and 7) are the same underlying `claude` process and conversation the whole time — see "Why tmux"
below — even though the browser navigates away to the editor and the live site in between.

**This is not a CI test.** It's a recording tool — `npm run demo:record` drives a real running
stack at a deliberately unhurried, presentation pace and writes a single file,
`demo-footage/garden-waste-permit-demo.webm` (sibling of `tests/`; `.mp4` too, if `ffmpeg` is on
your PATH — more portable for Keynote/PowerPoint than `.webm`). One file, not five: every act
navigates the *same* Playwright page rather than opening a new one, so Playwright's own video
recording (one file per page) naturally spans the whole recording — there's no stitching step to
run afterward.

The story is told two ways at once, always in the same order — **what we have → what we're about
to do → [it happens] → what just happened** — so a viewer (or a presenter narrating live) can
follow along even with the sound off:
- **On screen**: `support/narration.ts` burns a professional lower-third (or upper-third — see
  below) bar into the video for each beat, tagged and held for a reading-paced duration computed
  from its word count (not a fixed flash), plus full-screen title slates for the cold open and
  closing recap.
- **In the interaction itself**: `support/human-interactions.ts` moves a visible cursor to
  whatever it's about to click (a real animated `page.mouse.move`, not a teleport — this also
  makes real `:hover`-only affordances like Umbraco's row "+" button actually fire) and types
  into visible fields character-by-character with a human jitter, instead of `locator.fill()`'s
  instant value-set.

**Narration position**: the bar defaults to the bottom. Call `moveNarrationTo(page, 'top')` (a
smooth slide, not a jump cut — see `narration.ts`) only where the bottom would genuinely cover
something the audience needs to read, and let it settle back to the bottom by just letting the
next `beat()` call use its default — don't hardcode "always top from here on" for a whole act.
Act 4 (the CLI) is the clearest example: the bar starts at the bottom like everywhere else, then
slides to the top right before the agent's prompt starts streaming in, and stays there through the
recap since the terminal keeps producing output the whole act. The editor acts (3 and 5) move the
recap beat to the top too, specifically because the panel-collapse+zoom step (below) widens the
canvas enough that a bottom bar would sit over the very stages/gateway being shown off.

**Editor visibility**: the outline and properties side panels plus 100% zoom leave surprisingly
little room for the canvas itself, especially at Playwright's 1280×720 default. Two fixes, both
scripted rather than left to chance: the context is created with a 1920×1080 viewport (real
captured detail, not CSS scaling), and both editor acts narrate collapsing the outline/properties
panels and clicking "Fit" before the canvas is the thing being shown off. This state doesn't
survive a navigation, so Act 5 (a fresh `page.goto` back into the editor) repeats it — it's not
duplicated by accident.

One deliberate trade-off from moving to a single page: Act 5 no longer demonstrates the editor's
live staleness-banner poll (that required the editor tab to sit open, untouched, in the
*background* while Act 4's agent worked in a separate tab — which would have meant a second video
file again). It now just narrates "let's go back and see what it built" and does a fresh
navigation instead. If you want that specific feature demonstrated, it needs its own short clip.

Playwright's own automatic video-to-test-result attachment (`test-results/`) only works for pages
scoped to a single test; this page is deliberately shared across every act via
`beforeAll`/`afterAll`, so the spec saves its video explicitly in `afterAll` instead
(`page.video().saveAs(...)`) rather than relying on the automatic mechanism, which silently drops
it for cross-test pages.

## Setup

> **These ttyd steps apply to the garden-waste demo (`npm run demo:record`) only.** The
> licence-transfer demo (`npm run demo:record:licence-transfer`) no longer uses ttyd at all: its
> terminal is a plain tmux session the spec starts itself, mirrored into the recorded page as
> styled DOM (`support/tmux-terminal.ts`) — no password, no manual terminal setup, no extra env
> vars. Just start Aspire (with `PRISM_TESTSITE_RESET_RUNTIME=true
> Umbraco__CMS__Global__TimeOut=02:00:00` for a real take) and run the npm script. The switch was
> deliberate: driving ttyd/xterm.js in the recorded browser produced mis-sized text, unpainted
> grey canvas regions, and multi-minute visual freezes that assertions can't catch — a
> capture-pane-fed DOM mirror can't desync from the real session and renders at exactly the font
> size the recording needs.

Note: The ttyd is strickly just so we can automate the recording. If you want to do this as a manual walkthrough you do not need it, this is just your AI tool of choice, e.g. ```claude```

1. **Warm the stack first, off-camera.** Start Aspire and wait for the dashboard to go green —
   Keycloak's cold start alone can take up to two minutes.
   ```
   dotnet run --project src/UmbracoPrism.AppHost
   ```
2. **Find the real MCP HTTP port.** Aspire assigns MockBusinessApp's plain-HTTP endpoint a random
   port per run (`WithHttpEndpoint(port: null, ...)` in `UmbracoPrism.AppHost/Program.cs`) — the
   fixed `7245` you see everywhere else is the **HTTPS** port, and MCP clients need HTTP (self-signed
   dev certs aren't trusted). Read it off the Aspire dashboard, or find it directly:
   ```
   lsof -p $(pgrep -f UmbracoPrism.MockBusinessApp/bin) -a -i -P | grep LISTEN
   # try each candidate port with: curl http://localhost:<port>/prism/workflow-authoring/mcp
   # the real one returns 400 (bad request — no session header) rather than connection-refused
   ```
3. **Start the terminal surface, from an empty scratch directory** (no repo checkout in reach —
   this is deliberate, see Act 4 below). **Critically**, strip your own session's
   `CLAUDECODE`/`CLAUDE_CODE_*` env vars first — if inherited, the spawned `claude` process is
   treated as a *child* of your current session (shares its cwd/context) instead of a genuinely
   independent one, which quietly defeats the whole "no shortcut" premise of this act:
   ```
   mkdir -p ~/prism-demo-scratch && cd ~/prism-demo-scratch
   rm -f /tmp/claude-session.log; tmux kill-session -t prism-demo 2>/dev/null   # clean slate — see "Why tmux"
   PASS=$(openssl rand -hex 8); echo "$PASS"   # keep this for step 5 and TTYD_PASSWORD
   env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT -u CLAUDE_CODE_BRIDGE_SESSION_ID \
       -u CLAUDE_CODE_EXECPATH -u CLAUDE_CODE_SESSION_ID -u CLAUDE_CODE_CHILD_SESSION \
       -u AI_AGENT -u CLAUDE_EFFORT \
     ttyd --writable --interface 127.0.0.1 -p 7681 -c "demo:$PASS" \
       tmux new-session -A -s prism-demo -- \
       script -q -F /tmp/claude-session.log claude \
         --tools "mcp__prism-workflow__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" \
         --permission-mode bypassPermissions
   ```
   Six things bundled into that command, each load-bearing:
   - `--writable` — ttyd defaults to read-only (viewers can look but not type), which would
     silently make the whole act inert.
   - No `-t fontSize=`: tried first, reverted. It passes an xterm.js Terminal option straight
     through, and reads fine on the *static* splash content, but destabilizes xterm.js's
     column/reflow math once real streamed content arrives — reproduced reliably as corrupted,
     overlapping text once the agent's response started rendering. CSS `zoom` on the page (tried
     second) was *also* reverted — see "Why not CSS zoom" below. The spec instead hooks ttyd's own
     `window.term` assignment to bump xterm's `fontSize` option directly (see Act 4 in the spec).
   - `tmux new-session -A -s prism-demo -- ...` — see "Why tmux" below: this is what lets Act 4 and
     Act 7 be genuinely the same conversation even though the browser navigates away in between.
   - `script -q -F /tmp/claude-session.log claude` — ttyd's xterm.js renders to canvas layers,
     not DOM text, so there's no way to read the terminal's content from the browser side to
     detect when the agent is done. `script` is a transparent PTY passthrough (still fully
     interactive over ttyd) that also tees every byte to a plain file, which the spec polls
     directly. `-F` flushes after every write so the poll sees output immediately, not in
     buffered chunks.
   - `--tools "mcp__prism-workflow__*"` — restricts the *entire available toolset* (not an
     allow-list on top of the default one) to just the five `prism-workflow` MCP tools. Without
     this, Claude Code's own built-in `Agent`/Task tool stays available, and on a bare/scratch
     invocation the model has been observed to spontaneously delegate a read to a background
     sub-agent fork instead of calling the MCP tool directly; that fork call failed ("Invalid
     tool parameters") and left the session stuck waiting on a fork that never returns — a real
     hang, not a recording artifact. This also makes for a more honest demo: the terminal shows
     the model calling `read_workflow`/`validate_workflow`/`simulate_workflow`/`save_workflow`
     directly, not delegating through an opaque sub-agent.
   - `--permission-mode bypassPermissions` — `--allowedTools "mcp__prism-workflow__*"` was tried
     first and looked sufficient (an allow-list glob should pre-approve matching tool calls), but
     in practice only `read_workflow` went through silently; `validate_workflow`/`save_workflow`/
     `simulate_workflow` still stopped on an unanswered approval prompt with no interactive human
     there to answer it, stalling the agent mid-task (confirmed by reading the agent's own
     narration in `/tmp/claude-session.log`, which correctly diagnosed the stall as a permission
     issue). `bypassPermissions` removes the confirmation gate entirely — reasonable here only
     because `--tools` has *already* narrowed this session to exactly those 5 read/validate/
     save/simulate calls against your own local dev stack; there is no broader capability being
     unlocked, just the redundant "are you sure" on an already-restricted surface.

   Localhost-only, basic-auth anyway as defense in depth, ephemeral — kill it (`pkill ttyd`) **and**
   the tmux session (`tmux kill-session -t prism-demo`) when you're done recording, or a stale
   conversation bleeds into the next take. It's a real shell over HTTP; treat it that way.

   **Why tmux.** ttyd spawns a brand-new child process per browser connection — normally that means
   navigating away from the terminal and back (as Acts 4→5→6→7 do) kills the `claude` process and
   starts a fresh one with no memory of Act 4's conversation, which would make Act 7's "same
   conversation" framing a lie. Wrapping the command in `tmux new-session -A -s prism-demo` fixes
   this: the *first* connection creates the tmux session (which runs `script`/`claude` inside it);
   every later connection's `tmux new-session -A` finds a session with that name already running and
   *attaches* to it instead of creating a new one, so it's the same underlying `claude` process both
   times. One side effect worth knowing: the one-time BypassPermissions consent gate (if your Claude
   Code version even shows it — some don't) can now only appear on the very first attach, since the
   process itself only starts once. The spec's `connectToClaudeTerminal()` helper checks the tail of
   `/tmp/claude-session.log` for the gate's own text before answering it, rather than blindly sending
   "2"+Enter on every visit — doing that unconditionally, on a version/attach where the gate wasn't
   showing, typed a literal "2" into a live empty prompt and visibly confused the agent.

   **Why not CSS zoom.** Also tried and reverted: CSS `zoom` on `<html>`, applied via
   `page.evaluate()` after `page.goto`/`networkidle`, was too late — ttyd/xterm negotiate a column
   count with the server exactly once, at connection, so the PTY keeps the old (wider, pre-zoom)
   column count and long lines overflow off the right edge instead of wrapping. Registering it
   earlier via `addInitScript` didn't help either: `document.documentElement` doesn't exist yet at
   the point `addInitScript` fires (the assignment throws and is silently swallowed), and even fixed
   to apply on `DOMContentLoaded` instead, zoom on the root element shrinks the whole terminal into a
   small corner of the viewport rather than enlarging it — a real, reproduced Chromium zoom quirk.
   ttyd assigns its live xterm.js `Terminal` instance to the global `window.term` synchronously,
   before it calls `fitAddon.fit()` to compute columns from the container/font metrics — hooking
   that one assignment with a property setter to bump `term.options.fontSize` in between keeps
   xterm's own renderer and its own column/reflow math in sync, since both read the same option.
4. **Wire the agent**, using the port from step 2, from inside that same scratch directory (only
   needs doing once — the config persists in `~/.claude.json` for that project path):
   ```
   claude mcp add --transport http prism-workflow http://localhost:<port>/prism/workflow-authoring/mcp
   claude mcp list   # confirm "✔ Connected"
   ```
   The first launch in a new directory also shows a one-time "do you trust this folder?" prompt —
   accept it once before recording; it won't reappear for that directory afterward.
5. **Run it**, with the ttyd password from step 3:
   ```
   TTYD_PASSWORD=<password from step 3> npm run demo:record
   ```

## Why a separate config

`playwright.demo-recording.config.ts` assumes the stack is already running (inverted-polarity
port check in `support/demo-prereqs-setup.ts` — the opposite of
`playwright.localhost-auth.config.ts`'s "wait for ports to be free" check, which spins up its own
AppHost). Nothing here runs in CI: the spec's filename doesn't match any `testMatch` pattern in
the CI-facing config, and no `test:playwright:*` script or GitHub Actions workflow references
`playwright.demo-recording.config.ts`.

## Act 1 manual-fallback cue sheet

Umbraco backoffice content creation has no automation precedent anywhere in this repo (see the
project's testing convention in `.claude/skills/walkthroughs-as-executable-specs/SKILL.md` — this
class of flow is normally treated as "manual capture only"). Act 1 attempts full automation
anyway, since backoffice admin login is plain native Umbraco auth, not OIDC. If a specific step
proves too fragile after real attempts, fall back to doing it by hand while the recording is
running (the script still opens the right starting page — you just click through manually for
that one segment):

1. Log in to `/umbraco` as `admin@prism.local` / `PrismLocal!12345`.
2. Content → Create (under Home) → **Workflow Page**. Name it "Garden Waste Permit".
3. In the "Workflow Configuration" group, set **Workflow Key** = `garden-waste-permit`.
4. Save and Publish. Note the resulting URL (shown in the content app / "view page" link).
5. Navigate to the **Settings** node → **Web Navigation** tab → add a block: Label "Garden Waste
   Permit", URL = the page's published URL from step 4. Save and Publish.

## Disposable content — and resetting between takes

The workflow, its Umbraco page, and the `webNavLinks` entry are all demo-only, but
`resetWorkflows()` (which runs at the start of the script) only clears running workflow
*instances* — not the authored workflow *definition* (memory-only; wiped by restarting the whole
stack, since it's just an in-process singleton) and not the Umbraco content page/nav entry Act 1
creates (persisted to the real Umbraco SQLite database, which survives a plain restart). Run the
script twice against the same warm stack without resetting anything in between and Act 1 creates a
second "Garden Waste Permit" page under Home — Umbraco doesn't error on the duplicate name, it
just appends "(1)" and quietly leaves you with two.

For a genuinely clean take, restart Aspire with the TestSite runtime reset flag set — this wipes
its Umbraco database and Data Protection keys and does a full unattended reinstall (same
`admin@prism.local` credentials, defined in `TestSiteRuntimeLayout.cs`) on next start, which also
happens to reseed the whole content tree from scratch:
```
PRISM_TESTSITE_RESET_RUNTIME=true dotnet run --project src/UmbracoPrism.AppHost
```
Note the real MCP HTTP port changes on every restart (step 2 above) — re-derive it and re-run
`claude mcp add` before recording again.
