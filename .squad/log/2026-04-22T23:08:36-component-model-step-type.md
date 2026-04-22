# Session Log: Component Model & stepType Architectural Alignment

**Date:** 2026-04-22T23:08:36+01:00  
**Participants:** Tom Nook (Lead), Blathers (Backend), Scribe (Documentation)

## Key Decisions

1. **Remove authored stepType** from workflow JSON; engine derives `shell` from component tree.
2. **Promote WaitingConfig** from sidecar to component type.
3. **Validation pipeline remains stable** — component-agnostic, field-keyed.
4. **Replace stepType consumers** with explicit `terminal` flag and `responseState` metadata.

## Outcomes

- **Tom Nook:** stepType removal reduces authoring burden and removes a class of errors.
- **Blathers:** All architectural layers (validation, persistence, GDS) are safe to proceed.

## Next Steps

- Implement shell derivation and component migration (Blathers).
- Update partial views and conditionals (Isabelle).
- Test inference logic and polling (Tangy).
- Migrate seed files.
