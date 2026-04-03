# Adding Prism to Your Umbraco Site

This guide walks you through adding Umbraco Prism to an existing Umbraco v17+ site (or bootstrapping it in a greenfield project). The process is designed to be minimal: install the package, register services, and Prism handles the rest.

## 1. Install the NuGet Package

In your Umbraco project:

```bash
dotnet add package UmbracoPrism
```

## 2. Configure Program.cs

Your `Program.cs` needs two lines: one to configure Key Vault and one to register Prism services.

### Set Up Key Vault (Optional for Production)

If using Azure Key Vault in production, add this line **before** `builder.AddUmbraco()`:

```csharp
builder.AddPrismKeyVault();
```

This single line handles Key Vault setup:
- Reads `Prism:VaultUri` from `appsettings.json` automatically
- Skips silently if `Prism:VaultUri` is not set (no changes needed for local dev)
- Validates the URI is HTTPS before connecting

### Register Prism Services

Add Prism services after Umbraco setup:

```csharp
builder.Services.AddUmbraco(env, builder.Configuration)
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Add this line:
builder.Services.AddPrism(builder.Configuration);
```

**Full example `Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Key Vault for production (reads Prism:VaultUri from appsettings)
builder.AddPrismKeyVault();

// Register Umbraco and Prism services
builder.Services.AddUmbraco(env, builder.Configuration)
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

builder.Services.AddPrism(builder.Configuration);

var app = builder.Build();
// ... rest of Program.cs
```

## 3. What Happens Automatically on First Startup

When your Umbraco site starts, Prism's `PrismContentTypeSeeder` runs automatically:

- **Creates two document types** (if they don't exist):
  - `homePage` — the root/landing page type
  - `memberDashboard` — the authenticated member portal page type
- **Non-destructive:** If either type already exists, Prism skips creation and uses what's there.
- **No breaking changes:** Existing content, members, and navigation remain untouched.

**What Prism does NOT touch:**
- Your existing content tree (if any)
- Your existing member records
- Navigation menus or other templates
- Any document types outside `homePage` and `memberDashboard`

## 4. Content Tree Structure

Prism expects a simple content hierarchy:

```
Content/
  └── Home (document type: homePage)
        └── Dashboard (document type: memberDashboard)
```

- **Home page:** The public landing page. Users see "Sign In" and "Register" CTAs here.
- **Dashboard page:** A child of Home. Renders only for authenticated members. Shows the member portal with personalized content.

### For Existing Sites (Manual Setup)

If you have an existing Umbraco content tree:

1. **Create a Home page** using the `homePage` document type (if you don't have one).
2. **Create a Dashboard page** as a child of Home, using the `memberDashboard` document type.
3. **Publish both pages.**
4. **Configure your first tenant** in the Prism backoffice dashboard (see step 5 below).

### For New/Greenfield Sites (Auto-Seeding)

If you're starting fresh, set the optional auto-seed flag:

```json
{
  "Prism": {
    "SeedStarterContent": true
  }
}
```

On the next startup, Prism will:
- Create the `homePage` document type
- Create the `memberDashboard` document type
- Auto-create a **Home** page (document type: `homePage`)
- Auto-create a **Dashboard** page (child of Home, document type: `memberDashboard`)
- Auto-create a **Content Blueprint** so editors can use "Create from Blueprint" for additional member portal pages

Your content tree is then ready to use. No manual page creation needed.

## 5. Configure Your First Tenant

Tenants are managed in the Umbraco backoffice. Navigate to:

**Settings → Prism Dashboard**

In the Prism dashboard:
- **Add a new tenant** with a name and hostname (e.g., `localhost:44345` for local dev, or your production domain).
- **Set the Entra configuration** (OIDC Client ID, Tenant ID, etc.) if using authentication.
- **Configure branding** (logo, colors, theme) in the tenant editor.

For local development without Azure Key Vault, you can test Prism without authentication. The site will render pages but without member sign-in.

## 6. The MockBackOffice Demo (Optional)

Prism ships with a `MockBackOffice` example that demonstrates downstream credential flow — showing how Prism tenant credentials can be securely passed to a business API or microservice.

### Running the Demo

In one terminal, start your main Umbraco site:

```bash
dotnet run --project src/UmbracoPrism.TestSite
```

In another terminal, start the MockBackOffice service:

```bash
dotnet run --project src/UmbracoPrism.MockBackOffice
```

### Testing the Demo

1. Navigate to the Umbraco site (e.g., `https://localhost:44345`).
2. Log in or use the Sign In CTA.
3. Once authenticated, visit `/dashboard?callApi=true` in the authenticated session.
4. The dashboard will call the MockBackOffice API to fetch mock data, demonstrating the credential flow in action.

This example shows how Prism isolates tenant identity and can safely propagate context to downstream services without exposing secrets.

## 7. Verify It's Working

After setup, you should see:

- ✅ **Homepage loads** and displays Sign In / Register CTAs (if authentication is configured).
- ✅ **Document types exist:** In Umbraco Settings → Document Types, you see `homePage` and `memberDashboard`.
- ✅ **Content tree is correct:** Home page exists; Dashboard page exists as a child of Home.
- ✅ **Tenant is configured:** In Settings → Prism Dashboard, your tenant is listed and assigned to the appropriate hostname.
- ✅ **Dashboard loads when authenticated:** Log in, navigate to `/dashboard`, and you see the member portal page.

## 8. Next Steps

Once Prism is running:

- **Configure Key Vault (production only):** Add your `Prism:VaultUri` to `appsettings.Production.json` and call `builder.AddPrismKeyVault()` in Program.cs. Secrets are loaded automatically from Azure Key Vault.
- **Configure Entra authentication** by providing your Entra app registration details in `appsettings.json` (Client ID, Tenant ID, etc.).
- **Customize the dashboard** by editing the Dashboard Razor template (`memberDashboard.cshtml`).
- **Add more pages** under Dashboard by creating new documents and assigning the `memberDashboard` type (or custom subtypes).
- **Generate a mobile app** from the Prism backoffice tenant editor to ship native iOS/Android apps for your portal.
- **Enable biometric auth** (optional) for returning users to skip OIDC on subsequent app launches. See `/docs/biometric-setup.md` for key configuration details.

For detailed feature walkthroughs, see the main [README.md](../README.md).
