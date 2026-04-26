# Session Log: Auth Failure Investigation — 2026-04-14T19:12:55Z

## Summary

Investigated GitHub Actions run 24415783660 (`localhost-auth-playwright` lane). Two background agents completed work; both converged on the same finding: **Linux certificate trust bootstrap failure in workflow, not product/Playwright regression**.

## Agent Outcomes

- **Tangy:** Identified first meaningful failure at Trust .NET development certificate step; `dotnet dev-certs https --trust` exit code 4; missing `SSL_CERT_DIR` configuration.
- **Blathers:** Confirmed workflow/bootstrap setup is sound; traced required fix to minimal ci-tests.yml change for Linux certificate trust wiring.

## Next Steps

Update `.github/workflows/ci-tests.yml` to export/persist `SSL_CERT_DIR` on Ubuntu runners before `dotnet dev-certs https --trust`, then rerun the lane.

## Decisions Merged

- tangy-auth-failure-investigation.md → decisions.md (deduplicated)
- blathers-auth-failure-investigation.md → decisions.md (merged with complementary Tangy perspective)

---

**Scribed by:** Scribe at 2026-04-14T19:12:55Z
