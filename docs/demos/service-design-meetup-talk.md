# Demo script: "Service Design: From Potato Peelers to Butterflies" (Umbraco meetup talk)

A follow-along script for setup and for the session itself. Every step has the same three parts:
**What it does**, **Do it** (copy-paste code block), **Success looks like**.

**Assumptions.** macOS, zsh, both repos checked out under `~/Documents/Projects/`:

| Repo | Path |
|---|---|
| Umbraco.Prism (this repo) | `~/Documents/Projects/Umbraco.Prism` |
| Wayfinder.Umbraco | `~/Documents/Projects/Wayfinder.Umbraco` |

Checked out elsewhere, or on Windows? Swap the `cd` paths (and use `set VAR=value` or
`$env:VAR="value"` in place of `export VAR=value`). Nothing else changes.

**Running order.** Two separate stacks. Stack 1 (Wayfinder.Umbraco ReferenceApp) serves Demos 1, 2 and
3. Stack 2 (Umbraco.Prism) serves Demo 4.

**The thread through all four demos: one person, two roles.**

- 🧑‍💻 Developer role: `PackageReference`, three delegates in `Program.cs`, no visual editor.
- 🎨 Designer role: only the backoffice, or plain-language chat with an agent. Never `Program.cs`.

Say the role switch out loud each time it happens. The system, not the person's memory, enforces the
contract between the two jobs, which is why one person can do both.

**Timing (~30 min):** service design intro 5, Demo 1+2 9-10, Demo 3 4-5, Demo 4 7-8, takeaways 3.

---

# Part 1: Setup

## 1.0 Prerequisites (do once, days before)

**What it does.** Confirms the tools the stacks need are installed and the local HTTPS certificate
is trusted.

**Do it.**

```bash
dotnet --version
node --version
docker info > /dev/null && echo "docker ok"
claude --version
dotnet dev-certs https --trust
```

**Success looks like.** `dotnet` prints 10.x, `node` prints a version, `docker ok` appears (start
Docker Desktop first if not), `claude` prints a version, and the dev-certs command reports the
certificate is trusted (macOS may prompt for your password).

## 1.1 Rehearsal dry run (the day before)

**What it does.** Proves both stacks boot cleanly on this machine. Run through Part 1 and Part 2
once, end to end, then do the reset in 1.2 and 1.3 for the real thing.

**Success looks like.** You reach the end of Demo 4 with no surprises.

## 1.2 Stack 1: Wayfinder.Umbraco ReferenceApp

Open **Terminal window 1**. Leave it running for the whole talk.

### Step 1: go to the repo

**What it does.** Puts the terminal in the Wayfinder.Umbraco repo.

**Do it.**

```bash
cd ~/Documents/Projects/Wayfinder.Umbraco
```

**Success looks like.** `pwd` prints `.../Projects/Wayfinder.Umbraco`.

### Step 2: wipe the database (clean run)

**What it does.** Deletes the local Umbraco SQLite database so everything reseeds from scratch: content,
the `njf-coaching-register` blueprint, the demo MCP agent's credentials, and any stage added in a
previous rehearsal of Demo 3. Do this right before the talk. Stop the app first if it is running
(Ctrl+C in its terminal).

**Do it.**

```bash
rm -rf Wayfinder.Umbraco.ReferenceApp/umbraco/Data/*
```

**Success looks like.** `ls Wayfinder.Umbraco.ReferenceApp/umbraco/Data` prints nothing.

### Step 3: build the client bundle

**What it does.** Builds the TypeScript backoffice bundle the ReferenceApp serves.

**Do it.**

```bash
cd Wayfinder.Umbraco.Client && npm ci && npm run build && cd ..
```

**Success looks like.** The build finishes with no errors and you are back in
`~/Documents/Projects/Wayfinder.Umbraco`.

### Step 4: raise the backoffice session timeout

**What it does.** Sets an environment variable in this terminal window only. Umbraco reads
`Umbraco__CMS__Global__TimeOut` as its backoffice login timeout, so this stops the backoffice
logging you out mid-demo. The variable lives only in this terminal session and dies when you close
the window, so the next step must run in the same window.

**Do it.**

```bash
export Umbraco__CMS__Global__TimeOut=02:00:00
```

**Success looks like.**

```bash
echo $Umbraco__CMS__Global__TimeOut
```

prints `02:00:00`.

### Step 5: start the stack

**What it does.** Starts the Aspire host, which launches Mailpit (a Docker container acting as the
demo inbox) and the ReferenceApp, and reseeds everything on first boot. The first boot after a wipe
takes a couple of minutes.

**Do it.**

```bash
dotnet run --project Wayfinder.Umbraco.AppHost
```

**Success looks like.** The terminal prints a line like `Login to the dashboard at https://localhost:49675/login?t=...`
and stays running. The dashboard port is different on every run, so open the URL the terminal
prints (Cmd+click it). This Aspire dashboard is your starting point for the whole stack: open every
service from its link in the **Endpoints** column rather than typing a URL. Wait until `mailpit` and
`referenceapp` both show **Running**, then open these from the dashboard:

| What | Open from the dashboard | Then go to | Login |
|---|---|---|---|
| Backoffice | `referenceapp` endpoint | `/umbraco` | `admin@example.test` / `Wayfinder123!` |
| Front end personas | `referenceapp` endpoint | `/demo/login` | pick a persona |
| Mailpit inbox | `mailpit` endpoint | none | none |

In the backoffice, **Settings > Blueprints** lists `njf-coaching-register`.

## 1.3 Stack 2: Umbraco.Prism

Open **Terminal window 2**. Leave it running for the whole talk.

### Step 1: go to the repo and check prerequisites

**What it does.** Puts the terminal in this repo and validates Docker, .NET and free ports before
launching.

**Do it.**

```bash
cd ~/Documents/Projects/Umbraco.Prism
node scripts/validate-aspire-prereqs.mjs
```

**Success looks like.** The script exits with no problems listed.

### Step 2: (optional) wipe the runtime for a pristine start

**What it does.** Resets TestSite's disposable runtime (database, throwaway tenants, stuck
automation runs) so everything reseeds. Skip if the last rehearsal left things tidy. If you do this,
use this command in place of the plain start in Step 4. Stop any running instance first.

**Do it.**

```bash
export PRISM_TESTSITE_RESET_RUNTIME=true
```

**Success looks like.** `echo $PRISM_TESTSITE_RESET_RUNTIME` prints `true`. After the stack has
booted once, run `unset PRISM_TESTSITE_RESET_RUNTIME` in this window so a later restart does not wipe
again.

### Step 3: raise the backoffice session timeout

**What it does.** Same insurance as Stack 1, for this window only.

**Do it.**

```bash
export Umbraco__CMS__Global__TimeOut=02:00:00
```

**Success looks like.** `echo $Umbraco__CMS__Global__TimeOut` prints `02:00:00`.

### Step 4: start the stack

**What it does.** Starts Keycloak, the Keycloak proxy, TestSite and MockBusinessApp via Aspire.

**Do it.**

```bash
dotnet run --project src/UmbracoPrism.AppHost
```

**Success looks like.** Open the Aspire dashboard (`https://localhost:17214`). It is your starting
point for this stack: open every service from its link in the **Endpoints** column rather than
typing a URL. Wait until every resource shows **Running**, then open these from the dashboard:

| What | Open from the dashboard | Then go to | Login |
|---|---|---|---|
| TestSite | `testsite` endpoint | none | none |
| Backoffice | `testsite` endpoint | `/umbraco` | `admin@prism.local` / `PrismLocal!12345` |
| Keycloak SSO on the front end | `testsite` endpoint, then sign in | none | `demo@prism.local` / `password` |

In the backoffice, **Settings > Prism Dashboard** shows the seeded "Local Dev (Keycloak)" tenant.

## 1.4 Terminal window 3: scratch terminal for Demo 3

**What it does.** A clean directory outside both repos, so the MCP demo is obviously "just talking
over MCP" and not an agent with secret codebase context. Clears any earlier registration so the
`mcp add` in Demo 3 starts fresh. It also tells the Claude CLI to accept the local development
certificate: the CLI's Node HTTP client does not read the macOS keychain that
`dotnet dev-certs https --trust` populates, so without this `claude mcp login` fails with
"unable to verify the first certificate". The setting lasts for this terminal window only, so run
every Demo 3 command in it.

**Do it.**

```bash
mkdir -p ~/demo-scratch && cd ~/demo-scratch
export NODE_TLS_REJECT_UNAUTHORIZED=0
claude mcp remove wayfinder-umbraco 2>/dev/null; echo "scratch ready"
```

**Success looks like.** Prints `scratch ready`, `pwd` is `~/demo-scratch`, and
`echo $NODE_TLS_REJECT_UNAUTHORIZED` prints `0`.

## 1.5 VS Code

**What it does.** Two windows, one per repo, so there is never a window hunt mid-flow. Pre-open the
tabs in this order so you only click tabs.

**Do it.**

```bash
code ~/Documents/Projects/Wayfinder.Umbraco
code -n ~/Documents/Projects/Umbraco.Prism
```

Then open these files as tabs:

- Window `Wayfinder.Umbraco`:
  1. `Wayfinder.Umbraco.ReferenceApp/Program.cs`
  2. `Wayfinder.Umbraco.ReferenceApp/AutomateCoachingStandardsSeeder.cs`
  3. `Wayfinder.Umbraco.ReferenceApp/service-blueprints/njf-coaching-register.json`
- Window `Umbraco.Prism`:
  1. `src/UmbracoPrism.TestSite/Program.cs`
  2. `src/UmbracoPrism.TestSite/TestSiteComposer.cs`

**Success looks like.** Two VS Code windows, each with its tabs in order. Increase the editor font
size now so the back row can read it.

## 1.6 Browser

**What it does.** Pre-opened, pinned tabs so nothing loads cold on stage. Use two browser windows
(or profiles) so the switch between stacks is one click.

**Do it.** Start from each stack's Aspire dashboard and open the tabs from its **Endpoints** links,
in this order.

Window A (Stack 1, the dashboard URL printed by `dotnet run` in Terminal window 1):

1. The Aspire dashboard itself.
2. `referenceapp` endpoint, then `/umbraco`.
3. `referenceapp` endpoint (the front end).
4. `mailpit` endpoint.

Window B (Stack 2, the dashboard at `https://localhost:17214`):

1. The Aspire dashboard itself.
2. `testsite` endpoint, then `/umbraco`.
3. `testsite` endpoint (the front end).

**Success looks like.** Both backoffices are logged in (Stack 1: `admin@example.test`, Stack 2:
`admin@prism.local`). Open a private window for Alex Applicant, and another for Casey Caseworker, so
the two personas can be signed in side by side in Demo 1+2.

## 1.7 Backups and the last checks

**What it does.** Gets the fallback recordings and files in place, so a failure means cutting to a
clip, never debugging live.

- Put a one-page PDF on the Desktop for any file-upload step.
- Have the backup recordings queued (see Backup recordings at the end).
- Silence notifications (Do Not Disturb on), close Slack and mail, set the display to mirror.

**Success looks like.** Nothing pops up over the top of a demo.

---

# Part 2: The session

## Between rehearsal passes (reset cheat sheet)

| Need | Command |
|---|---|
| Restart the coach journey (Demo 1+2), no wipe needed | open the `referenceapp` endpoint from its Aspire dashboard and go to `/apply-to-coach?action=start-new` |
| Full Stack 1 reset (also removes stages added by Demo 3) | stop app, then `rm -rf Wayfinder.Umbraco.ReferenceApp/umbraco/Data/*` from `~/Documents/Projects/Wayfinder.Umbraco`, then repeat 1.2 Steps 4 and 5 |
| Restart the licence journey (Demo 4) | open the `testsite` endpoint from its Aspire dashboard and go to `/apply-for-a-juggling-licence?action=start-new` |
| Full Stack 2 reset | stop app, then repeat 1.3 Steps 2 to 4 |

Seed JSON edits only take effect after a full Stack 1 wipe. `ReferenceBlueprintSeeder` seeds once
and never re-syncs an existing blueprint on later boots.

Demo 3 edits `njf-coaching-register` itself, so rehearsing Demos 1 to 3 again without a wipe adds a
second nickname stage on top of the first.

---

## Demo 1 + 2: Wayfinder.Umbraco, then Automate (slides 6-8, ~9-10 min)

Goal: prove the backoffice service design surface, and that wiring in a real external system is
config plus a few seeded steps.

### Beat A: the code (🧑‍💻, VS Code window `Wayfinder.Umbraco`, ~2 min)

**What it does.** Shows how little C# turns a stock Umbraco site into this.

**Do it.** Open the `Program.cs` tab. Scroll to these three places, do not read linearly.

1. `builder.Services.AddWayfinderUmbraco(options => { ... })`
   > "Three delegates. Who's the tenant, who's the user, what can they see and do. That's the entire
   > contract between Wayfinder.Umbraco and a host."
2. `.AddComposers()` and the comment above it about Umbraco Automate.
   > "Automate is a separate MIT-licensed Umbraco package. It registers itself the moment it's
   > referenced. I haven't written a line of glue for it yet."
3. The `app.MapWebhookSupportSystemCallbacks(...)` line near the bottom.
   > "That's the one line that lets an external system call back in and say 'this step is done, here's
   > the outcome.' One route."

**Success looks like.** Under two minutes, and the room has seen the three delegates.

**Role switch, out loud:** "That's the last C# you'll see from me for a while." Minimise VS Code.

### Beat B: tour the blueprint (🎨, browser Window A, ~2 min)

**What it does.** Shows the visual editor, and that the JSON behind it is real.

**Do it.**

1. Backoffice tab, **Settings > Blueprints**, open **njf-coaching-register** (the slide 7 screenshot).
2. Click **Fit**.
3. Click through: "Apply to coach", the Split, "Review application", the Split into the standards
   check, the three outcome routes.
4. Click the **Definition** tab briefly.
   > "Same editor whether a human built this or an AI agent did. That matters in ten minutes."

**Success looks like.** The graph fits the screen and the Definition tab shows JSON.

### Beat C: run it as a citizen (~2 min)

**What it does.** Starts a real instance and parks it on a genuine wait state.

**Do it.**

1. Private window 1: open the `referenceapp` endpoint from the Aspire dashboard, go to `/demo/login`, pick **Alex Applicant**.
2. Open **Apply to coach**. Name and email are already filled in from the signed-in persona.
   > "Nobody wrote code for that. The blueprint declares a `user` value and says these two fields
   > default from it."

   Fill in the rest: `yearsCoaching` = `1` (this forces the review branch), a disclosure
   reference, a first-aid expiry date.
3. Submit.
   > "That's a real wait state. Nothing polling client-side, a genuinely paused instance."

**Success looks like.** Name and email arrive pre-filled, and after submitting the page says the
application is being reviewed.

### Beat D: pick it up as the registrar and hit Automate (🎨, ~3 min)

**What it does.** Shows a branch, an external system, a human approval and a push notification, all
without code.

> Up front: "I haven't gone back to code once since Beat A."

**Do it.**

1. Private window 2: open the `referenceapp` endpoint from the Aspire dashboard, go to `/demo/login`, pick **Casey Caseworker**. Open
   **Coaching register queue**, pick up the application, open **Review application**.
2. Click **Run coaching-standards check**. The stage shows its own waiting screen.
   > "This is the third lane of a service blueprint, the support process. This app just POSTed a
   > signed webhook out to an Automate automation. Let's go and look at it."
3. Backoffice tab, **Umbraco Automate** section. Optional, skip if short of time: show
   `AutomateCoachingStandardsSeeder.cs` first.
   > "This whole automation is built and published in C# on every boot via `IAutomationService`."
4. Open the **Approvals** tab and point at the pending request naming the applicant.
5. Switch to the Mailpit tab (the `mailpit` endpoint from the Aspire dashboard) and open the "needs review" email.
6. Back in Automate > **Approvals**, approve via the dialog. Point at the run resolving.
7. Casey's window: the case is still **With you**, and the wait screen has become **Confirm the
   outcome**. Click **Record and notify the applicant**.
8. Alex's window: refresh.

**Success looks like.** The Approvals tab shows the pending request, Mailpit shows the email, and
Alex's page reads **Application complete** with the outcome.

> Close: "Branching logic, a real external system, a human approval gate, a notification back to
> the applicant, and the only code we wrote for the integration was three delegates and a callback
> route."

**If it breaks:** cut to the recorded backup for this sequence. Do not debug live.

---

## Demo 3: MCP, have a conversation with a blueprint (slide 9, ~4-5 min)

Goal: show a blueprint is something you talk to. A small, real change to the same
`njf-coaching-register` the room just watched run.

> Setup narration: "Everything we just did in the backoffice, an AI agent can do too, over the same
> identity and permissions. It can even design a whole service from a blank page, which is a
> different demo, ask me afterwards. Right now, the thing you'll reach for most days: a small change
> to a live service."

### Beat A: connect (🧑‍💻, Terminal window 3, ~30 sec)

**What it does.** Registers the Wayfinder MCP server with Claude Code, logs in through the same OAuth
screen the backoffice uses, then lists servers.

**Do it.** Copy the `referenceapp` endpoint from the Stack 1 Aspire dashboard and use it as the base
of the URL below (it is normally `https://localhost:44399`).

```bash
claude mcp add --transport http wayfinder-umbraco \
  https://localhost:44399/wayfinder/service-blueprint-authoring/mcp \
  --client-id umbraco-back-office-wayfinder-mcp \
  --callback-port 33418
```

```bash
claude mcp login wayfinder-umbraco
```

```bash
claude mcp list
```

**Success looks like.** A browser tab opens for login and completes, and `claude mcp list` shows
`wayfinder-umbraco` as **Connected**.

> "Same OAuth screen the backoffice uses. Whatever group membership I have is what the agent gets."

**Role switch, out loud:** "From here I'm a designer, I'm just going to talk to it."

### Beat B: ask the room, then the agent (🎨, ~2-3 min, the centrepiece)

**What it does.** Starts Claude Code with only the Wayfinder MCP tools available, so it can read and
edit blueprints and nothing else.

**Do it.**

```bash
claude --model sonnet \
  --tools "mcp__wayfinder-umbraco__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" \
  --permission-mode bypassPermissions
```

**Success looks like.** The Claude Code prompt appears in the scratch terminal.

Ask the room: "A coach just got accredited. What's one small, fun thing we should give them?" Take a
suggestion. If the room is quiet, paste this fallback verbatim:

```text
In the njf-coaching-register definition, right after a coach is accredited, before the final confirmation, add a stage where they pick a fun coaching nickname from a short list you invent, juggling-themed, playful (think "The Whirling Dervish," "Captain Diabolo"). Show their chosen nickname back to them on the confirmation screen. Validate and simulate the accredited path, then save.
```

> While typing: "Notice what I'm not telling it. Not which component type, not where in the JSON,
> not how routing works. It knows that from reading this definition."

> While it works: "It's reading the live definition as its style reference, adding one stage in the
> right place, and it'll validate and simulate before saving. That's knowledge in the system, not in
> an agent's head, which goes stale."

**Success looks like.** The agent reports the blueprint saved, well under a minute.

### Beat C: see it (~1 min)

**What it does.** Shows the change is live with no restart.

**Do it.** Window A backoffice, **Settings > Blueprints > njf-coaching-register**, click **Fit**.

**Success looks like.** A new nickname stage sits between the accredited outcome and confirmation.

> "That's live. The next coach who's accredited sees this. No restart, no redeploy, nobody touched
> code."

If time allows, restart the coach journey (`/apply-to-coach?action=start-new` on the `referenceapp` endpoint)
as Alex and watch the new stage render. Skip if tight.

> Close: "That's the capability at its smallest. Given more space, the same conversation builds the
> whole thing from nothing. Ask me afterwards."

Leave the scratch terminal with `/exit` before moving on.

---

## Demo 4: Umbraco.Prism (slide 10, ~7-8 min)

Goal: the same "how little code" beat, two fast backoffice wins (multi-tenant, live rebrand), a
video-only mobile beat, then close the loop by showing the juggling world running on
Wayfinder.Umbraco inside Prism.

**Switch now:** browser Window B, VS Code window `Umbraco.Prism`.

### Beat A: the code (🧑‍💻, ~2 min)

**What it does.** Shows Prism uses the exact same three-delegate contract.

**Do it.** Open `src/UmbracoPrism.TestSite/TestSiteComposer.cs` and scroll to the
`AddWayfinderUmbraco(options => ...)` block.

> "Spot the difference. A full multi-tenant, OIDC-secured site with its own branding system, and the
> only extra thing Prism adds is resolving tenant, user and access profile from its own concepts
> instead of a demo cookie. Two products, one seam."

> "And yes, same juggling world. TestSite installs Wayfinder.Umbraco directly, like any host would."

**Success looks like.** The block is on screen and the room can see it matches Beat A of Demo 1.

**Role switch, out loud:** "Laptop closed again. Everything from here is Settings and forms."

### Beat B: multi-tenant, live (🎨, ~2 min)

**What it does.** Creates a second independent branded portal on the running instance.

**Do it.** Backoffice **Settings > Prism Dashboard**, show the seeded tenant, then create a **new
tenant**. Name it something the room will enjoy. Point its OIDC authority at the local Keycloak realm
`prism-dev`, so it is a real working login.

> "A second fully independent branded portal, its own identity provider config, on the one Umbraco
> instance already running."

**Success looks like.** The new tenant appears in the tenant list.

Short of time: skip creation and edit the seeded "Local Dev (Keycloak)" tenant instead. Delete any
tenant you created from the dashboard after each rehearsal.

### Beat C: live rebrand (~2 min)

**What it does.** Changes the brand colour live, with no redeploy.

**Do it.** Open a tenant's branding editor.

> "This isn't a theme picker with six presets. It's your actual CSS custom properties, introspected
> into a labelled settings form."

Change the primary colour token, save, switch to the TestSite front-end tab, refresh.

**Success looks like.** The front end shows the new colour immediately.

### Beat D: mobile (video only, ~1 min)

**What it does.** Shows the mobile output without risking a live simulator.

**Do it.** Play the pre-recorded clip.

> "I'm not going to fight a simulator live. Here's what one click against that tenant's branding
> produces."

**Success looks like.** The clip shows the tenant's icon, splash and colours, and the biometric
login prompt.

### Beat E: close the loop (~1-2 min)

**What it does.** Ties Prism's tenancy and branding to the Wayfinder.Umbraco journey.

**Do it.** TestSite front end, sign in via Keycloak SSO as `demo@prism.local` / `password`, and open
**Apply for a juggling licence**.

> "Tenancy and branding from Prism. The service journey from Wayfinder.Umbraco. One site, the same
> pattern we spent twenty minutes looking at from the other side."

Optional, 30 seconds: open `JugglingLicenceDecisionAutomationSeeder.cs` and the Automate section.

**Success looks like.** The licence journey renders in the tenant's branding after SSO sign-in.

> Closing line for the whole talk: "Four demos, one running joke. Every time, the same person opened
> `Program.cs` and then closed it and opened a browser, or for ten minutes in the middle opened a
> terminal and just talked. Two roles, same afternoon. That's the point of building these
> packages this way."

Then straight into takeaways.

---

# Part 3: Teardown

**What it does.** Stops both stacks and clears the MCP registration.

**Do it.** Ctrl+C in Terminal windows 1 and 2, then:

```bash
claude mcp remove wayfinder-umbraco
```

**Success looks like.** Both terminals return to a prompt, and `claude mcp list` no longer shows
`wayfinder-umbraco`. Docker Desktop shows no leftover `mailpit`, Keycloak or proxy containers.

---

# Backup recordings

Cut to these if a live step fails. Each is gitignored output, so record them ahead against a warmed
stack, and again whenever the blueprint or backoffice changes.

| Demo | Record with | Run from |
|---|---|---|
| 1+2 (Automate) | `npm run demo:record:automate` | `~/Documents/Projects/Wayfinder.Umbraco/tests/demo` |
| 3 (add a stage) | `npm run demo:record:add-stage` | `~/Documents/Projects/Wayfinder.Umbraco/tests/demo` |
| 4 (Prism) | `npm run demo:record:tenancy-branding` | `~/Documents/Projects/Umbraco.Prism/src/UmbracoPrism.Client` |

The Demo 1 code beats have no recording. Keep a static screenshot of `Program.cs` as the fallback.

Do not use `UmbracoPrism.Client/demo-footage/licence-transfer-demo.*`, `garden-waste-permit-demo*.*`
or `juggle*.*` as backups. They show a backoffice feature that no longer exists.

---

# Appendix: build a service from scratch (not part of this talk)

The full "design an entire service from a blank page" demo, roughly 12-15 minutes, is a separate
session. Its script lives in
[`Wayfinder.Umbraco/docs/mcp-authoring-walkthrough.md`](https://github.com/jonnymuir/Wayfinder.Umbraco/blob/main/docs/mcp-authoring-walkthrough.md).
The footage is
`Wayfinder.Umbraco/tests/demo/demo-footage/wayfinder-umbraco-mcp-authoring-demo.mp4`.

# Related

- [`Wayfinder.Umbraco/docs/automate-support-system-walkthrough.md`](https://github.com/jonnymuir/Wayfinder.Umbraco/blob/main/docs/automate-support-system-walkthrough.md): Demo 1+2's source walkthrough.
