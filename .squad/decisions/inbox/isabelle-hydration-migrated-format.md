# Editor hydration support for migrated workflow JSON format

**Date:** 2026-06-06  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**For:** Blathers, Tangy

---

## Decision

Extended `hydrateWorkflowDefinition()` in `types.ts` to accept the alternate field names used in the migrated workflow JSON format (produced by C# fixtures and potentially by the authoring API):

- Stages: `key` → `stateKey`, `title` → `displayName`, `type` → `kind`
- Gateways: `title` → `displayName`, `type` → `gatewayType`/`kind`, `waitingInfo` → `waiting` block  
- Queues/lanes: `title` → `displayName`

All changes are additive (new fields are lowest-priority fallbacks) — existing field names remain first priority. No regressions to existing workflows.

## Why

The three migrated workflows (planning, community-enquiry, information-request) use `key`/`title`/`type` field names on their stages and gateways. Without this fix, stages would hydrate with empty `stateKey`, stages would default to kind `'Question'` regardless of their actual type, and Join gateways would be rendered as Split gateways.

## Testing

Added 15 Playwright tests (`workflow-migrated-workflows.spec.ts`) and 3 Storybook stories. All pass. TypeScript build clean.

## Implications for Blathers

The runtime seed format (`stateKey`/`displayName`/`stageType`/`queueKey`) was already supported and requires no changes. The fix is purely for the authored definition format served to the editor.
