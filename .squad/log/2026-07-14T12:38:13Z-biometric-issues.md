# Session Log 2026-07-14T12:38:13Z

## Biometric Auth Issue Decomposition

**Participant:** Tom Nook (Lead Architect)

**Context:** `Design/biometric-auth.md` is complete (all JWT/UUID consistency fixes from the previous session are accepted). This session converts the design into 17 actionable GitHub issues across 4 implementation phases, assigns squad labels, and files the decomposition decision.

**Outcomes:**

1. **17 GitHub Issues Created (#12–#28)** in `jonnymuir/Umbraco.Prism`:
   - Phase 1 — Backend Foundation: #12–#18 (Blathers)
   - Phase 2 — MobileBundleService: #19–#21 (Blathers + Kicks)
   - Phase 3 — Capacitor Client: #22–#25 (Isabelle + Kicks)
   - Phase 4 — Security & Hardening: #26–#28 (Copper + Kicks)

2. **New Label Created:** `squad:kicks` — added to the repository label set; was absent prior to this session.

3. **Labels Applied:** All issues carry `biometric-auth` plus the relevant `squad:*` labels for routing.

4. **Key Constraints Encoded in Issues:**
   - Rolling refresh token rotation is v1 mandatory (#15)
   - `/exchange` is unauthenticated by design — rate limiting is non-negotiable (#18)
   - `biometricToken` must never appear in logs (#17)
   - Cross-tenant deletion guard is explicit in #16 and #27
   - `@capacitor/preferences` is explicitly forbidden in #19 and #22 (not hardware-backed)

5. **Decision Filed:**
   - `.squad/decisions/inbox/tom-nook-biometric-issues-created.md`

**Next Phase:** Blathers picks up Phase 1 issues (#12–#18); Kicks joins for Phase 2 co-ownership.
