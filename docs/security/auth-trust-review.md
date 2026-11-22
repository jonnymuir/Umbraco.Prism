# Authentication and trust review

A review of how Prism establishes trust, authenticates members and authorises requests, where it
leans on standard .NET libraries, and where it implements a standard itself. It is a findings
register: each item states what is true of the code now, and what to do about it. The method used
to produce it is in [the review procedure](auth-trust-review-procedure.md), so the exercise can be
repeated and the register updated.

This is a self-review by Prism's author, assisted by AI tooling. It is not an independent audit
or penetration test, and it is no assurance that Prism is secure. Responsibility for your
deployment stays with you: see [Security responsibility and assurance](responsibility.md).

Scope: `UmbracoPrism.Core` (OIDC, member cookie, tenant resolution, bearer validation, biometric
and mobile sign-in, secrets, page access, admin APIs), the host wiring a consumer must supply
(`UmbracoPrism.TestSite` as the reference), `UmbracoPrism.MockBusinessApp` as the downstream API,
and the CI/CD pipeline. Wayfinder and Wayfinder.Umbraco are out of scope.

Severity is the reviewer's estimate of impact times likelihood for a typical production host.
Items marked *verify* rest on library defaults or behaviour that the reviewer could not confirm
by running code.

## 1. The basis of trust

Trust flows down this chain. Every link is something an attacker would like to control.

| # | Root of trust | Established by | Who can change it |
|---|---|---|---|
| 1 | Which tenant a request belongs to | The `Host` header, looked up in `prismTenants` (unique hostname index), cached 30 minutes | Anyone who can send a request picks a hostname; only a Prism admin, a uSync import or the config seeder can create a row |
| 2 | Which identity provider that tenant trusts | `OidcAuthority` / `EntraTenantId` + client id on the tenant row, with `{{TOKEN}}` placeholders resolved from configuration | Prism admins (backoffice), uSync files, config |
| 3 | The provider's signing keys | JWKS fetched over HTTPS from the authority's discovery document, cached in memory (45 min refresh, 1 h hard expiry, refresh attempts limited to one per 10 seconds per authority) | The provider, and anything that can redirect the fetch |
| 4 | That a person signed in | ID token validated in `OnAuthorizationCodeReceived`: signature, issuer, audience, lifetime, nonce. PKCE and the state/correlation cookie from the OIDC handler | The provider |
| 5 | That a session is still theirs | A `__Host-prism.sid` cookie holding a random reference, protected by ASP.NET Data Protection; the ticket itself (claims and tokens) sits in a server-side store, also protected by Data Protection | Whoever holds the Data Protection key ring, and the session store's operator |
| 6 | That the session belongs to this tenant | `PrismTenantBindingValidator` on every request (`tid`, or `iss` plus `aud`/`azp`) | Nobody, provided the check runs |
| 7 | What the member may see | `PrismPageAccessFilter` + `IPrismPageAccessResolver` (sign-in, tenant allow-list), `PrismStrictIsolation` on JSON APIs | Prism admins |
| 8 | Who may administer | Umbraco backoffice login plus group alias (`PrismAdmins`, default `admin`) | Umbraco administrators |
| 9 | Downstream API callers | JWT bearer: issuer allow-list from tenant config, audience per tenant, per-issuer JWKS | Tenant configuration |
| 10 | A phone holding a biometric credential | HMAC JWT (`BiometricToken`), hash stored server-side, AES-GCM-wrapped refresh token, per-token and per-IP rate limits | Whoever holds the signing and encryption keys, or the token itself |
| 11 | An app receiving a sign-in handover | One-time code, S256 PKCE verifier, Data Protection ticket, iOS/Android verified app links | Whoever controls the tenant's app-link config |

The single most important property is link 6: even if a cookie from tenant A reaches tenant B's
hostname, the request is treated as anonymous (cookie validation) or refused (`PrismStrictIsolation`).
Everything else assumes link 1 is honest, so a wrong hostname-to-tenant mapping is the failure that
matters most.

## 2. Library or home-made, layer by layer

The question is where a vulnerability would be Prism's alone, because nobody else reviews that
code. "Wrapper" means Prism configures a library; "own code" means Prism implements the logic.

| Layer | Standard component used | Prism's own implementation | Roll-your-own risk |
|---|---|---|---|
| Authorization request (redirect, state, correlation, PKCE, nonce) | ASP.NET Core `OpenIdConnectHandler`, Microsoft.Identity.Web | Event hooks that rewrite authority, client id, scope, prompt per tenant; endpoints come from the validated discovery document (`IPrismOidcDiscovery`) | Medium |
| Authorization code redemption | None. The handler's own redemption is bypassed | `OnAuthorizationCodeReceived` posts to the token endpoint with a named `IHttpClientFactory` client (15 second timeout), parses the JSON, builds the ticket | **High**: this is the heart of the flow |
| ID token validation | `JwtSecurityTokenHandler.ValidateToken` with `TokenValidationParameters` | Choice of parameters, nonce comparison, key fetch, `azp` handling (none) | Medium: parameters are right, the surrounding checks are bespoke |
| JWKS retrieval and caching | `ConfigurationManager<OpenIdConnectConfiguration>` | `PrismSigningKeyCache`: its own TTL, attempt-based cooldown, semaphore, backchannel rewriting retriever; a new manager per fetch | Medium: reimplements what the library already does, but now bounded (see AUTH-02) |
| Multi-tenant bearer validation | `AddJwtBearer`, `TokenValidationParameters` | `IssuerValidator`, `AudienceValidator`, `IssuerSigningKeyResolver` delegates (multi-issuer trust) | **High**: no library covers "many issuers chosen at runtime", so this is bespoke |
| Member session | ASP.NET Core cookie authentication, Data Protection | `PrismTicketStore` (the standard `ITicketStore`) over a database or `IDistributedCache` storage; `PrismSessionValidator`: tenant binding, absolute lifetime, periodic provider re-check | Low: the store is a small adapter, the cookie handling is the framework's |
| Tenant resolution | None exists for this model | `TenantService`, `PrismTenantMiddleware`, scheme selectors | **High** (the trust root) |
| Authorization policies | ASP.NET Core authorization | `PrismTenantHandler`, `PrismAdminHandler`, page access resolver and filter | Medium |
| Antiforgery | ASP.NET Core Antiforgery | Per-controller decisions on where to skip it | Low |
| Refresh-token grant | `HttpClient`, Polly | `PrismTokenRefreshService` (form post, parse, circuit breaker) | Medium; MSAL could do this |
| Secret retrieval | Azure SDK `SecretClient` | In-memory caching and name-keyed lookup; failures are not cached | Low |
| Biometric credential | `JwtSecurityTokenHandler` (HS256), `AesGcm`, `SHA256` | The whole protocol: token format, registration, exchange, rotation, revocation, rate limiting, lockout | **High**: a home-made authentication scheme, though built from sound primitives |
| Mobile sign-in handover | `IDistributedCache`, Data Protection `TicketDataFormat`, `RandomNumberGenerator`, `SHA256` | A small authorization-code-plus-PKCE server (issue, take, verify) | **High**: the same shape as an OAuth authorization server |
| CSRF/CORS for the mobile APIs | None | Origin allow-list, manual `Access-Control-*` headers | Medium |
| Rate limiting | `Microsoft.AspNetCore.RateLimiting` for the sign-in endpoints | `ExchangeRateLimitService` (bounded, in process), `NotificationRateLimitService` | Medium |
| Log hygiene | Serilog / `ILogger` | `LogScrub`; an invariant test keeps `Console` out of Core | Low |

Where Prism uses primitives correctly and the finding is only about the protocol around them, the
table still marks the layer, because the protocol is what nobody else has reviewed.

## 3. Findings

OWASP references are to the Top 10 (2021) and, for the token handling, the ASVS V3/V8 chapters.
Only open items are listed. A finding that is fixed is deleted from this register, and a finding
with a residual risk states only what is left.

No High finding is open.

### Medium

**AUTH-10. Biometric credential: a home-made authentication scheme.** A02/A07. The stored refresh
token is bound to its credential, carries a key id and is encrypted under a key derived per
credential; each tenant's tokens are signed with a key derived from the signing key; keys rotate
through `Prism:Biometric:PreviousEncryptionKeys`; a configured vault that lacks the keys stops a
non-Development host; the default lifetime is 14 days. What remains is the design: the credential is a
bearer token (see [proof-of-possession analysis](../proof-of-possession-analysis.md)); one master
signing key and one master encryption key serve every tenant (derived keys separate tenants, they do
not protect against the master leaking); the exchange mints the cookie without re-validating any
token, so the account is only as fresh as the credential row; it works for Entra tenants only. Tokens
signed before derivation validate until they expire unless `AcceptLegacyTokens` is turned off.
Fix: a hardware-bound key with a signed challenge (the proof-of-possession design) when the assurance
level justifies it.

### Low

**AUTH-12 (residual). The reference site's Data Protection keys are unprotected.** A02/A05. The member
cookie, the mobile handover ticket and antiforgery tokens are only as strong as the key ring. Prism
refuses to start outside Development unless the host has chosen where keys are stored and how they
are protected, with one exception: the reference site sets
`Prism:DataProtection:RequireConfiguredKeys` to `false`, so it logs a warning and runs with the
framework's default key store, unencrypted.
Fix: persist the reference site's keys to a chosen directory and protect them with a certificate or
Key Vault, then remove the opt-out. Revisit when the reference site holds anything beyond demo data.

**AUTH-09 (residual). Rosters and demo records are matched by verified email.** A07. A signed-in
member owns their service requests, and their registered devices and journey statuses, under
`ClaimsPrincipal.GetSubject()` (a stable `{tid}:{oid}` or `{issuer}|{sub}`), not their email. What
still uses the identity-provider-verified email is the reference site's caseworker roster and its
demo systems of record (the membership tier and the savings record), because a roster written before
anyone has signed in can only name people by address. The email counts only when the provider vouches
for it (`GetEmailAssertion`), so a login name a person chose cannot match.
Fix, for a host with a real roster: record the subject against the roster entry at first verified
sign-in and match on that afterwards.

**AUTH-02 (residual). The signing-key cache is still bespoke.** A04. Fetch attempts per authority
are limited (one per 10 seconds for an unknown key id, one per 30 seconds otherwise, failures
included) and the HTTP client times out after 10 seconds, but the cache still builds a fresh
`ConfigurationManager` per fetch and the key resolver still blocks on it.
Fix: one long-lived `ConfigurationManager<OpenIdConnectConfiguration>` per authority with its
`RequestRefresh()`, and a scheduled pre-warm so the resolver never waits on the network.

**AUTH-03 (residual). Session revalidation has edges.** A07. Sessions slide for 8 hours, end after 7
days and are re-confirmed with the provider every 15 minutes. Two requests racing at the interval
boundary against a provider that rotates refresh tokens with reuse detection can sign a member out
(they sign in again). A session with no refresh token has only the two time limits. The biometric
credential's own stored refresh token is not revoked when the member signs out (see AUTH-10).

**AUTH-06 (residual). Tenant identity validation does not resolve DNS.** A10. The validator refuses
non-https authorities, credentials, query strings, and loopback, private and link-local literals and
well-known internal names. A public name that resolves to a private address is not caught.
Fix: resolve and check at connect time with a `SocketsHttpHandler.ConnectCallback`, or egress
filtering at the network edge.

**AUTH-11 (residual). Rate limits are per process and per address.** A04. The sign-in limiter and the
biometric exchange limiter do not span instances, and the address is the proxy's unless the host
configures forwarded headers. Fix: document it (done) and use a shared limiter if the host scales out.

**AUTH-15 (residual). Mobile handover codes depend on a shared cache.** A code is claimed in process
before the cache is read, so concurrent redemptions in one process cannot both succeed. The default
`AddDistributedMemoryCache` is per instance; a multi-instance host must register a shared cache or
sign-in fails intermittently, and across instances the S256 verifier is the only second check.
Fix: an atomic get-and-delete (Redis `GETDEL`) behind a small interface.

## 4. Mitigating "roll your own"

In order of return:

1. **Replace, do not tune.** Two items still have a standard answer: one long-lived
   `ConfigurationManager` per authority instead of `PrismSigningKeyCache`'s fetch logic (AUTH-02).
   It deletes security-relevant code rather than adding to it. Endpoint discovery, the framework
   rate limiter, the shared signing-key cache for the ID token and the server-side `ITicketStore`
   have been moved onto the libraries already.
2. **Keep the inevitable parts small and boxed.** Multi-issuer bearer validation, tenant resolution
   and the tenant binding check have no library equivalent. Keep each in one class, with one
   shared implementation (the binding validator and `PrismTenantValidator` already are), and no
   second copy of any comparison.
3. **Hold the protocol to its specification with tests named after the RFC clause.** The code
   redemption, the biometric exchange and the mobile handover each need a table of "must reject"
   cases: wrong nonce, wrong audience, wrong issuer, expired, replayed, wrong tenant, wrong device,
   missing verifier. Most exist; section 5 lists the gaps.
4. **Encode the invariants as tests that fail on drift.** `AuthInvariantTests` fails if token
   validation is switched off, if Core writes to the console, or if it creates an `HttpClient`
   directly. Still to add: every endpoint has an explicit authorization requirement or an entry on a
   reviewed anonymous allow-list (the MockBusinessApp's deny-by-default fallback policy is the model).
5. **External review of the protocol, not the code.** The three bespoke protocols (code
   redemption, biometric, mobile handover) are small enough for a one-off third-party review;
   that is a better use of money than a generic pen test.
6. **Say what is custom in the package README and `SECURITY.md`**, so consumers and reporters know
   where to look.

## 5. Pipeline coverage

What protects the repository now, and what it does not.

| Control | State |
|---|---|
| Branch ruleset on `main` | Active, no bypass actors, PR required (0 approvals, solo), up-to-date branch required, no force push or delete. Required checks: `security / security-all`, `ci-tests-all`, `zizmor`, `CodeQL`, `baseline / DAST baseline` |
| CodeQL (`csharp`, `javascript-typescript`, `security-extended`) | Runs on every PR and weekly; required as a check run |
| Vulnerable NuGet gate | Blocking at High/Critical, with a reviewed allow-list (`.github/allowed-advisories.txt`); weekly re-scan |
| npm audit | Production dependencies of both manifests, at High, on every PR and weekly |
| Dependency review | PR only, fails at High |
| Secret scanning | Platform scanning and push protection on; `gitleaks dir` in CI (working tree only) |
| Actions hardening | Actions pinned to SHA, default workflow token read-only, zizmor required, Dependabot with a 7 day cooldown |
| DAST | ZAP baseline against the booted Aspire stack, blocking, unauthenticated |
| Release | NuGet Trusted Publishing (no long-lived key), version/tag check |
| Tests | Unit security regression suites in `Core.Tests` (including cross-tenant cookie isolation and the auth invariants); a booted-host suite in `Core.IntegrationTests` (including two tenants on two realms, where one tenant's member cookie and biometric token are refused on the other); Playwright live-auth lane |

Gaps, in order of importance:

- **P-02. DAST is unauthenticated.** The OIDC flow, member pages, biometric endpoints and the
  mobile handover are never scanned. Add an authenticated ZAP context (Keycloak demo user) or a
  scripted run of the live-auth lane through the ZAP proxy.
- **P-04. Release has no approval gate or tag protection.** Any `v*` tag push or manual dispatch
  can publish three packages, and there is no tag ruleset or GitHub Environment with a required
  reviewer. Add both, and pin the nuget.org trusted-publishing policy to that environment.
  Consider build provenance (`actions/attest-build-provenance`) and an SBOM attached to releases.
- **P-05. Deploy uses password SSH.** `deploy-prism-reference.yml` and
  `deploy-mock-business-app.yml` authenticate to the VPS with `PRISM_VPS_PASSWORD` and are not
  bound to an Environment. Move to a key pair, pin the host key, and use an Environment with a
  reviewer. Both are manual-dispatch only, which is the right posture.
- **P-06. Invariants cover only the first few smells.** The auth invariant tests exist; a small
  Semgrep ruleset or more invariants (anonymous endpoint allow-list) would extend them.
- **P-07. `gitleaks` scans the tree, not history.** Low, since push protection is on; run
  `gitleaks git` once, and again after any history rewrite.
- **P-08. Required reviews are zero.** Acceptable for a solo project, and it means the automated
  checks are the only reviewer. This register is itself part of that compensation; rerun it on a
  schedule.

## 6. What is already right

Worth stating so it is not undone.

- The ID token is validated on every sign-in with signature, issuer, audience, lifetime and a
  mandatory nonce; the middleware's own correlation cookie and PKCE are in place, the discovery
  document's issuer must equal the configured authority, and an RFC 9207 `iss` response parameter
  must match it too.
- Tenant binding runs on every request from one shared implementation, and unknown hosts fall
  back to the host application's own authentication rather than Prism's.
- A tenant identity that fails `PrismTenantValidator` loses its sign-in, whatever wrote the row.
- Page access fails closed: a failed policy lookup denies the page and caches nothing.
- Member sessions are bounded and re-confirmed with the provider.
- Return URLs go through a local-URL check; logout is POST with antiforgery.
- Inline client secrets and the demo tenant behaviours are refused outside Development.
- Page-access denial is a plain 404, not a revealing redirect.
- The mobile handover stores only a hash of the code, expires in two minutes and requires the
  PKCE verifier; verification uses fixed-time comparison.
- The biometric token's hash, not the token, is stored; lifetime, revocation, tenant, device and
  user are all checked; failure responses are generic; stored refresh tokens are bound to their
  credential.
- The mock business API denies by default and rate limits per caller.
- The supply-chain posture (SHA-pinned actions, trusted publishing, cooldowns, branch ruleset with
  no bypass) is well above the norm for a solo project.

## 7. Suggested order of work

1. P-04 and P-05: release approval and key-based deploys. They need repository settings and
   secrets rather than code.
2. AUTH-12 (residual): protect the reference site's Data Protection keys and remove its opt-out.
3. AUTH-02 (residual): the long-lived `ConfigurationManager`, which deletes bespoke code.
4. AUTH-10: proof of possession, when the assurance level justifies it.
5. Pipeline P-02: an authenticated DAST run.

Each change reaches `main` through a pull request; a breaking behaviour change (for example
mandatory `Audience`) needs a `BREAKING CHANGE:` line in the commit.
