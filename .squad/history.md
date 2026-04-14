# Project Context

- **Owner:** {user name}
- **Project:** {project description}
- **Stack:** {languages, frameworks, tools}
- **Created:** {timestamp}

## Team Composition

**Current Roster (2026-03-28):**
- Tom Nook (Lead) — architecture, feature design, team coordination
- Copper (Security Engineer) — CIA hardening, threat models, security review
- Kicks (Mobile Native Specialist) — Capacitor native integration, iOS/Android implementation (joined 2026-03-28)
- Blathers (Backend Specialist) — C# implementation, database schema, authentication flows
- Isabelle (Frontend Engineer) — Web Components, Storybook, Playwright UI tests
- Tangy (Testing Specialist) — Test coverage, edge cases, reliability
- Celeste (Documentation Engineer) — XML docs, public API clarity, developer guides
- Mabel (Release Manager) — Versioning, release notes, changelog management
- Scribe (Documentation Specialist) — Session logging, decisions, team memory

## Learnings

### 2026-03-28: Biometric Auth Design Complete

**Context:** Multi-tenant mobile authentication feature designed for Prism Mobile via Capacitor.

**Key Outcomes:**
- **Design Document:** `/Design/biometric-auth.md` created (merged contributions from Tom Nook, Copper, Kicks)
- **Architecture:** Opaque BiometricToken model (server-side Entra refresh token storage, no device token leakage)
- **Security Threat Model:** Device credential registry with admin revocation, multi-tenant isolation, 30-day bounded lifetime
- **Native Implementation:** Plugin selection (@aparajita/capacitor-biometric-auth + @aparajita/capacitor-secure-storage), platform entitlements auto-injection, registration/login flows
- **Decisions:** 10+ architectural decisions documented and merged into `.squad/decisions.md`
- **Team Expansion:** Kicks successfully integrated as Mobile Native Specialist; delivered native implementation section

**Decision Quality:** All decisions include rationale, threat model analysis, implementation constraints, and phased roadmap (MVP → Hardening → Advanced).

**Open Questions for Implementation:** Copper (encryption key scoping), Blathers (token expiry validation, rate limiting strategy) — documented in decisions.md pending implementation phase.

**Delivery Mechanism:** Orchestration logs recorded for each team member; session log created; decisions merged and inbox cleared.

## Session: 2026-04-13 — Dashboard Test Investigation

**Spawn Request:** Investigate localhost auth Playwright regression for dashboard route (requested by Jonny Muir)

**Participants:**
- Brewster (Umbraco Platform Specialist)
- Tangy (Tester)

**Outcomes:**
- Root cause identified: Dashboard Playwright test was not verifying authored CTA navigation before asserting dashboard UI
- Dashboard route contract confirmed: `/dashboard` is correct; seeded route wiring is sound
- Test pattern recommendation: Playwright flows should navigate via authored CTAs (same path users take) rather than direct routes
- Decision merged to `.squad/decisions.md`: **Brewster — Dashboard Route Contract**
- Session log: `.squad/log/2026-04-13T23:05:08Z-dashboard-test-investigation.md`

**Key Finding:** Test false negatives were caused by incomplete state transitions. Fix ensures tests exercise authored Umbraco navigation structure.

## Session: 2026-04-14 — E2E Readiness Strategy

**Spawn Request:** Define three-layer E2E readiness strategy for cold-start flake; recommend Umbraco-specific readiness contract.

**Participants:**
- Tangy (🧪 Tester) — readiness layers, test gating, diagnostic capture
- Brewster (⚙️ Umbraco Platform Specialist) — Umbraco readiness contract, dashboard CTA pattern, route classification
- Blathers (🔧 Backend Specialist) — startup artefact classification, fallback route context

**Outcomes:**
- **Three-layer readiness strategy** (Layer 1: machine-readable contracts, Layer 2: page affordances, Layer 3: behaviour assertions)
- **Umbraco route classification:** Transient `/` resolution during cold boot is startup convergence artefact, not steady-state
- **Readiness contract:** Use `/api/prism/downstream-demo/seed-contract-ready` as authoritative gate
- **Dashboard CTA pattern:** Public CTAs for protected content pass authored URL as login `returnUrl`
- **Fallback route strategy:** Treat `/` as unstable for non-home pages during cold-start; keep authored fallback routes
- **Flaky test evidence:** Documented redirect loop (signin-oidc -> /dashboard -> /dashboard... -> net::ERR_TOO_MANY_REDIRECTS)

**Decisions Merged:** 7 decisions (tangy-e2e-strategy, tangy-flaky-dashboard-flow, brewster-classify-umbraco-behavior, brewster-umbraco-readiness-strategy, brewster-dashboard-link-race, blathers-classify-startup-impact, blathers-first-load-auth-race)

**Session Log:** `.squad/log/2026-04-14T08:03:15Z-e2e-readiness-strategy.md`

**Orchestration Logs:**
- `.squad/orchestration-log/2026-04-14T08:03:15Z-tangy.md`
- `.squad/orchestration-log/2026-04-14T08:03:15Z-brewster.md`
- `.squad/orchestration-log/2026-04-14T08:03:15Z-blathers.md`

**Key Finding:** Cold-start route instability is a repo-specific seeding/runtime pattern, not a platform limitation. Layered readiness gating separates infrastructure drift from product behaviour failures.
