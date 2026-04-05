# Skill: Shadow DOM Focus Management in Modal Web Components

**Author:** Isabelle  
**Date:** 2026-04-08  
**Applies To:** Lit Web Components used as modals / dialogs in Umbraco backoffice

---

## The Problem

When a Lit web component is rendered as a modal dialog (e.g., inside Umbraco's modal manager), the component's shadow root contains all interactive elements. Two common failures occur:

1. **Keyboard focus is not seeded** — Tab key does not reach buttons because no element inside the shadow root holds focus when the dialog opens. Without a focus-seeded entry point, the browser's focus trap (if any) has no anchor.

2. **Third-party layout components swallow slots** — If a component like `uui-dialog-layout` is used as a wrapper and action buttons are placed in `slot="headline"`, the shadow DOM of `uui-dialog-layout` renders that slot inside its own scrollable container. This can prevent buttons from being reachable via Tab in some browsers, and makes the headline scroll away with the content.

---

## The Pattern

### 1. Seed Focus on First Render

In `firstUpdated()`, use `requestAnimationFrame` to defer the focus call until after the browser has painted. Direct `focus()` calls in `connectedCallback` often fire before the shadow root is ready.

```ts
protected firstUpdated() {
  // Seed focus so keyboard users enter the focus trap at the primary CTA.
  // requestAnimationFrame defers until after the first paint.
  requestAnimationFrame(() => {
    this.shadowRoot?.querySelector<HTMLButtonElement>('.primary-btn')?.focus();
  });
}
```

Also add `autofocus` as a belt-and-suspenders fallback on the primary button in the template:

```html
<button class="primary-btn" autofocus @click=${this._handleSubmit}>
  Save
</button>
```

### 2. Avoid Slotting Action Buttons into Third-Party Layout Components

**Don't do this:**
```html
<uui-dialog-layout>
  <div slot="headline">
    <button>Save</button>   <!-- inside shadow of uui-dialog-layout — may not be keyboard-reachable -->
  </div>
</uui-dialog-layout>
```

**Do this instead** — own the full layout in `:host`:
```ts
// CSS
:host {
  display: flex;
  flex-direction: column;
  overflow: hidden;        /* host does NOT scroll */
}
.dialog-headline {
  flex-shrink: 0;          /* never scrolls away */
  padding: 9px 12px;
  background: var(--uui-color-surface);
}
uui-tab-group {
  flex-shrink: 0;          /* always visible */
}
.container {
  flex: 1;
  overflow-y: auto;        /* ONLY the content scrolls */
  min-height: 0;           /* required for flex children with overflow */
}
```

```html
<!-- template: headline, tabs, container are direct shadow-DOM children -->
<div class="dialog-headline">
  <button class="primary-btn" autofocus>Save</button>
  <button>Cancel</button>
</div>
<uui-tab-group>...</uui-tab-group>
<div class="container">...form content...</div>
```

### 3. Host Element ARIA

Set `role`, `aria-modal`, and `aria-label` on the host element in `connectedCallback`. Keep `aria-label` in sync when data changes (e.g., create vs. edit mode):

```ts
connectedCallback() {
  super.connectedCallback();
  this.setAttribute('role', 'dialog');
  this.setAttribute('aria-modal', 'true');
  this.setAttribute('aria-label', this.data?.item ? 'Edit Item' : 'Create Item');
}

protected updated(changedProperties: Map<string, unknown>) {
  super.updated(changedProperties);
  if (changedProperties.has('data')) {
    this.setAttribute('aria-label', this.data?.item ? 'Edit Item' : 'Create Item');
  }
}
```

> ⚠️ `aria-labelledby` referencing an `id` inside the shadow root does **not** work cross-tree. Use `aria-label` on the host, or use the ARIAMixin / ElementInternals API (Chromium 81+) for shadow-crossing label associations.

---

## Why `min-height: 0` Matters

Flex children with `overflow-y: auto` do **not** scroll unless their height is constrained. By default a flex child's minimum height is its content height (`auto`). Setting `min-height: 0` overrides this, allowing the browser to clip the child and enable scroll.

```css
.container {
  flex: 1;
  overflow-y: auto;
  min-height: 0;   /* <-- without this, the container expands to fit content and never scrolls */
}
```

---

## Checklist for Modal Web Components

- [ ] `role="dialog"`, `aria-modal="true"`, `aria-label` on `:host`
- [ ] `firstUpdated()` seeds focus with `requestAnimationFrame`
- [ ] Primary action button has `autofocus`
- [ ] Action buttons are **not** slotted into a third-party layout component
- [ ] `:host` uses `display:flex; flex-direction:column; overflow:hidden`
- [ ] Only the content `.container` has `overflow-y:auto; min-height:0`
- [ ] All interactive elements are reachable via Tab
- [ ] Escape key closes the dialog (`keydown` → `reject()`)
- [ ] `:focus-visible` ring on every button, including custom toggles
- [ ] `@media (prefers-reduced-motion:reduce)` gates all transitions
