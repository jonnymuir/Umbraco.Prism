# Decision: Media URL icons in prism-mobile-nav

**Date:** 2025-07-14  
**Author:** Isabelle (Frontend Dev)

## Context

The `icon` field on `NavItem` previously only accepted named built-in keys (`home`, `account`, etc.). Umbraco editors now need to pick icons from the media library, which produces URLs.

## Decision

Distinguish icon types at runtime using a prefix check (`/`, `http`, `data:`). Named keys use the existing SVG path lookup; URLs render as `<img aria-hidden="true">` elements.

## Rationale

- Zero breaking changes — existing named icons unchanged
- No new dependencies
- `<img>` with `aria-hidden="true"` and empty `alt` is accessible (decorative icon, label from sibling `<span>`)
- Opacity transitions (0.6 inactive → 1 active → 0.85 hover) mirror named icon behaviour via `color` inheritance

## CSS approach

Added `.nav-icon--img` class. Named SVG icons use `currentColor` (inherits from `.nav-item` `color` transition). `<img>` elements can't use `currentColor`, so opacity is used instead. Editors should upload SVGs in a neutral colour for best results.
