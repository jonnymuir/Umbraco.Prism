# Documentation & Onboarding Review — Mabel

**Date:** 2026-05-01T08:57:29+01:00
**Reviewer:** Mabel (Technical Writer)
**Scope:** `README.md`, `docs/`, `docs/walkthroughs/` (all 9), SKILL.md (walkthroughs-as-executable-specs)

---

## Verdict

The documentation is well-engineered for one persona — the developer installing the NuGet package — and almost invisible to everyone else. The README opens with a Codespaces button and a `dotnet add package` command, which is correct for its primary audience, but the docs surface has never been interrogated through the lens of the other five personas Jonny named. Content creators, editors, business users, and service designers all crash through the same developer door or find no door at all. The walkthrough portfolio sits closer to "implementation notes for developers" than "showing the system at work" — even the four fully-automated end-user walkthroughs pivot, mid-step, into OIDC token exchange and polymorphic JSON schema. The docs are honest about what the system does technically, but aspirational about completeness: five skeletal walkthroughs are listed in the README table as if they are finished. Two internal design docs (`docs/design/`) sit in the public navigation surface with no indication they are contributor-facing. Rams would mark this a qualified pass for innovativeness and usefulness-to-developers, and a clear failure for understandability and unobtrusiveness for every other reader.

---

## Persona-by-Persona Entry-Door Audit

### Developer

**Door exists.** `README.md` opens with `dotnet add package UmbracoPrism`, a Codespaces badge, a local setup checklist, a `How It Works` section, an architecture table, and a documentation index. `docs/umbraco-setup.md` and `docs/guides/workflow-setup.md` provide installation and configuration depth. The authoring walkthrough (`docs/walkthroughs/authoring-a-workflow.md`) is thorough: fluent builder API, polymorphic JSON model, type discriminator table — real engineering content for the right audience. The R5 spec ↔ markdown back-reference policy (`.squad/skills/walkthroughs-as-executable-specs/SKILL.md`) provides rigorous process backstop. **Verdict: 🟢 well served.**

### Content Creator

**No door.** A content creator — someone using the Umbraco backoffice to publish pages, manage workflow triggers, or configure notifications — is never addressed by name in the docs. The README's "Add Your First Tenant" section briefly mentions the backoffice, but it is written for an operator, not someone whose primary role is content. There is no guide explaining what the Prism-extended backoffice looks like for a day-to-day content editor, what new document types (`homePage`, `memberDashboard`, `workflowPage`) mean for them, or how content decisions affect tenant rendering. **Verdict: 🔴 no door.**

### Designer

**Partial door, wrong key.** `docs/branding-design-system.md` is the closest thing to a designer-facing document — it explains the `@property` / `@prism` annotation system, CSS variable pipeline, and the tenant editor UI. But the document assumes the designer can write CSS with `@property` declarations. There is no entry point for a non-coding designer asking "what does the branding editor look like?", "what can I change without touching code?", or "how do I translate a brand guideline into a Prism theme?" The design system walkthrough (`docs/walkthroughs/design-system.md`) is skeletal — every screenshot is `<!-- pending capture -->` — so it cannot guide anyone through the actual UI. **Verdict: 🟡 door exists but locked for non-coders.**

### Editor

**No door.** An editor maintaining published content — editing pages, updating workflow descriptions, managing Umbraco nodes — has nothing. No "Umbraco editor's guide," no explanation of how the Prism document types differ from standard Umbraco nodes, no indication of which settings are safe for them to change versus which are operator/admin territory. The gap between "Umbraco Setup Guide" (developer-authored, installation-focused) and a daily editorial workflow is entirely undocumented. **Verdict: 🔴 no door.**

### Business User

**No door.** A business user evaluating Prism for procurement, or a project sponsor needing to understand what it delivers, has nowhere to land. The README opens with a code block. The "What You Get" section (`README.md` lines 94–152) is the closest thing to a plain-English capability summary, but it is immediately followed by implementation commands and JSON snippets. There is no capability overview, no "who is this for" section, and no case for value without navigating past developer tooling. **Verdict: 🔴 no door.**

### Service Designer

**Partial door, inverted.** Service designers need to understand the user journey — what citizens experience, where friction sits, how forms are structured. The four fully-automated walkthroughs (`community-enquiry.md`, `payment-demo.md`, `planning-notification.md`, `information-request.md`) notionally show user flows. But each pivots, mid-walkthrough, into implementation detail: `planning-notification.md` step 2 spends six bullet points explaining OIDC token exchange when a service designer needs to see the journey, not the plumbing. The `💡 What's happening` annotations are excellent for developers; they are noise — or worse, anxiety — for a service designer trying to map the citizen experience. The walkthroughs are showing developers the implementation, wearing user-journey clothing. **Verdict: 🟡 accidental door, wrong narrative angle.**

---

## Walkthrough Portfolio Assessment

**Four fully automated (screenshots live):**

| Walkthrough | Persona served | Dual-audience problem |
|---|---|---|
| `community-enquiry.md` | End user → developer | Minimal. Leanest of the four; JSON schema explanation is appropriate footnote |
| `payment-demo.md` | End user → developer | Moderate. Check-answers section good; Stripe integration explanation is developer noise for a service designer |
| `planning-notification.md` | End user → developer | High. Most comprehensive but OIDC explanation in step 2 is a developer aside in a user-facing walkthrough. Labeled "most comprehensive end-user walkthrough" in the README index |
| `information-request.md` | End user | Low. Cleanest persona alignment. Stays on the citizen experience throughout |

**Five skeletal (screenshots missing or TODO):**

| Walkthrough | Status | Problem |
|---|---|---|
| `authoring-a-workflow.md` | Complete narrative, no screenshots | Explicitly developer-facing. Well written. R5 back-reference footer absent |
| `creating-a-tenant.md` | `<!-- TODO: capture -->` throughout | Skeletal. Zero screenshots. Listed in README table as if complete |
| `design-system.md` | `<!-- pending capture -->` throughout | Skeletal. Zero screenshots. Cannot guide a designer |
| `push-notifications.md` | Full narrative, architecture diagram | Operational/technical. ASCII flow diagram present — charter requires Mermaid |
| `building-a-mobile-app.md` | Narrative exists, screenshots absent | Developer-facing. Prerequisite assumes completed workflow walkthrough |

The R5 back-reference policy (spec ↔ markdown link) is not consistently applied across all walkthroughs.

---

## Docs That Don't Earn Their Keep (Rams #10)

**`docs/design/` in the public README table.** Six internal design documents (`notifications-architecture.md`, `notifications-backend.md`, `notifications-mobile.md`, `notifications-umbraco-demo.md`, `workflow-forms-engine*.md`, etc.) sit in the README documentation table alongside user-facing guides. These are contributor/architecture decision records. Surfacing them in the public navigation creates noise for every persona who is not a platform contributor. They belong under `.squad/` or with a clear "contributor reference" label — not in the main docs index.

**`docs/archive/`** — referenced in the history as having been reviewed but still part of the tree. Archive material with no explicit deprecation or removal path is dead weight.

**README's "Setup & Development" section (lines 296–380)** — contains Storybook test commands, localhost auth Playwright test lane details, and CI workflow explanations. These are contributor concerns, not user-facing setup. A developer installing Prism does not need to know how to run `npm run test-storybook:all` on first read.

---

## Rams Scorecard

| Principle | Verdict | Note |
|---|---|---|
| 1. Innovative | ✅ | The system is genuinely novel; docs surface the innovations clearly for developers |
| 2. Makes a product useful | ⚠️ | Useful path exists for developers. No utility communicated to 5 of 6 personas |
| 3. Aesthetic | ⚠️ | README is well structured but too long; mixes audience levels in same scroll |
| 4. Understandable (Rams #4) | ❌ | OIDC, JWT, polymorphic discriminators in walkthroughs labeled for end users. Design docs in public table. No role-based navigation |
| 5. Unobtrusive (Rams #5) | ❌ | Internal design docs visible in main nav. README's contributor-only sections interrupt the user journey |
| 6. Honest | ⚠️ | Technically accurate. But five skeletal walkthroughs are listed without indication of incompleteness |
| 7. Long-lasting | ✅ | Mermaid diagram standard in charter. Markdown with relative paths. Stable structure |
| 8. Thorough to last detail | ⚠️ | R5 back-reference policy is rigorous; not consistently applied. `push-notifications.md` has ASCII diagram violating charter standard |
| 9. Environmentally friendly | N/A | Not applicable to documentation |
| 10. Involves as little design as possible | ❌ | Overdesigned for one audience; underdesigned for five. Duplicate navigation entries between README table and `docs/walkthroughs/README.md` |

---

## Three Improvements (Prioritized)

### 1. Add a "Start here by role" section to `README.md`

**File:** `README.md` — insert after the "Try it Now" block, before "What You Get"

Five of six personas currently have no entry door. A short role-routing block — six bullet points, one sentence each, with links — costs nothing to write and eliminates the "every persona crashes through the developer door" failure. Example:

- **Editors and content creators:** [How Prism works in the backoffice →](docs/umbraco-setup.md#editors)
- **Designers:** [Branding and the design system →](docs/branding-design-system.md)
- **Service designers:** [See the citizen journeys →](docs/walkthroughs/)
- **Business stakeholders:** [What Prism delivers →](#what-you-get)
- **Developers:** [Install and configure →](#quick-start)
- **Contributors:** [Architecture and decisions →](.squad/decisions.md)

This is Rams #4 (understandable) and #5 (unobtrusive) applied with minimum intervention.

### 2. Strip internal design docs from the public `README.md` documentation table

**File:** `README.md` lines 248–254 — remove the "Design Docs" subsection from the table; leave a single link: `→ Architecture reference (for contributors): docs/design/`

The six `docs/design/` entries in the public docs table are contributor-facing architecture decisions. A developer installing Prism, a designer using the branding editor, or a service designer reviewing citizen journeys does not need `notifications-backend.md` in their navigation. Moving them reduces noise for all five non-developer personas and makes the table scannable. This is Rams #10 (as little design as possible) and #5 (unobtrusive).

### 3. Mark skeletal walkthroughs honestly in `docs/walkthroughs/README.md`

**File:** `docs/walkthroughs/README.md` — add status badges (`🚧 In progress` / `✅ Complete`) next to `creating-a-tenant.md`, `design-system.md`, `building-a-mobile-app.md`, `push-notifications.md`

Listing five incomplete walkthroughs in the index without qualification is the only honesty failure in the docs. It is also an implicit promise to designers (the design system walkthrough is the only thing resembling a designer entry point) that the system cannot currently fulfill. Marking them honestly costs one character per line. The alternative — capturing the screenshots — is preferable but requires the full Aspire stack + Playwright run, which is not always available. Honesty now; completeness later. This is Rams #6 (honest).
