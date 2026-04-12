# Decisions Archive

Historical decisions older than 30 days. Kept for reference.

---

## 📌 2025-07-22: uui-input Accessibility Label Pattern (Isabelle)

Every `uui-input` element must have a `label` attribute, regardless of whether a visible `<label>` element already wraps or precedes it. The UUI component library requires the attribute on the element itself for its internal accessibility wiring.

- **Dynamic fields** (`_renderDynamicField`): use `label=${variable.label}` (in scope from `BrandingMetadata` variable object).
- **Table loop inputs** (`_renderStaticBrandingContent`): use template literals for uniqueness, e.g. `"${variable.name} (desktop override)"`.

Visible labels do not satisfy the UUI component's internal label requirement. Omitting the `label` attribute causes console noise and screen-reader issues.

---

## 📌 2025-07-15: Test Philosophy — Behavioural Contracts (Tangy)

Tests are **behavioural contracts** — they express what the product should *do* from a user/product-owner perspective, not *how* it does it. Tests must remain green after any refactor that preserves observable behaviour.

**Key principles:**

1. **Prefer semantic selectors over structural selectors.** `data-variable="--color-primary"` expresses intent. `uui-table-row:first-of-type` expresses position and breaks if rows are reordered.

2. **Wait for visible state before querying shadow DOM.** Always add `await expect(...).toBeVisible()` before any `evaluate` that depends on async-rendered content.

3. **Follow named-ID patterns** for stable assertions (`#mobile-app-name`, `#mobile-app-id`) with real semantic values.

Additional fixes made alongside: `_fetchBrandingMetadata` fixed with `Promise.race` + 500ms timeout so fetch fires in test environments; duplicate-ID bug fixed by extracting `_renderStaticBrandingContent` from `_renderStaticBrandingTab`.

---
