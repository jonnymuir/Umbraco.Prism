# Skill: CodeMirror 6 in Lit Shadow DOM — Wheel Scrolling & Find UX

**Author:** Isabelle  
**Date:** 2026-05-31  
**Confidence:** Medium (confirmed by production regression + fix)  
**Applies To:** CodeMirror 6 editors embedded in Lit Web Components

---

## The Problem

When embedding CodeMirror 6 inside a Lit web component's Shadow DOM, three common UX failures occur:

1. **Mouse wheel scrolling doesn't work** — Wheel events never reach the CodeMirror `.cm-scroller` element, leaving authors unable to scroll the editor content with their mouse.

2. **No scrollbar appears** — The editor grows to fit all content instead of being constrained by the parent container.

3. **Cmd/Ctrl+F opens browser Find instead of in-editor search** — The browser's native Find searches the entire page (including the editor shell), not just the document content.

---

## Root Causes

### 1. Wheel Event Interception

If the **host element** (the custom element wrapping CodeMirror) or any ancestor has `overflow: hidden`, wheel events are absorbed at that boundary and never propagate to the `.cm-scroller` inside the Shadow DOM.

**Broken CSS:**
```css
prism-definition-editor {
  overflow: hidden;   /* ❌ blocks wheel events */
}
```

CodeMirror's `.cm-scroller { overflow: auto }` can't work if the parent intercepts the event.

### 2. Unbounded Height

If the host element or `.cm-editor` / `.cm-scroller` are not properly constrained by a flex layout chain, they will grow to fit all content instead of establishing an overflow container. This happens when:
- The host is `display: block` with `height: 100%` (doesn't work in flex)
- Any flex item in the chain has `min-height: 240px` (prevents shrinking)
- Parent containers use `height: 100%` instead of `flex: 1`
- Tab panels or slot wrappers force `display: block` on flex children

**Critical:** Every element in the flex chain from the viewport-constrained root down to `.cm-scroller` must have `flex: 1` with `min-height: 0` to allow the editor to shrink and establish a bounded scrollable region.

### 3. Missing Search Extension

CodeMirror 6 ships Find functionality in `@codemirror/search`, but it's opt-in. Without wiring `search()` and `searchKeymap`, Cmd/Ctrl+F triggers the browser's native Find.

---

## The Pattern

### 1. Establish a Complete Flex Layout Chain

**Critical:** The host element and every parent in the chain must participate in flex layout with `min-height: 0` to allow the editor to shrink and create a bounded scrollable region.

**Working CSS (inside the Shadow DOM — Lit component styles):**
```css
:host {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  /* no overflow property — let wheel events pass through */
}

.editor-host {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

/* CodeMirror's editor and scroller must also use flex to be constrained */
.cm-editor {
  flex: 1 1 0% !important;  /* !important to override CM defaults */
  min-height: 0 !important;
  display: flex !important;
  flex-direction: column !important;
}

.cm-scroller {
  flex: 1 1 0% !important;
  min-height: 0 !important;
  overflow: auto !important;
}
```

**Parent styling (in prism-workflow-editor.ts or equivalent):**
```css
.definition-editor-frame prism-definition-editor {
  flex: 1;
  min-height: 0;  /* NOT min-height: 240px — that prevents flex constraint */
  border: 1px solid #b1b4b6;
  border-radius: 4px;
  /* overflow: hidden removed — was blocking wheel events */
}
```

**Tab panel styling (if using tabs):**
```css
.tab-panel-container {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.tab-panel {
  flex: 1;
  min-height: 0;
  display: none;
}

.tab-panel-active {
  display: flex;  /* NOT display: block */
  flex-direction: column;
}

::slotted(*) {
  flex: 1;
  min-height: 0;
  display: flex !important;
  flex-direction: column !important;
}
```

### 2. Avoid `overflow: hidden` on the Host Element

The host element should **not** block wheel events. Remove `overflow: hidden` or use `overflow: visible` / `overflow: clip` (which doesn't intercept events).

### 3. Wire in `@codemirror/search` for In-Editor Find

**Install the dependency:**
```bash
npm install @codemirror/search --save
```

**Import and configure:**
```ts
import { search, searchKeymap } from '@codemirror/search';
import { keymap } from '@codemirror/view';
import { defaultKeymap, historyKeymap } from '@codemirror/commands';

const state = EditorState.create({
  doc,
  extensions: [
    // ... other extensions (lineNumbers, highlightActiveLine, etc.)
    search({ top: true }),  // 👈 search panel at top (default is bottom)
    keymap.of([
      ...defaultKeymap,
      ...historyKeymap,
      ...searchKeymap,      // 👈 Cmd/Ctrl+F → open search, Esc → close
      indentWithTab,
    ]),
    // ... rest of extensions
  ],
});
```

**Why `{ top: true }`?**  
Placing the search panel at the top keeps it visible when the editor scrolls and aligns with GDS design proximity (close to the user's focus).

**Keymap order matters:**  
`searchKeymap` must come after `defaultKeymap` and `historyKeymap` to avoid conflicts. CodeMirror's keymap priority is first-wins, so putting `searchKeymap` at the end ensures Cmd/Ctrl+F opens the in-editor panel, not the browser Find.

### 4. Verify Focus & Keyboard Accessibility

The CodeMirror editor should be keyboard-reachable. If your host component has a `focus()` method (for external focus management), forward it to the CodeMirror view:

```ts
/** Imperative method used by host tests / focus management. */
focus(options?: FocusOptions) {
  if (this._view) {
    this._view.focus();
    return;
  }
  super.focus(options);
}
```

---

## Testing the Fix

### Wheel Scrolling & Bounded Height

**Test that content actually overflows and is scrollable:**
```ts
test('Mouse wheel scrolling container is properly configured', async ({ page }) => {
  // ... open editor ...

  // Verify parent does not have overflow:hidden
  const parentOverflow = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor') as HTMLElement | null;
    const style = window.getComputedStyle(def!);
    return style.overflow;
  });
  expect(parentOverflow).not.toBe('hidden');

  // Verify scroller has overflow: auto
  const { scrollerOverflow, isActuallyScrollable, clientHeight, scrollHeight } = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    const scroller = def?.shadowRoot?.querySelector('.cm-scroller') as HTMLElement | null;
    const style = window.getComputedStyle(scroller!);
    return {
      scrollerOverflow: style.overflowY,
      isActuallyScrollable: scroller!.scrollHeight > scroller!.clientHeight,
      clientHeight: scroller!.clientHeight,
      scrollHeight: scroller!.scrollHeight,
    };
  });
  expect(scrollerOverflow).toBe('auto');
  
  // CRITICAL: Content must actually overflow to create a scrollbar
  expect(isActuallyScrollable).toBe(true);
  expect(scrollHeight).toBeGreaterThan(clientHeight);
  
  // Verify scrolling actually works
  const scrollWorked = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    const scroller = def?.shadowRoot?.querySelector('.cm-scroller') as HTMLElement | null;
    const before = scroller!.scrollTop;
    scroller!.scrollTop = 100;
    const after = scroller!.scrollTop;
    return after > before;
  });
  expect(scrollWorked).toBe(true);
});
```

### Find Panel

**Test that Cmd/Ctrl+F opens the search panel:**
```ts
test('Cmd/Ctrl+F opens the CodeMirror search panel', async ({ page }) => {
  // ... open editor and focus ...

  // Search panel should not be visible initially
  const panelBefore = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    return !!def?.shadowRoot?.querySelector('.cm-search');
  });
  expect(panelBefore).toBe(false);

  // Press Cmd/Ctrl+F
  const isMac = await page.evaluate(() => navigator.platform.toLowerCase().includes('mac'));
  if (isMac) {
    await page.keyboard.press('Meta+f');
  } else {
    await page.keyboard.press('Control+f');
  }
  await page.waitForTimeout(100);

  // Search panel should appear
  const panelAfter = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    return !!def?.shadowRoot?.querySelector('.cm-search');
  });
  expect(panelAfter).toBe(true);
});
```

---

## Checklist for CodeMirror 6 in Shadow DOM

- [ ] No `overflow: hidden` on the host element or its parent
- [ ] Host element is `display: flex` with `flex-direction: column`
- [ ] `.cm-editor` and `.cm-scroller` have `flex: 1 1 0%` with `min-height: 0`
- [ ] Every parent in the flex chain has `flex: 1` with `min-height: 0` (no `min-height: 240px`)
- [ ] Tab panels (if used) are `display: flex` when active, not `display: block`
- [ ] `::slotted(*)` rules force `display: flex` on slot content
- [ ] `@codemirror/search` installed as a dependency
- [ ] `search({ top: true })` extension added
- [ ] `searchKeymap` in keymap array (after `defaultKeymap`)
- [ ] `focus()` method forwards to `view.focus()`
- [ ] Playwright tests verify `scrollHeight > clientHeight` (not just overflow styles)
- [ ] Playwright tests verify programmatic scrolling works

---

## Example: Definition Editor Component

**Files:**
- `src/workflow-editor/prism-definition-editor.ts` — Lit host component
- `src/workflow-editor/prism-definition-editor-codemirror.ts` — CodeMirror setup (lazy-loaded)
- `src/workflow-editor/prism-confidence-tabs.ts` — Tab panel container
- `tests/workflow-editor/definition-editor-ux.spec.ts` — UX tests

**Key learnings:**
- Wheel events and Shadow DOM require the entire ancestor chain to allow event propagation. Even one `overflow: hidden` blocks it.
- **Height constraints require a complete flex layout chain.** Using `height: 100%` or `min-height: 240px` on flex items breaks the constraint—every element from viewport to `.cm-scroller` must have `flex: 1` with `min-height: 0`.
- The search panel is keyboard-dismissable (Esc) and ARIA-live-announced — no custom announcements needed.
- Tests must assert `scrollHeight > clientHeight`, not just check `overflow: auto`—the latter passes green even when the layout is broken and no scrollbar appears.

---

## Related Skills

- `shadow-dom-focus` — Focus seeding patterns for modal web components
- `inspector-create-and-focus` — RAF-based focus-after-create pattern
