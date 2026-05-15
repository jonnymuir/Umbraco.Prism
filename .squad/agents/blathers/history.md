# Blathers — History (Summarized)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Focus Areas:** Aspire dashboard Codespaces access, authentication diagnostics, runtime stale-code diagnosis, backchannel OIDC validation, dynamic endpoint discovery, transport diagnostics.

---

## Recent Work Summary

### Transport Diagnostics & Downstream Demo Fixes (2026-05-03 → 2026-05-04)
- ✅ Implemented response-visible transport diagnostics for downstream API calls
- ✅ Fixed workflow API backchannel URL resolution in Codespaces
- ✅ Diagnosed JWKS backchannel escape as root cause of auth timeouts
- ✅ Added logging for null auth headers in workflow clients
- ✅ Aligned workflow handlers to `Results.Problem()` for consistency
- ✅ Fixed `PrismContextTests` race condition via `EnvVarSensitiveTestCollection`

### Key Learnings
- Named HttpClients must be registered via AddHttpClient() even when timeout is managed via CancellationToken
- Any test class reading `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must use `EnvVarSensitiveTestCollection` to avoid parallelism hazards
- Response-visible diagnostics beat verbose logs for operator troubleshooting
- Safe transport diagnostics must mask internal ports but show public URLs (browser-visible anyway)

### Implementation Patterns
- Use `BUSINESSAPP_BACKCHANNEL_URL` fallback for internal Codespaces calls, then `PrismBusinessApp:WorkflowApiBaseUrl` for production
- Instrument critical paths with safe metadata (transport type, backchannel presence, timeout cause) for diagnostics
- Guard dev diagnostics with `IsDevelopment` or `Prism:EnableDownstreamDemo` flags

---

## Full Session Archive

See `history-archive.md` for complete session-by-session work logs prior to 2026-05-03 summarization.

---

## Latest Coordination (2026-05-04)

**Status:** Release-ready. All tests passing. Awaiting final squad state consolidation and merge.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-04 | Workflow Admin UI Cleanup

**Status:** In Progress

Implemented workflow admin UI cleanup for walkthrough and manual documentation use.
Coordinating with Brewster (dashboard navigation) and Tangy (screenshot integration).

## Learnings

### 2026-05-15T06:35:47.013+01:00 | PASA death-process design

- A third-party initiated workflow should authenticate the notifier as the actor and link the deceased member as a server-side subject, not as the signed-in user.
- Save/resume for sensitive one-off cases works better with verified case access (magic link or OTP) plus a separate case aggregate than with mandatory permanent registration.
- Prism remains the workflow shell; case tracking, member matching, evidence manifests, and reviewer notes belong in business-app domain persistence.

## 2026-05-15: PASA Death Process Backend Decision

Produced backend decision on notifier workflow mechanics. Specified lightweight verified contact (magic link/SMS OTP), case-scoped identity, Prism-hosted workflow for notifier, case persistence in business app. Defined need for NotifierIdentity/NotifierSession model alongside DeathCase. Merged to shared registry.

