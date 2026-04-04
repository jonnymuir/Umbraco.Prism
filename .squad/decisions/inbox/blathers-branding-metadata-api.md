# Decision: CSS Branding Metadata Parser Architecture

**Date:** 2026-04-08  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented  
**Related:** Tenant Editor Dynamic Form (Isabelle)

## Context

The tenant editor in the Umbraco backoffice needs to present a dynamic, design-system-style form for editing CSS variables. Rather than hardcoding form fields, the editor should read metadata from the branding CSS files at startup and build the form dynamically.

## Decision

Implemented a CSS metadata parser that reads structured annotations from branding CSS files and exposes them via a backoffice API endpoint.

### Annotation Format

Each brandable CSS variable follows this pattern:

```css
@property --prism-primary {
  syntax: '<color>';
  inherits: true;
  initial-value: #4f46e5;
}

:root {
  /* @prism section: Brand Colours | label: Primary Brand Colour | description: Used for buttons, links, and highlights */
  --prism-primary: #4f46e5;
}
```

**Annotation Keys:**
- `section` — groups variables into sections in the editor UI
- `label` — human-readable name for the field
- `description` — tooltip/hint text
- `type` — picker type hint: `color`, `image`, `url`, `font`, `length`, `text`

**Type Resolution:**
1. Explicit `@prism type:` override (if present)
2. Inferred from `@property syntax` (`<color>` → color, `<url>` → url, `<length>` → length, `*` or `<string>` → text)
3. Default to `text` if neither present

### API Contract

**Endpoint:** `GET /umbraco/api/prism/branding/metadata`  
**Auth:** Umbraco backoffice access (`BackOfficeAccess` policy)

**Response:**
```json
{
  "sections": [
    {
      "name": "Brand Colours",
      "variables": [
        {
          "variable": "--prism-primary",
          "label": "Primary Brand Colour",
          "description": "Used for buttons, links, and highlights",
          "type": "color",
          "syntax": "<color>",
          "currentValue": "#4f46e5"
        }
      ]
    }
  ]
}
```

### Implementation

**Models:**
- `BrandingVariableMetadata` — variable name, label, description, type, syntax, currentValue
- `BrandingSection` — section name + list of variables

**Service:**
- `IPrismBrandingMetadataService` / `PrismBrandingMetadataService`
- Reads all `*.css` files from `wwwroot/branding/` EXCEPT `prism-branding.css` (aggregator file)
- Parses `@property` declarations and `/* @prism ... */` annotations using regex
- Groups by section (first-appearance order, not alphabetical)
- Caches result in `IMemoryCache` (1-hour sliding expiration)
- Registered as singleton in `PrismComposer`

**Controller:**
- Added `GET branding/metadata` endpoint to `TenantManagementController`
- Requires backoffice authentication

## Rationale

**Why parse at runtime (not build-time)?**
- Simpler deployment — no build step for CSS changes
- Supports hot-reload scenarios in dev
- Metadata cache means negligible runtime cost

**Why regex parsing (not PostCSS/AST)?**
- Annotation format is simple and well-defined
- No need for Node.js tooling in backend
- Regex patterns are tested and reliable for this use case
- Performance is not a concern (cached result, ~6 CSS files)

**Why exclude `prism-branding.css`?**
- It's an `@import` aggregator with no variable declarations
- Prevents duplicate parsing when variables are in source files

**Why section order by first-appearance?**
- Allows authors to control section order by organizing CSS files
- More intuitive than alphabetical sorting
- Matches typical design-system organization (colors first, typography second, etc.)

**Why 1-hour cache expiration?**
- CSS changes are rare in production
- Dev can restart to pick up changes
- Balances freshness vs. performance

## Consequences

**Benefits:**
- ✅ UI form is automatically in sync with available CSS variables
- ✅ Adding new brandable variables requires zero UI code changes
- ✅ Type inference from `@property` reduces annotation boilerplate
- ✅ Section grouping improves UX for large variable sets

**Limitations:**
- ⚠️ CSS authors must follow annotation format exactly (no validation at write-time)
- ⚠️ Cache expiration means CSS changes require restart or cache eviction
- ⚠️ Regex parsing is fragile to format variations (mitigated by unit tests)

**Future Enhancements:**
- Add CSS validation/linting to enforce annotation format at build time
- Support nested section hierarchies (e.g., `section: Colors / Primary`)
- Add metadata for default values, min/max constraints (for length types)
- Expose cache invalidation endpoint for hot-reload scenarios

## Testing

Added 12 unit tests in `PrismBrandingMetadataServiceTests.cs`:
- Annotation parsing (label, description, section, type)
- Type inference from `@property syntax`
- Explicit type overrides
- Section grouping and ordering
- Default section fallback
- Caching behavior
- Multi-file parsing

All tests pass. No integration test needed (endpoint is trivial wrapper).

## Team Coordination

This API contract was agreed with Isabelle (UI Dev) before implementation. She is simultaneously:
1. Annotating CSS files with `@prism` comments
2. Building the dynamic tenant editor UI to consume this endpoint
3. Implementing type-specific form fields (color picker, image uploader, etc.)
