# GOV.UK Frontend Token Bridge

## The problem

GOV.UK Frontend ships its own `--govuk-*` CSS custom properties on `:root` (colours, mostly, plus
a few structural constants). Prism ships a separate set of semantic tokens, `--prism-*`, in
`prism-colors.css`. Left alone, these are two independent value sets that happen to look similar
by coincidence, not by relationship, so a tenant who changes `--prism-danger` in the branding
editor doesn't change the colour of a GOV.UK error summary, because the error summary reads
`--govuk-error-colour`, a completely different property with its own hardcoded default.

## The mechanism: aliasing, not two value sets

`--prism-*` stays the single source of truth. A new file, `prism-govuk-bridge.css`, redefines the
relevant `--govuk-*` properties as `var()` references to the matching `--prism-*` token, so GOV.UK
components keep consuming `--govuk-brand-colour` exactly as before (zero changes to GDS
markup/CSS or to `Wayfinder.Umbraco`), but that property's value now tracks whatever the tenant
sets on the Prism token.

This is TestSite's own convention, the same way `prism-colors.css`/`prism-typography.css`/etc.
are TestSite's own convention. `UmbracoPrism.Core` ships no CSS of its own and needs no code
change to support it: `BrandingService`'s tab discovery scans any `.css` file dropped under
`wwwroot/branding/`, and any file is fair game to alias whatever properties it wants.

## Load order

A CSS custom property's effective value is whichever declaration on that specific property, in
that scope, comes last in cascade order. `Master.cshtml` already loads
`govuk-frontend.min.css` first, then site CSS, then `prism-branding.css` (which
`@import`s `prism-govuk-bridge.css` alongside the other `prism-*.css` files), then finally
`/umbraco/prism/branding.css` (the tenant's live overrides). Because `prism-branding.css` as a
whole already loads after `govuk-frontend.min.css`, the bridge file's declarations win the
cascade regardless of where in `prism-branding.css`'s own `@import` list it sits, and a tenant's
own override of `--prism-danger` (say) still flows through automatically, no separate override of
`--govuk-error-colour` needed.

## Linked fields in the tenant editor

A variable whose declared value is `var(--x, fallback)` renders in the tenant editor as a
"Linked to `<label of x>`" badge with a colour swatch preview, instead of the raw widget, a native
`<input type="color">` can't render a `var()` string as its value (see the gotcha below). A
"Customise" action on the badge reveals a `uui-select` picker of every other same-type token,
preselected to the current link target, plus a "Custom value (not linked)" option that drops back
to the normal colour/text widget seeded with the link's resolved colour. Picking a different
token repoints the link (`var(--newTarget, ...)`); picking "Custom value" breaks it entirely. This
lives entirely in `prism-create-tenant-modal.ts` (`_renderDynamicField`'s `renderValueControl`,
plus the `_parseLink`/`_resolveLiteralValue`/`_getLinkableTargets` helpers) and needed **no**
`UmbracoPrism.Core` change: `CurrentValue` was already the literal declared CSS text
(`PrismBrandingMetadataService.ParseCssFile`), so detecting a `var()` reference, resolving it to a
literal colour for the preview swatch, and finding same-type link targets is all done client-side
against the metadata the editor already holds in memory.

The picker is a plain `uui-select` (already part of the backoffice UI library, no new dependency),
not a free-text/intellisense editor: the set of things you can link to is a small, closed list of
known tokens, exactly what a native select is for. An open-ended expression editor (the kind
`Wayfinder.Umbraco`'s calculation authoring uses) would be solving a different problem, letting
someone type an arbitrary formula referencing dynamic identifiers, not picking one of ~15 known
colours.

This generalises beyond the GOV.UK bridge: any two same-type `--prism-*`/`--govuk-*` variables can
be linked this way, the picker doesn't special-case the bridge file.

### The gotcha that shapes this

`PrismBrandingMetadataService.ParseCssFile` sets `CurrentValue` to whatever literal text follows
the colon in the `:root` declaration. For `--govuk-brand-colour: var(--prism-primary, #1d70b8);`
that's the literal string `var(--prism-primary, #1d70b8)`, not a resolved colour. Two consequences
follow, both handled client-side rather than by changing what the backend sends:

- A native `<input type="color">` can't accept that string as its `value`, so the editor must
  detect the link and render a badge/picker instead of feeding it straight to the widget.
- `@property`'s `initial-value` can't reference `var()` either (the CSS Custom Properties spec
  requires it to be computationally independent of the cascade), so `prism-govuk-bridge.css`'s
  `@property` blocks declare each `--govuk-*` property's own literal GOV.UK default
  (`initial-value: #1d70b8`, say), never the Prism token it's linked to. The `:root` declaration
  underneath is what actually carries the `var()` reference.

## The mapping

GOV.UK Frontend 6.5.0 ships 25 `--govuk-*` custom properties on `:root`. Three tiers, plus a set
of structural constants that are never design tokens:

### Direct links (GOV.UK's default already matches Prism's default)

| GOV.UK variable | Prism token | Current values |
|---|---|---|
| `--govuk-brand-colour` | `--prism-primary` | `#1d70b8` = `#1d70b8` |
| `--govuk-text-colour` | `--prism-text` | `#0b0c0c` = `#0b0c0c` |
| `--govuk-inverse-text-colour` | `--prism-primary-contrast` | `#fff` = `#ffffff` |
| `--govuk-body-background-colour` | `--prism-surface` | `#fff` = `#ffffff` |
| `--govuk-focus-colour` | `--prism-focus` | `#fd0` = `#ffdd00` |
| `--govuk-focus-text-colour` | `--prism-text` | `#0b0c0c` |
| `--govuk-input-border-colour` | `--prism-text` | `#0b0c0c` |
| `--govuk-link-active-colour` | `--prism-text` | `#0b0c0c` |
| `--govuk-surface-text-colour` | `--prism-text` | `#0b0c0c` |

Linking these is a no-op visually today. It exists purely to stop the two values drifting apart
the next time either side changes its default.

### Corrective links (GOV.UK's default and Prism's default currently disagree)

| GOV.UK variable | Prism token | Current values |
|---|---|---|
| `--govuk-error-colour` | `--prism-danger` | `#ca3535` vs `#d4351c` |
| `--govuk-success-colour` | `--prism-success` | `#0f7a52` vs `#00703c` |
| `--govuk-border-colour` | `--prism-border` | `#cecece` vs `#b1b4b6` |
| `--govuk-hover-colour` | `--prism-border` | `#cecece` vs `#b1b4b6` |
| `--govuk-secondary-text-colour` | `--prism-muted` | `#484949` vs `#505a5f` |
| `--govuk-link-colour` | `--prism-link` | `#1a65a6` vs `#1d70b8` |

Linking these visibly shifts GOV.UK component colours (error summaries, success panels, borders,
secondary text, links) to match Prism's existing tokens. That shift is the point, the two systems
were meant to agree and didn't.

`--govuk-link-colour` deserves a callout: GDS deliberately uses a *different* blue for hyperlinks
than for brand elements like buttons (`--govuk-brand-colour`). Prism already collapses that
distinction (`--prism-link` in `prism-colors.css` is set equal to `--prism-primary`), so this link
preserves an existing Prism simplification rather than introducing a new one. Worth knowing that's
a deliberate choice if GDS's nuance is ever wanted back.

### Overridable, but not linked (no equivalent Prism concept)

These six have no matching Prism token, so there's nothing sensible to alias them to, but "no
natural link" isn't the same as "should never be tenant-editable". Each is a plain literal
declaration in `prism-govuk-bridge.css` (its own `@property` initial-value and `@prism`
annotation, same as any other colour field), so it shows up as a normal editable field in the
GOV.UK Alignment section. Editing it changes only that one GOV.UK property, directly, it doesn't
track any Prism token, so if a related Prism token changes later these won't follow automatically.

| GOV.UK variable | Why there's no link | Default |
|---|---|---|
| `--govuk-template-background-colour` | GDS's own pale page-canvas tint. No Prism token represents "page canvas" as distinct from `--prism-surface` (card/content background) today. | `#f4f8fb` |
| `--govuk-surface-background-colour` | Same concept, used on GOV.UK's pale surface panels (e.g. notification banners). | `#f4f8fb` |
| `--govuk-print-text-colour` | Print-only concern, not a brand colour. | `#000000` |
| `--govuk-link-hover-colour` | No hover-state token in Prism yet. A future addition could derive it from `--prism-link` with `color-mix()` rather than adding a fourth manually-picked blue, at which point this would move up to the corrective-links table. | `#0f385c` |
| `--govuk-link-visited-colour` | Visited-link purple is a GDS accessibility/UX convention, arguably shouldn't move in lockstep with the brand palette even if a "visited" Prism token existed. | `#54319f` |
| `--govuk-surface-border-colour` | No equivalent concept in Prism today (light-blue border used against GDS's "surface" panels). | `#8eb8dc` |

If a genuine Prism-token equivalent for one of these shows up later, move it into the linked
tables above instead of leaving it as a literal, same process as "Adding a new bridge link" below.

### Structural (never a design token)

`--govuk-breakpoint-mobile`, `--govuk-breakpoint-tablet`, `--govuk-breakpoint-desktop`,
`--govuk-frontend-version`.

## Adding a new bridge link

1. Confirm the GOV.UK variable and the Prism token genuinely represent the same design concept,
   not just a coincidentally similar colour. If there's no existing Prism token for the concept,
   don't force a mapping, either add a new Prism token first (in `prism-colors.css` or wherever
   fits) or expose it as a plain unlinked literal instead, same as the "Overridable, but not
   linked" table above.
2. Add an `@property` block with GOV.UK's own literal default (never a `var()` reference, see the
   gotcha above), then the declaration itself in `:root`:
   ```css
   @property --govuk-some-colour { syntax: '<color>'; inherits: true; initial-value: #govukdefault; }

   :root {
     /* @prism section: GOV.UK Alignment | label: GOV.UK Some Colour | description: What this drives in GOV.UK Frontend. Linked to Some Token. */
     --govuk-some-colour: var(--prism-some-token, #govukdefault);
   }
   ```
   The fallback inside `var()` should be GOV.UK's own current default, so the file degrades
   gracefully to the stock GDS look if ever loaded without the rest of the Prism branding chain.
3. Update the mapping table in this doc.
