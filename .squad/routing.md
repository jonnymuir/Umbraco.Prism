# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture & scope | Tom Nook | Design decisions, what to build next, refactoring strategy, trade-offs |
| Code review & quality | Tom Nook | Review PRs, enforce patterns, suggest improvements |
| Web Components & UI | Isabelle | Build/enhance components, Storybook stories, styling, accessibility, UI logic |
| C# services & APIs | Blathers | Services, middleware, authentication, database, business logic, controllers |
| Testing strategy & QA | Tangy | Playwright tests, edge cases, test coverage, quality gate, accessibility validation |
| XML documentation and API docs | Celeste | XML style docs, public API summaries, param/returns/exception docs, readability standards |
| Public docs & README | Mabel | README.md, /docs/ content, marketplace listing, changelogs, onboarding guides |
| Release / versioning | Mabel | "cut a release", "bump version", "what's changed since last release", CHANGELOG.md, semver |
| Security, tenant isolation, CIA | Copper | Confidentiality/integrity/availability analysis, auth hardening, cross-tenant leak prevention |
| Mobile native & Capacitor plugins | Kicks | Biometric auth, iOS Keychain/Android Keystore, Capacitor plugin integration, native entitlements |
| Umbraco CMS patterns & test site | Brewster | Document types, route-hijacking controllers, Umbraco templates, MockBackOffice demo, v17-idiomatic patterns |
| Async issue work (bugs, small features) | @copilot 🤖 | Well-defined tasks matching capability profile (see Lead triage) |
| Session logging & decisions | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Tom Nook (Lead) |
| `squad:{name}` | Pick up issue and complete the work | Named member (Isabelle, Blathers, Tangy, Celeste, Mabel, or Copper) |
| `squad:copilot` | Assign to @copilot for autonomous work (if auto-assign enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Tom Nook (Lead)** triages it — analyzing content, evaluating @copilot's capability profile (see below), assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** Tom Nook checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, Tom Nook routes to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" — untriaged issues waiting for Tom Nook's review.

### Lead Triage Guidance for @copilot

When triaging, Tom Nook should ask:

1. **Is this well-defined?** Clear title, reproduction steps or acceptance criteria, bounded scope → likely 🟢
2. **Does it follow existing patterns?** Adding a test, fixing a known bug, updating a dependency → likely 🟢
3. **Does it need design judgment?** Architecture, API design, UX decisions → likely 🔴
4. **Is it security-sensitive?** Auth, encryption, access control → always 🔴
5. **Is it medium complexity with specs?** Feature with clear requirements, refactoring with tests → likely 🟡

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn Tangy to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. Tom Nook handles all `squad` (base label) triage.
8. **@copilot routing** — when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.

---

## Work Item Routing — Feature-Branch + PR Workflow

**Effective:** 2026-04-30 (immediately for all new work)

**Policy:** One unit of work per feature branch. Whole team collaborates on the same branch. CI gates all merges to main.

### Branch Strategy

- **Default:** Every feature, bug fix, security finding, or substantial change gets its own feature branch. Do NOT commit directly to `main`.
- **Exception:** Trivial `.squad/`-only bookkeeping with **NO code changes** (e.g., routine session logging, metadata updates) may go direct to `main` at the coordinator's discretion. When in doubt, branch.

### Branch Naming Convention

- `squad/{issue-number}-{kebab-slug}` — for tracked issues (e.g., `squad/42-login-validation`)
- `feat/{feature-name}` — for features (e.g., `feat/notifications-backend`)
- `fix/{bug-name}` — for bug fixes (e.g., `fix/cors-rate-limiting`)
- `docs/{topic}` — for documentation (e.g., `docs/workflow-sanitization`)

### Team Collaboration

- Multiple agents may push commits to the same feature branch for a single work item.
- **Do NOT open separate PRs** for sub-tasks of the same item. One item → one branch → one PR.
- Handoffs happen within the branch (e.g., Blathers builds the wire-up, Copper implements the real logic, both commit to `squad/sec-003-sanitization`).

### Pull Request Gate

1. PR opened against `main` from the feature branch.
2. All GitHub Actions, linters, tests, and builds must pass (green).
3. Coordinator (or assigned reviewer) merges **only after all checks pass**.
4. Squash-on-merge is the default; multiple commits are preserved if important for history.

### Scribe Bookkeeping

- Scribe's `.squad/decisions/`, `.squad/sessions/`, `.squad/agents/*/history.md` commits land on the same feature branch as the work.
- Decisions, logs, and history flow into `main` together with the PR, keeping the ledger in sync with code changes.

### Rationale

**Why?** The previous "work on main" policy bypassed CI gates. Even in a single-author repo, direct commits to main can regress silently. Feature branches + PR workflow restores CI as a regression prevention gate and enables team review before merge.

---
