# Prism Branding (Tenant-Specific CSS Variables)

## Overview
Prism will allow tenant-specific branding by overriding CSS custom properties (variables) at runtime. Each tenant can supply values for a known set of variables discovered from the site’s CSS files. These overrides are applied per tenant to affect visual styling without changing shared CSS assets.

This design covers:
- Discovery of brandable CSS variables.
- Data model and storage for tenant overrides.
- Runtime application of overrides.
- Backoffice UI for managing overrides.

---

## Goals
- Per-tenant branding via CSS custom property overrides.
- Discover variables from existing CSS files automatically.
- Provide a clear backoffice experience grouped by CSS file.
- Allow “custom/other” overrides for variables not discovered.
- Safe runtime injection that does not require CSS rebuilds.

## Non-Goals
- Editing non-variable CSS rules.
- Theme compilation or CSS pre-processing.
- Global (non-tenant) branding management.

---

## Discovery: CSS Variable Scanning
### Sources
- Scan CSS files served by the site (local project CSS files). Initially, scanning focuses on:
  - UmbracoPrism.Client/src/index.css
  - UmbracoPrism.Client/src/**/*.css
  - Any additional CSS files configured in Prism settings (future).

### Parsing Rules
- Extract CSS custom property definitions in the form:
  - `:root { --color-primary: #123; }`
  - `.some-class { --brand-accent: var(--color-primary); }`
- Include variables regardless of selector, but categorize by source file and keep original order.
- Ignore commented-out definitions.

### Output Model
Each discovered variable:
- `name` (e.g., `--color-primary`)
- `defaultValue` (string as found)
- `sourceFile` (relative path)
- `selector` (optional, if we decide to store)

### Tabs Mapping
- One backoffice tab per CSS source file that contains variable definitions.
- Variables not found in any file but present in stored overrides show under a special tab:
  - **Branding, Other**

---

## Data Model
### Tenant Branding Overrides
- Store per-tenant overrides as a JSON object:
  ```json
  {
    "--color-primary": "#0d6efd",
    "--brand-accent": "rgb(255, 0, 0)"
  }
  ```

### Persistence
- Store in the Prism tenant data store.
- A single field for overrides (JSON) to simplify read/write.

---

## Runtime Application
### Strategy
- At request time, determine tenant context.
- Load tenant overrides.
- Emit a style block with `:root` overrides into the rendered page.

Example injection:
```html
<style id="prism-tenant-branding">
  :root {
    --color-primary: #0d6efd;
    --brand-accent: rgb(255, 0, 0);
  }
</style>
```

### Where to Inject
- Preferred: in server-side rendered layout or middleware response injection.
- If using backoffice/dashboard, ensure injection applies to front-end site, not just backoffice.
- Injection only when overrides exist.

---

## Backoffice Experience
### Dashboard Tab(s)
- Add a **Branding** tab to Prism dashboard.
- If multiple CSS files contain variables, create a tab per CSS file.
- Each tab lists variables with:
  - Variable name (read-only)
  - Default value (read-only)
  - Override value (editable)

### Branding: Other Tab
- A tab that lists overrides that do not match any discovered variable.
- Allows editing/removing those overrides.
- Optional: add new variable by name (future enhancement).

### Validation Rules
- Store values as strings (no strict validation), but basic guardrails:
  - Disallow empty variable names.
  - Preserve original value if input is cleared (or treat empty as “remove override”).

---

## Implementation Plan (High-Level)
1. **Scanner**: Implement CSS variable discovery in server or build pipeline.
2. **Storage**: Extend tenant model to include branding overrides JSON.
3. **Runtime**: Middleware/service to inject overrides in responses.
4. **Backoffice**: Add dashboard UI with tabs and list editing.

---

## Open Questions
- Should variables be scoped to selectors other than `:root`?
- How should overrides be merged when the same variable exists in multiple files?
- Should we allow per-user preview in backoffice?

---

## Future Enhancements
- UI color picker for common color variables.
- Variable grouping by prefix (e.g., `--color-*`).
- Import/export branding presets.
