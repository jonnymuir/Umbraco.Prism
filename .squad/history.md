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
