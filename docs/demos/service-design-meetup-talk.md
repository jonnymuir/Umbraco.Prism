# Demo script: "Service Design: From Potato Peelers to Butterflies" (Umbraco meetup talk)

> **What this is.** A live-demo script for a conference/meetup talk, kept here because
> `docs/demos/` is already this repo's home for narrated demo storyboards rather than CI-gated
> `docs/walkthroughs/` content (see `licence-transfer-mcp-walkthrough.md` in this same folder for
> that distinction). It genuinely spans two repos. Most of it runs against
> [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco)'s own
> `Wayfinder.Umbraco.ReferenceApp`, only the last demo runs against this repo. Kept as one
> combined doc here anyway so there's exactly one place to look when preparing the talk, rather
> than splitting attention across repos mid-rehearsal.

Audience: Umbraco meetup, mostly developers. Order follows the deck as-is, skipping a
standalone (non-Umbraco) Wayfinder demo per the presenter's call, straight into
Wayfinder.Umbraco. Every demo below is built from features that already exist and, where
possible, already have a tested walkthrough behind them:

- Demo 1+2 (Wayfinder.Umbraco + Automate) = slides 6-8, based on
  `Wayfinder.Umbraco/docs/automate-support-system-walkthrough.md`.
- Demo 3 (MCP) = slide 9: a small, live conversational edit to the same `njf-coaching-register`
  definition Demo 1+2 just ran, not a from-scratch build. The bigger "design an entire service"
  capability is referenced out loud but not shown; its full script lives in the Appendix at the
  end of this doc, as a separate demo in its own right.
- Demo 4 (Prism) = slide 10, built from `UmbracoPrism.TestSite`'s composer/Program.cs.

The whole talk (explore service design, all four demos, takeaways) targets **~30 minutes** (see
Rough timing at the end of this doc), leaving real room for questions in a typical meetup slot.

### The thread running through all four demos: one person, two hats

Every demo below is deliberately shot from both points of view, and the whole talk's argument is
that these are not two different jobs done by two different people. They can be the same
afternoon, worn by the same you:

- 🧑‍💻 **Developer hat**: the person who adds a `PackageReference`, writes three delegates in
  `Program.cs`, and never opens a visual editor.
- 🎨 **Designer hat**: the person who only ever opens the backoffice (or talks to an agent in
  plain language) and never opens `Program.cs`.

Call out the hat switch explicitly, out loud, every time it happens, naming the skill-set switch
is doing real work here, not just decoration. The switches land at:

1. **Demo 1, Beat A to Beat B**: write three lines of C#, then in the same breath open the same
   site's backoffice and behave like someone who has never seen that file.
2. **Demo 3, Beat A to Beat B**: Beat A is pure developer work (OAuth client, MCP registration).
   Beat B is pure designer work (stop typing commands, start typing sentences). Same terminal,
   same person, thirty seconds apart.
3. **Demo 3's real thesis, stated once, directly**: the agent itself is doing both jobs at once,
   it reads the live definition like a developer (the existing stages, routing, component
   contract) and writes the addition like a designer (a stage, a label, plain-language copy).
   That's the "knowledge in system, not in head" point from slide 9, and it's *why* one person can
   do both jobs. The system, not the person's memory, is what enforces the contract between them.
4. **Demo 4, Beat A to Beat E**: opens on `Program.cs` (developer), closes back on the same
   juggling-licence journey from Demo 1-3 now running happily alongside a live-edited brand
   (designer), full circle, same pattern, different repo.

Two separate running stacks, in this order:

1. **Wayfinder.Umbraco.ReferenceApp** (via its own Aspire host, in the sibling
   `jonnymuir/Wayfinder.Umbraco` repo), Demos 1, 2, 3.
2. **Umbraco.Prism** full stack (via `UmbracoPrism.AppHost`, this repo), Demo 4.

---

## Before doors open

**Stack 1: Wayfinder.Umbraco.ReferenceApp** (sibling repo)
```bash
cd Wayfinder.Umbraco.Client && npm ci && npm run build && cd ..
dotnet run --project Wayfinder.Umbraco.AppHost
```
- Backoffice: `https://localhost:44399/umbraco`, `admin@example.test` / `Wayfinder123!`
- Front end personas at `/demo/login`: Alex Applicant (citizen), Casey/Jordan Caseworker
- Mailpit: `https://localhost:8025` (real inbox for the standards-officer email)
- For a completely clean run (recommended right before the talk), wipe the DB first:
  `rm -rf Wayfinder.Umbraco.ReferenceApp/umbraco/Data/*` and boot with
  `Umbraco__CMS__Global__TimeOut=02:00:00` set, cheap insurance against the backoffice session
  timing out mid-demo (matters far more if the Appendix's from-scratch build ever gets shown
  instead of the short Demo 3).

**Stack 2: Umbraco.Prism** (this repo)
```bash
dotnet run --project src/UmbracoPrism.AppHost
```
- TestSite: `https://localhost:44345`
- Backoffice: `admin@prism.local` / `PrismLocal!12345`
- Keycloak SSO: `demo@prism.local` / `password`

**VS Code**: two workspaces open in separate windows, `Wayfinder.Umbraco` and `Umbraco.Prism`,
so you're never waiting on a window switch mid-flow. Pre-open these files as tabs, in this order,
so you just click tabs rather than navigating:
- `Wayfinder.Umbraco.ReferenceApp/Program.cs`
- `Wayfinder.Umbraco.ReferenceApp/AutomateCoachingStandardsSeeder.cs`
- `Wayfinder.Umbraco.ReferenceApp/service-blueprints/njf-coaching-register.json`
- `UmbracoPrism.TestSite/Program.cs`
- `UmbracoPrism.TestSite/TestSiteComposer.cs`

**Terminal**: a clean scratch directory outside both repos, for the `claude mcp add` /
`claude mcp login` dance in Demo 3. Don't run this from inside either repo, so it's obviously
"just talking over MCP," not "the demo has secret extra context from the codebase."

**Browser tabs**, pre-opened and pinned, in this order: ReferenceApp backoffice (logged in),
ReferenceApp home, Mailpit, then (second window/profile) Prism backoffice (logged in), Prism
TestSite home.

**A one-page PDF** on the Desktop, for any file-upload step you reach.

---

## Resetting between rehearsal passes

Run this many times before the real thing. Here's exactly when a reset is needed and when it
isn't, so seeding does as much of the work as it actually can.

### Demo 1+2 (`njf-coaching-register`, Wayfinder.Umbraco.ReferenceApp)

`njf-coaching-register` has `allowManualRestart: true`, so reset the coach journey any time,
mid-flow or after completion, just by visiting `/apply-to-coach?action=start-new`. **No DB wipe
needed for this demo.**

- **If you ever edit a seed JSON on an install that's already booted once, a plain restart won't
  pick it up.** `ReferenceBlueprintSeeder` seeds with `store.SaveAsync(blueprint, expectedVersion: 0)`,
  which only succeeds when nothing is stored yet. An existing blueprint is seeded once, ever, and
  never re-synced from the JSON on later boots (`DemoTenantSeeder` on the Prism side behaves
  differently and really does reconcile every boot, don't assume one implies the other). A seed
  JSON change needs a full DB wipe and restart to take effect, not just a restart.
- **Demo 3 now edits `njf-coaching-register` itself** (adds a stage) rather than creating a
  separate blueprint, so it stacks on top of whatever's already there. Rehearsing Demos 1-3
  together more than once without a wipe in between will add another stage on top of the last
  one, not replace it. Wipe between full rehearsal passes if doing all three in sequence more than
  once.
- **Full wipe command**, when one is wanted (e.g. to declutter Settings > Blueprints after many
  MCP rehearsal passes, or for a pristine take before the final recording):
  ```bash
  # stop the running app first
  rm -rf Wayfinder.Umbraco.ReferenceApp/umbraco/Data/*
  ```
  Everything reseeds from scratch on next boot (content, `njf-coaching-register`, the demo MCP
  agent's client credentials), nothing to redo by hand.

### Demo 4 (`apply-for-a-juggling-licence` + tenancy, Umbraco.Prism)

- The juggling-licence journey **already has `allowManualRestart: true`**, no fix needed, no
  wipe needed, ever. Reset with `/apply-for-a-juggling-licence?action=start-new`.
- **Live tenant creation (Beat B)** has no reset button of its own. Each rehearsal pass leaves a
  new tenant row behind unless it's deleted. Two options: delete the created tenant from
  Settings > Prism Dashboard after each pass (fast, in-app, no restart), or, better for
  rehearsal, just edit the branding of the already-seeded "Local Dev (Keycloak)" tenant instead
  of creating a new one every time, and save the live "create a tenant from scratch" moment for
  the real talk.
- **Full wipe**, only if things get generally messy (lots of throwaway tenants, a stuck
  automation run):
  ```bash
  PRISM_TESTSITE_RESET_RUNTIME=true dotnet run --project src/UmbracoPrism.AppHost
  ```
  This is the same disposable-runtime mechanism `TestSiteRuntimeLayout` already gives you,
  everything reseeds.

---

## Demo 1 + 2: Wayfinder.Umbraco, then Automate (slides 6-8)

One continuous live thread, ~9-10 minutes. Goal: prove the backoffice-hosted service design
surface *and* that wiring a real external-system integration into it is config plus a few seeded
steps, not bespoke plumbing. This block does the hat-switch twice: developer to designer at Beat
A to B, then back to "designer watching a system a developer built actually integrate" at Beat D.

### Beat A: the code (🧑‍💻 developer hat, VS Code, ~2 min)

Open `Program.cs`. Narrate while scrolling to the relevant lines, don't read the file linearly:

> "This is a stock Umbraco 17 site. Three things turn it into what you're about to see."

1. Point at:
   ```csharp
   builder.Services.AddWayfinderUmbraco(options => {
       options.ResolveTenantId = _ => "reference";
       options.ResolveUserId = ctx => ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
       options.ResolveAccessProfile = ReferenceAppAuth.ResolveAccessProfile;
   });
   ```
   **Say:** "Three delegates. Who's the tenant, who's the user, what can they see and do. That's
   the entire contract between Wayfinder.Umbraco and a host. Everything else about *this*
   site's login system is irrelevant to it."

2. Point at `.AddComposers()` and the comment above it about Umbraco Automate:
   **Say:** "Automate is a completely separate MIT-licensed Umbraco package. It registers itself
   the moment it's referenced. I haven't written a line of glue for it yet."

3. Point at the single `app.MapWebhookSupportSystemCallbacks(...)` line near the bottom.
   **Say:** "And that's the one line that lets an *external* system, in a minute, an Automate
   automation, call back in and say 'this step is done, here's the outcome.' One route."

Keep this under two minutes, it's a taste, not a code review.

**Hat switch, said out loud:** "Right, that's the last C# you'll see from me for a while.
Closing the laptop's code brain, opening the browser's design brain." (Physically close VS Code
or minimise it, the visual beat matters as much as the line.)

### Beat B: tour the blueprint in the backoffice (🎨 designer hat, ~2 min)

Switch to the browser, already logged into the backoffice.

1. **Settings > Blueprints** (this is Umbraco's own Settings > Advanced group, same convention
   as the Webhooks package, no bespoke "Prism" or "Wayfinder" section in the tree).
2. Open **njf-coaching-register**. This is the exact screenshot on slide 7.
3. Click **Fit**, then click through: the "Apply to coach" stage, the Split, "Review
   application," the Split into the standards check, the three outcome routes.
4. Click the **Definition** tab briefly, real JSON, CodeMirror-rendered.
   **Say:** "This is the same editor whether a human built this or an AI agent did, which
   matters in about ten minutes."

### Beat C: run it as a citizen (~2 min)

1. New tab, `/demo/login`, pick **Alex Applicant**.
2. **Apply to coach**: name, email, `yearsCoaching = 1` (deliberately forces the review branch,
   the interesting path), a disclosure reference, a first-aid expiry date.
3. Submit, lands on a "we are reviewing your application" wait screen.
   **Say:** "That's a real wait state. Nothing is polling in a loop client-side hoping, this is
   a genuine paused instance."

### Beat D: pick it up as the registrar, hit Automate (still 🎨 designer hat, ~3 min)

**Say up front:** "Notice I haven't gone back to code once since Beat A. Everything from here,
the review, the automation, the approval, is built and run by someone who only ever opens a
browser."

1. `/demo/login`, pick **Casey Caseworker**. Open **Coaching register queue**, pick up the
   application, open **Review application**.
2. Click **Run coaching-standards check**. The stage shows its own waiting screen.
3. **Say, before switching tabs:** "This is the third lane of a service blueprint that's easy to
   forget about when you're staring at the front-end journey, the *support process*. Right now
   this app just POSTed a signed webhook out to an Automate automation. Let's go look at it."
4. Switch to **Umbraco Automate** section in the backoffice. **Optional, skip if short on time:**
   open `AutomateCoachingStandardsSeeder.cs` in VS Code first for one more code beat, **say**
   "this whole automation, the branch, the email, the approval gate, is built and published in
   C# on every boot, via `IAutomationService`. Nobody clicked this together by hand, though you
   *could* have."
5. Open the **Approvals** tab. Point out the pending request, naming the real applicant.
6. Switch to **Mailpit**, the "needs review" email has arrived for real.
7. Back in the backoffice Automate section's **Approvals** tab, approve it via the dialog.
   Point out the run resolving.
8. Back to Casey Caseworker's tab: the case is still **With you** (not re-queued, team-tray
   ownership survived the round trip). The wait screen has released into **Confirm the outcome**,
   showing the real `coachingStandardsOutcome` and note. Click **Record and notify the
   applicant**.
9. Back to Alex Applicant's tab: refresh, **Application complete**, showing the outcome.

**Closing line for this whole block:** "Branching logic, a real external system, a human
approval gate, a push notification back to the applicant, and the only code any of us wrote for
the integration itself was three delegates and a webhook callback route."

**If it breaks:** cut to the recorded backup for this exact sequence (see Recording Plan below)
rather than debugging live. Note the timestamp for "Beat D" once it's recorded.

---

## Demo 3: MCP, have a conversation with a service blueprint (slide 9)

~4-5 minutes. Goal: prove a service blueprint isn't a static file, it's something to *talk to*
about a small, real change, on the exact same `njf-coaching-register` definition the room just
watched work end to end in Demo 1+2. Deliberately not a from-scratch build, that capability gets
referenced out loud, not shown (see the Appendix for the full version, a separate demo in its own
right).

### Setup narration (before typing anything)

> "Everything we just watched happen in the backoffice, an AI agent can do too, over the exact
> same identity, the exact same permissions. And it's not limited to small edits like the one
> we're about to make. The same conversation can design an entire branching service from a blank
> page. That's genuinely a whole other demo, ask me about it afterwards. Right now, let's do the
> thing you'll actually reach for most days: change something small on a service that's already
> live."

### Beat A: connect (🧑‍💻 developer hat, live, ~30 sec)

In the scratch terminal:

```bash
claude mcp add --transport http wayfinder-umbraco \
  https://localhost:44399/wayfinder/service-blueprint-authoring/mcp \
  --client-id umbraco-back-office-wayfinder-mcp \
  --callback-port 33418
claude mcp login wayfinder-umbraco
claude mcp list
```

**Narrate briefly:** "Same OAuth screen the backoffice login uses. Whatever group membership I
have is what the agent gets."

**Hat switch, said out loud:** "Done. From here I'm a designer, not a developer, just going to
talk to it."

### Beat B: ask the room, then the agent (🎨 designer hat, live, ~2-3 min, the centrepiece)

**Ask the room:** "A coach just got accredited. Before we send them off, what's one small, fun
thing we should give them?" Take a suggestion, almost anything works, since the ask is
deliberately small: one new stage, at one clear point in an already-working flow.

**Scripted good fallback**, use verbatim if the room is quiet:

> In the `njf-coaching-register` definition, right after a coach is accredited, before the final
> confirmation, add a stage where they pick a fun coaching nickname from a short list you invent,
> juggling-themed, playful (think "The Whirling Dervish," "Captain Diabolo"). Show their chosen
> nickname back to them on the confirmation screen. Validate and simulate the accredited path,
> then save.

**Say while typing the brief:** "Notice what I'm *not* telling it, not which component type to
use, not where in the JSON the stage goes, not how routing works. It already knows all of that
from reading this definition."

**While it works, narrate (this is slide 9's actual thesis):** "It's reading the live
`njf-coaching-register` definition as its own style reference, adding one stage in the right
place, and it'll validate and simulate before it saves, same discipline whether the change is
one stage or twenty. That's the difference between knowledge in an agent's *head*, which goes
stale, and knowledge it can *look up in the system*, which can't."

Done in well under a minute for a change this size, reporting the save is the signal to move on.

### Beat C: see it (~1 min)

**Settings > Blueprints > njf-coaching-register**, the new stage is sitting in the graph,
between the accredited outcome and confirmation.

**Say:** "That's live. The next coach who gets accredited sees this. No restart, no redeploy, and
nobody touched a line of code."

If there's time, re-run the Demo 1+2 happy path once more as Alex Applicant to see the new
nickname stage render for real, skip this if the clock's tight, the graph view alone lands the
point.

**Closing line:** "That's the capability at its smallest. Given more space, the same conversation
builds the whole thing from nothing. I've done that as its own demo, happy to show anyone who
wants to see it afterwards."

---

## Demo 4: Umbraco.Prism (slide 10)

~7-8 minutes. Different stack, switch browser window/profile and VS Code window to Prism now.
Goal: same "how little code" philosophy beat as Demo 1, then two fast backoffice wins
(multi-tenant + live rebrand), a **video-only** mobile beat, then close the whole talk's loop by
showing this same NJF/juggling world already running on Wayfinder.Umbraco *inside* Prism.

### Beat A: the code, side by side with what was already shown (🧑‍💻 developer hat, ~2 min)

Open `UmbracoPrism.TestSite/TestSiteComposer.cs`, scrolled to the `AddWayfinderUmbraco(options => ...)`
block (it's the same three-delegate shape already shown in the ReferenceApp).
**Say:** "Spot the difference. This is a full multi-tenant, OIDC-secured site with its own
branding system, and the *only* extra thing Prism adds on top of the exact same three-delegate
Wayfinder.Umbraco contract is resolving tenant/user/access profile from Prism's own concepts
instead of a demo cookie. Two products, one seam."

Point at `TestSiteSeedContract.JugglingLicenceBlueprintSlug` if it's still on screen from setup,
**say:** "And yes, same juggling world. TestSite installs Wayfinder.Umbraco directly, exactly
the way any host would, for its own citizen journey."

**Hat switch, said out loud:** "Laptop closed again. Everything from here is Settings and forms,
the same backoffice a content editor with zero C# would use."

### Beat B: multi-tenant, live (🎨 designer/product hat, ~2 min)

Backoffice > **Settings > Prism Dashboard** (Settings > Advanced, same placement convention as
Blueprints). Show the existing seeded tenant. Then, live:

1. Create a **new tenant**, name it something the room will enjoy, point its OIDC authority at
   the same local Keycloak realm (`prism-dev`) so it's a real working login, not a stub.
2. **Say while it saves:** "That's a second fully independent branded portal, its own identity
   provider config, on the one Umbraco instance already running."

### Beat C: live rebrand (~2 min)

Open the branding editor for a tenant. **Say:** "This isn't a theme picker with six presets, it's
your actual CSS custom properties, introspected into a labelled settings form." Change the
primary colour token, save, switch to the front-end tab, refresh. Instant rebrand, no redeploy.

### Beat D: mobile (video only, ~1 min, don't attempt live)

**Say plainly:** "I'm not going to fight a simulator live in front of you. Here's what one click
against that same tenant's branding produces." Play a short pre-recorded clip: the generated
Capacitor app, its icon/splash/colours matching the tenant just rebranded, biometric login
prompt.

### Beat E: close the loop (~1-2 min)

Switch to TestSite's public front end. Sign in via Keycloak SSO as `demo@prism.local`. Open
**Apply for a juggling licence**. **Say:** "Tenancy and branding from Prism. The service journey
itself from Wayfinder.Umbraco. Running in the one site, wired by the same pattern we just spent
twenty minutes looking at from the other side." If there's time, a 30-second peek at
`JugglingLicenceDecisionAutomationSeeder.cs` and the Automate section, "TestSite's own copy of
the exact automation pattern already shown working."

**Closing line for the whole talk, land the two-hats thread here explicitly:**

> "Four demos, one running joke: every single time, the same person opened `Program.cs` and then
> closed it and opened a browser instead, or, for ten minutes in the middle, opened a terminal
> and just talked. Different hats, same afternoon. That's not a coincidence of how these three
> packages were built. It's the point of building them this way."

Straight into whatever the "where next" / takeaways section lands on.

---

## Recording plan (for backups)

What exists versus what's still needed:

| Demo | Recording tool | Status |
|---|---|---|
| 1+2 (Automate) | `Wayfinder.Umbraco/tests/demo/coaching-register-automate-demo.spec.ts` (`npm run demo:record:automate`) | Exists, current architecture. Needs the full Aspire stack (Mailpit included). |
| 3 (MCP, in-talk: add a stage) | `Wayfinder.Umbraco/tests/demo/coaching-register-add-stage-demo.spec.ts` (`npm run demo:record:add-stage`) | Exists, current architecture. A real agent call, so re-record periodically as the reference app changes. |
| 4 (Prism) | `UmbracoPrism.Client/tests/demo/prism-tenancy-branding-demo.spec.ts` (`npm run demo:record:tenancy-branding`) | Exists, current architecture. Its own cleanup pass reverts the seeded tenant's branding and removes the tenant it creates, so rerunning it never leaves shared demo state altered. |
| 1 (code reveal beats) | N/A, VS Code screen only | No recording possible; worst case, talk through the file live from a static screenshot. |
| Appendix (from-scratch build) | `Wayfinder.Umbraco/tests/demo/demo-footage/wayfinder-umbraco-mcp-authoring-demo.mp4` (+ `.webm`, `.compressed.mp4`) | Exists, current architecture. Not needed for this talk's timing, kept for whenever the full version gets shown as its own session. `npm run demo:record` from `tests/demo/` re-runs it. |

**Do not use as backups:** `UmbracoPrism.Client/demo-footage/licence-transfer-demo.*`,
`garden-waste-permit-demo*.*`, `juggle*.*`. These demo Prism's own now-removed "CMS Service
Blueprint" backoffice feature, the architecture they show no longer exists (`TestSite` now
installs `Wayfinder.Umbraco` directly instead). They'd actively mislead if played as a "this is
how it works" backup. Worth deleting or clearly archiving so nobody grabs one by mistake mid-panic
before a talk.

Every recording tool above is gitignored output (`demo-footage/`), so the files themselves aren't
committed. Run the relevant `npm run demo:record:*` script against a warmed stack to produce them
before a real talk, and again any time the underlying blueprint or backoffice changes enough to
make a rerecord worthwhile.

---

## Rough timing

| Section | Minutes |
|---|---|
| Explore service design (slides 3-5, no demo) | 5 |
| Demo 1+2: Wayfinder.Umbraco + Automate | 9-10 |
| Demo 3: MCP (add a stage) | 4-5 |
| Demo 4: Prism | 7-8 |
| Where next / takeaways | 3 |
| **Total** | **~28-31** |

Leaves genuine room for questions and a bit of drift in a typical meetup slot. If still tight:
skip the optional `AutomateCoachingStandardsSeeder.cs` code peek in Demo 1+2 Beat D, or skip live
tenant creation in Demo 4 Beat B and just rebrand the already-seeded tenant instead.

---

## Appendix: build a brand-new service from scratch (not part of this talk)

The full "design an entire service from a blank page" demo that the main Demo 3 above references
but doesn't show, ~12-15 minutes on its own, worth doing as a separate session (or folding into a
longer version of this talk another time). Based on
`Wayfinder.Umbraco/docs/mcp-authoring-walkthrough.md`.

~12-15 minutes, the highest-risk block of this version, agent runs for this shape of brief have
taken 30-45 minutes in prior recordings, so the whole thing isn't run live end to end. Live:
connect + brief + watch it work + review in the editor. **Recorded only:** the full
applicant/caseworker run-through (Act 5 below), narrate over the recording instead of doing it
live.

### Act 1: connect (🧑‍💻 developer hat, live, ~1-2 min)

In the scratch terminal:

```bash
claude mcp add --transport http wayfinder-umbraco \
  https://localhost:44399/wayfinder/service-blueprint-authoring/mcp \
  --client-id umbraco-back-office-wayfinder-mcp \
  --callback-port 33418
claude mcp login wayfinder-umbraco
claude mcp list
```

**Narrate while it runs:** "That's registering the MCP server, then logging in, the exact same
OAuth screen the backoffice itself uses. Whatever group membership I have is what the agent gets.
No open door."

### Act 2: the brief, with the room's idea folded in (🎨 designer hat, live, this is the centrepiece)

**Ask the room first:** "The National Juggling Federation needs a new public service. What should
it be?" Let one or two suggestions land. There's latitude here, the brief below is a proven
*shape* (eligibility branch, evidence upload, declaration, caseworker decision), so the
specifics can genuinely be reflavoured live around whatever the room gives, as long as it roughly
fits that shape. If it's a wildly different shape (e.g. "a calculator," "a chatbot"), that's a
great "let's take that as an idea for next time" line, and fall back to the scripted one below.

**Scripted good fallback**, fully written, use verbatim if the room is quiet or the suggestion
doesn't fit a blueprint's shape:

> Hi. I work on public safety for the National Juggling Federation. I need a new service.
>
> The problem: performers keep asking to run flaming-torch juggling displays at public events,
> and right now there's no proper route to get one signed off, people are just doing it and
> hoping. I want an "Apply for a Flaming Torches Display Permit" service.
>
> What I know about how it needs to work:
> - Only for jugglers who already hold a current standard juggling licence with us. If they
>   don't, they need to get a standard licence first; that's a separate existing service.
> - They need a valid public liability insurance certificate, and a written risk assessment for
>   the specific venue.
> - Before we grant anything, they need to formally declare they won't perform within 3 metres of
>   flammable bunting, decorations, or a bouncy castle (yes, this has happened).
> - A safety officer always has to check the evidence and make the actual decision, this can
>   never be auto-approved.
> - Same accessibility bar as everything else we ship: WCAG double-A.
>
> Can you help me design this properly? Ask me anything you need.

**Launch, in the scratch terminal:**
```bash
claude --model sonnet \
  --tools "mcp__wayfinder-umbraco__*,ListMcpResourcesTool,ReadMcpResourceDirTool,ReadMcpResourceTool" \
  --permission-mode bypassPermissions
```
Paste the brief (adapted live with the room's flavour, or verbatim fallback above). Answer its
clarifying questions in plain language, in character as "the service owner," don't explain
implementation details, that's the point.

**Optional safety net:** the same brief can be run headlessly ahead of time (no `claude mcp login`
needed, the seeded demo-agent client credentials work non-interactively), so `Settings >
Blueprints` already has a finished, saved definition to open cold if the live room run struggles.

**While it works, narrate over the top:**

> "Notice it isn't guessing conventions. Before it drafted anything it read this host's existing
> blueprints as a style reference and asked what component types this host can actually render.
> That's the difference between knowledge in an agent's *head*, which goes stale, and knowledge
> it can *look up in the system*, which can't. The harness matters as much as the model here:
> Wayfinder has to be able to validate and simulate with real, specific errors, not just accept
> or reject, or the agent has nothing to correct itself against."

Let it run as long as the room's patience allows. It's done when it reports the blueprint saved.
Watch **Settings > Blueprints** in the backoffice tab for the new entry to appear the moment the
first save lands.

### Act 3: wire it in (live if time allows, ~1 min)

Backoffice > **Content > Apply** page > the service request stage block > change **Blueprint
key** to the new one > **Update** > **Save and publish**.
**Say:** "No restart. No redeploy. That save reached the live engine the moment it happened."

### Act 4: tour it in the visual editor (live, ~2 min)

**Settings > Blueprints** > open the new one. Fit to screen. Find the eligibility stage, the
document-upload stage, the safety/case-worker review stage, and the Join where both cursors
converge into the approved/rejected outcome. Open **Validation**, clean.

**Don't script the exact branching mechanism in advance.** A Split gateway fans out
unconditionally in this engine, so a genuine either/or (e.g. "holds a licence? yes/no") is
usually modelled as two distinct action buttons routing by trigger, rather than a condition on a
single Split, but the agent may choose either shape. Both are valid; narrate whichever is
actually on screen rather than pointing at a mechanism decided beforehand.

**Say:** "Every one of those traces to a line in the brief. Nothing in the brief named a stage, a
condition, a button, or a component type, that vocabulary is the agent's, from reading the
system, not mine."

### Act 5: run it (recorded only, narrate over playback)

Cut to the recording of: applicant journey through to confirmation, caseworker picking it up and
deciding. ~2 min of playback while narrating, not silent.

**Closing line:** "One conversation, one access token, a working, branching, GDS-shaped public
service, reviewed and adjustable by a human at every step along the way."

---

## Related

- [`Wayfinder.Umbraco/docs/automate-support-system-walkthrough.md`](https://github.com/jonnymuir/Wayfinder.Umbraco/blob/main/docs/automate-support-system-walkthrough.md): Demo 1+2's source walkthrough.
- [`Wayfinder.Umbraco/docs/mcp-authoring-walkthrough.md`](https://github.com/jonnymuir/Wayfinder.Umbraco/blob/main/docs/mcp-authoring-walkthrough.md): the Appendix's source walkthrough.
- [`licence-transfer-mcp-walkthrough.md`](licence-transfer-mcp-walkthrough.md) (this folder): the historical Prism-side MCP demo the Appendix's framing is modelled on; superseded architecturally, kept as a record.
