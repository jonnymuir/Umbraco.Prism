# Session Log — Umbraco 17 Review

**Date:** 2026-04-14T20:24:57Z  
**Topic:** Umbraco 17 and Prism-fit review of workflow pages, supporting components, and dashboard  
**Agents:** Brewster (Umbraco specialist), Tom Nook (Architecture lead)  
**Outcome:** Complete — consensus achieved on architectural fit

## Session Summary

Two-agent parallel review of the Umbraco.Prism workflow/dashboard surface against idiomatic Umbraco v17 patterns and Prism architectural boundaries.

### Findings

**Architectural Fit:** ✅ Good fit for Prism — route-hijacked pages with external Business App ownership

**Key Strength:** Clear boundary — Umbraco owns content/routing, Prism owns auth/tenant/session, Business App owns workflow state

**Highest-Value Follow-up:**
1. Add `[ModelType]` attributes to route hijackers
2. Fix untyped models and hardcoded dashboard routes
3. Complete unfinished demo surface areas
4. Reduce demo-only coupling in seeded patterns

### Decisions Made

- **Decision:** Keep current route-hijacked pattern as canonical for workflow pages (Tom Nook)
- **Approved:** Use BusinessApp as external source of truth; do not duplicate workflow logic in Prism UI (Tom Nook)
- **Action Items:** Tagged for future sprints; no implementation work this session

### Agents

- **Brewster:** Umbraco 17 specialist review — identified missing ModelType, hardcoded routes, skeletal doc types
- **Tom Nook:** Architecture and coupling analysis — approved pattern, ranked follow-up debt, established decision boundary

## Next Steps

- Team consensus on findings
- Prioritise follow-up items based on product roadmap
- Ensure future feature work preserves established boundary
