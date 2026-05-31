# Skill: CodeMirror 6 in Lit Shadow DOM — Wheel Scrolling & Find UX

**Author:** Isabelle  
**Date:** 2026-05-31  
**Applies To:** CodeMirror 6 editors embedded in Lit Web Components

---

## The Problem

When embedding CodeMirror 6 inside a Lit web component's Shadow DOM, two common UX failures occur:

1. **Mouse wheel scrolling doesn't work** — Wheel events never reach the CodeMirror `.cm-scroller` element, leaving authors unable to scroll the editor content with their mouse.

2. **Cmd/Ctrl+F opens browser Find instead of in-editor search** — The browser's native Find searches the entire page (including the editor shell), not just the document content.

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

### 2. Missing Search Extension

CodeMirror 6 ships Find functionality in `@codemirror/search`, but it's opt-in. Without wiring `search()` and `searchKeymap`, Cmd/Ctrl+F triggers the browser's native Find.

---

## The Pattern

### 1. Avoid `overflow: hidden` on the Host Element

The host element should **not** block wheel events. Remove `overflow: hidden` or use `overflow: visible` / `overflow: clip` (which doesn't intercept events).

**Working CSS:**
```css
/* Parent styling in prism-workflow-editor.ts */
.definition-editor-frame prism-definition-editor {
  flex: 1;
  min-height: 240px;
  border: 1px solid #b1b4b6;
  border-radius: 4px;
  /* overflow: hidden removed — was blocking wheel events */
}
```

**Inside the Shadow DOM (Lit component styles):**
```css
:host {
  display: block;
  height: 100%;
  min-height: 0;
  /* no overflow property — let wheel events pass through */
}

.editor-host {
  height: 100%;
  min-height: 0;
}

/* CodeMirror's scroller handles overflow internally */
.editor-host .cm-scroller {
  overflow: auto;  /* ✅ this works if parent doesn't block events */
}
```

### 2. Wire in `@codemirror/search` for In-Editor Find

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

### 3. Verify Focus & Keyboard Accessibility

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

### Wheel Scrolling

**Test that the parent does not block events:**
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
  const scrollerOverflow = await page.evaluate(() => {
    const host = document.querySelector('prism-workflow-editor');
    const def = host?.shadowRoot?.querySelector('prism-definition-editor');
    const scroller = def?.shadowRoot?.querySelector('.cm-scroller') as HTMLElement | null;
    const style = window.getComputedStyle(scroller!);
    return style.overflowY;
  });
  expect(scrollerOverflow).toBe('auto');
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
- [ ] `.cm-scroller` has `overflow: auto` (default in CodeMirror)
- [ ] `@codemirror/search` installed as a dependency
- [ ] `search({ top: true })` extension added
- [ ] `searchKeymap` in keymap array (after `defaultKeymap`)
- [ ] `focus()` method forwards to `view.focus()`
- [ ] Playwright tests cover wheel scrolling setup + Find open/close

---

## Example: Definition Editor Component

**Files:**
- `src/workflow-editor/prism-definition-editor.ts` — Lit host component
- `src/workflow-editor/prism-definition-editor-codemirror.ts` — CodeMirror setup (lazy-loaded)
- `tests/workflow-editor/definition-editor-ux.spec.ts` — UX tests

**Key learnings:**
- Wheel events and Shadow DOM require the entire ancestor chain to allow event propagation. Even one `overflow: hidden` blocks it.
- The search panel is keyboard-dismissable (Esc) and ARIA-live-announced — no custom announcements needed.
- If the editor is inside a flex container, use `min-height: 0` on the host and `.editor-host` to let the scroller constrain properly.

---

## Related Skills

- `shadow-dom-focus` — Focus seeding patterns for modal web components
- `inspector-create-and-focus` — RAF-based focus-after-create pattern
