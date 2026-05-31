# Skill: Inspector Affordances — Create-and-Focus a New Sub-Item

**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Established:** 2026-05-31  
**Applies to:** Lit `LitElement` web component inspectors that need to append a new sub-item (route, action, field, etc.) and immediately focus a field inside it.

---

## The Pattern

### Problem

An inspector panel renders a list of sub-items (e.g. gateway routes). A user clicks "+ Add item" — the item is created, the model is updated, a re-render is triggered. But:
- The new item isn't in the DOM when the click handler runs (Lit hasn't re-rendered yet)
- If the creation also changes which sub-view is shown (e.g. switching from stage view to gateway view), the target element isn't even in the *right* shadow subtree until after the update cycle

### Solution

1. **Store the new item's stable ID in a plain private field** (not `@state()`) before emitting the update event:
   ```typescript
   private _newlyAddedItemId: string | null = null;
   
   private _handleAddItem() {
     const itemId = generateStableId();
     this._newlyAddedItemId = itemId;
     // ... create item, emit event that may cause view-switch ...
   }
   ```
   Using a plain field (not `@state()`) avoids a spurious extra re-render when it is cleared.

2. **Check in `updated()`** — fires after every Lit update cycle, regardless of cause:
   ```typescript
   protected updated(changed: Map<string, unknown>) {
     // ... existing property-change handlers ...
     
     if (this._newlyAddedItemId) {
       const itemId = this._newlyAddedItemId;
       this._newlyAddedItemId = null;  // clear before scheduling — safe because we captured the value
       requestAnimationFrame(() => {
         const container = this.shadowRoot?.querySelector<HTMLElement>(`[data-item-id="${itemId}"]`);
         const focusTarget = container?.querySelector<HTMLElement>('[data-item-first-field]');
         if (container) container.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
         if (focusTarget) focusTarget.focus();
       });
     }
   }
   ```
   The `requestAnimationFrame` is necessary because Lit's `updated()` fires synchronously after DOM mutation but before the browser has painted — `scrollIntoView` with `smooth` needs a post-paint RAF to work correctly inside shadow DOM.

3. **Add a stable data attribute to each sub-item container** so the RAF query can find it:
   ```html
   <li data-item-id="${item.id}">
     <select data-item-first-field ...>...</select>
   </li>
   ```

### Accessibility requirements for the button

- `aria-label` should include the context (e.g. "Add route from {stage name}") so screen readers get meaningful text beyond just "+ Add route"
- Button must be keyboard-activatable (standard `<button type="button">` — do not use a div/span)
- Reuse the nearest existing aria-live region (look for `role="status"` or `aria-live="polite"`) to announce creation; don't add a duplicate live region
- Announce via the live region *before or synchronously with* the focus move, so the announcement reads before the newly-focused field's label

### Inline validation for newly-created items

Newly-created items often have empty required fields. Show an inline warning immediately using the existing `field-error` / `field-control-error` CSS classes and the `aria-invalid` + `aria-describedby` pattern:

```html
<select
  class="field-control ${fieldEmpty ? 'field-control-error' : ''}"
  aria-invalid=${String(fieldEmpty)}
  aria-describedby=${fieldEmpty ? warningId : ''}
  ...
>
  <option value="" ?selected=${fieldEmpty} disabled>Choose a value…</option>
  ...
</select>
${fieldEmpty
  ? html`<span id="${warningId}" class="field-error" data-field-warning>Choose a value</span>`
  : nothing}
```

Do **not** block saving — the server validator is the hard gate.

---

## Reference Implementation

- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
  - `_handleAddRoute()` — creation handler
  - `updated()` — focus management
  - `_renderGatewayOutgoingRoutes()` — button placement, `data-prism-route-id` on `<li>`
  - `_renderRouteEditor()` — inline validation

- `src/UmbracoPrism.Client/tests/workflow-editor/add-route-affordance.spec.ts`
  - Playwright specs for: create-from-no-gateway, append-to-existing-gateway, focus landing, inline-warning lifecycle, keyboard-only flow
