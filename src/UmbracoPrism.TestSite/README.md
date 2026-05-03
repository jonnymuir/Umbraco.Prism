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

Replace `<your-vault>` with your Azure Key Vault instance name. If you don't need Key Vault locally, omit the file entirely — the app will start without it.

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
