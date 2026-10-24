# UmbracoPrism.TestSite

Local development host for Umbraco.Prism. Not for production deployment.

## Local secrets

Secrets are kept out of source control via a gitignored `appsettings.Local.json` file that is loaded at startup with higher priority than `appsettings.json`.

### First-time setup

Create `src/UmbracoPrism.TestSite/appsettings.Local.json` (this file is gitignored):

```json
{
  "Prism": {
    "VaultUri": "https://<your-vault>.vault.azure.net/"
  }
}
```

Replace `<your-vault>` with your Azure Key Vault instance name. If you don't need Key Vault locally, omit the file entirely, the app will start without it.

### Umbraco HMAC imaging key

On first run after this change, Umbraco will generate a fresh `Umbraco:CMS:Imaging:HMACSecretKey` and write it to `appsettings.json`. **Do not commit it.**

Move the generated key to `appsettings.Local.json` instead:

```json
{
  "Prism": {
    "VaultUri": "https://<your-vault>.vault.azure.net/"
  },
  "Umbraco": {
    "CMS": {
      "Imaging": {
        "HMACSecretKey": "<paste generated value here>"
      }
    }
  }
}
```

Then revert `appsettings.json` to remove the key (`git checkout src/UmbracoPrism.TestSite/appsettings.json`). On subsequent runs Umbraco reads the key from `appsettings.Local.json` and does not regenerate it.

**Rule:** Never commit values for `Umbraco:CMS:Imaging:HMACSecretKey` or `Prism:VaultUri` to `appsettings.json`.
The Core test suite now fails if any tracked `appsettings*.json` file contains `Umbraco:CMS:Imaging:HMACSecretKey`.

### User secrets (alternative)

`UserSecretsId` is already configured in the `.csproj`. You can also use `dotnet user-secrets` if you prefer:

```sh
dotnet user-secrets set "Prism:VaultUri" "https://<your-vault>.vault.azure.net/" \
  --project src/UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj
```

The `appsettings.Local.json` approach is preferred because it mirrors what Umbraco writes on first run, making the bootstrap step self-contained.

## Connecting an AI design partner

The site serves Wayfinder's MCP authoring endpoint at `/wayfinder/service-blueprint-authoring/mcp`.
An MCP client such as Claude Code signs in through the Umbraco backoffice and can then do what the
signed-in user can do with blueprints (the `BlueprintsAdmin` policy).

In the backoffice, **Settings → Advanced → Blueprints → Connect a design partner…** shows the setup
for Claude Code, Claude Desktop, VS Code and other MCP clients, with this site's details filled in.
Locally, Claude Code also needs to trust the ASP.NET Core development certificate, as described in
Wayfinder.Umbraco's
[MCP walkthrough](https://github.com/jonnymuir/Wayfinder.Umbraco/blob/main/docs/mcp-authoring-walkthrough.md#act-1-connect-the-design-partner).

Outside Development no loopback callbacks are registered, so a client's sign-in callback must be
listed under `Wayfinder:Mcp:RedirectUris`. The panel says which one each client needs.
