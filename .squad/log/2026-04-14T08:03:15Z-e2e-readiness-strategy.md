# Session Log — E2E Readiness Strategy

**Date:** 2026-04-14  
**Session ID:** 2026-04-14T08:03:15Z-e2e-readiness-strategy  
**Requested by:** Jonny Muir

## Summary

Team session to define three-layer E2E readiness strategy for localhost Aspire Playwright suite, investigate cold-start route stability, and document Umbraco-specific recommendations for seeded content testing.

## Participants

- **Tangy** (🧪 Tester) — Readiness strategy layers, test gating pattern
- **Brewster** (⚙️ Umbraco Platform Specialist) — Umbraco readiness contract, dashboard CTA pattern, route classification
- **Blathers** (🔧 Backend Specialist) — Startup artefact classification, fallback route context

## Key Outcomes

### 1. Three-Layer E2E Readiness Strategy
- **Layer 1 (Infrastructure):** Machine-readable signals only (home-page marker, `/api/prism/downstream-demo/seed-contract-ready`, Keycloak, MockBusinessApp)
- **Layer 2 (Page readiness):** Assert authored `href` before click; wait for page-specific affordances (not shared UI)
- **Layer 3 (Behaviour):** Product assertions only after layers 1 & 2 pass

### 2. Umbraco Route Classification
- Transient `/` resolution during cold boot is startup convergence artefact, not steady-state behaviour
- Use `/api/prism/downstream-demo/seed-contract-ready` as authoritative gate for seeded route contract
- Home-page `data-prism-home-ready="true"` is smoke check only

### 3. Dashboard CTA Pattern
- Public CTAs for protected content now pass authored URL as login `returnUrl`
- Example: `HomePage.cshtml` derives `Go to Dashboard` from content-resolved dashboard URL
- Removes ambiguous intermediate home-page bounce

## Decisions Merged

- `tangy-e2e-strategy.md`
- `tangy-flaky-dashboard-flow.md`
- `brewster-classify-umbraco-behavior.md`
- `brewster-umbraco-readiness-strategy.md`
- `brewster-dashboard-link-race.md`
- `blathers-classify-startup-impact.md`
- `blathers-first-load-auth-race.md`

## Next Steps

- Implement layer-based readiness gates in `localhost-auth-session.spec.ts`
- Apply fallback route strategy to seeded navigation helpers
- Monitor cold-start flake patterns with new diagnostics

## Status

✅ Complete — decisions documented, orchestration logs recorded, session closed.
