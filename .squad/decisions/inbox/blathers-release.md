# Release Decision: v1.10.0

**Date:** 2026-06-06  
**Author:** Blathers (Backend Dev)  
**Branch merged:** `fix/workflow-editor-save-and-layout` → `main` via PR #90

## Version Bump

`1.9.1` → `1.10.0` (minor bump — new features, no breaking changes).

Files updated:
- `package.json` (root)
- `src/UmbracoPrism.Client/package.json`
- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj`

## Key Highlights

### New
- **Gateway routing validation** — Backend enforces that all state routes target a gateway. Returns HTTP 400 `application/problem+json` with `errorCode: workflow-gateway-routing-invalid` on violations.
- **Cycle-breaking Join gateways** — Backward loop edges (save-draft, return-to-form) are now modelled via Join gateways so Kahn's ranking algorithm produces correct top-to-bottom layouts.

### Fixed
- **Runtime sync on save** — Workflow edits in the authoring UI now immediately update the running engine (no restart required).
- **Y-axis layout algorithm** — Backward edges from Join gateways are excluded from rank computation.
- **AllowOutOfOrderMetadataProperties** — Save endpoint handles alphabetically-sorted JSON keys for polymorphic components.

### Seed workflows updated
- `planning.json`, `community-enquiry.json`, `information-request.json` — all state-to-state routes now route via Split gateways per the gateway-first rule.

## Tests

- `dotnet build` ✅  
- `dotnet test` ✅ (809 passed)  
- `npm run build` ✅  
- `npm run test-storybook:ci:all` ✅ (168 passed)

## Tag

`git tag v1.10.0` pushed to origin.

## Notes for Scribe

The `localhost-auth-playwright` integration test has a pre-existing mismatch: it expects `heading: "Your details"` on `/get-in-touch`, but the page renders `"Tell us about your enquiry"` (the actual `displayName` from `community-enquiry.json`). This is a test fixture issue that exists independently of this release. Recommend filing a separate issue to align the test expectation with the actual heading rendered by the runtime.
