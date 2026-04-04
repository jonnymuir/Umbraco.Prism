# Copilot Coding Agent — Squad Instructions

You are working on a project that uses **Squad**, an AI team framework. When picking up issues autonomously, follow these guidelines.

## Team Context

Before starting work on any issue:

1. Read `.squad/team.md` for the team roster, member roles, and your capability profile.
2. Read `.squad/routing.md` for work routing rules.
3. If the issue has a `squad:{member}` label, read that member's charter at `.squad/agents/{member}/charter.md` to understand their domain expertise and coding style — work in their voice.

## Capability Self-Check

Before starting work, check your capability profile in `.squad/team.md` under the **Coding Agent → Capabilities** section.

- **🟢 Good fit** — proceed autonomously.
- **🟡 Needs review** — proceed, but note in the PR description that a squad member should review.
- **🔴 Not suitable** — do NOT start work. Instead, comment on the issue:
  ```
  🤖 This issue doesn't match my capability profile (reason: {why}). Suggesting reassignment to a squad member.
  ```

## Branch Naming

Use the squad branch convention:
```
squad/{issue-number}-{kebab-case-slug}
```
Example: `squad/42-fix-login-validation`

## PR Guidelines

When opening a PR:
- Reference the issue: `Closes #{issue-number}`
- If the issue had a `squad:{member}` label, mention the member: `Working as {member} ({role})`
- If this is a 🟡 needs-review task, add to the PR description: `⚠️ This task was flagged as "needs review" — please have a squad member review before merging.`
- Follow any project conventions in `.squad/decisions.md`

## Test Hygiene

Tests are **behavioural contracts** — they describe what the product should do, not how it does it. A failing test means a desired behaviour is broken. A passing test on wrong code means the test is poorly written.

### Running the full test suite

Before starting any code change, establish a **passing baseline** across all suites:

```bash
# Backend unit tests
dotnet test src/UmbracoPrism.Core.Tests/

# Playwright E2E (requires Storybook — starts automatically)
cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test --reporter=line
```

After making changes, run both suites again. **All tests must still pass.** If the baseline was already red, fix those failures as part of your work — they represent regressions from a prior change.

### Writing tests

- **Test desired behaviour, not implementation details.** Ask: "Would a product owner care if this changed?" If a test would break purely because you renamed a CSS class or restructured a DOM element — without changing what the user sees or can do — it is testing implementation, not behaviour. Rewrite it.
- **Use semantic selectors.** Prefer `data-variable`, `role`, `label`, `aria-*`, and visible text over positional selectors like `:first-of-type` or `:nth-child`.
- **Wait for async state.** If a component loads data asynchronously, wait for the loaded state (e.g., `await expect(header).toBeVisible()`) before querying DOM values.
- **Name tests as behaviours.** "Mobile override value is shown for each branding variable" is better than "test branding table row 1 cell 4".

Never leave tests in a worse state than you found them. Never silence a failing test without understanding and documenting why it fails.

## Decisions

If you make a decision that affects other team members, write it to:
```
.squad/decisions/inbox/copilot-{brief-slug}.md
```
The Scribe will merge it into the shared decisions file.
