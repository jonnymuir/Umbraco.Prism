# Garden waste permit — demo recording

`garden-waste-demo.spec.ts` captures the "AI-assisted workflow authoring" walkthrough as raw
video footage: a service designer wires up a new civic service in Umbraco, hand-builds its first
stage in the visual editor, hands a second stage (with a real calculation) to an AI agent
connected only through the MCP toolkit, and runs the result as a logged-in user.

**This is not a CI test.** It's a recording tool — `npm run demo:record` drives a real running
stack at human-watchable pace and writes footage to `demo-footage/` (sibling of `tests/`), each
clip with captions burned in via DOM injection. Four files, not five: Act 2 and Act 4 share one
continuous take (`act-2-and-4-editor.webm`) — the editor tab genuinely stays open across both,
so it's one page/recording, not two. Stitching the clips together and adding narration is a
manual editing step afterward, not part of this script.

Playwright's own automatic video-to-test-result attachment (`test-results/`) only works for
pages scoped to a single test; these pages are deliberately shared across acts via
`beforeAll`/`afterAll` for that same continuity, so the spec saves each page's video explicitly
in `afterAll` instead (`page.video().saveAs(...)`) rather than relying on the automatic
mechanism, which silently drops it for cross-test pages.

## Setup

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
   this is deliberate, see Act 3 below). **Critically**, strip your own session's
   `CLAUDECODE`/`CLAUDE_CODE_*` env vars first — if inherited, the spawned `claude` process is
   treated as a *child* of your current session (shares its cwd/context) instead of a genuinely
   independent one, which quietly defeats the whole "no shortcut" premise of this act:
   ```
   mkdir -p ~/prism-demo-scratch && cd ~/prism-demo-scratch
   PASS=$(openssl rand -hex 8); echo "$PASS"   # keep this for step 5 and TTYD_PASSWORD
   env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT -u CLAUDE_CODE_BRIDGE_SESSION_ID \
       -u CLAUDE_CODE_EXECPATH -u CLAUDE_CODE_SESSION_ID -u CLAUDE_CODE_CHILD_SESSION \
       -u AI_AGENT -u CLAUDE_EFFORT \
     ttyd --writable --interface 127.0.0.1 -p 7681 -c "demo:$PASS" \
       script -q -F /tmp/claude-session.log claude \
         --tools "mcp__prism-workflow__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" \
         --permission-mode bypassPermissions
   ```
   Five things bundled into that command, each load-bearing:
   - `--writable` — ttyd defaults to read-only (viewers can look but not type), which would
     silently make the whole act inert.
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

   Localhost-only, basic-auth anyway as defense in depth, ephemeral — kill it (`pkill ttyd`) when
   you're done recording. It's a real shell over HTTP; treat it that way.
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
project's testing convention in `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — this
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

## Disposable content

The workflow, its Umbraco page, and the `webNavLinks` entry are all demo-only and safe to
re-seed/discard between takes — `resetWorkflows()` runs at the start of the script.
