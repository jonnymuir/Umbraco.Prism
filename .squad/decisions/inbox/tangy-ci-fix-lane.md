# Decision: CI Lane Recovery Patterns (2026-05-23)

**Author:** Tangy (Tester)  
**Commit:** `25a72d5`

## Context

Three CI lanes were broken by the role-first swim-lane refactor (`d5e76ca0`). This doc records the
testing patterns that proved fragile and the conventions that should replace them.

## Decision 1: CSS semitransparency is a WCAG AA hazard

`rgba(255,255,255,0.85)` composited on `#1d70b8` yields ≈4.19:1 contrast — below the WCAG AA
4.5:1 threshold. AXE detects this violation even when the element is not explicitly selected in a
story, because the Storybook test runner does not guarantee a full DOM reset between stories in a
shared browser tab.

**Convention:** Use fully opaque foreground colours in component CSS. Avoid alpha-channel white text
on brand-blue backgrounds. If reduced opacity is needed for aesthetic reasons, calculate the
composited hex value and verify contrast ≥ 4.5:1 before committing.

## Decision 2: `window.fetch` stubs in Storybook must use identity-guarded cleanup

The `stubFetchFor` helper in shell stories was restoring `window.fetch` via a MutationObserver
callback. Because the callback fires as a microtask, it runs after the next story has already
installed its own stub — silently overwriting it with the real (un-stubbed) fetch.

**Convention:** When globally patching `window.fetch` in Storybook:
1. Capture the stub function reference (`const stubbedFetch = async (...) => { ... }`)
2. Assign it to `window.fetch`
3. In cleanup, guard: `if (window.fetch === stubbedFetch) { window.fetch = originalFetch; }`

This prevents a late-firing cleanup from clobbering a newer story's stub.

## Decision 3: `aria-current` values must match component output exactly

Playwright selectors like `[aria-current="true"]` silently time-out when the component emits
`aria-current="location"` (per ARIA spec for navigation landmarks). The walkthrough spec should
always be derived from reading the component source, not assumed.

**Convention:** Before writing a Playwright selector for `aria-current`, grep the component source
for the actual emitted value. The `prism-workflow-outline` component uses `"location"`.
