# Skill: CSS Custom Property Token Review

**When to apply:** Auditing or designing a CSS custom property token system — whether pure CSS, Sass-backed, or inline-style injection (as in Prism's middleware approach).

---

## Pattern

A token system using CSS custom properties is **correct** when:

1. **All tokens share a consistent namespace prefix** (`--prism-*`, `--ds-*`, etc.) — no exceptions.
2. **Derived tokens are computed, not hardcoded.** Hover states, dark variants, and contrast colours use `color-mix()`, `oklch()` lightness manipulation, or `calc()` — never a manually chosen hex.
3. **`@property` declarations are paired with `:root {}` assignments.** The `@property` block declares type/inheritance/initial-value; the `:root {}` block sets the live value. Both are needed — the `@property` initial-value only fires when the property is not set on the element.
4. **Accessibility-constrained tokens are segregated or annotated.** Focus ring, error, and warning colours must carry a WCAG-constraint comment or live in a dedicated file. They are not brand tokens and must not be casually overridden.
5. **CSS cascade layers are declared explicitly** when third-party systems (GDS, Bootstrap, etc.) are loaded alongside bespoke tokens. One `@layer` declaration at the HTML head entry point makes the intent machine-readable and prevents implicit specificity wars.
6. **`body {}` is declared exactly once** in a canonical location. Multiple files resetting body typography/background is a maintenance hazard and a Rams violation ("as little design as possible").

---

## Anti-patterns found in the wild (Prism, 2026-05-01)

| Anti-pattern | Example | Fix |
|---|---|---|
| Hardcoded dark variant of a brand token | `--prism-button-hover: #003078` | `color-mix(in srgb, var(--prism-primary) 80%, #000 20%)` |
| Missing namespace prefix | `--bg-offset` | `--prism-surface-page` |
| Token duplicated across files | `--prism-nav-height: 56px` in both `prism-layout.css:57` and `prism-components.css:137` | Single declaration in layout file |
| Body declared in N>1 files | `base.css`, `prism-typography.css`, `prism-layout.css` all set `body {}` | One canonical block in typography file |
| No `@layer` contract with third-party CSS | GDS loaded by file order before `--prism-*` rules | `@layer govuk, prism-*;` at head entry point |

---

## Shadow DOM theming checklist

When a LitElement web component needs to be brandable:
- Expose every colour, size, and font via CSS custom properties with `var(--my-token, sensible-fallback)` in the component's `static styles`
- Inherit from `--prism-primary` et al. rather than setting absolute values for active/brand states
- Document all exposed tokens in a JSDoc block on the class (see `prism-mobile-nav.ts` as the reference implementation)
- Add a Storybook story demonstrating brand colour override via inline `--prism-primary` on the host element

---

## References

- `src/UmbracoPrism.TestSite/wwwroot/branding/prism-colors.css` — `@property` + `:root {}` pattern (good); `--bg-offset` (bad)
- `src/UmbracoPrism.TestSite/wwwroot/branding/prism-components.css:120` — hardcoded hover (bad)
- `src/UmbracoPrism.Client/src/mobile/prism-mobile-nav.ts` — reference implementation for Shadow DOM theming (good)
- `.squad/reviews/2026-05-01-prism-reflection/02-isabelle-design-system.md` — full audit
