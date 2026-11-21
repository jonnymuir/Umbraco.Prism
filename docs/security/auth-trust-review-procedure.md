# Authentication and trust review: procedure

How to repeat the [authentication and trust review](auth-trust-review.md). It is written for any
reviewer, human or tool. Run it at least before each release that touches `UmbracoPrism.Core`
authentication, tenancy, mobile sign-in or secrets, and quarterly otherwise. Update the register
in place: change a finding's text when the code changes, delete it when fixed, add new ones.

## 1. Fix the scope and baseline

1. Record the commit reviewed and the package version.
2. List the auth surface so nothing is skipped:
   `git ls-files src/UmbracoPrism.Core | grep -iE 'auth|oidc|token|tenant|jwt|session|secret|biometric|cookie|policy'`
   and every controller (`grep -rn "\[Authorize\|\[AllowAnonymous\|\[IgnoreAntiforgeryToken" src`).
3. Read the previous register so each earlier finding is marked fixed, still open or changed.

## 2. Walk the trust chain

For each row of the trust table in the register (hostname, tenant config, signing keys, ID token,
cookie, tenant binding, authorization, admin, bearer, biometric, mobile handover), answer:

- What exactly establishes trust, and which line of code does it?
- Who or what can change the input? Include admins, uSync files, configuration, environment
  variables, the `Host` header and unauthenticated callers.
- What happens when the lookup or the check fails? Fail open is a finding.
- Is it checked on every request, or only at sign-in?

Trace one real request end to end for each of: browser sign-in, session request on another
tenant's host, JSON API call, bearer call to the downstream API, biometric exchange, mobile
handover.

## 3. Inventory library versus own code

For each layer in the register's second table, record the standard component and what Prism wrote
itself. Flag any new code that re-implements a library feature (token parsing, JWKS caching, rate
limiting, CSRF, crypto envelopes). Searches that find it:

```
grep -rnE "new HttpClient\(|GetAwaiter\(\)\.GetResult\(|Console\.Write|JsonDocument\.Parse|Convert\.FromBase64" src/UmbracoPrism.Core
grep -rnE "ValidateIssuer *= *false|ValidateAudience *= *false|ValidateLifetime *= *false|RequireSignedTokens *= *false|ValidateIssuerSigningKey *= *false" src
grep -rnE "AesGcm|HMAC|SHA256|RandomNumberGenerator|SecurityAlgorithms" src/UmbracoPrism.Core
```

Home-made protocol code gets the highest scrutiny: write down its messages, its replay story and
its rate limits.

## 4. OWASP checklist

Walk each category against the package, not only the reference site.

| Category | Questions specific to Prism |
|---|---|
| A01 Broken access control | Any lookup failure that returns "allowed"? Every endpoint has an explicit requirement or is on the anonymous allow-list? Cross-tenant cookie, token and credential refused? IDOR on device, push and policy ids? |
| A02 Cryptographic failures | Keys from a vault or config, rotated how? Envelope has key id and associated data? Data Protection ring persisted and encrypted? Secrets absent from logs and responses? |
| A03 Injection | Tenant fields interpolated into URLs, SQL, HTML, CSS, JavaScript? Validated on every write path including uSync and seeders? |
| A04 Insecure design | Unauthenticated work that costs the server (network fetches, blocking calls, unbounded dictionaries)? Any rate limit that is per process only? |
| A05 Misconfiguration | Every environment variable and option that changes trust: who can set it, is it gated by environment, does a consumer inherit the guard? Debug output in production? Unknown hostnames? Headers, cookies, CORS? |
| A07 Authentication failures | Session lifetime, revalidation, logout, refresh-token revocation, identity key (stable subject, not email), nonce, PKCE, state, replay |
| A08 Integrity | Unsigned configuration imports, package signing, update channels, CI trust |
| A09 Logging | Attacker-controlled text unscrubbed? Security events (failed exchange, tenant mismatch) logged with enough context? |
| A10 SSRF | Every server-side fetch whose address an admin, claim or config value can influence |

## 5. Check the pipeline

Read, do not assume:

1. `gh api repos/{owner}/{repo}/rulesets` and `rules/branches/main`: required checks, bypass
   actors, tag rules. Confirm each required check name still exists in a workflow.
2. Every workflow in `.github/workflows`: permissions, triggers (`pull_request_target`,
   `workflow_run`), secret use, third-party actions pinned to a SHA, environments.
3. The reusable security workflow the repo calls, at the pinned SHA: what is blocking, what is
   advisory.
4. Release and deploy: who can trigger, what approval, what credential, what provenance.
5. Dependency, secret and DAST coverage against the surface from step 1: is anything on the auth
   surface not exercised dynamically?
6. `gh api repos/{owner}/{repo} --jq .security_and_analysis` for secret scanning and push
   protection.

## 6. Verify before you report

- A finding about behaviour the reviewer has not run is labelled *verify*.
- Prefer a failing test to a paragraph. For each High finding, write the test that would catch it
  and add it to `Core.Tests` or `Core.IntegrationTests` when the fix lands.
- Check a claimed library default against the library source or a booted host before calling it a
  vulnerability.

## 7. Write up

Keep the register's shape: trust table, library versus own code, findings by severity with
location, impact and fix, pipeline gaps, what is right, suggested order. State current behaviour,
not history. Severity is impact times likelihood for a typical production host, not the reference
site. Do not include working exploit steps; describe the class of weakness and the fix.

## 8. Close the loop

- Open a pull request for the register update.
- For each accepted finding, open an issue that links the finding id.
- Add any new invariant to the test suite (see "Encode the invariants" in the register).
- If a finding is accepted rather than fixed, record the reason and a revisit condition next to it,
  as `.github/allowed-advisories.txt` does for dependencies.
