# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## 📌 2026-04-30: Cross-Agent Note — Umbraco Route Review Pending

**Note:** No new work on Brewster's roadmap as of 2026-04-30, but architectural debt flagged in prior work (2026-04-14 review):
- Missing `[ModelType("alias")]` on route-hijack controllers
- Unfinished `workflowDemoPage` surface area (placeholder bundle)
- Custom `Prism.Section` unused in backoffice (conditioned into `Umb.Section.Content`)

**Status:** Documented for future sprint review; no blocking issues on v2.0 rollout.

---

## Prior Work Summary (2026-04-14 — Umbraco v17 Solution Review)

**Architecture Validation:**

✅ **Route Hijacking Pattern:** Prism strongest when Umbraco owns authored route + page shell; business app owns workflow state. Current `workflowPage`/`workflowHub` pattern is close to idiomatic v17.

✅ **Route Contract:** `/dashboard`, `/get-in-touch`, `/my-workflows` are valid published routes with correct auth challenge behavior.

⚠️ **Document Type Design:** `PrismContentTypeSeeder` minimal (no richer page fields, thin editor affordances). `workflowPage` seeded as root (should be under Home).

⚠️ **Umbraco-Specific Risks:**
- Missing `[ModelType("alias")]` on hijacked controllers
- `MemberDashboardController` hardcodes `/dashboard`, bypasses `CurrentTemplate()`
- `HomePage.cshtml` untyped, reads non-existent model properties

⚠️ **Backoffice Dashboard:** v17-native extension stack (Lit/UUI) but custom `Prism.Section` unused; dashboard conditioned into `Umb.Section.Content`.

⚠️ **Unfinished Surfaces:** `workflowDemoPage` points at placeholder bundle (doesn't exist); instance-picker UI in Razor but never activated.

**Auth Flow Validation:**

✅ **Auth Cookie Behavior:** Fixed restart-recovery by detecting IssuedUtc < ProcessStartedUtc; force token refresh on runtime restart.

✅ **OIDC Scope Strategy:** Localhost demo requests offline_access for restart tolerance; generic OIDC tenants default to "openid profile" only.

✅ **Keycloak Refresh:** Refresh calls omit scope parameter when using offline_access-issued tokens.

✅ **Route-Readiness Strategy:** Tests should wait for seed-contract-ready (`GET /api/prism/downstream-demo/seed-contract-ready`), not cold-start transient `/` fallbacks.

**Cold-Boot Convergence Insight:**

Seeded routes briefly resolving to `/` on first boot is a cold-start artifact of this app's runtime pattern (reset DB → install → publish demo tree → eager route consumption). Warm, settled Umbraco shouldn't persist this.

---

## Key Learnings Preserved

1. **Umbraco owns routes; Prism owns plumbing** — clear separation keeps architecture clean
2. **Cold-start readiness probes > page copy checks** — route contract is authoritative signal
3. **Don't persist one-off OIDC redirects in auth cookies** — causes false fallbacks on later navigation
4. **Behavior tests should never absorb cold-start quirks** — wait for settled contract, then assert final state
5. **Auth scope strategy differs by tenant** — offline_access for demo, standard scopes for production

---
