# Demo script: "Service Design: From Potato Peelers to Butterflies" (Umbraco meetup talk)

Two parts. **Part 1** is setup, done before you walk on. **Part 2** is the talk itself, one page per
demo, written to be glanced at while standing up.

**The thread through all four demos: one person, two roles.**

- 🧑‍💻 Developer: `PackageReference`, three delegates in `Program.cs`, no visual editor.
- 🎨 Designer: only the backoffice, or plain-language chat with an agent. Never `Program.cs`.

Say the role switch out loud each time. The system, not the person's memory, enforces the contract
between the two jobs, which is why one person can do both.

**Timing (~30 min):** intro 5, Demo 1+2 9-10, Demo 3 4-5, Demo 4 7-8, takeaways 3.

---

# Run sheet (the page to keep open)

## Fixed URLs

Every URL is pinned by a launch profile, so nothing changes between runs. Type them, bookmark them,
never hunt for a port.

| Stack | What | URL | Login |
|---|---|---|---|
| 1 Wayfinder.Umbraco | Backoffice | `https://localhost:44399/umbraco` | `admin@example.test` / `Wayfinder123!` |
| 1 | Front end personas | `https://localhost:44399/demo/login` | pick a persona |
| 1 | Coach journey restart | `https://localhost:44399/apply-to-coach?action=start-new` | none |
| 1 | Mailpit inbox | `https://localhost:8025` | none |
| 1 | MCP server (plain http, see Demo 3) | `http://localhost:5299/wayfinder/service-blueprint-authoring/mcp` | OAuth in browser |
| 2 Umbraco.Prism | Backoffice | `https://localhost:44345/umbraco` | `admin@prism.local` / `PrismLocal!12345` |
| 2 | Front end | `https://localhost:44345` | Keycloak SSO: `demo@prism.local` / `password` |
| 2 | Licence journey restart | `https://localhost:44345/apply-for-a-juggling-licence?action=start-new` | none |
| 2 | Aspire dashboard | `https://localhost:17214` | none |

Stack 1's Aspire dashboard port changes every run. You do not need it: the table above has every
URL the talk uses.

## Terminals

| Window | Directory | Runs |
|---|---|---|
| 1 | `~/Documents/Projects/Wayfinder.Umbraco` | Stack 1, stays running |
| 2 | `~/Documents/Projects/Umbraco.Prism` | Stack 2, stays running |
| 3 | `~/Documents/Projects/Umbraco.Prism` | the helper script for Demo 3 and the pre-flight check |

## The one command that tells you if you are ready

```bash
scripts/demo.sh check
```

It prints a tick or a cross for every tool and every URL above, including whether the dev
certificate is trusted. Run it after both stacks are up, again 10 minutes before you go on, and
again if anything feels off. If it is all ticks, the talk will not hit a certificate or port
problem.

---

# Part 1: Setup

**Assumptions.** macOS, zsh, both repos under `~/Documents/Projects/`. Elsewhere, or on Windows? Swap
the `cd` paths and use `set VAR=value` or `$env:VAR="value"` in place of `export VAR=value`.

## 1.1 Once, days before

```bash
dotnet dev-certs https --trust
```

Do the full dry run (1.2 to 1.5, then Part 2) the day before. macOS may ask for your password.

## 1.2 Terminal 1: Stack 1 (Wayfinder.Umbraco)

Run these in order, in the one window.

```bash
cd ~/Documents/Projects/Wayfinder.Umbraco
rm -rf Wayfinder.Umbraco.ReferenceApp/umbraco/Data/*
(cd Wayfinder.Umbraco.Client && npm ci && npm run build)
export Umbraco__CMS__Global__TimeOut=02:00:00
dotnet run --project Wayfinder.Umbraco.AppHost
```

What each line does:

1. Go to the repo.
2. Wipe the local database so content, the `njf-coaching-register` blueprint and the MCP client
   credentials reseed cleanly, and any stage added by a previous Demo 3 rehearsal disappears. Stop a
   running instance first.
3. Build the backoffice TypeScript bundle.
4. Stop the backoffice logging you out mid-talk. This lives only in this window.
5. Start Aspire. The first boot after a wipe takes a couple of minutes.

✔ Ready when `https://localhost:44399/umbraco` shows the login screen and, after signing in,
**Settings > Blueprints** lists `njf-coaching-register`.

## 1.3 Terminal 2: Stack 2 (Umbraco.Prism)

```bash
cd ~/Documents/Projects/Umbraco.Prism
node scripts/validate-aspire-prereqs.mjs
export Umbraco__CMS__Global__TimeOut=02:00:00
dotnet run --project src/UmbracoPrism.AppHost
```

For a pristine start (wipes TestSite's database, throwaway tenants and stuck runs), add
`export PRISM_TESTSITE_RESET_RUNTIME=true` before the last line, and `unset PRISM_TESTSITE_RESET_RUNTIME`
once it has booted so a later restart does not wipe again.

✔ Ready when `https://localhost:17214` shows every resource **Running**, and **Settings > Prism
Dashboard** in the backoffice shows the seeded "Local Dev (Keycloak)" tenant.

## 1.4 Terminal 3: check everything

```bash
cd ~/Documents/Projects/Umbraco.Prism
scripts/demo.sh check
```

✔ Ready when it ends with **All good. Ready for the talk.** Fix every cross before continuing. The
message beside each cross says what to do.

## 1.5 Screen layout

Two VS Code windows and two browser windows, so a stack switch is one click.

```bash
code ~/Documents/Projects/Wayfinder.Umbraco
code -n ~/Documents/Projects/Umbraco.Prism
```

VS Code tabs, in order:

- `Wayfinder.Umbraco` window: `Wayfinder.Umbraco.ReferenceApp/Program.cs`, `AutomateCoachingStandardsSeeder.cs`, `service-blueprints/njf-coaching-register.json`
- `Umbraco.Prism` window: `src/UmbracoPrism.TestSite/Program.cs`, `src/UmbracoPrism.TestSite/TestSiteComposer.cs`

Browser tabs (open them from the URL table):

- Window A (Stack 1): backoffice, front end, Mailpit. Signed in as `admin@example.test`.
- Window B (Stack 2): Aspire dashboard, backoffice, front end. Signed in as `admin@prism.local`.
- Two private windows for Demo 1+2: one for Alex Applicant, one for Casey Caseworker.

Increase the editor font size so the back row can read it.

## 1.6 Last checks

- A one-page PDF on the Desktop for any file-upload step.
- Backup recordings queued (see the end of this page).
- Do Not Disturb on, Slack and mail closed, display mirroring.
- `scripts/demo.sh check` one more time.

---

# Part 2: The session

## Resetting between rehearsals

| Need | Do |
|---|---|
| Restart the coach journey | open `https://localhost:44399/apply-to-coach?action=start-new` |
| Restart the licence journey | open `https://localhost:44345/apply-for-a-juggling-licence?action=start-new` |
| Full Stack 1 reset (also removes the Demo 3 stage) | Ctrl+C in Terminal 1, then repeat 1.2 |
| Full Stack 2 reset | Ctrl+C in Terminal 2, then repeat 1.3 with the reset variable |

Seed JSON edits only take effect after a full Stack 1 wipe. Demo 3 edits `njf-coaching-register`
itself, so rehearsing Demos 1 to 3 again without a wipe adds a second nickname stage.

---

## Demo 1 + 2: Wayfinder.Umbraco, then Automate (~9-10 min)

Goal: the backoffice service design surface, and that wiring in a real external system is config plus
a few seeded steps.

### Beat A: the code (🧑‍💻, VS Code `Wayfinder.Umbraco`, 2 min)

Open the `Program.cs` tab. Do not read linearly. Jump to three places.

| Scroll to | Say |
|---|---|
| `builder.Services.AddWayfinderUmbraco(options => { ... })` | "Three delegates. Who's the tenant, who's the user, what can they see and do. That's the entire contract between Wayfinder.Umbraco and a host." |
| `.AddComposers()` and the comment above it | "Automate is a separate MIT-licensed Umbraco package. It registers itself the moment it's referenced. I haven't written a line of glue for it yet." |
| `app.MapWebhookSupportSystemCallbacks(...)` near the bottom | "That's the one line that lets an external system call back in and say 'this step is done, here's the outcome.' One route." |

✔ Under two minutes, and the room has seen the three delegates.

**Role switch, out loud:** "That's the last C# you'll see from me for a while." Minimise VS Code.

### Beat B: tour the blueprint (🎨, browser Window A, 2 min)

1. Backoffice, **Settings > Blueprints**, open **njf-coaching-register**.
2. Click **Fit**.
3. Click through: "Apply to coach", the Split, "Review application", the Split into the standards
   check, the three outcome routes.
4. Click the **Definition** tab briefly.
   > "Same editor whether a human built this or an AI agent did. That matters in ten minutes."

✔ The graph fits the screen and the Definition tab shows JSON.

### Beat C: run it as a citizen (2 min)

1. Alex's private window: `https://localhost:44399/demo/login`, pick **Alex Applicant**.
2. Open **Apply to coach**. Name and email are already filled in.
   > "Nobody wrote code for that. The blueprint declares a `user` value and says these two fields
   > default from it."
3. Fill in `yearsCoaching` = `1` (forces the review branch), a disclosure reference, a first-aid
   expiry date. Submit.
   > "That's a real wait state. Nothing polling client-side, a genuinely paused instance."

✔ The page says the application is being reviewed.

### Beat D: the registrar and Automate (🎨, 3 min)

> Up front: "I haven't gone back to code once since Beat A."

1. Casey's private window: `https://localhost:44399/demo/login`, pick **Casey Caseworker**. Open
   **Coaching register queue**, pick up the application, open **Review application**.
2. Click **Run coaching-standards check**. The stage shows its own waiting screen.
   > "This is the third lane of a service blueprint, the support process. This app just POSTed a
   > signed webhook out to an Automate automation. Let's go and look at it."
3. Backoffice, **Umbraco Automate** section. Optional: show `AutomateCoachingStandardsSeeder.cs` first.
   > "This whole automation is built and published in C# on every boot via `IAutomationService`."
4. **Approvals** tab: point at the pending request naming the applicant.
5. Mailpit (`https://localhost:8025`): open the "needs review" email.
6. Back in Automate **Approvals**, approve via the dialog. Point at the run resolving.
7. Casey's window: the wait screen has become **Confirm the outcome**. Click **Record and notify the
   applicant**.
8. Alex's window: refresh.

✔ Alex's page reads **Application complete** with the outcome.

> Close: "Branching logic, a real external system, a human approval gate, a notification back to the
> applicant, and the only code we wrote for the integration was three delegates and a callback route."

**If it breaks:** cut to the recorded backup. Do not debug live.

---

## Demo 3: MCP, have a conversation with a blueprint (~4-5 min)

Goal: a blueprint is something you talk to. A small, real change to the `njf-coaching-register` the
room just watched run.

> "Everything we just did in the backoffice, an AI agent can do too, over the same identity and
> permissions. Right now, the thing you'll reach for most days: a small change to a live service."

Use **Terminal 3**. Everything below runs there.

### Beat A: connect (🧑‍💻, 30 sec)

```bash
scripts/demo.sh mcp-connect
```

The script registers the Wayfinder MCP server with Claude Code, opens the browser login (sign in as
`admin@example.test` / `Wayfinder123!`), then lists the servers.

✔ A browser tab opens, completes, and the list shows `wayfinder-umbraco` as **Connected**.

> "Same OAuth screen the backoffice uses. Whatever group membership I have is what the agent gets."

If you would rather show the individual commands, this is what the script runs, from `~/demo-scratch`:

```bash
claude mcp add --transport http wayfinder-umbraco \
  http://localhost:5299/wayfinder/service-blueprint-authoring/mcp \
  --client-id umbraco-back-office-wayfinder-mcp \
  --callback-port 33418
claude mcp login wayfinder-umbraco
claude mcp list
```

**Why http and not https.** The Claude CLI does not trust the .NET development certificate. It
ignores the macOS keychain that `dotnet dev-certs https --trust` fills, and it ignores
`NODE_EXTRA_CA_CERTS`, so an `https://localhost:44399` MCP URL fails with "unable to verify the first
certificate". The Wayfinder ReferenceApp also listens on `http://localhost:5299`, and the OAuth
discovery documents and login work the same over loopback, so the MCP leg uses that. Nothing to
trust, nothing to export, nothing to forget. The rest of the talk stays on https.

**Role switch, out loud:** "From here I'm a designer, I'm just going to talk to it."

### Beat B: ask the room, then the agent (🎨, 2-3 min, the centrepiece)

Still in Terminal 3, from `~/demo-scratch` (a directory outside both repos, so the agent has no
codebase context):

```bash
cd ~/demo-scratch
claude --model sonnet \
  --tools "mcp__wayfinder-umbraco__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" \
  --permission-mode bypassPermissions
```

The flags are staging choices, not requirements. `--tools` limits the agent to the Wayfinder tools (no
Bash or file tools), `--permission-mode bypassPermissions` stops it asking approval before each call,
and `--model` picks the model. A plain `claude` from `~/demo-scratch` also finds the registered
server, which is fine for a rehearsal.

✔ The Claude Code prompt appears.

Ask the room: "A coach just got accredited. What's one small, fun thing we should give them?" Take a
suggestion. If the room is quiet, paste this fallback verbatim:

```text
In the njf-coaching-register definition, right after a coach is accredited, before the final confirmation, add a stage where they pick a fun coaching nickname from a short list you invent, juggling-themed, playful (think "The Whirling Dervish," "Captain Diabolo"). Show their chosen nickname back to them on the confirmation screen. Validate and simulate the accredited path, then save.
```

> While typing: "Notice what I'm not telling it. Not which component type, not where in the JSON, not
> how routing works. It knows that from reading this definition."

> While it works: "It's reading the live definition as its style reference, adding one stage in the
> right place, and it'll validate and simulate before saving. That's knowledge in the system, not in
> an agent's head, which goes stale."

✔ The agent reports the blueprint saved, well under a minute.

### Beat C: see it (1 min)

Window A backoffice, **Settings > Blueprints > njf-coaching-register**, click **Fit**.

✔ A new nickname stage sits between the accredited outcome and confirmation.

> "That's live. The next coach who's accredited sees this. No restart, no redeploy, nobody touched
> code."

If time allows, open `https://localhost:44399/apply-to-coach?action=start-new` as Alex and watch the
new stage render.

> Close: "That's the capability at its smallest. Given more space, the same conversation builds the
> whole thing from nothing. Ask me afterwards."

Type `/exit` before moving on.

---

## Demo 4: Umbraco.Prism (~7-8 min)

Goal: the same "how little code" beat, two fast backoffice wins (multi-tenant, live rebrand), a
video-only mobile beat, then close the loop by showing the juggling world running on Wayfinder.Umbraco
inside Prism.

**Switch now:** browser Window B, VS Code window `Umbraco.Prism`.

### Beat A: the code (🧑‍💻, 2 min)

Open `src/UmbracoPrism.TestSite/TestSiteComposer.cs`, scroll to the `AddWayfinderUmbraco(options => ...)`
block.

> "Spot the difference. A full multi-tenant, OIDC-secured site with its own branding system, and the
> only extra thing Prism adds is resolving tenant, user and access profile from its own concepts
> instead of a demo cookie. Two products, one seam."

> "And yes, same juggling world. TestSite installs Wayfinder.Umbraco directly, like any host would."

✔ The block is on screen and matches Beat A of Demo 1.

**Role switch, out loud:** "Laptop closed again. Everything from here is Settings and forms."

### Beat B: multi-tenant, live (🎨, 2 min)

Backoffice **Settings > Prism Dashboard**. Show the seeded tenant, then create a **new tenant**. Name it
something the room will enjoy. Point its OIDC authority at the local Keycloak realm `prism-dev`, so it
is a real working login.

> "A second fully independent branded portal, its own identity provider config, on the one Umbraco
> instance already running."

✔ The new tenant appears in the tenant list.

Short of time: skip creation and edit the seeded "Local Dev (Keycloak)" tenant instead. Delete any
tenant you created after each rehearsal.

### Beat C: live rebrand (2 min)

Open a tenant's branding editor.

> "This isn't a theme picker with six presets. It's your actual CSS custom properties, introspected
> into a labelled settings form."

Change the primary colour token, save, switch to the front-end tab, refresh.

✔ The front end shows the new colour immediately.

### Beat D: mobile (video only, 1 min)

Play the pre-recorded clip.

> "I'm not going to fight a simulator live. Here's what one click against that tenant's branding
> produces."

✔ The clip shows the tenant's icon, splash and colours, and the biometric login prompt.

### Beat E: close the loop (1-2 min)

Front end `https://localhost:44345`, sign in via Keycloak SSO as `demo@prism.local` / `password`, open
**Apply for a juggling licence**.

> "Tenancy and branding from Prism. The service journey from Wayfinder.Umbraco. One site, the same
> pattern we spent twenty minutes looking at from the other side."

Optional, 30 seconds: open `JugglingLicenceDecisionAutomationSeeder.cs` and the Automate section.

✔ The licence journey renders in the tenant's branding after SSO sign-in.

> Closing line for the whole talk: "Four demos, one running joke. Every time, the same person opened
> `Program.cs` and then closed it and opened a browser, or for ten minutes in the middle opened a
> terminal and just talked. Two roles, same afternoon. That's the point of building these packages
> this way."

Then straight into takeaways.

---

# Part 3: Teardown

Ctrl+C in Terminals 1 and 2, then:

```bash
claude mcp remove wayfinder-umbraco
```

✔ Both terminals return to a prompt, and Docker Desktop shows no leftover `mailpit`, Keycloak or proxy
containers.

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
