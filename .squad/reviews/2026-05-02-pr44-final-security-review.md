# PR #44 — Final Security Review (Copper)

**Date:** 2026-05-02
**Branch:** `fix/codespaces-401-downstream-auth`
**Reviewer:** Copper (Security/Auth)
**Scope:** Full diff `main..HEAD` (3 source commits + tests + squad meta)

## Bedrock invariants

| # | Invariant | Status | Notes |
|---|-----------|--------|-------|
| 1 | No `RequireHttpsMetadata = false` introduced | ✅ | No occurrences in diff. PrismAuthExtensions still uses `RequireHttpsMetadata = true` (line 73 area). Production path test `ProductionPath_RequireHttpsMetadata_IsTrue` proves it. |
| 2 | No `Validate{Issuer,Audience,Lifetime,IssuerSigningKey} = false` introduced | ✅ | Diff is clean. PrismAuthExtensions JwtBearer keeps all four `= true`. The pre-existing `ValidateIssuer/Audience = false` initial state in `PrismOidcConfiguration.PostConfigure` (lines 145–146) is **out of scope** — already overridden to `true` inside the `IssuerSigningKeyResolver` per-request and is not touched by this PR. |
| 3 | No `ServerCertificateCustomValidationCallback` / cert bypass | ✅ | No occurrences anywhere in repo. |
| 4 | No suffix-trust of `*.app.github.dev` / wildcard hosts | ✅ | No new wildcard trust. Pre-existing `EndsWith(".app.github.dev")` checks in `PrismOidcConfiguration.cs:80,100` are scope-limited (Codespaces detection in `IsValidLocalAuthority`) and unchanged. |
| 5 | No `IsPrincipalBoundToCurrentTenant` weakening | ✅ | Function untouched (PrismContext.cs:235). Both call sites (lines 54, 104) unchanged. |
| 6 | No Development-only "skip tenant binding" branches | ✅ | `IsDevelopment` is consulted **only** to gate transport rewrites; never to bypass principal/tenant binding or any validation. |
| 7 | All three rewrite sites dual-gated (`KEYCLOAK_BACKCHANNEL_URL` AND `IsDevelopment()`) | ✅ | (a) `PrismContext.RefreshTokenAsync` lines 138–143; (b) `PrismSigningKeyCache.WarmAsync` lines 159–166; (c) `PrismAuthExtensions.ResolveSigningKeys` lines 234–240 (Tester's discovered fix). All three use the same `ASPNETCORE_ENVIRONMENT == "Development"` ordinal check + non-empty env var. |
| 8 | Issuer claim validation against PUBLIC OidcAuthority remains strict | ✅ | `ValidIssuer = tenant.OidcAuthority` is unchanged (PrismOidcConfiguration line 170, PrismAuthExtensions IssuerValidator). The `BackchannelRewritingDocumentRetriever` rewrites the *transport address* before the HTTP call but does not touch `validationParameters` or `tenant.OidcAuthority`. Tests `JwtValidation_StillRejectsTokenWithMismatchedIssuer_EvenWhenJwksFetchedFromBackchannel` and `RefreshTokenAsync_StillValidatesIssuerOnRefreshedToken` actively prove this. |
| 9 | Fail-loud guard at `MockBusinessApp/Program.cs:38-41` untouched | ✅ | `git diff main...HEAD -- src/UmbracoPrism.MockBusinessApp/Program.cs` returns 0 lines. Guard verified in source and asserted by `MockBusinessApp_FailLoudGuard_ExistsAndWouldThrow_WhenBackchannelSetInProduction`. (Equivalent guard also present in `TestSite/Program.cs:29-31`.) |
| 10 | Production path byte-equivalent to pre-PR | ✅ | All three rewrite blocks are guarded by `if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))`. Outside that guard the original assignments / factory call execute unchanged. Verified by tests `*_DoesNotRewrite_WhenBackchannelUnset` and `*_DoesNotRewrite_WhenNotDevelopment`. |

## Per-file findings

### `src/UmbracoPrism.Core/Models/PrismContext.cs` (Copper, e0e8ee3)
- 20 lines added inside `RefreshTokenAsync`. Token endpoint is computed first against `OidcAuthority`, then conditionally overwritten. Path-extraction via `new Uri(...).AbsolutePath` is safe (input is already validated as the configured authority). Console log echoes the rewritten URL — fine for dev, redacts no secret material.
- ✅ No issuer/audience/transport trust changes.

### `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs` (Blathers, 4a47acc)
- New `BackchannelRewritingDocumentRetriever` wraps `IDocumentRetriever`. Origin match uses `Uri.GetLeftPart(UriPartial.Authority)` → scheme + host + port, with `OrdinalIgnoreCase`. Activation also requires `publicUri.Scheme == Uri.UriSchemeHttps`, ensuring we never rewrite a non-HTTPS authority (defence-in-depth).
- Rewrite triggers on `address.StartsWith(publicOrigin, OrdinalIgnoreCase)`. **Theoretical edge:** an address like `https://kc.example.com.evil.com/...` would textually start with `https://kc.example.com` and be rewritten. This is benign — it would only matter if the trusted Keycloak's discovery doc returned an attacker-controlled `jwks_uri`, in which case the threat model is already breached, and the *worst* this rewrite can do is route the malicious URL through the dev backchannel base instead of the public host. Issuer validation still gates trust. Out-of-scope follow-up below.
- Else-branch falls through to `_configurationManagerFactory(http, metadataAddress, requireHttps)` → identical to pre-PR behaviour.
- Includes diagnostic `Console.WriteLine` — fine.

### `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs` (Tester)
- Added the `isDevelopmentForJwks` gate so the `metadataAddress` rewrite at `ResolveSigningKeys` is now dual-gated. **This closes a real (narrow) vulnerability** present pre-PR: a non-Development environment with `KEYCLOAK_BACKCHANNEL_URL` set (e.g. via container env leak) would have caused JWKS metadata fetches to be silently redirected to whatever URL that variable contained, with `RequireHttps` derived from the rewritten URL — i.e. an attacker who could plant the env var could redirect the JWKS endpoint to `http://attacker/...`. The MockBusinessApp/TestSite fail-loud guards mitigated this for those hosts, but a downstream consumer of `UmbracoPrism.Shared` would not necessarily have the guard. With this gate, even a leaked env var has zero effect outside Development.
- ✅ Fix is correct and minimal.

### `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs` (Tester, ba14053)
- 11 tests, organised A/B/C. Reviewed name-vs-assertion alignment for each:
  - `*_RewritesTokenEndpoint_WhenBackchannelSetAndDevelopment` / `*_DoesNotRewrite_WhenBackchannelUnset` / `*_DoesNotRewrite_WhenNotDevelopment` — assertions match names; the "not Development" cases set `ASPNETCORE_ENVIRONMENT=Production` AND set `KEYCLOAK_BACKCHANNEL_URL`, then assert the captured endpoint **starts with `OidcAuthority`** AND **does not contain `BackchannelUrl`** — i.e. they prove the safety property under the precise hostile configuration.
  - `RefreshTokenAsync_StillValidatesIssuerOnRefreshedToken` & `JwtValidation_StillRejectsTokenWithMismatchedIssuer_EvenWhenJwksFetchedFromBackchannel` — invoke the configured `IssuerValidator` directly with an `evil.example.com` issuer and assert `SecurityTokenInvalidIssuerException`. Solid trust-boundary proof.
  - `ProductionPath_RequireHttpsMetadata_IsTrue` & `JwtBearer_ValidateIssuerAndAudience_AreTrueInOptions` — read the resolved `JwtBearerOptions` from the DI container; correct.
  - `MockBusinessApp_FailLoudGuard_ExistsAndWouldThrow_WhenBackchannelSetInProduction` — read-only source check; pragmatic and valuable as a tripwire.
- All env-var mutating tests are decorated `[Collection(EnvVarSensitiveTestCollection.Name)]`, with `TempEnvVar` IDisposable restoring originals — process-wide leakage is properly contained.

### `src/UmbracoPrism.Core.Tests/EnvVarSensitiveTestCollection.cs` (Tester)
- Standard xUnit `CollectionDefinition` with `ICollectionFixture<self>`. Correct serialisation primitive for env-var mutating tests.

### `src/UmbracoPrism.Core.Tests/PrismSigningKeyCacheTests.cs`
- One-line addition (collection attribute) — same serialisation rationale.

### `.squad/*` files
- History/decisions/skill — non-code, no security implications.

## New vulnerabilities introduced

**None.** The PR is purely additive on the dev path and idempotent on the prod path.

## Pre-existing vulnerabilities reduced

- `PrismAuthExtensions.ResolveSigningKeys` JWKS rewrite was previously env-var-only-gated. Tester's `IsDevelopment` gate eliminates the residual "leaked env var redirects JWKS" surface in non-Development environments for downstream consumers of `UmbracoPrism.Shared`.

## Out-of-scope follow-ups (do NOT bundle)

1. **Origin StartsWith hardening** in `BackchannelRewritingDocumentRetriever`: tighten the prefix match by appending `/` to `publicOrigin` (or comparing parsed `Uri.Authority`) so `https://kc.example.com.evil.com/...` cannot textually match. Low risk (dev-only path, threat model assumes trusted authority), but cheap defence-in-depth.
2. **Centralise the dual-gate**: three near-identical "isDevelopment && !empty" blocks in three files. Extract a single `PrismBackchannel.TryRewrite(publicAuthority, out string rewritten)` helper to ensure future rewrite sites can't drift (e.g. forget the Development gate, as nearly happened in PrismAuthExtensions).
3. **No other server-side Keycloak fetches** were found that need rewriting:
   - Token introspection / userinfo: not invoked server-side in this codebase.
   - End-session / logout: built only for browser redirect (`DownstreamDemoController.cs:312`), not a server-side call.
   - `/debug/auth` probe in MockBusinessApp manually targets the backchannel and is `IsDevelopment()`-gated at line 192.
4. Unrelated nullable/obsolete warnings (PrismAuthExtensions:34, PrismComposer:67, LocalhostGenericOidcRegressionTests:254) — pre-existing, not in scope.

## Build / test verification

- `dotnet build UmbracoPrism.sln -c Release` → **succeeded**, 0 errors, 5 pre-existing warnings.
- `dotnet test … --filter FullyQualifiedName~UmbracoPrism.Core.Tests` → **642 passed, 0 failed, 0 skipped**.

## Final verdict

# ✅ APPROVE-FOR-MERGE

All ten bedrock invariants hold. The transport reroute is strictly a dev-only convenience, dual-gated, fail-loud-on-misconfig, and proven by 11 dedicated regression tests including critical "DoesNotRewrite_WhenNotDevelopment" safety tests. Tester's discovered missing gate on the third rewrite site is fixed. Trust boundary (issuer/audience) is unchanged.

Do not merge — leaving CI to run, then Jonny to merge.
