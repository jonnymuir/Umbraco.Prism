# Adding Prism to Your Umbraco Site

This guide walks you through adding Umbraco Prism to an existing Umbraco v17+ site (or bootstrapping it in a greenfield project). The process is designed to be minimal: install the package and Prism handles the rest — there is no service registration to write.

## 1. Install the NuGet Package

In your Umbraco project:

```bash
dotnet add package UmbracoPrism
```

Two optional add-ons, released with the same version as `UmbracoPrism`:

- **`UmbracoPrism.uSync`** exports and imports tenants and page access policies with uSync. Install
  it if you move settings between environments with uSync: without it, each environment's tenants
  and access rules have to be set up by hand. See its
  [README](../src/UmbracoPrism.uSync/README.md).
- **`UmbracoPrism.Automate`** adds a "Send Prism push notification" action to Umbraco Automate.
  See its [README](../src/UmbracoPrism.Automate/README.md).

## 2. Nothing to Configure in Program.cs

Prism registers itself via `PrismComposer`, an Umbraco `IComposer` that `.AddComposers()` — the
same call every Umbraco site already makes to pick up its own composers — discovers
automatically. There is no `AddPrism()`-style call to add; if your `Program.cs` already looks
like this, Prism is already wired up the moment the package reference is added:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()   // <- picks up PrismComposer automatically, nothing else needed
    .Build();
```

### Optional: Key Vault for Production

If using Azure Key Vault in production, Prism loads secrets automatically when you add `Prism:VaultUri` to your `appsettings.json`:

```json
{
  "Prism": {
    "VaultUri": "https://prismvault.vault.azure.net/"
  }
}
```

That's all you need. Secrets load automatically on first use (fail-late, the default). 

**Authentication note:** `Prism:VaultUri` is all this config needs to *say*, but Key Vault access
itself goes through `DefaultAzureCredential`, which needs *something* in its credential chain to
actually succeed — this "just works" in two common cases, and needs one explicit extra step in a
third:
- **Local development**: your machine's own interactive Azure CLI/Visual Studio sign-in is picked
  up automatically. Nothing to configure.
- **Hosted on Azure itself** (App Service, a VM, etc.): a Managed Identity is picked up
  automatically. Nothing to configure — no app registration needed at all.
- **Hosted anywhere else** (a plain VPS, on-prem, another cloud): neither of the above exists, so
  `DefaultAzureCredential` has nothing to fall back to. You need a real service-principal identity
  — a dedicated Entra app registration, in whichever tenant owns the Key Vault (which may not be
  the same tenant as your OIDC login app — Entra External ID tenants in particular are usually
  standalone identity directories with no Azure subscription of their own) — granted the **Key
  Vault Secrets User** role, with its Client ID/Secret/Tenant ID supplied as the standard
  `AZURE_CLIENT_ID`/`AZURE_CLIENT_SECRET`/`AZURE_TENANT_ID` environment variables.

For fail-fast behaviour (validate Key Vault at startup rather than on first use), add one line to
`Program.cs` before the Umbraco builder:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddPrismKeyVault();   // optional: fail at startup if Key Vault isn't reachable

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()           // picks up PrismComposer
    .Build();

var app = builder.Build();
// ... the rest of your Program.cs
```

## 3. What Happens Automatically on First Startup

Prism creates its own database tables (`prismTenants`, `prismPageAccessPolicies`,
`prismDeviceCredentials` and `prismNotificationSubscriptions`) through an Umbraco package
migration, and adds its screens under **Settings → Advanced** in the backoffice.

It never creates or changes your document types, templates, content or media, and it sets no
site-wide response headers. Your content model and your security policy are yours, so installing
Prism on an existing site leaves every page as it was.

If your site sends a Content-Security-Policy, merge in the sources Prism's sign-in needs for the
current tenant: `PrismContentSecurityPolicy.SignInSources(prismContext.CurrentTenant)` returns the
`form-action` and `frame-src` additions for its identity provider (empty when the tenant has no
sign-in configured). Prism's own pages and assets need nothing else: they use no inline script or
style. The reference site's `SecurityHeadersMiddleware` (in `UmbracoPrism.TestSite/Security`) is a
worked example of a strict policy.

Member sign-in applies only on the hostname of a tenant with sign-in configured: a generic OIDC
authority and client ID, or an Entra tenant ID and client ID. On every other hostname your site's
own authentication stays exactly as it was: your `[Authorize]` endpoints challenge and
authenticate with your own defaults, Prism's `/auth/login` and `/auth/register` return 404, and
Prism's member cookie is not honoured.

On a tenant's hostname, Prism's member session is the default. If your site also serves its own
signed-in areas there (an admin area, say), name your scheme on them, for example
`[Authorize(AuthenticationSchemes = IdentityConstants.ApplicationScheme, Roles = "Administrator")]`,
so a member signed in through the tenant's identity provider can never satisfy them, whatever
role claims that provider issues.

## 4. Your Content

Prism works with whatever pages your site already has:

- **Restrict a page** to signed-in members, or to members of named tenants, from
  **Settings → Advanced → Page access**. The policy applies to the page wherever it moves.
- **Read the signed-in member** in your own templates and controllers through `IPrismContext`
  (the current tenant) and the member's claims.
- **Send push notifications on publish** by listing the document types that should trigger them in
  `Prism:Notifications:NotifiableContentTypes`; see [Notifications](notifications.md).

The reference site, `UmbracoPrism.TestSite`, shows one way to build a member portal on top: a
`homePage` with a member `Dashboard` child, defined and seeded by its own
`StarterContentTypeSeeder` and `StarterContentSeeder` (switched on by its
`TestSite:SeedStarterContent` setting), with `MemberDashboardController` rendering the dashboard.
Copy as much or as little of it as suits your site.

## 5. Configure Your First Tenant

Tenants are managed in the Umbraco backoffice. Navigate to:

**Settings → Advanced → Tenants**

On the Tenants screen:
- **Add a new tenant** with a name and hostname (e.g., `localhost:44345` for local dev, or your production domain).
- **Set the identity provider** if using authentication. For Entra ID, give the Entra tenant ID and client ID; for any other OIDC provider (Keycloak, Auth0...), give its authority URL and client ID, which take precedence. Either way the client secret is a *reference* to a secret in your vault, never the secret itself; see [Secret management](secret-management.md).
- **Configure branding** (logo, colors, theme) in the tenant editor.

For local development without Azure Key Vault, you can test Prism without authentication. The site will render pages but without member sign-in.

### Seeding a Tenant from Configuration (No Backoffice, No Manual Step)

Manually adding a tenant via the backoffice is fine for local development, but a real deployment
usually wants a tenant that just exists automatically once the app boots — no interactive step,
safe to redeploy repeatedly. Set `Prism:SeedTenant` in configuration and
`PrismConfiguredTenantSeeder` (registered automatically, no `Program.cs` change needed) creates
it — and **reconciles** it back to these values on every boot, the same idempotent pattern
the reference site uses for its starter content: a config change takes effect on the
next deploy/restart, no migration step required.

```json
{
  "Prism": {
    "SeedTenant": {
      "Hostname": "my-tenant.example.com",
      "Name": "My Tenant",
      "EntraTenantId": "...",
      "EntraClientId": "...",
      "SecretKeyName": "my-tenant-oidc-secret"
    }
  }
}
```

Seeding only runs while `Hostname` is set — omit the whole section (or leave it out of a given
environment's config) to skip it entirely.

Notably, nothing here is inherently secret — `EntraTenantId`/`EntraClientId` are public
identifiers (Microsoft's own docs treat both as safe to expose), and `SecretKeyName` is just the
*name* of a secret in Key Vault, not its value — so this section is safe to commit even in
`appsettings.json` on a public repo, if you want a tenant to always exist for every deployment of
a given codebase.

**Promoting the same deployment across environments** (dev → staging → production, where the
Entra tenant/app registration genuinely differs per environment): since this reads from
`IConfiguration`, standard ASP.NET Core configuration layering already gives you this — set
`Prism:SeedTenant:*` via environment variables (`Prism__SeedTenant__Hostname`,
`Prism__SeedTenant__EntraTenantId`, ...) in each environment's own deployment config (a systemd
`EnvironmentFile`, a container's env vars, GitHub Actions environment secrets/variables surfaced
at deploy time, etc.), rather than in a committed `appsettings.json` — no code or template
placeholders needed, it's the same mechanism any other environment-specific setting already uses.

Use this seeding, not uSync, for a tenant that must exist as soon as an environment first boots.
The optional `UmbracoPrism.uSync` package moves tenants and page access policies between existing
environments through uSync's export and import; a committed tenant file is not applied on a
fresh deploy.

## 6. Calling your own API as the signed-in member (optional)

Prism can pass a member's identity on to your own business API, scoped to their tenant, without
exposing any secret to the browser. The reference site shows this with
`UmbracoPrism.MockBusinessApp`: run the full stack (`dotnet run --project src/UmbracoPrism.AppHost`),
sign in, and on the dashboard choose **Call Mock Business App API**. See
[Calling a business app as the signed-in member](walkthroughs/authenticated-business-app-call.md)
for how it works and the security checks behind it.

## 7. Verify It's Working

After setup, you should see:

- ✅ **Your site loads** exactly as it did before installing Prism.
- ✅ **Tenant is configured:** In Settings → Advanced → Tenants, your tenant is listed and assigned to the appropriate hostname.
- ✅ **Sign-in works:** a page restricted under Page access sends an anonymous visitor to your identity provider and shows the page once they sign in.

## 8. Next Steps

Once Prism is running:

- **Configure Key Vault (production only):** Add `Prism:VaultUri` to `appsettings.Production.json`. Secrets load automatically. Optionally call `builder.AddPrismKeyVault()` in `Program.cs` for fail-fast validation at startup.
- **Configure sign-in** for each tenant with your identity provider's details; see [Creating a tenant](walkthroughs/creating-a-tenant.md).
- **Restrict pages to signed-in members or named tenants** from Settings → Advanced → Page access.
- **Add service journeys** (citizen forms, caseworker queues) by installing [Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco); Prism has no service-design features of its own.
- **Add tenant branding overrides** by referencing `<link rel="stylesheet" href="/umbraco/prism/branding.css" />` in your layout — see [branding-design-system.md](branding-design-system.md#how-overrides-reach-the-live-site). Prism doesn't inject this for you.
- **Generate a mobile app** from the tenant editor (Settings → Advanced → Tenants) to ship native iOS/Android apps for your portal. If you do, your layout needs a few more asset references; see [Wiring the Mobile Shell and Biometric Scripts into Your Layout](walkthroughs/building-a-mobile-app.md#wiring-the-mobile-shell-and-biometric-scripts-into-your-layout).
- **Enable biometric auth** (optional) for returning users to skip OIDC on subsequent app launches. See [Biometric setup](biometric-setup.md) for key configuration details.

For detailed feature walkthroughs, see the main [README.md](../README.md).
