---
name: "auth-trust-review"
description: "Repeat the full authentication, multi-tenant OIDC and authorisation trust review of Prism, including library versus home-made code and CI pipeline coverage"
domain: "security"
confidence: "high"
source: "observed"
---

## Context

Use this skill before a release that touches `UmbracoPrism.Core` authentication, tenancy, mobile
sign-in or secrets, after any change to the OIDC, bearer, biometric or mobile handover code, and
on a regular schedule. The method and the findings live in the repo so any tool can use them.

## Patterns

1. Read `docs/security/auth-trust-review-procedure.md` and follow it step by step.
2. Read `docs/security/auth-trust-review.md`, the current register. Mark each finding fixed, open
   or changed against the code at the commit you are reviewing.
3. Update the register in place: edit changed findings, delete fixed ones, add new ones with the
   next `AUTH-nn` or `P-nn` id.
4. Run the searches in procedure step 3 and the pipeline reads in step 5.
5. Where a finding is High, write the failing test and note where it belongs.

## Anti-Patterns

- Reporting a library default as a vulnerability without checking it (label it *verify*).
- Reviewing only the reference site; the package is what consumers inherit.
- Narrating history in the register; it states current behaviour only.
- Using em dashes in any output; use a comma or full stop.
