# Decisions

Umbraco.Prism team decisions. Append-only ledger.

---

## 📌 2026-03-22: Architecture Review Complete (Tom Nook)

**Session Log:** `.squad/log/2026-03-22-architecture-review.md`

**Scope:** Core services, middleware, identity, persistence, frontend integration

**Key Findings:**
- ✅ Stateless OIDC architecture is elegant and scales horizontally
- 🔴 P0 Risks: Blocking async in OIDC config; token refresh without retry; authorization inconsistency (Entra vs. Umbraco groups)
- 🟠 Scaling concerns: Tenant cache 30-min TTL; CSS scan on cold start; 1K tenant ceiling
- 🟡 OIDC metadata cache never invalidates; mobile bundle missing validation + rate limits

**Decision Inbox (3 items):**
1. Extract TokenRefreshService with Polly retry/circuit breaker (P0) → Blathers
2. Standardize authorization on Entra groups (P0) → Blathers
3. Document tenant rejection policy (P0) → Tom Nook

**Handoff:** Isabelle (branding UI), Blathers (token resilience + P1 cache/security), Tangy (edge case tests)

---

## 📌 2026-03-22: Squad initialized (Animal Crossing cast)

**Team roster hired:**
- Tom Nook: Lead (architect, scope, code review)
- Isabelle: Frontend Dev (Web Components, Storybook, UI)
- Blathers: Backend Dev (C# APIs, services, auth, database)
- Tangy: Tester (testing strategy, edge cases, quality)
- @copilot: Coding Agent (async issue work)
- Scribe: Session Logger (memories, decisions, logs)

**Universe:** Animal Crossing (character names drawn from Nook family empire, Isabelle's assistant role, Blathers' curator expertise, Tangy's cranky attention to detail)

**Casting policy:** One universe per assignment, persistent names, no re-casting. Stored in `.squad/casting/` (policy.json, registry.json, history.json).

---
