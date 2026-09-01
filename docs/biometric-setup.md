# Biometric Authentication Security Key Setup

This guide walks you through configuring the security keys required for biometric authentication in Umbraco Prism. Two keys work together: a signing key (for JWT validation) and an encryption key (for protecting stored refresh tokens).

## Overview

Biometric authentication in Umbraco Prism allows users to bypass OIDC authentication on subsequent app launches. After their first login, users can enroll a biometric credential (fingerprint, face ID) on their mobile device.

The system uses two cryptographic keys configured under `Prism:Biometric`:

- **SigningKey**: HMAC-SHA256 key that signs biometric tokens (JWTs). Used to verify token integrity and prevent tampering.
- **EncryptionKey**: AES-256 key that encrypts Entra refresh tokens at rest on the device. Refresh tokens are only encrypted, never signed locally.

Both keys are required. Without them, the app throws `InvalidOperationException` at startup.

## Prerequisites

Before configuring biometric keys:

- Your Umbraco Prism site is running (see `/docs/umbraco-setup.md`).
- Biometric auth is enabled in your tenant settings (Backoffice → Settings → Prism Dashboard → Tenant Editor → "Biometric Auth Enabled").
- For local development: You have access to .NET User Secrets setup.
- For production: You have access to your Azure Key Vault and the necessary permissions to add/update secrets.

## Local Development Setup

### Step 1: Generate a Signing Key

The signing key must be at least 32 characters. You can generate one using any of these methods.

**Using OpenSSL (macOS/Linux):**

```bash
openssl rand -base64 32
```

Copy the output. You now have a valid signing key.

**Using PowerShell (Windows):**

```powershell
[Convert]::ToBase64String((New-Object 'System.Security.Cryptography.RNGCryptoServiceProvider').GetBytes(32))
```

Copy the output.

**Using a password manager:**

Generate a random 32+ character string and use it directly.

### Step 2: Generate an Encryption Key

The encryption key must be a Base64-encoded 32-byte value.

**Using PowerShell (Windows):**

```powershell
Add-Type -AssemblyName System.Security
$bytes = New-Object byte[] 32
(New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

Copy the entire Base64 output.

**Using bash/dotnet (macOS/Linux):**

```bash
dotnet -h > /dev/null && \
  dotnet new console -o /tmp/keygen -f net9.0 -q && \
  cd /tmp/keygen && \
  dotnet add package System.Security.Cryptography && \
  cat > Program.cs << 'EOF'
using System.Security.Cryptography;
var key = RandomNumberGenerator.GetBytes(32);
Console.WriteLine(Convert.ToBase64String(key));
EOF
dotnet run && cd - && rm -rf /tmp/keygen
```

Or, if you have C# interactive installed:

```bash
csi -c 'using System.Security.Cryptography; Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));'
```

Copy the entire Base64 string.

### Step 3: Store Keys Using .NET User Secrets

User Secrets are stored securely outside your source tree and are only used for local development.

Navigate to your Umbraco Prism site directory (typically `src/UmbracoPrism.TestSite` for the demo site):

```bash
cd src/UmbracoPrism.TestSite
```

Initialize User Secrets if you haven't already:

```bash
dotnet user-secrets init
```

Store the signing key:

```bash
dotnet user-secrets set "Prism:Biometric:SigningKey" "YOUR_SIGNING_KEY_HERE"
```

Store the encryption key:

```bash
dotnet user-secrets set "Prism:Biometric:EncryptionKey" "YOUR_ENCRYPTION_KEY_BASE64_HERE"
```

Replace `YOUR_SIGNING_KEY_HERE` and `YOUR_ENCRYPTION_KEY_BASE64_HERE` with the actual keys you generated in Steps 1 and 2.

### Step 4: Verify User Secrets Storage

**On macOS/Linux:**

User secrets are stored in:

```
~/.microsoft/usersecrets/<project-user-secrets-id>/secrets.json
```

To find your project's user-secrets ID, check the `.csproj` file:

```bash
grep -A 1 "UserSecretsId" src/UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj
```

Then view the secrets file:

```bash
cat ~/.microsoft/usersecrets/<YOUR_USER_SECRETS_ID>/secrets.json
```

**On Windows:**

User secrets are stored in:

```
%APPDATA%\Microsoft\UserSecrets\<project-user-secrets-id>\secrets.json
```

Open the file in your text editor to verify both keys are present.

### Step 5: Verify It Works

Start your Umbraco site:

```bash
dotnet run --project src/UmbracoPrism.TestSite
```

If the keys are missing or invalid, you'll see one of these startup exceptions:

- `Prism: BiometricToken signing key must be at least 32 characters.`
- `Prism: RefreshToken encryption key must be configured.`
- `Prism: RefreshToken encryption key must be exactly 32 bytes.`

If neither exception appears, your keys are valid and loaded correctly. Your site can now issue and validate biometric tokens.

## Production Setup (Azure Key Vault)

### Step 1: Configure Key Vault URI in appsettings.json

Add the vault URI to your `appsettings.json` or `appsettings.Production.json`:

```json
{
  "Prism": {
    "VaultUri": "https://prismvault.vault.azure.net/"
  }
}
```

Replace `prismvault` with your actual vault name.

That's it. Prism automatically loads your secrets from Key Vault on the first biometric login. No code changes needed in `Program.cs`.

### Optional: Fail-Fast Behavior

By default, Key Vault errors surface on the **first biometric login** (fail-late). If you prefer to catch Key Vault issues at **startup** (fail-fast), add this line to your `Program.cs` before calling `builder.AddUmbraco()`:

```csharp
builder.AddPrismKeyVault();
```

This optional call:
- Validates Key Vault is reachable and secrets exist at startup
- Fails the entire app startup if Key Vault is misconfigured
- Is useful in strictly controlled production environments where immediate feedback is preferred

**When to use fail-fast:**
- Production deployments where Key Vault availability is guaranteed
- Environments with strict deployment validation policies
- Teams that prefer deployment failure over runtime surprises

**When to use fail-late (default):**
- Development or staging where Key Vault setup may be incomplete
- Multi-tenant deployments where not all tenants need Key Vault yet
- Graceful degradation scenarios (local fallback via User Secrets)

### Step 2: Generate Production Keys

Use the same methods from the Local Development section to generate new signing and encryption keys. **Do not reuse local development keys in production.**

### Step 3: Add Keys to Azure Key Vault

Azure Key Vault uses `--` (double hyphen) as a hierarchy separator for configuration keys. Use the Azure Portal or Azure CLI.

**Using Azure CLI:**

```bash
az keyvault secret set --vault-name prismvault --name "Prism--Biometric--SigningKey" --value "YOUR_SIGNING_KEY"
az keyvault secret set --vault-name prismvault --name "Prism--Biometric--EncryptionKey" --value "YOUR_ENCRYPTION_KEY_BASE64"
```

**Using Azure Portal:**

1. Open your Key Vault in the Azure Portal.
2. Click **Secrets** in the left panel.
3. Click **Generate/Import**.
4. Create a new secret:
   - **Name:** `Prism--Biometric--SigningKey`
   - **Value:** Your signing key
5. Click **Create**.
6. Repeat for `Prism--Biometric--EncryptionKey`.

### Step 4: Confirm Managed Identity Access

Your Umbraco Prism app runs under a managed identity (in Azure App Service) or service principal. Verify it has `Get` and `List` permissions on your Key Vault secrets.

**In Azure Portal:**

1. Open your Key Vault.
2. Click **Access Control (IAM)**.
3. Ensure your App Service's managed identity has the **Key Vault Secrets User** role.

Alternatively, use **Access Policies**:

1. Click **Access Policies**.
2. Ensure your managed identity has **Get** and **List** permissions on Secrets.

### Step 5: Test Production Deployment

#### Fail-Late Behavior (Default)

When your app starts in production without calling `AddPrismKeyVault()`:

- Your app starts normally **even if Key Vault is unreachable or secrets are missing.**
- Key Vault connection errors are cached and surfaced on the **first biometric login attempt**, not at startup.
- We recommend a **smoke test after deployment** to catch Key Vault misconfiguration early: log in via biometric (or request biometric login) within minutes of deployment.

Monitor your App Service logs for biometric-related errors during the smoke test window.

#### Fail-Fast Behavior (If Using AddPrismKeyVault())

If you added `builder.AddPrismKeyVault()` to your `Program.cs`:

- If the vault is unreachable, the app fails to start.
- If a secret is missing, the app throws `InvalidOperationException` with a clear message.
- If both secrets are present and valid, the biometric token service initializes at startup.

#### Common Configuration Errors and Error Messages

Key Vault errors surface with actionable messages:

- **401 Unauthorized:** Your app's managed identity or service principal is not authenticated with Azure. Check that the identity can access Azure Key Vault.
- **403 Forbidden:** Your app's managed identity has access to the vault but lacks **Get** or **List** permissions on Secrets. Check Access Policies or IAM roles.
- **404 Not Found:** One of the expected secrets (`Prism--Biometric--SigningKey` or `Prism--Biometric--EncryptionKey`) is missing from the vault.
- **Transient errors (timeouts, connection drops):** Prism retries automatically. If errors persist, check network connectivity and vault availability.

Monitor your App Service startup logs to debug Key Vault issues during early testing.

## Security Notes

### Key Rotation

Signing and encryption keys should be rotated periodically (e.g., annually or when team membership changes).

- **Signing key rotation:** Issue tokens with the new key. Old tokens remain valid until they expire. No action required on the server.
- **Encryption key rotation:** Encrypted refresh tokens stored on devices cannot be decrypted with a new key. Users must re-enroll biometric after key rotation (automatic fallback to OIDC happens transparently).

### Never Commit Keys to Source Control

- User Secrets are excluded from `.gitignore` by default and are never committed.
- Never hardcode `SigningKey` or `EncryptionKey` in `appsettings.json` or `appsettings.Production.json`.
- Always use Azure Key Vault in production or environment variables in staging.

### Keep Keys Separate

The signing key and encryption key serve different purposes:

- **Signing key** proves the token came from your server (HMAC-SHA256).
- **Encryption key** protects refresh tokens stored on the device (AES-256-GCM).

Use different key values for each to minimize blast radius if one is compromised.

### Audit and Monitoring

In production:

- Enable Azure Key Vault logging to track secret access.
- Monitor `BiometricController` endpoints for rate-limit violations or unusual token exchange patterns.
- Review enrollment and revocation logs in your Umbraco audit trail.

## Troubleshooting

### "BiometricToken signing key must be at least 32 characters"

- **Cause:** `SigningKey` is missing, empty, or fewer than 32 characters.
- **Solution:** Verify you ran `dotnet user-secrets set` correctly. Check the value with `dotnet user-secrets list`. In production, verify the `Prism--Biometric--SigningKey` secret exists in Key Vault and is at least 32 characters.

### "RefreshToken encryption key must be configured"

- **Cause:** `EncryptionKey` is missing from configuration.
- **Solution:** Verify you ran `dotnet user-secrets set "Prism:Biometric:EncryptionKey" ...`. In production, verify `Prism--Biometric--EncryptionKey` exists in Key Vault.

### "RefreshToken encryption key must be exactly 32 bytes"

- **Cause:** The encryption key is not a valid Base64-encoded 32-byte value.
- **Solution:** Regenerate the encryption key using the PowerShell or bash method in this guide. Ensure you copy the **entire Base64 output** (typically ~44 characters). Re-run `dotnet user-secrets set` with the new value.

### "Key Vault access denied" (production)

- **Cause:** The App Service's managed identity does not have permission to read secrets from the vault.
- **Solution:** Verify the managed identity has the **Key Vault Secrets User** role in the vault's **Access Control (IAM)** section, or check **Access Policies** for **Get** and **List** permissions on Secrets.

### "Key Vault is unreachable" (production)

- **Cause:** Network connectivity issue or vault URI is incorrect.
- **Solution:** Verify `Prism:VaultUri` in `appsettings.json` is correct. Ensure your App Service can reach the vault (check firewall rules, service endpoints, or managed identity networking). Test connectivity with the Azure CLI: `az keyvault secret list --vault-name <your-vault>`.

## See Also

- [Biometric Authentication in README](../README.md#9-biometric-authentication-mobile), Overview and usage.
- [Umbraco Setup](./umbraco-setup.md), Installing and configuring Prism in Umbraco.
- [Azure Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/), Vault management and best practices.
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/python/api/azure-identity/azure.identity.defaultazurecredential), Managed identity authentication patterns.
