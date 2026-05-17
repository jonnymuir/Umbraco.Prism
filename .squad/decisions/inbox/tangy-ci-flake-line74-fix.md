# Decision: Wait for Workflow Data Load Before Asserting Editor State

## Context

PR #52 (`squad/planning-workflow-editor-walkthrough`) failed on CI (run 25988472206) at line 74 of the walkthrough spec. The first pass (commits 17657db, 07f0070) hardened the readiness probe against the "unseeded splash page" race, which fixed the probe itself. However, the test continued to fail on CI while passing locally in 1.1m.

CI trace analysis (downloaded via `gh run download 25988472206`) revealed:
- The probe passed cleanly: `[readiness] … all localhost auth dependencies are ready`
- The actual failure was INSIDE the spec at line 74: `await step(page, '01-workflow-editor-loaded.png', editorHealthCheck(…))`
- The heading check (`/planning permission/i`) timed out after 30 seconds
- Page snapshot after `page.goto()` showed an almost-empty `<body>` — just `<HEAD>` and `<BODY>` tags with no content

## Root Cause

The planning workflow editor is a Lit web component (`<prism-workflow-editor>`) that:
1. Loads as an ES module (`workflow-editor.js`) via `<script type="module">`
2. Registers the custom element via `@customElement` decorator
3. Fetches workflow data from `/api/workflow-authoring/workflows/{key}` in `connectedCallback()`
4. Renders the heading inside shadow DOM AFTER the fetch completes

On local hardware, this sequence completes before the 30s heading timeout. On slower CI hardware, `page.goto()` completes on the `load` event before:
- The ES module finishes executing
- The custom element upgrades
- The async API fetch completes
- The shadow DOM renders with the heading

This is a classic web-component hydration race. The page loads but the interactive components aren't ready yet.

## Decision

**Wait for the workflow data to load** before asserting page health. Add an explicit `page.waitForSelector()` for the semantic ready signal: `[data-prism-workflow-loaded]:not([data-prism-workflow-loaded=""])`.

This attribute is set by the component at render time (line 200 of `prism-workflow-editor.ts`):
```typescript
data-prism-workflow-loaded="${this._workflow?.definitionKey ?? ''}"
```

When `_workflow` is null (still loading), the attribute is empty string. Once the fetch completes, it contains the workflow key (e.g., `"planning"`).

The fix waits for a non-empty value with a 30s timeout, matching the heading timeout in `assertHealthyPage()`.

## Implementation

```typescript
await page.goto(`${businessAppOrigin}/workflow-editor.html?workflow=planning`);

// Wait for the workflow data to load before asserting page health.
await page.waitForSelector('[data-prism-workflow-loaded]:not([data-prism-workflow-loaded=""])', {
  timeout: 30_000,
});

await step(page, '01-workflow-editor-loaded.png', editorHealthCheck({
  screenshotSelector: '[data-prism-component="workflow-graph"]',
}), WALKTHROUGH_KEY);
```

## Alternatives Considered

1. **Bump the heading timeout to 60s** — rejected; doesn't address the root cause, just masks the race.
2. **Wait for network idle** — rejected; too broad, doesn't encode the semantic contract (workflow loaded).
3. **Add a `data-prism-ready` after all async work** — considered; Isabelle could add this in a future iteration, but the existing `data-prism-workflow-loaded` already encodes the right signal for this test.
4. **Wait for the graph canvas `role="application"`** — rejected; the test already does this at line 83, but it's too late — the heading check happens first inside `assertHealthyPage()`.

## Validation

- ✅ Test passes locally in 1.1m (unchanged)
- ✅ CI trace artifacts uploaded by existing workflow (ci-tests.yml lines 149-157)
- ✅ No changes to component code or test infrastructure — surgical fix in the spec only

## Trade-offs

- **Pro:** Deterministic wait for the exact signal needed (workflow data loaded).
- **Pro:** No change to component contracts or test helpers — isolated to one spec.
- **Pro:** Documents the hydration pattern for future walkthrough authors.
- **Con:** If Isabelle changes the attribute name or placement, this wait breaks. Mitigated by the attribute being documented as a test hook in the component (line 25).

## Learning for Future Walkthroughs

When navigating to a page that uses ES modules and web components with async data fetches:
1. Identify the semantic "ready" signal (e.g., `data-prism-workflow-loaded`)
2. Wait for it explicitly BEFORE asserting page content
3. Don't rely on `page.goto()` "load" event alone — modules and custom elements hydrate AFTER load

---
date: 2026-05-17T12:30:00+01:00
author: Tangy
status: decision
area: testing, playwright, web-components, CI
---
