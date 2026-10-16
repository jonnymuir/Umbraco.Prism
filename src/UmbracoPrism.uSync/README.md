# UmbracoPrism.uSync

[uSync](https://jumoo.co.uk/usync/) handlers for [Umbraco Prism](https://github.com/jonnymuir/Umbraco.Prism):
move your tenants and page access policies between environments with the rest of your uSync
files.

Tenants and page access policies are stored in Prism's own database tables, not as Umbraco
content, so uSync doesn't export them unless you install this package. Without it, you have to
recreate each tenant and each page's access rules by hand in every environment. A rule you forget
on live leaves that page open to anyone.

## Install

```bash
dotnet add package UmbracoPrism.uSync
```

It needs `UmbracoPrism` and uSync 17 (`uSync.BackOffice`), and registers itself: there is no code
to add. Both handlers appear in **Settings → uSync** and export to `uSync/v17/`:

| Handler | Folder | What it exports |
|---|---|---|
| Prism Tenants | `Tenants` | Name, hostname, identity provider settings, biometric sign-in, and web and mobile branding overrides. |
| Prism Page Access | `PageAccess` | Each policy's page route, whether it requires sign-in, and its tenant allow-list. |

## What moves and what doesn't

- **No secrets are exported.** A tenant's export holds the *name* or *reference* of its client
  secret in your vault (`SecretKeyName`, `OidcClientSecretProvider`/`OidcClientSecretReference`),
  never the secret. An older tenant with an inline client secret keeps it on import, but it never
  appears in the file.
- **Hostnames can differ per environment.** Write the hostname as a `{{TOKEN}}` placeholder, such
  as `{{TENANT_A_HOST}}`, and set `TENANT_A_HOST` in each environment's configuration. The token
  is resolved when the file is imported. An import fails if the hostname resolves to nothing.
- **Pages are matched by route, not by id.** A page's key differs between databases, so a policy
  is exported with the page's route and bound on import to the published page at that route in
  the target environment. If no page is published there yet, the import fails rather than leave
  the page unprotected: publish the page, then import again.

For a tenant that must exist as soon as a new environment first boots, use `Prism:SeedTenant`
configuration instead (see
[Adding Prism to your Umbraco site](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/umbraco-setup.md)).

## Versions

Released together with `UmbracoPrism`, with the same version number. Install the same version of
both.

## Licence

MIT. See the [Umbraco Prism repository](https://github.com/jonnymuir/Umbraco.Prism) for source,
issues and the [security policy](https://github.com/jonnymuir/Umbraco.Prism/blob/main/SECURITY.md).
