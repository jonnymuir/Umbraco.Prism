# Decision: CI Fix Verification — Both Fixes Confirmed Green

**Date:** 2026-05-17T18:30:56.987+01:00  
**Author:** Tangy (Tester)  
**Branch:** `feat/workflow-editor-library-extraction`  
**Status:** ✅ Confirmed Green

---

## Context

PR #53 had two concurrent CI failures diagnosed in the previous session:

1. **`core-tests` failure** — `TestSiteAppsettingsSecretGuardTests` caught a re-leaked `Umbraco:CMS:Imaging:HMACSecretKey` in `appsettings.json`. Blathers' extraction commit `9ab9ba4` re-introduced the burned key from a pre-security-fix base. Fix: commit `47a50cf` removed the key.

2. **`planning-workflow-editor-smoke` failure** — Transient timeout; Aspire + Umbraco cold-start exceeded the 5-minute readiness window on a slow CI runner. Fix: commit `125f166` increased `readinessTimeoutMs` to 480,000 ms (8 min) and raised the smoke job `timeout-minutes` from 10 → 15.

---

## Verification

Both fixes were already committed and pushed before this session. HEAD at time of verification: `125f166`.

| Commit | Fix |
|--------|-----|
| `47a50cf` | Removed re-leaked HMAC key from `appsettings.json` |
| `125f166` | Increased smoke readiness timeout 5 min → 8 min; job cap 10 min → 15 min |

---

## CI Outcome

**Run IDs:** `25997011837` (CI Tests) and `25997011833` (Squad CI) on commit `125f166`

| Job | Outcome |
|-----|---------|
| `core-tests` | ✅ success |
| `planning-workflow-editor-smoke` | ✅ success |
| `localhost-auth-playwright` | ✅ success |
| `storybook-tests` | ✅ success |
| `marketplace-description` | ✅ success |

All five CI jobs are green on HEAD. The branch is ready for merge review.

---

## Decision

The two diagnosed fixes are verified correct by CI. No further code changes are needed before merge. The branch `feat/workflow-editor-library-extraction` is in a fully green state.

**Team impact:** Any agent reviewing PR #53 can treat CI as a trustworthy green signal — the failures were real bugs (re-leaked secret, marginal timeout), both are now fixed with evidence.
