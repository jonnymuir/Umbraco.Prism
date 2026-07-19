# Walkthrough — The Prism Design System

An exploration of how Prism's design system works end-to-end: from GDS-aligned Lit web components in Storybook, through the branding annotation system, to CSS variables that update live on the tenant's frontend — all without a rebuild.

> **Prerequisites:** Stack running. See [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup).

---

## Overview

Prism's design system has three layers:

| Layer | Where | What it does |
|---|---|---|
| **GDS Components** | `src/UmbracoPrism.Client/` | Lit web components implementing the GOV.UK Design System pattern library |
| **Branding annotation** | `wwwroot/branding/*.css` | CSS `@property` declarations + `@prism` comments that drive the tenant editor UI |
| **Token pipeline** | Backoffice → database → HTTP → `:root` | CSS variables set per-tenant and served as a thin override stylesheet |

This walkthrough covers all three layers and shows you how to extend the system with a custom component.

For the full branding annotation reference — annotation syntax, type inference rules, file structure, and API — see **[docs/branding-design-system.md](../branding-design-system.md)**. This walkthrough links to that document rather than duplicating it.

---

## Part 1: GDS Components in Storybook

### Step 1: Start Storybook

```bash
cd src/UmbracoPrism.Client
npm run storybook
```

Storybook starts at `http://localhost:6006`.

<!-- TODO: capture 01-storybook-home.png via automated Playwright or manual: navigate to localhost:6006 -->
<!-- pending capture -->

### Step 2: Explore the Component Library

In the left sidebar, browse the component stories. You'll find components organized by category:

- **Backoffice** — Dashboard, tenant modal, branding editor, biometric settings
- **Mobile** — Mobile navigation, branding inheritance previews

<!-- TODO: capture 02-storybook-sidebar.png via Storybook sidebar navigation -->
<!-- pending capture -->

Click on **Prism Dashboard** to open its story.

💡 **What's happening:** Each Storybook story renders a standalone instance of a Lit web component. The components use Umbraco's backoffice element APIs (`UmbElementMixin`, `umbHttpClient`) but Storybook mocks these dependencies through story decorators, so you can work on the UI without running the full Umbraco stack.

### Step 3: Inspect the Tenant Modal Story

1. Click **Prism Create Tenant Modal** in the sidebar.

2. You see the tenant creation form rendered in isolation.

3. Use the **Controls** panel at the bottom to change props and see how the component responds.

<!-- TODO: capture 03-storybook-tenant-modal.png via Storybook: Prism Create Tenant Modal story -->
<!-- pending capture -->

4. ✅ **What you can do in Storybook:**
   - Inspect every state of every component (default, filled, error, loading, etc.) without navigating a full workflow.
   - Use **Accessibility** add-on to check ARIA labels and colour contrast against WCAG 2.1 AA.
   - Use **Actions** add-on to inspect custom events emitted by components.
   - Write new stories to document edge cases before implementing them.

### How the Components Are Built

Components live in `src/UmbracoPrism.Client/src/`:

```
src/
  backoffice/
    prism-dashboard.ts              ← Dashboard section
    prism-create-tenant-modal.ts    ← Tenant creation modal
    prism-biometric-settings.ts     ← Biometric auth UI
    prism-biometric-register.ts     ← Registration flow
    push-notifications.ts           ← Push notification helpers
  mobile/
    prism-mobile-nav.ts             ← Mobile navigation web component
```

Each `.ts` file is a `@customElement`-decorated `LitElement`. The accompanying `.stories.ts` file defines Storybook stories.

💡 **What's happening:** Components are standard Web Components (Custom Elements v1). Because they are framework-agnostic, they work in Umbraco's Angular-based backoffice, in Razor views in the TestSite, and in Ionic/Capacitor mobile shells — no wrappers or adapters needed.

---

## Part 2: The Branding Token Pipeline

### How Tokens Flow

```
Backoffice editor (saves a tenant's brand colours)
  ↓
POST /umbraco/api/prism/tenants/{id}/branding
  { "--prism-primary": "#e63946", "--prism-font-body": "\"Roboto\", sans-serif" }
  ↓
PrismBrandingService stores values in prismTenantBranding table
  ↓
GET /branding/tenant/{tenantId}/overrides.css  (served by Prism middleware)
  Returns:
    :root {
      --prism-primary: #e63946;
      --prism-font-body: "Roboto", sans-serif;
    }
  ↓
<link rel="stylesheet" href="/branding/tenant/{tenantId}/overrides.css"> in <head>
  ↓
CSS variables cascade to every element, including web components that use var(--prism-*)
```

### Step 4: Open the Branding Editor in the Backoffice

1. Log into the Umbraco backoffice at `https://localhost:44345/umbraco`.
   - Username: `admin@prism.local`, Password: `PrismLocal!12345`

2. Navigate to **Settings → Prism Dashboard**.

3. Click on the `localhost` tenant.

4. Click the **Branding** tab.

<!-- TODO: capture 04-branding-editor.png via backoffice: Prism Dashboard → localhost → Branding tab -->
<!-- pending capture -->

   You see sections: **Brand Colours**, **Typography**, **Imagery**, **Components**, **Layout**.

5. 💡 **What's happening:** The branding editor fetches `GET /umbraco/api/prism/branding/metadata`, which returns the parsed metadata from all annotated CSS files in `wwwroot/branding/`. Each annotated variable becomes a typed form field — color pickers for `<color>` syntax, number inputs for `<length>`, etc.

   See [Branding Design System](../branding-design-system.md#the-annotation-format) for the full `@property` + `@prism` annotation syntax.

### Step 5: Change a Brand Colour

1. Click **Brand Colours**.

2. Find **Primary Brand Colour** (the `--prism-primary` variable).

3. Click the color picker and select a new colour — for example, `#e63946` (a vivid red).

4. Click **Save**.

5. Open a new browser tab and navigate to `https://localhost:44345` (the TestSite frontend).

6. The buttons, links, and highlighted elements now use your new colour. No rebuild. No deploy.

<!-- TODO: capture 05-branding-updated-frontend.png via TestSite after changing primary colour -->
<!-- pending capture -->

💡 **What's happening:** The override stylesheet is served per-tenant with a short cache TTL (or cache-busted on save). The `:root` rule overrides the default value set in `wwwroot/branding/prism-colors.css`, and because CSS custom properties cascade naturally through the shadow DOM via `inherits: true` on each `@property`, web components pick up the new value without re-render.

---

## Part 3: How Web Components Consume Tokens

Lit web components reference CSS variables in their static styles using `var()`:

```typescript
// src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.ts

static styles = css`
  :host {
    display: block;
    background: var(--prism-nav-bg, rgba(255, 255, 255, 0.95));
    height: var(--prism-nav-height, 56px);
    font-family: var(--prism-font-body, "Inter", sans-serif);
    border-top: 2px solid var(--prism-primary, #4f46e5);
  }
`;
```

The second argument to `var()` is a fallback — used when the variable is not defined (e.g., when the component is rendered outside a Prism tenant context, such as in Storybook).

✅ **What you can do:** Because each variable has an `initial-value` declared in its `@property` block, browsers always have a typed fallback. This means components are correctly styled even if a tenant has not customised a specific variable.

---

## Part 4: Extending the System — Adding a Custom Component

This section walks through adding a new web component and wiring it into the design system.

### Scenario

You want a `<prism-status-badge>` component that displays a coloured status pill (e.g., "Pending", "Approved", "Rejected") using the tenant's brand colours.

### Step 1: Add a Branding Token

In `src/UmbracoPrism.Client/public/branding/prism-components.css`, add the `@property` declaration and annotated variable:

```css
@property --prism-badge-bg {
  syntax: '<color>';
  inherits: true;
  initial-value: #f3f4f6;
}

:root {
  /* @prism section: Components | label: Badge Background | description: Background colour for status badges and chips */
  --prism-badge-bg: #f3f4f6;
}
```

The next time the branding editor fetches `/umbraco/api/prism/branding/metadata` (or after the 1-hour cache expires), this variable will appear in the **Components** section of the tenant editor.

### Step 2: Create the Component

Create `src/UmbracoPrism.Client/src/backoffice/prism-status-badge.ts`:

```typescript
import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';

@customElement('prism-status-badge')
export class PrismStatusBadge extends LitElement {
  @property({ type: String }) status = 'pending';

  static styles = css`
    :host {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.875rem;
      font-weight: 600;
      font-family: var(--prism-font-body, "Inter", sans-serif);
      background: var(--prism-badge-bg, #f3f4f6);
      color: var(--prism-text, #0f172a);
    }
  `;

  render() {
    return html`<slot>${this.status}</slot>`;
  }
}
```

### Step 3: Write a Storybook Story

Create `src/UmbracoPrism.Client/src/backoffice/prism-status-badge.stories.ts`:

```typescript
import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './prism-status-badge.js';

const meta: Meta = {
  title: 'Backoffice/Prism Status Badge',
  component: 'prism-status-badge',
  args: { status: 'Pending' },
};
export default meta;

export const Default: StoryObj = {
  render: (args) => html`<prism-status-badge status="${args.status}"></prism-status-badge>`,
};

export const Approved: StoryObj = {
  render: () => html`<prism-status-badge status="Approved"></prism-status-badge>`,
};
```

### Step 4: Verify in Storybook

```bash
cd src/UmbracoPrism.Client
npm run storybook
```

Navigate to **Backoffice → Prism Status Badge** — you'll see your component rendered with the default branding. Change `--prism-badge-bg` in the branding editor and reload Storybook to see it update.

### Step 5: Type-Check

```bash
cd src/UmbracoPrism.Client
npx tsc --noEmit -p tsconfig.json
```

No errors should be reported for your new files.

---

## Summary

| Concept | Where to look |
|---|---|
| GDS components | `src/UmbracoPrism.Client/src/` |
| Component stories | `src/UmbracoPrism.Client/src/**/*.stories.ts` |
| Branding CSS files | `src/UmbracoPrism.Client/public/branding/*.css` |
| Annotation syntax reference | [docs/branding-design-system.md](../branding-design-system.md) |
| Token API | `GET /umbraco/api/prism/branding/metadata` |
| Creating a tenant + branding | [Creating a Tenant walkthrough](creating-a-tenant.md) |

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`design-system.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/design-system.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
