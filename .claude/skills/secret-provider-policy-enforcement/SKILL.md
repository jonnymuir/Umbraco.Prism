---
name: "secret-provider-policy-enforcement"
description: "Keep secret-provider restrictions consistent across validation, migration, caching, and runtime resolution paths"
domain: "security"
confidence: "high"
source: "earned"
---

## Context
Use this when a project supports multiple secret providers (for example Key Vault plus a localhost-only inline demo path) and you need to guarantee production flows never silently fall back to unsafe storage.

## Patterns
- Enforce provider restrictions in **every** runtime consumer, not just in controller/model validation.
- Audit all paths that can materialize the secret: migrations, ORM/schema backfills, cached domain models, login code, refresh code, and helper services.
- If a provider is only allowed for a demo/dev exception, add one shared predicate (for example `IsRepoOwnedLocalDemoTenant(...)`) and make all runtime resolution paths fail closed when it returns false.
- Migration compatibility should not reactivate legacy raw secrets in production; either null them, convert them to safe references, or block startup until they are remediated.
- Add regression tests for each resolution path, not only the initial login path.

## Examples
- In Umbraco.Prism, `PrismOidcConfiguration.ResolveClientSecretAsync(...)` correctly fails closed for inline secrets outside the localhost demo path, but `PrismContext.RefreshTokenAsync(...)` separately called `ISecretVaultService.ResolveSecretAsync(provider, reference)` and would still honor inline secrets unless the same policy was enforced there too.
- `AddOidcSecretProviderColumns` is part of the security surface because migration backfills can reintroduce secrets into active runtime fields.

## Anti-Patterns
- Validating provider rules only in management APIs and assuming runtime code will stay aligned.
- Migrating legacy raw secrets into active inline references for production tenants.
- Hiding secret references from API responses while still honoring unsafe secret storage internally.
