# Tangy — History Archive

Summarized entries from prior sessions (< 2026-05-03).

## May 2–3: Auth Backchannel Hardening & Codespaces Diagnostics

- **2026-05-02:** Reviewed PR #45 (Codespaces URL derivation fix); validated 11 regression tests
- **2026-05-02:** Tested Codespaces 401 downstream auth backchannel hardening (commit 7a9b1c3)
- **2026-05-03 Startup phase:** Validated startup helper, URL regression suite, downstream diagnostics coverage
- **2026-05-03 Early:** Diagnosed live Codespaces 404/blank page (Aspire stack down); provided AppHost restart guidance

**Key learnings:** When all Aspire ports return GitHub-tunnel 404 with 0-byte response, AppHost is not running (not app error). Port 3000 is health canary.

All prior work maintained robust test coverage; codespaces-specific diagnostics framework in place.

