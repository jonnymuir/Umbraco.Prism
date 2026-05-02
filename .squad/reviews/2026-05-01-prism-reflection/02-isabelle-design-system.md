# Design system review — Isabelle
_2026-05-01T08:57:29+01:00_

## Verdict

The design system is a competent, honest first draft — but it is two systems pretending to be one. GDS Frontend is loaded as an opaque pre-minified blob and runs in its own lane; the `--prism-*` CSS custom property layer governs the shell (header, layout, branding). They coexist without collision but they do not compose. The branding infrastructure — `@property` typed tokens, `@prism` annotations, the `PrismBrandingMetadataService`, and middleware injection — is genuinely clever and architecturally sound. What it lacks is completeness: a hardcoded hover colour, a misprefixed token, triplicated body declarations, and no Storybook coverage of any GDS component mean the Rams test of "thorough down to the last detail" is failed. The bones are right. The finish is rough.

---

## ITCSS audit

### Does the layered architecture exist?

`src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml` (line 37) carries the comment *"ITCSS layer order"* over four `<link>` tags: `base.css → layout.css → components.css → utilities.css`. That is the intent. The reality:

| ITCSS layer | Status | Evidence |
|---|---|---|
| **Settings** | ⚠️ Inverted | `@property` declarations live in `/branding/prism-*.css`, loaded *after* site CSS — logically last, not first |
| **Tools** | ✅ N/A | No Sass; pure CSS — nothing to map |
| **Generic / Reset** | ⚠️ Delegated | GDS ships its own internal reset inside the minified blob; `base.css` is 16 lines that partially re-declare `body` |
| **Elements** | ⚠️ Split | `base.css` + `prism-typography.css` both set `body {}` — three total declarations of body across three files |
| **Objects** | ❌ Missing | `.container`, `.header`, `.footer` live inside `prism-layout.css` mixed with structural rules; no layout-agnostic object layer |
| **Components** | ⚠️ Split | `components.css` (TestSite, 31 KB) + `prism-components.css` (branding, partially) + GDS inside the minified blob |
| **Utilities** | ❌ Vestigial | `utilities.css` is 15 lines: two debug rules and one mobile web-component toggle |

The branding files also introduce a second, parallel cascade: `prism-layout.css` re-declares `--prism-nav-height: 56px` in `:root` and `prism-components.css` re-declares the same value at line 137 — a silent duplication.

### Where it breaks down

The sharpest break is the Settings layer being the last loaded. `prism-branding.css` is a manifest that imports colours → typography → layout → imagery → components → forms. This cascade defines the `--prism-*` tokens *after* the site CSS that consumes them. It works because CSS custom properties resolve at use-time, not parse-time — but it inverts the conceptual ITCSS flow and makes the loading order feel accidental rather than principled.

### Does GDS Frontend need to follow ITCSS? Argue both sides.

**Against:** GDS is a complete, self-contained design system. It has its own internal token layer (`$govuk-brand-colour`, etc.), its own reset, and its own specificity contract. Imposing ITCSS on it is cargo-culting — GDS's team didn't design for that taxonomy and wrapping it would require forking the source. Treating it as a black-box dependency, loaded first, is correct.

**For:** The cascade order still matters even for third-party systems. Loading GDS first with no `@layer` declaration means its specificity is invisible — any Prism rule that wants to override a GDS default must either out-specificity it (fragile) or come later in the file order (implicit). A named CSS cascade layer (`@layer govuk`) would make the intent explicit and safe.

**Position (Isabelle's take):** GDS does *not* need to follow ITCSS internally. But the integration seam — where GDS ends and Prism begins — should be declared explicitly via CSS cascade layers. One line in `Master.cshtml` before the first `<link>` tag would do it:
```css
@layer govuk, prism-base, prism-layout, prism-components, prism-branding;
```
Without it, the system works by convention, not contract.

---

## Branding walkthrough — "If Jonny wanted to apply his own brand right now…"

### Step by step

**Step 1 — Colours** (`src/UmbracoPrism.TestSite/wwwroot/branding/prism-colors.css`)

Jonny opens the file and sees `--prism-primary: #1d70b8` (GDS blue). He changes it to his brand blue. Primary buttons (`var(--prism-button-bg)` in `prism-components.css:118`) pick it up immediately ✅. Links (`var(--prism-link)`) pick it up ✅.

**Step 2 — First friction: hover state** (`src/UmbracoPrism.TestSite/wwwroot/branding/prism-components.css`, line 120)

```css
--prism-button-hover: #003078;
```

This value is *hardcoded* — not derived from `--prism-primary`. Jonny's brand blue gets the right idle state but the wrong hover. He either never notices (bad) or goes hunting across six files to find this token (friction).

**Step 3 — The misfired token** (`src/UmbracoPrism.TestSite/wwwroot/branding/prism-colors.css`, line 88)

```css
--bg-offset: #f3f2f1;
```

Every other token is `--prism-*`. This one isn't. It won't appear in a find-replace of `--prism-` prefixed variables. The `PrismBrandingMetadataService` parses `@prism section:` annotations — `--bg-offset` *does* carry the annotation, but the asymmetric name is a distraction.

**Step 4 — Typography** (`src/UmbracoPrism.TestSite/wwwroot/branding/prism-typography.css`)

Jonny changes `--prism-font-body` from `"GDS Transport"` to `"Inter"`. He must load the font face himself — there is no guidance in the file on where to add a `<link rel="preconnect">` or `@font-face`. The token works for the shell. But GDS form components (`.govuk-input`, `.govuk-label`, etc.) resolve their font from GDS's internal stylesheet — they do not consume `--prism-font-body`. The workflow form fields still render in GDS Transport after his change.

**Step 5 — GDS form colours**

`govuk-button` uses a hardcoded `#00703c` background (green) in govuk-frontend v5's minified CSS. There is no `--govuk-button-colour` custom property exposed. Jonny cannot change GDS button colours by overriding `--prism-primary`. To rebrand GDS buttons he would need to ship a custom govuk-frontend build — which is currently not documented anywhere.

**Step 6 — Focus ring**

`--prism-focus: #ffdd00` is the GDS accessibility yellow. It appears in the branding UI with no guard rail. Jonny *could* change it to his brand colour, destroying WCAG 2.2 focus visibility. The token needs a stronger in-file signal that this is an accessibility constraint, not a brand decision.

**Step 7 — Tenant-level overrides via middleware**

The sophisticated path: `PrismBrandingMiddleware.cs` injects `<style id="prism-branding-overrides">` into HTML at request time from `PrismTenant.BrandingCssDeclarations`. This allows per-tenant theming without touching files. `PrismBrandingMetadataService` parses the `@prism` annotations to build a schema. But there is no visible UI in the backoffice dashboard web component for editing these per-token values for a tenant. The backend plumbing is there; the editor-facing UI is not.

### Where the friction is

| Friction point | File | Severity |
|---|---|---|
| `--prism-button-hover` not derived from primary | `prism-components.css:120` | High |
| GDS form typography not consumed by `--prism-font-body` | `govuk-frontend.min.css` (opaque) | High |
| GDS button colour not themeable | `govuk-frontend.min.css` (opaque) | Medium |
| `--bg-offset` missing `--prism-` prefix | `prism-colors.css:88` | Low |
| `--prism-focus` not guarded as accessibility token | `prism-colors.css:104` | Medium |
| Body declared in three files | `base.css`, `prism-typography.css`, `prism-layout.css` | Low |
| No font-face loading guidance | `prism-typography.css` | Medium |

### What it should look like

A single `prism-tokens.css` file with two sections: primitive tokens (`--prism-color-brand-100` etc.) and semantic aliases (`--prism-primary: var(--prism-color-brand-500)`). Hover states derived with `color-mix()`. Typography tokens that apply to both shell and GDS via a thin GDS override block. One body declaration. A comment block flagging accessibility-constrained tokens.

---

## Storybook & component ergonomics (creator lens)

**What exists:** Five web components have stories — biometric register, biometric settings, create-tenant modal, dashboard, mobile nav. The mobile nav stories are genuinely well-written: light/dark/brand colour variants, play() assertions, accessibility smoke-tests. The `BrandColour` story shows exactly how `--prism-primary` cascades into the shadow DOM — a creator can understand the theming contract in thirty seconds.

**What is missing:** Every GDS component (accordion, inset text, notification banner, warning text, summary list, task list) and every PrismField partial (input, radio, checkbox, date, select, textarea) has zero Storybook coverage. These are the components that workflow authors and content designers *actually configure* in Umbraco. They are invisible to designers and developer consumers of the system.

**The Storybook preview** (`src/UmbracoPrism.Client/.storybook/preview.ts`) loads UUI CSS and the backoffice `index.css` but not the `--prism-*` branding tokens. If a developer adds a story for a new component that uses `--prism-primary`, it will render the hardcoded fallback value, not the Prism theme. The preview needs `prism-colors.css` and `prism-typography.css` imported.

**For a designer:** Storybook is currently a developer-only artifact. A designer theming the system would need to know which CSS files to edit, which tokens map to which components, and what the GDS constraints are — none of which is discoverable through Storybook.

**For an editor selecting workflow components:** There is no Storybook story to preview what `notification-banner`, `inset-text`, or `task-list` will look like before configuring them in the CMS.

---

## Rams scorecard

| # | Principle | Score | Evidence |
|---|---|---|---|
| 1 | **Innovative** | ⚠️ | `@property` typed tokens + middleware injection is genuinely novel. GDS integration is "load it first and hope" — no bridge layer. |
| 2 | **Useful** | ✅ | Workflow field rendering is functional and WCAG 2.2 AA compliant. GDS patterns are proven for citizen services. |
| 3 | **Aesthetic** | ⚠️ | Default theme is GDS verbatim — nothing distinctive until tokens are overridden. The system looks like GDS, not Prism. |
| 4 | **Understandable** | ⚠️ | `--bg-offset` vs `--prism-*` is an outlier; `@property` + `:root {}` dual declaration is redundant noise; hover not derived from primary is a silent inconsistency. |
| 5 | **Unobtrusive** | ✅ | GDS components are deliberately quiet. No gratuitous decoration in the shell. Shadow DOM encapsulation is clean. |
| 6 | **Honest** | ⚠️ | `@prism` annotations imply a working branding UI; the metadata service and middleware exist; the editor-facing UI to use them does not yet. |
| 7 | **Long-lasting** | ✅ | CSS custom properties, GDS, and LitElement are stable long-term bets. No framework lock-in in the shell layer. |
| 8 | **Thorough** | ❌ | `--prism-button-hover` hardcoded; `--prism-nav-height` declared twice; `--bg-offset` misprefixed; body declared in three files; GDS font not bridged. |
| 9 | **Environmentally friendly** | ✅ | No bloat. Shadow DOM prevents style leakage. Minimal JS footprint on the member-facing shell. |
| 10 | **As little design as possible** | ⚠️ | `@property` + `:root {}` dual declaration doubles every token. Seven branding files could be three. GDS could be declared behind a CSS layer in one line. |

---

## Three concrete improvements (prioritised)

### 1 — Derive `--prism-button-hover` from `--prism-primary` (High impact, low risk)

**File:** `src/UmbracoPrism.TestSite/wwwroot/branding/prism-components.css`, line 120

Replace:
```css
--prism-button-hover: #003078;
```
With:
```css
--prism-button-hover: color-mix(in srgb, var(--prism-primary) 80%, #000 20%);
```

This makes hover states cascade automatically for any brand colour change — which is precisely what the branding middleware injection is designed to enable. Without this fix, a tenant setting `--prism-primary: #c0392b` gets red buttons with a GDS-blue hover state.

### 2 — Declare CSS cascade layers at the `<head>` entry point (Medium impact, zero-risk)

**File:** `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`, before line 34

Add:
```html
<style>@layer govuk, prism-base, prism-layout, prism-components, prism-branding;</style>
```

Then wrap each `<link>` in a corresponding `@layer` import or add `layer(prism-base)` to the link attributes. This makes the GDS/Prism cascade relationship explicit, eliminates implicit specificity wars, and means future engineers can read the intent without decoding source order.

### 3 — Rename `--bg-offset` → `--prism-surface-page` and consolidate `body {}` declarations (Low impact, low risk)

**Files:** `prism-colors.css:1,88`, `prism-typography.css:182`, `prism-layout.css:81`, `base.css:9`

The misprefixed token is a minor but recurring friction — it breaks the naming contract, it breaks find-replace, and it will confuse the branding metadata service consumers. Rename it to `--prism-surface-page` and collapse the three `body {}` declarations in `base.css`, `prism-typography.css`, and `prism-layout.css` into a single canonical block in `prism-typography.css`. Rams: "as little design as possible" applies to token schemas too.
