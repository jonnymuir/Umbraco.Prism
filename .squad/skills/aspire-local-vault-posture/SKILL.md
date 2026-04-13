---
name: "aspire-local-vault-posture"
description: "How to evaluate whether an Aspire local stack should ship a vault by default or keep vault support optional"
domain: "security-architecture"
confidence: "high"
source: "earned"
---

## Context

Use this when a repo has an Aspire-based local stack and someone proposes adding a vault service/container so secrets are “secure from day one” in local development.

## Patterns

### Separate demo-secret convenience from real-secret protection

- If the local stack ships a repo-owned demo identity provider, demo client, and demo secret, treat that as an explicit local exception.
- Do not claim a security win by moving the same repo-known demo secret into a local vault container.

### Keep the default local path press-play

- Default Aspire stacks should favor clone-and-run reliability.
- Every required vault service adds bootstrap/auth/seed/troubleshooting complexity and increases first-run failure modes.

### Use a provider abstraction, not a hard dependency on one vault

- Model real tenant secrets as references/aliases, not raw secret values in tenant storage.
- Resolve those references through a secret-provider abstraction so production can use Azure Key Vault while local dev can use user-secrets, environment variables, or another provider.

### Prefer optional local secret sources over mandatory local vaults

- Aspire parameters, .NET user-secrets, and environment variables are good low-friction local override paths.
- They are config-source patterns, not true vaults, but they often solve the actual developer need without new infrastructure.

### Call out residual risk honestly

- A local vault still needs bootstrap credentials somewhere.
- Secrets may still pass through env vars, config, logs, or container state.
- Local workstation compromise still defeats any local-only vault.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` currently keeps the local stack simple: Keycloak container + HTTPS proxy + TestSite/MockBusinessApp wiring, with no vault dependency.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` seeds the repo-owned Keycloak demo tenant with `prism-dev-secret`.
- `keycloak/realm-export.json` contains the matching demo client secret, showing why a default local vault would not materially improve secrecy for this path.
- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` and `src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` show the real security gap: generic OIDC secrets are still handled as raw values rather than references.

## Anti-Patterns

- Making Azure Key Vault a fresh-clone requirement for local Aspire demos.
- Adding a seeded local vault container and calling that “secure by default” when the demo secret is still repo-known.
- Treating a local vault as a substitute for refactoring the tenant model to use secret references/provider-backed resolution.
