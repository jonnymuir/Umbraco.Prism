# Tangy — History

## Core Context

QA validation, test coverage analysis, and edge-case identification.

**Key domains:** Playwright testing, E2E validation, Edge case coverage, CI/CD readiness, Performance analysis

## 📋 Recent Sessions

---

## 2026-05-03: Spawn Manifest — Codespaces Dashboard Failure Reproduction

**Timestamp:** 2026-05-03T11:07:19.866Z  
**Status:** ✅ Reproduced

Tangy reproduced the live Codespaces dashboard failure at `https://organic-space-fortnight-77g9wvq6jxhxg97-44345.app.github.dev/dashboard`.

**Evidence Captured:**
- Found hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` in code
- This may not resolve correctly in live Codespaces environment
- Hypothesis: backend service discovery issue or network isolation

**Coordination:**
- Blathers deployed enhanced diagnostics (token kid, ASPNETCORE_ENVIRONMENT, JWKS URLs)
- Copper verified trust chain and recommended restart of MockBusinessApp
- Brewster fixed Codespaces URL printing regression

**Next Steps:**
- Monitor operator actions when next 401 occurs in live Codespaces
- Use enhanced /debug/auth endpoint to confirm backchannel state
- Leverage Codespaces recovery scripts if needed

