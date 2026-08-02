# Recorded demo: "Transfer a Professional Juggling Licence" — built live over MCP

> **Historical record, not a reproducible guide.** This documents a demo that was actually
> recorded, but the feature it's built on — Prism's own "CMS Service Blueprint" backoffice
> authoring surface (`CmsServiceBlueprintAuthoringController`, its keyed MCP endpoint, the
> `cms-visitor` queue) — was removed entirely once it became redundant with `Wayfinder.Umbraco`'s
> own independent "Blueprints" backoffice section. The demo content itself (the juggling-licence
> transfer blueprint) still exists, now authored the same way any Wayfinder.Umbraco-hosted
> definition is — see `UmbracoPrism.TestSite`'s own `PublicVisitorQueue`/`LicenceTransferServiceRequestSeeder`.
> Kept as a record of what was built and how, not as current step-by-step instructions.

A narrated screen recording (not a CI-gated behavioural walkthrough — see
[Why this isn't under `docs/walkthroughs/`](#why-this-isnt-under-docswalkthroughs) below) showing
an AI agent design and build a real, complex Prism Cms Service Blueprint — branching eligibility, a
guidance checklist you must acknowledge before continuing, real document upload, a review and
declaration — from a single conversation, using nothing but Prism's documented MCP toolkit. This
doc is both the write-up and the storyboard: every act below is what gets recorded, in order,
with the actual commands and narration.

## Who this is for

Three audiences at once, and the recording is paced to keep all three following:

- **Developers** — wiring the MCP toolkit and its real backoffice auth into their own app.
- **Content creators** — seeing that the guidance content the service blueprint links to is ordinary,
  independently-editable CMS content, not baked into the service blueprint.
- **Service Blueprint / service designers** — seeing the actual design loop (draft → validate → simulate →
  save) an agent follows, and that a complex, branching, multi-capability service is well within
  reach of a single well-briefed conversation.

## What you'll see

| Act | What happens | Why it's here |
|---|---|---|
| Cold open | Frame the demo and the three audiences | Sets expectations before the technical parts start |
| 1. Getting the agent real access | Create a service-account admin, register client credentials, exchange for a bearer token, wire `claude mcp add` | The one thing genuinely new versus Prism's other two MCP demos — Cms Service Blueprint's MCP requires real backoffice auth, not an open endpoint |
| 2. Handing over the brief | Paste one brief into the agent's terminal; it researches nothing (this is a fictional domain), designs, validates, simulates, and saves | The actual "wire up a complex service blueprint simply" moment |
| 3. Wiring it into the site | Backoffice: create the page, set its Blueprint Key, publish, add a nav link | No restart, no redeploy — the save already reached the live engine |
| 4. Reviewing what it built | Open the visual editor, tour the graph | Proves a human can inspect/adjust anything the agent authored |
| 5. Running it as a visitor | Eligibility → guidance checklist → licence details → upload evidence → check answers → declaration → confirmation | The proof: it's not just saved, it actually works end to end |
| Closing | Recap | — |

## Before you start

1. **A clean, fully-seeded stack.** This demo depends on the guidance articles (Transfer Rules,
   International Transfers, Supporting Evidence, Professional Standards) and the `file-upload`/
   `guidance-checklist` component types already existing — both shipped in the component-build
   pass this walkthrough follows. Reset and restart so every seeder runs from scratch:
   ```
   PRISM_TESTSITE_RESET_RUNTIME=true dotnet run --project src/UmbracoPrism.AppHost
   ```
   Wait for the Aspire dashboard to go green — Keycloak's cold start alone can take up to two
   minutes.
2. **Confirm the guidance articles are live** (they should 200, with real body content):
   ```
   curl -sk https://localhost:44345/transfer-rules
   curl -sk https://localhost:44345/international-transfers
   curl -sk https://localhost:44345/supporting-evidence
   curl -sk https://localhost:44345/professional-standards
   ```
3. **The ttyd/tmux terminal surface.** This demo reuses the exact mechanics documented in
   [`tests/demo/README.md`](../../src/UmbracoPrism.Client/tests/demo/README.md) (steps 3-5: scratch
   directory, stripped `CLAUDECODE`/`CLAUDE_CODE_*` env vars, `tmux new-session -A`, `--tools`
   restricted to just the MCP toolkit, `--permission-mode bypassPermissions`) — read that doc for
   the full rationale. The one difference: **this demo's `claude mcp add` needs the bearer token
   from Act 1 below**, so wire the agent *after* Act 1, not before.

---

## Act 1 — Getting the agent real access

**On screen:** the Umbraco backoffice, then a real terminal — every auth-setup command actually
typed and executed on camera, not narrated over an invisible network call.

**Narration beat (setup):** "We need real backoffice authentication for this — Prism's CMS
Service Blueprint MCP talks to the same live engine a human editor uses, not an open sandbox endpoint — so
an agent needs to log in exactly the way a new team member would. Let's show you exactly how that
works."

Umbraco 17 ships a first-class client-credentials grant on its own Management API token endpoint
— the same OpenIddict flow the backoffice's own login uses, just with `grant_type=client_credentials`
instead of `authorization_code`. Whichever grant mints the token, `CmsServiceBlueprintAuthoringController`
and the MCP endpoint resolve the exact same real `IUser`, with real group memberships — so this is
"the same security as doing it manually," not a parallel scheme.

1. **Log into the backoffice** (`admin@prism.local` / `PrismLocal!12345`). **Narration beat:**
   "Behind that login is a dedicated service-account identity with its own client credentials —
   provisioned once, ahead of time, the same way any integration would be." (The recording tool
   provisions this idempotently via the Management API; a real integrator does the equivalent once
   by hand, in the `admin` group per `Prism:AdminGroups:GroupAliases`'s default.)

2. **In a real terminal**, exchange those credentials for a genuine access token — the exact same
   OAuth client-credentials flow the backoffice's own login uses:
   ```
   curl -sk -X POST https://localhost:44345/umbraco/management/api/v1/security/back-office/token \
     -d grant_type=client_credentials -d client_id=umbraco-back-office-prism-mcp-agent \
     -d client_secret=<the-service-account's-secret> -o mcp-token.json && cat mcp-token.json | jq .
   ```
   **Narration beat:** "That's a real, short-lived access token — same shape as the one your
   browser is holding right now after your own login."

3. **Register it with Claude Code**, in the same terminal:
   ```
   claude mcp add --transport http prism-cms-service-blueprint \
     http://localhost:<testsite-http-port>/prism/service-blueprint-authoring/mcp \
     --header "Authorization: Bearer $(jq -r .access_token mcp-token.json)"
   claude mcp list   # confirm "✔ Connected"
   ```
   (TestSite's real plain-HTTP port is dynamic per Aspire run — read it off the Aspire dashboard's
   "CMS Service Blueprint Authoring MCP" label on the `testsite` row, the same dev-cert-trust reasoning as
   every other Prism MCP demo: self-signed local certs aren't trusted by MCP HTTP clients.)

**Narration beat (recap):** "Done — a real identity, a real token, a real MCP connection, entirely
reproducible from the command line. From here it works exactly like giving a new starter their
login."

---

## Act 2 — Handing over the brief

**On screen:** the ttyd terminal (bigger font, per the shared demo tooling), narration bar
pinned to the top while the agent's response streams.

**Narration beat (setup):** "This is Claude, connected to Prism's Cms Service Blueprint MCP with the token
we just minted — no special access beyond what that token grants."

**Narration beat (intent):** "We're going to hand it one brief, and watch it design, validate,
simulate, and save a real service — branching eligibility, a guidance checklist, document upload,
the works."

**Note on scope baked into the MCP layer, not this brief:** the single-queue constraint ("this host
only ever has one queue, named `cms-visitor`") and the instruction to call `list_queue_capabilities`
first now come from the MCP server's own `ServerInstructions` (`CmsWorkflowBuilderExtensions.
AddPrismCmsWorkflow`), surfaced to the agent automatically at `initialize` time — not from the
human-typed brief. That's deliberate: it's a fact about *this host's* implementation of the
generic toolkit, not something a person briefing an agent should have to know or repeat.

Paste the brief below into the terminal (character-typed on camera, matching the other Prism MCP
demos' pacing):

> You're acting as a service designer with access to Umbraco Prism's CMS Service Blueprint Authoring MCP
> toolkit (server name "prism-cms-service blueprint"). Your task: design and build "Transfer a Professional
> Juggling Licence" — a fictional but structurally real GDS-style public service for someone who
> already holds a professional juggling licence from another juggling authority and wants to
> transfer it to the National Juggling Authority.
>
> Read `service-blueprint-docs://authoring-guide` for the contract shape, and use
> `list_workflows`/`read_workflow` to look at the existing `apply-for-a-juggling-licence`
> definition as your style reference for this host's conventions (it's the same fictional domain,
> a simpler application rather than a transfer) — including how it defaults a field from the
> visitor's real membership data via a service input and a calculated pass-through field; do the
> same here.
>
> Design and save a new definition under the key `transfer-a-juggling-licence` with this shape:
>
> 1. **Eligibility** — three real branching questions (previously performed professionally?
>    licence issued outside the UK? overseas authority recognised by the "International Juggling
>    Accreditation Register"?), each "no"/failing answer routing to its own distinct
>    ineligible-outcome state, not just a validation message.
> 2. **Guidance** — a `guidance-checklist` component with these four items, `required: true` (all
>    four must be acknowledged before continuing):
>    - "Transfer Rules" → `/transfer-rules`
>    - "International Transfers" → `/international-transfers`
>    - "Supporting Evidence" → `/supporting-evidence`
>    - "Professional Standards" → `/professional-standards`
>
>    (These are real, already-published CMS pages — link to them exactly as given, don't invent
>    different URLs.)
> 3. **Existing licence details** — current authority, licence reference, issue date, expiry date,
>    professional category. Default professional category from the visitor's real Juggling Society
>    membership tier, exactly the way `apply-for-a-juggling-licence` defaults its own licence-type
>    field — a visitor who isn't a member simply gets no default, same as that reference service blueprint.
> 4. **Upload evidence** — `file-upload` fields: current licence, proof of identity, proof of
>    address, and a professional portfolio (all `required: true`), plus optional video evidence
>    (`required: false`).
> 5. **Check your answers** — a `summary-list` reviewing everything captured, with `changeStateKey`
>    (or per-row overrides) so the applicant can go back and fix an answer before submitting.
> 6. **Declaration** — three required `boolean` statements (information is accurate; authorise the
>    National Juggling Authority to contact the current licensing body; understands misleading
>    information may cause rejection).
> 7. **Confirmation** — a simple submitted panel. Don't build any post-submission case tracking —
>    that's explicitly out of scope for this version.
>
> Validate with `validate_workflow`, dry-run the eligibility branches and the full happy path with
> `simulate_workflow`, fix anything it flags, then `save_workflow`. Finish with a short summary of
> the design decisions you made.

**Narration beat (note, top-pinned):** "It's checking what this host can actually render before it
drafts anything, reading the existing juggling-licence service blueprint as a style guide, then designing
against the real contract."

**Completion signal — not anything printed in the terminal.** Poll the same backoffice-authenticated
REST surface a human editor session already uses, with the Act 1 bearer token:
```
GET /umbraco/management/api/v1/prism/cms-service-blueprints/transfer-a-juggling-licence
Authorization: Bearer <token>
```
"Done" means the response actually has the new definition's real shape — states long enough to be
the full seven-stage design (not a trivial scaffold), exactly one queue keyed `cms-visitor`, and at
least one state whose components include a `file-upload` and a `guidance-checklist` entry. Real
agent calls doing an iterative validate → fix → re-validate loop have been observed elsewhere in
this repo's demos to take well over half an hour — budget the poll timeout accordingly (the
existing demos use `35 * 60_000`ms with 10s intervals; match `test.setTimeout` to the same order).

**Narration beat (recap, top-pinned):** "Researched nothing — this domain's fictional — but
designed, validated, simulated, and saved, entirely on its own, against real capability
constraints."

---

## Act 3 — Wiring it into the site

**On screen:** Umbraco backoffice.

**Narration beat (setup):** "Here's the back office — this is where a service designer wires a new
service into the real site, no different from any other Prism Cms Service Blueprint page."

1. Log in as `admin@prism.local` / `PrismLocal!12345`.
2. Content → Create (under Home) → **Cms Service Blueprint Page**. Name it "Transfer your existing juggling
   licence".
3. Set **Blueprint Key** = `transfer-a-juggling-licence`.
4. Save and publish. Note the published URL.
5. Settings → Web Navigation → add a link ("Transfer your licence" → the published URL). Save and
   publish.

**Narration beat (recap):** "One page, one key, published — and it's already backed by the
definition the agent designed a minute ago. No restart, no redeploy."

---

## Act 4 — Reviewing what it built

**On screen:** the visual service blueprint editor.

**Narration beat (intent):** "Let's open the editor and actually look at what it designed — this
is the same editor a human would use to adjust anything here by hand."

1. Navigate to `/umbraco/section/prism/workspace/prism-cms-service-blueprint/edit/transfer-a-juggling-licence`
   (a backoffice workspace keyed by the definition's own key, not a standalone route).
2. Click **Fit**, then click into the eligibility question and the upload-evidence states in turn
   — narrate each briefly. (The agent chooses its own exact question wording each run, so don't
   script an exact string to click; match loosely, e.g. on "professionally"/"upload".)
3. **Switch to the Definition tab** (`data-prism-confidence-tab="definition"`) — the real
   `ServiceBlueprint` JSON, CodeMirror-rendered, not just the graph. Point out the
   upload-evidence state's plain `file-upload` components, and the existing-licence-details
   state's professional-category field carrying a `defaultFrom` pointing at a calculated
   `membershipTier` value — the hook that'll fire for real in Act 5.

**Narration beat (recap):** "Branching eligibility, a guidance gate, real document upload, a
member-data hook — every one of those is a normal, editable, inspectable part of the definition,
not a special case."

---

## Act 5 — Running it as a visitor

**On screen:** the public site, first anonymous, then signed in as a real member.

**Narration beat (setup):** "Now let's actually run it — first anonymously, the way most
applicants would."

1. Navigate to the published page via the real nav link.
2. **Eligibility (anonymous)**: answer one branch as ineligible (narrate the dead-end state).
3. **Sign in for real** via Keycloak SSO as `demo@prism.local` / `password` — narrate that this
   proves the same service works identically for an authenticated visitor, and sets up the next
   beat.
4. **Eligibility (signed in)**: answer all three eligibly.
5. **Guidance**: acknowledge all four items.
6. **Existing licence details** — narrate the professional-category field arriving already
   filled in, pulled live from this member's real Juggling Society membership tier, not typed by
   the demo.
7. **Upload evidence** — real file uploads (small PDF/image) for each of the five fields, narrated
   explicitly rather than glossed over.
8. **Check your answers** — narrate the uploaded files' real "View" download link (a genuine
   ownership-checked endpoint, not a mocked filename), then click a "Change" link to prove the
   edit path routes back to exactly the right earlier stage, then continue again.
9. **Declaration**, then **Confirmation**.

**Narration beat (recap):** "Branching eligibility, a real acknowledgement gate, real document
upload, a genuine member-data default, an editable review step — designed and saved by an agent
that only ever spoke MCP, running exactly as a real applicant would experience it."

---

## Closing

**Narration beat:** "A complex, multi-capability public service — branching logic, gated guidance,
document upload — built from one conversation and one access token, then wired into a real site by
a human in minutes. That's the whole loop."

---

## Why this isn't under `docs/walkthroughs/`

`docs/walkthroughs/` (see `.claude/skills/walkthroughs-as-executable-specs/SKILL.md`) is a strict,
CI-gated convention: every walkthrough there has a Playwright spec that runs on **every PR** and
writes the embedded screenshots. That convention doesn't fit a recording whose Act 2 depends on a
real agent call that can take 30-45 minutes — it isn't something CI can reasonably gate on, and
Prism's other recorded MCP demo (`garden-waste-demo.spec.ts`) already establishes the pattern this
one follows instead: a `tests/demo/*.spec.ts` recording tool, explicitly excluded from CI, that
writes a narrated video to `demo-footage/` rather than CI-checked screenshots. This doc is that
pattern's first written companion — the storyboard the recording follows, not a CI-gated
behavioural spec.

## Executable companion

`src/UmbracoPrism.Client/tests/demo/licence-transfer-demo.spec.ts` (once written) is this
walkthrough's recording tool, following the exact structure documented above — same
`support/narration.ts`/`support/human-interactions.ts` helpers, same one-shared-page technique,
same `tests/demo/README.md` ttyd/tmux operator setup as `garden-waste-demo.spec.ts`. Not a CI
test — run with:
```
TTYD_PASSWORD=<password> npm run demo:record:licence-transfer
```
