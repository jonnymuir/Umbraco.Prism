<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

```bash
dotnet add package UmbracoPrism
```

One Umbraco instance. Multiple branded portals. Native mobile app included.

Multi-tenant website branding and identity at runtime. Add a mobile app with one click.

---

## Try it Now — No Install Required

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/jonnymuir/Umbraco.Prism)

Click the button to spin up the full Umbraco Prism stack in a browser — no local setup, no Docker, no .NET install. GitHub handles everything. The Codespace is completely throwaway when you're done.

**The stack starts automatically** — watch the terminal at the bottom of your screen. It polls until Keycloak, the Aspire Dashboard, and the TestSite are all ready (first boot: ~3 minutes), then prints the URLs and credentials. When the Aspire Dashboard port is detected VS Code opens it in your browser automatically.

1. Wait for the terminal to print **🎉 Umbraco Prism is ready!**
2. Click the TestSite URL → log in with `demo@prism.local` / `password` (Keycloak SSO)
3. Browse **My Workflows** to see the demo workflow in action

**Credentials at a glance:**

| What | Username | Password |
|------|----------|----------|
| TestSite (Keycloak SSO) | `demo@prism.local` | `password` |
| Umbraco backoffice (`/umbraco`) | `admin@prism.local` | `PrismLocal!12345` |
| Keycloak admin console | `admin` | `admin` |

> **When you're done:** go to [github.com/codespaces](https://github.com/codespaces), find your Codespace, and click **Stop** (or **Delete** to free quota immediately). Stopping halts billing; the Codespace resumes from where you left off.

---

## 🚀 Interactive Walkthrough — "Apply for Planning Permission"

Once your stack is running (Codespaces or local), this guide walks you through the demo workflow end-to-end, explaining what Umbraco.Prism and the Umbraco backoffice are doing behind the scenes at each step.

### Part 1: Log In and Start the Workflow

**Step 1: Navigate to the TestSite**

1. Open the TestSite in your browser:
   - **Codespaces:** Click the forwarded link from the terminal (or `https://{CODESPACE_NAME}-44345.app.github.dev`)
   - **Local:** `https://localhost:44345`

   You see a branded homepage.

2. 💡 **What's happening:** Umbraco.Prism's middleware resolved your hostname to a tenant (seeded as `localhost` for local dev, or `{CODESPACE_NAME}.app.github.dev` for Codespaces). This tenant configuration is stored in the Umbraco backoffice **Prism Dashboard** under **Settings** and includes the tenant's branding, OIDC authority (Keycloak), and tenant ID.

**Step 2: Log In**

1. Click **My Workflows** in the navigation or find the link on the homepage.

2. You are redirected to the Keycloak login screen.

3. Enter credentials:
   - Username: `demo@prism.local`
   - Password: `password`

4. Click **Sign In**.

   After a few seconds, you land on the **My Workflows** page with a list of available workflows.

5. 💡 **What's happening:** This is an OpenID Connect (OIDC) authentication flow. Here's what occurred:
   - Your browser was redirected to Keycloak at `https://localhost:8443/realms/prism-dev` (or the Codespaces forwarded URL).
   - Keycloak presented a login form and verified your credentials against the seeded realm.
   - Upon successful login, Keycloak issued an `id_token` (identity proof) and an `access_token` (authorization proof) and redirected your browser back to the TestSite with an authorization code.
   - The TestSite exchanged that code for tokens with Keycloak and stored the `id_token` in a secure cookie.
   - Your browser session now includes claims like `sub` (your unique ID) and `email_verified` that Prism uses to authorize downstream requests.

**Step 3: Find and Click "Apply for Planning Permission"**

1. On the **My Workflows** page, find the tile labeled **Apply for Planning Permission** and click it.

   The page title changes to **Describe your project** and you see a form with three fields.

2. ✅ **What you're about to do:** You are about to start a new workflow instance — a stateful conversation between you and the system that collects information, validates it, shows it back to you, and then submits it.

---

### Part 2: Walk Through the Workflow Steps

Each step collects information or presents a review screen. Let's fill in the form as we go.

#### Step 1: "Describe your project"

**What you see:**
- Form title: **Describe your project**
- Three input fields:
  - **Project name** (required, max 100 characters)
  - **Describe the proposed works** (required, textarea, max 2000 characters)
  - **Property address** (required, textarea, max 500 characters)

**What to type** (concrete example):
- **Project name:** `New garden extension`
- **Describe the proposed works:** `We plan to extend the existing kitchen with a covered garden area using materials that match the existing brick. No structural changes to the main house.`
- **Property address:** `42 Maple Lane, Springfield, IL 62701`

**Click Continue**

1. 💡 **How this works:**
   - The workflow step type is `question` — designed to collect user input.
   - Each field is defined in a field group file (e.g., `src/UmbracoPrism.MockBusinessApp/workflow-seeds/field-groups/project-info-v1.json`), which specifies the field name, label, input type (`text` or `textarea`), required flag, and max-length validation.
   - The Umbraco TestSite made an HTTP POST to `https://localhost:7245/api/workflow/planning-notification/current` (the MockBusinessApp's workflow engine) with your tenant ID, user ID, and bearer token.
   - The workflow engine created a new instance in memory, seeded it with the `project-info` field group, and returned a `WorkflowResponseEnvelope` describing the current state (display name, field definitions, allowed actions).
   - The TestSite rendered those field definitions as HTML form inputs using Razor partials (e.g., `_WorkflowStep-Question.cshtml`).

2. ✅ **Data validation happens in real-time:**
   - If you leave the **Project name** blank and click Continue, the browser validates (HTML5 `required` attribute) and the form doesn't submit.
   - If you exceed 100 characters, the browser truncates or the form rejects submission.
   - Server-side validation happens when you click Continue — if the MockBusinessApp receives invalid data, it returns a `WorkflowResponseEnvelope` with `isValid: false` and error messages, which the TestSite re-renders.

#### Step 2: "Type of work"

**What you see:**
- Form title: **Type of work**
- Dropdown menu: **Select the type of work** (required, multiple options)
- Radio buttons or checkboxes to select the primary work type

**What to select:**
- Choose **Extension** from the dropdown

**Click Continue**

1. 💡 **Field group reference:**
   - This step uses the `work-type-info` field group (defined in `workflow-seeds/field-groups/work-type-info-v1.json`).
   - The workflow definition (in `planning-notification-v1.json`) specifies that the `work-type` state includes the `work-type-info` field group.
   - The Umbraco client sent your filled-in `project-name`, `project-description`, and `property-address` values to the workflow engine along with an action `continue`.
   - The workflow engine validated those fields, stored them in the in-memory instance, transitioned to the `work-type` state, and returned the new state's field group definitions.

#### Step 3: "Timeline and cost"

**What you see:**
- Form title: **Timeline and cost**
- Date field: **When do you plan to start?** (required)
- Currency field: **Estimated cost** (required, formatted as currency)

**What to enter:**
- **Start date:** `2025-06-01` (click the calendar and select June 2025)
- **Estimated cost:** `15000` (the system will format as currency)

**Click Continue**

1. 💡 **What's happening:**
   - These field types are `date` and `currency`, with client-side validation (date pickers, numeric input).
   - The workflow engine stores these values as strings in the instance state.
   - When you click Continue, the TestSite POSTs the filled values to `/api/workflow/planning-notification/advance` with action `continue`, and the engine transitions to the next state.

#### Step 4: "Affected parties"

**What you see:**
- Form title: **Affected parties**
- Checkboxes: Select all that apply
  - ☐ Neighbour will be affected
  - ☐ Local business will be affected
  - ☐ Public rights of way affected

**What to select:**
- Check **Neighbour will be affected** and **Public rights of way affected**

**Click Continue**

1. ✅ **Multi-select fields:**
   - These are checkboxes defined in the `affected-parties-info` field group.
   - The engine stores which boxes were checked as an array or delimited list.
   - Validation ensures at least one is selected (if `required: true`).

#### Step 5: "Check your answers"

**What you see:**
- Form title: **Check your answers**
- A summary table showing all the information you entered, grouped by field:
  - **Project details:** Project name, description, address
  - **Work type:** Selected type
  - **Timeline and cost:** Start date, estimated cost
  - **Affected parties:** Checked options
- Two buttons: **Back** (to edit) and **Submit application**

1. 💡 **The check-answers step type:**
   - The workflow definition specifies `stepType: "check-answers"` for this state.
   - The TestSite rendered a special partial (`_WorkflowStep-Review.cshtml`) that displays a read-only summary instead of input fields.
   - The workflow engine does not include field definitions in the response for this step — only the collected data.
   - This is a UX checkpoint: users confirm their data is correct before submission.

2. ✅ **What you can do:**
   - **Edit:** Click **Back** to return to the previous step, make changes, and click **Continue** again. The workflow instance remembers your earlier answers, so you land back on the **Affected parties** step with your previous choices still selected.
   - **Submit:** Click **Submit application** to finalize the workflow.

**Click Submit application**

1. 💡 **Submitting the workflow:**
   - The TestSite POSTs to `/api/workflow/planning-notification/advance` with action `submit`.
   - The workflow engine validates that all required fields are filled, transitions to the `complete` state, and marks the instance as finished.
   - The Business App stores the instance in its in-memory state (for this demo; in production, it would persist to a database and possibly trigger downstream actions like sending emails or creating records).

#### Step 6: "Application received"

**What you see:**
- Form title: **Application received**
- A confirmation message:
  ```
  Thank you for your application!
  Your reference number is: {instanceId}
  We will review your application and contact you within 5 working days.
  ```
- One button: **Start another application**

1. 💡 **The confirmation step type:**
   - The workflow definition specifies `stepType: "confirmation"` for the `complete` state.
   - The TestSite rendered a special partial (`_WorkflowStep-Completion.cshtml`) that displays a success message and next steps.
   - The workflow engine includes a reference number (the instance ID) so the user can track their submission.
   - Subsequent requests to `/api/workflow/planning-notification/current` will show this same completion state (the instance is still active but completed).

2. ✅ **Starting another application:**
   - Click **Start another application**.
   - The workflow engine creates a fresh instance for the same user/tenant/workflow combination.
   - The `instancePolicy: "single"` setting in the workflow definition means only one active instance per user/workflow combination — starting a new one implicitly retires the old one.

---

### Part 3: Behind the Scenes — How Umbraco.Prism Powers This Workflow

#### File: The Workflow Definition

**Location:** `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification-v1.json`

This JSON file defines the entire workflow structure:

```json
{
  "definitionKey": "planning-notification",
  "displayName": "Apply for Planning Permission",
  "version": 1,
  "instancePolicy": "single",
  "initialState": "project-details",
  "states": [
    { "stateKey": "project-details", "displayName": "Describe your project", "stepType": "question", ... },
    { "stateKey": "work-type", "displayName": "Type of work", "stepType": "question", ... },
    // ... more states
    { "stateKey": "check-answers", "displayName": "Check your answers", "stepType": "check-answers", ... },
    { "stateKey": "complete", "displayName": "Application received", "stepType": "confirmation", ... }
  ],
  "transitions": [
    { "fromState": "project-details", "toState": "work-type", "action": "continue" },
    // ... more transitions
  ]
}
```

**What this means:**
- **definitionKey:** The unique workflow identifier (used in the URL: `/api/workflow/planning-notification/...`).
- **displayName:** Human-readable name shown to users.
- **instancePolicy:** `"single"` means only one active instance per user/workflow.
- **states:** Each state is a step in the workflow, with a type (`question`, `check-answers`, `confirmation`) and allowed actions (`continue`, `back`, `submit`).
- **transitions:** Defines which state follows each action (e.g., "from `project-details`, if action is `continue`, go to `work-type`").

#### File: Field Groups

**Location:** `src/UmbracoPrism.MockBusinessApp/workflow-seeds/field-groups/project-info-v1.json`

Each state references one or more field groups. A field group is a reusable set of fields:

```json
{
  "groupKey": "project-info",
  "displayName": "Project details",
  "version": 1,
  "fields": [
    {
      "fieldKey": "projectName",
      "label": "Project name",
      "hint": "Give your project a short descriptive name",
      "fieldType": "text",
      "required": true,
      "maxLength": 100
    },
    // ... more fields
  ]
}
```

**What this means:**
- **groupKey:** Unique identifier for this group (referenced in the workflow state).
- **fields:** Array of field definitions, each with a type (`text`, `textarea`, `date`, `currency`, etc.), validation rules, and UI metadata.

#### Service: The Workflow Engine

**Location:** `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`

This is the core engine that powers workflow logic. It:

1. **Loads seed data at startup:** Reads all JSON files from `workflow-seeds/` into memory as `WorkflowDefinitionFile` and `FieldGroupFile` objects.
2. **Maintains instance state:** Stores each user's workflow instances in a `ConcurrentDictionary<string, WorkflowInstanceState>` keyed by `{tenantId}:{userId}:{workflowKey}`.
3. **Handles GetCurrent:** Returns the current state of a workflow instance, or creates a fresh one if none exists.
4. **Handles Advance:** Validates the user's input against the current state's field group, transitions to the next state, and returns the new state's definition.
5. **Tracks completed instances:** Once a workflow reaches a terminal state (e.g., `complete`), it marks the instance as finished but keeps it in memory so the UI can display the confirmation.

#### Integration: How Umbraco Calls the Engine

**Location:** `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs`

The Umbraco site (TestSite) includes a service that makes HTTP calls to the Business App:

1. **GetCurrentAsync:** POSTs to `/api/workflow/{workflowKey}/current`
   - The TestSite controller routes this request through `BusinessAppWorkflowClient`.
   - The client forwards your bearer token (JWT) so the Business App can verify your identity.
   - Returns a `WorkflowResponseEnvelope` with the current state and field definitions.

2. **AdvanceAsync:** POSTs to `/api/workflow/{workflowKey}/advance` with your form data
   - The client serializes your filled-in fields as JSON.
   - The Business App engine validates them, transitions the instance, and returns the new state.
   - If validation fails, the engine returns an error envelope with validation messages.

#### ℹ️ The Response Envelope

Every workflow API response from the Business App includes a `WorkflowResponseEnvelope`:

```csharp
public class WorkflowResponseEnvelope
{
    public string InstanceId { get; set; }           // Unique ID for this workflow instance
    public string StateKey { get; set; }             // Current state (e.g., "project-details")
    public string StateDisplayName { get; set; }     // User-facing name
    public string StepType { get; set; }             // "question", "check-answers", "confirmation"
    public Dictionary<string, object> CollectedData { get; set; } // User's filled-in values
    public FieldGroup[] FieldGroups { get; set; }    // Field definitions for this state (if question type)
    public string[] AllowedActions { get; set; }     // ["continue"], ["submit", "back"], etc.
    public bool IsValid { get; set; }                // Whether the current state is valid
    public string[] ErrorMessages { get; set; }      // Validation errors if !IsValid
}
```

#### Rendering: How Umbraco Displays the Workflow

**Location:** `src/UmbracoPrism.TestSite/Views/WorkflowPage.cshtml`

This is the main workflow view in Umbraco:

1. The TestSite controller fetches the current state via `BusinessAppWorkflowClient.GetCurrentAsync()`.
2. It passes the `WorkflowResponseEnvelope` to `WorkflowPage.cshtml`.
3. The view maps the `StepType` to a partial view:
   - `question` → `_WorkflowStep-Question.cshtml` (renders form inputs)
   - `check-answers` → `_WorkflowStep-Review.cshtml` (renders read-only summary)
   - `confirmation` → `_WorkflowStep-Completion.cshtml` (renders success message)
4. Each partial uses the field definitions to generate HTML form fields.

#### Umbraco Backoffice Content

If you log into the Umbraco backoffice, you can see how this workflow is wired:

**Steps:**
1. Go to `https://localhost:44345/umbraco` (or the Codespaces URL + `/umbraco`)
2. Log in with:
   - Username: `admin@prism.local`
   - Password: `PrismLocal!12345`
3. Navigate to **Content** and find **Get in Touch** (or **Apply for Planning Permission** if renamed)
4. Look at the page properties:
   - **URL:** `/get-in-touch` or similar
   - **Workflow Key:** `planning-notification` (links to the workflow definition)

**What the backoffice user controls:**
- The page title, description, and any static UI text.
- The workflow key (which workflow definition to use).
- Content publishing and unpublishing (determines if the page is visible to users).

**What the backoffice user does NOT control:**
- The workflow states, transitions, or field definitions — those are baked into the JSON seed files in the MockBusinessApp.
- In a real system, the backoffice might include a visual workflow builder, but this demo uses JSON as the source of truth.

#### Keycloak: Identity and Authorization

When you logged in at the start, Keycloak issued tokens. Here's what happened:

1. **Authentication:** Keycloak verified your username and password against its user store.
2. **Token issuance:** Keycloak created:
   - `id_token`: Contains claims about your identity (subject ID, email, etc.)
   - `access_token`: An OAuth 2.0 JWT that proves you're authorized to call APIs
3. **Token storage:** The TestSite stored the `id_token` in a secure cookie and the `access_token` in memory (or local storage on the client).
4. **API authorization:** When the TestSite calls the workflow engine, it includes the `access_token` as a Bearer token in the HTTP Authorization header.
5. **Token validation:** The MockBusinessApp validates the token's signature using Keycloak's public key (fetched from `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`).
6. **Tenant resolution:** The token includes a `tenantId` claim (set by Prism during token exchange) that the Business App uses to isolate workflow instances by tenant.

---

### Exploring Further

#### View the Workflow Definition

In your Codespace or local terminal:

```bash
cat src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification-v1.json
```

You'll see the full state machine definition. Try changing a field label or adding a new field, restarting the AppHost, and see it reflected in the workflow UI.

#### View the Engine Logs

While running the workflow, watch the MockBusinessApp logs in the Aspire Dashboard:

1. Open the Aspire Dashboard (`https://localhost:17214` or Codespaces forwarded URL)
2. Click **Resources** and find **MockBusinessApp**
3. Click **View Logs** to see real-time engine activity

#### Edit the Backoffice

Log into the Umbraco backoffice and explore:

1. Navigate to **Content**
2. Find the **Get in Touch** or planning permission page
3. Edit the page name or description and click **Save**
4. Publish the page and see the change reflected on the frontend

#### Test with Multiple Browsers or Tabs

Open the TestSite in two separate browsers or private windows (both logged in as `demo@prism.local`):

1. Start the workflow in Browser A.
2. Go to the workflow in Browser B.
3. Both should see the same active instance (because `instancePolicy: "single"` and the engine uses `{tenantId}:{userId}:{workflowKey}` as the lookup key).
4. Complete the workflow in Browser B.
5. Go back to Browser A — you'll see the completion page (the instance is shared).

---

## Try the Demo — Local Setup

Get from clone to running in five minutes. No Azure account needed.

**One-time setup:**
- `.NET 10 SDK` ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Trust the .NET dev certificate** — run `dotnet dev-certs https --trust`
- Docker Desktop running ([Download](https://www.docker.com/products/docker-desktop/))
- `Node.js 20+` ([Download](https://nodejs.org/))
- Frontend dependencies: `cd src/UmbracoPrism.Client && npm install`

> VS Code tip: the **C#: Aspire (Full Stack)** launch now validates the .NET 10 SDK and Docker first. This repo uses the Aspire AppHost SDK and NuGet packages, so you do **not** need `dotnet workload install aspire`.

**Start the full stack:**
```bash
dotnet run --project src/UmbracoPrism.AppHost
```

Then:
1. Open the Aspire dashboard at `https://localhost:17214`
2. Click the TestSite URL → log in with `demo@prism.local` / `password`
3. Browse **My Workflows** to see the demo workflow in action
4. The MockBusinessApp runs alongside at `https://localhost:7245` — it accepts the same demo credentials and powers the workflow engine

**Optional:** Explore Keycloak admin at `https://localhost:8443/admin` (`admin` / `admin`).

**Why this matters for local dev:**
- The local Keycloak uses standard OIDC code-flow scopes — no offline tokens needed for a fresh clone.
- Prism preserves the `id_token` in the session, enabling logout callbacks to Keycloak with the required `id_token_hint`.
- MockBusinessApp trusts the browser-facing Keycloak authority (`https://localhost:8443`), so the workflow dashboard validates bearer tokens against the public issuer, not the internal container URL (`http://localhost:8080`).
- Aspire runtime state lives under `artifacts/aspire/testsite-runtime/` — the demo and Playwright suite never mutate the standalone TestSite database at `src/UmbracoPrism.TestSite/umbraco/Data/`.

> For detailed setup, troubleshooting, and architecture: See [ASPIRE_DEV.md](ASPIRE_DEV.md).

---

## What You Get

### Multi-Tenant Web — One Instance, Hundreds of Brands

Serve distinct branded portals from one Umbraco instance. Runtime branding, domain resolution, tenant isolation.

<div align="center">
<img src="screenshots/testsite.png" width="400" alt="Branded portal example">
<img src="screenshots/backoffice2.png" width="400" alt="Backoffice branding editor">
</div>

**Web features:**
- Domain-based tenant resolution — each client gets their own hostname
- Live branding editor — CSS variables update without deploy
- **Branding as a Design System** — annotated CSS variables become labeled form fields, grouped into sections (Colors, Typography, Components), with type-aware editors (color pickers, sliders, text inputs)
  
  ```css
  @property --prism-primary {
    syntax: '<color>';
    inherits: true;
    initial-value: #4f46e5;
  }
  
  :root {
    /* @prism section: Brand Colours | label: Primary Brand Colour | description: Main brand colour used for buttons and links */
    --prism-primary: #4f46e5;
  }
  ```
  
  → [Branding Design System →](docs/branding-design-system.md)

- Per-tenant OIDC — Entra ID integration, zero local Members
- Downstream auth — propagate tenant identity to internal APIs
- Tenant isolation — authorization policies enforce data boundaries

→ [Umbraco Setup Guide](docs/umbraco-setup.md)

### Produce Mobile — Generate Apps from Backoffice

Turn tenant settings into iOS/Android apps. No complex native coding, just click **Produce Mobile**.

<div align="center">
<img src="screenshots/example-IOS.png" width="300" alt="iOS app with tenant branding">
</div>

**Mobile features:**
- Biometric login (Face ID, fingerprint) — skip OIDC on return
- Push notifications (FCM/APNs) — content or API triggered
- Offline-ready layouts with safe-area handling
- Tenant branding at runtime (colors, logo, splash)

Run in simulator:

```bash
npm run bootstrap:ios
```

→ [Mobile Setup](docs/PUSH_SETUP.md) | [Biometric Auth](docs/biometric-setup.md)

---

## Quick Start

### 1. Install

```bash
dotnet add package UmbracoPrism
```

Prism registers automatically via `PrismComposer` — no manual service registration needed.

### 2. Configure

Add to `appsettings.json`:

```json
{
  "Prism": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

For local dev without Azure Key Vault, see [Local Authentication Walkthrough](#local-authentication-walkthrough).

### 3. Run

```bash
dotnet run
```

Prism auto-creates document types (`homePage`, `memberDashboard`) on first startup.

### 4. Add Your First Tenant

In backoffice:
1. **Settings → Prism Dashboard**
2. Add tenant (hostname, identity settings, branding)
   - **Entra tenants:** enter the vault secret name in `SecretKeyName`
   - **Generic OIDC tenants:** enter OIDC authority and client ID, then provide the Key Vault secret name as the `OidcClientSecretReference` with provider `azure-key-vault`; the localhost Keycloak demo is the only inline-secret exception
3. Visit the hostname — see branded portal

→ [Full Setup Guide](docs/umbraco-setup.md)

---

## How It Works

**Multi-tenancy at runtime:** Middleware resolves hostname to tenant. One content tree serves hundreds of portals.

**Stateless auth:** No local Members. Identity deferred to OIDC providers (Entra ID or generic OIDC). Confidential client secrets resolve through Key Vault or the repo-owned localhost demo exception.

**Secure-by-default secrets:** Production tenants use vault-backed secret references, never raw values in management responses. The localhost Keycloak demo is the only inline-secret path, and runtime rejects inline generic OIDC secrets anywhere else.

**Mobile generation:** Tenant settings → iOS/Android app. Run in simulator immediately.

**Downstream auth:** Pass tenant identity to internal APIs without shared state.

---

## Features

**Multi-tenant web:**
- Domain-based tenant resolution
- Live CSS variable branding
- Per-tenant Entra ID (OIDC)
- Tenant isolation policies
- Downstream API auth

**Mobile:**
- iOS/Android generation from backoffice
- Biometric login (Face ID, fingerprint)
- Push notifications (FCM/APNs)
- Offline-ready layouts

**Infrastructure:**
- Azure Key Vault secrets at runtime
- Zero local Member records
- Managed Identity support
- Admin-only backoffice policies

→ [Full Documentation](docs/)

---

## Documentation

| Guide | Description |
|---|---|
| [Secret Management](docs/secret-management.md) | Configure OIDC client secrets for production tenants, understand local dev demo |
| [Umbraco Setup](docs/umbraco-setup.md) | Install Prism, configure tenants, seed content |
| [Biometric Setup](docs/biometric-setup.md) | Generate signing/encryption keys for mobile biometric auth |
| [Push Notifications](docs/PUSH_SETUP.md) | Configure FCM (Android) and APNs (iOS) for push |
| [Notifications Design](docs/notifications-design.md) | Push notification architecture and API reference |
| **Design Docs** | |
| [Notifications Architecture](docs/design/notifications-architecture.md) | Internal design: notification system layers |
| [Notifications Backend](docs/design/notifications-backend.md) | Internal design: backend API and service layer |
| [Notifications Mobile](docs/design/notifications-mobile.md) | Internal design: Capacitor plugin integration |
| [Notifications Umbraco](docs/design/notifications-umbraco-demo.md) | Internal design: Umbraco content hooks and demo site |

→ [Full Documentation Index](docs/)

---

## Architecture

**Runtime layer:**
* `PrismTenantMiddleware` — resolves hostname to tenant
* `IPrismContext` — scoped service with tenant/theme data

**Identity layer:**
* Dynamic OIDC — swaps `ClientId`, `Authority`, `Issuer` per tenant
* `IPrismUserContext` — current user claims and tenant
* `SecretVaultService` — Azure Key Vault (Managed Identity in prod, Azure CLI local)
* Downstream flow — propagate tenant identity to APIs

**Secret Management:**
* **Entra ID tenants (production):** Secrets stored in Azure Key Vault, referenced by `SecretKeyName`
* **Generic OIDC tenants (production):** Secrets stored in Azure Key Vault, referenced by `OidcClientSecretProvider = "azure-key-vault"` plus `OidcClientSecretReference`
* **Local dev demo (Keycloak):** Repo-owned secret uses `OidcClientSecretProvider = "inline"` only for the seeded `localhost` tenant path
* **Management API/UI:** Responses expose `HasOidcClientSecret` and `OidcClientSecretProvider`, never the raw secret or reference value
* All confidential-client flows fail closed if a secret cannot be resolved at runtime

→ [Secret Management Guide](docs/secret-management.md) | [Architecture Docs](docs/)

---

## Prerequisites

- **.NET 10.0** ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([Download](https://nodejs.org/))
- **Docker Desktop** — for local demo with Aspire ([Download](https://www.docker.com/products/docker-desktop/))
- **Azure Key Vault** (production) or local dev without vault (see setup guide)
- **Entra ID** (for authentication)

> **Client dependencies:** Run before first build:
> ```bash
> cd src/UmbracoPrism.Client && npm install
> ```

---

## Setup & Development

### Local Dev Tunnel (Mobile Testing)

For testing Entra sign-in on mobile devices, use `scripts/dev/start-trycloudflare.sh`:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Automates:
- Cloudflare tunnel for `https://localhost:<port>`
- Entra redirect URI update
- Prism tenant hostname sync
- Cleanup on exit

**Security:** Dev use only. Mutates Entra app and local database.

→ [Full tunnel docs in README section below](#quick-start-phone-auth-via-cloudflare-tunnel-no-lan-ip-dependency)

### Storybook Tests (UmbracoPrism.Client)

Storybook is used for component-driven tests with the Storybook test runner + Playwright.

**Local usage:**

```bash
cd src/UmbracoPrism.Client
npm install
npm run storybook
```

In a second terminal:

```bash
cd src/UmbracoPrism.Client
npm run test-storybook
```

**VS Code (Optional):**

Optionally, install the **Playwright Test extension** for a convenient Testing view UI to run Playwright tests. Tests are in [src/UmbracoPrism.Client/tests](src/UmbracoPrism.Client/tests). You can also run `npm run test:playwright:ui` for the interactive runner without the extension.

**Headless multi-browser + WCAG checks (recommended):**

```bash
cd src/UmbracoPrism.Client
npm run test-storybook:all
```

**CI usage (GitHub Actions):**

The workflow in [.github/workflows/ci-tests.yml](.github/workflows/ci-tests.yml) runs the following:

```bash
cd src/UmbracoPrism.Client
npm ci
npx playwright install --with-deps
npm run test-storybook:ci:all
```

### Localhost auth/session Playwright regressions

These behavioural-contract tests run against the real Aspire stack rather than Storybook. The suite validates Aspire prerequisites, boots its own `UmbracoPrism.AppHost` session, waits for the dashboard plus seeded app resources to be ready, then signs into the seeded Keycloak demo user and restarts the whole localhost stack mid-run to verify session continuity.

**Before running:**

- Docker Desktop must be running
- `dotnet dev-certs https --trust` must already be done
- The default Aspire ports (`17214`, `44345`, `7245`, `8443`) must be free because the suite owns the stack lifecycle and will not attach to an existing or partial stack

```bash
cd src/UmbracoPrism.Client
npm run test:playwright:localhost-auth
```

The suite uses the seeded demo identity from `keycloak/realm-export.json`: `demo@prism.local` / `password`.

**Stable seeded content contract:** on a clean TestSite database, Development startup deterministically repairs the Umbraco nodes the localhost auth/workflow flows use — `Home` (`/`), `Dashboard` (`/dashboard`), `Get in Touch` (`/get-in-touch`, workflow key `community-enquiry`), `My Workflows` (`/my-workflows`), plus the `Settings` node mobile nav entries for Home/Dashboard/My Workflows. The Razor views resolve those destinations from published content, so route lookup does not depend on root-node ordering.

### Core Tests (UmbracoPrism.Core)

```bash
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests
```

### Dependency Vulnerability Check

Run a transitive package vulnerability scan for the Core project:

```bash
dotnet list src/UmbracoPrism.Core/UmbracoPrism.Core.csproj package --vulnerable --include-transitive
```

If vulnerabilities are reported, prefer upgrading the direct package first. For transitive-only issues, add a top-level package reference in the relevant `.csproj` to force a patched version.

**VS Code (Optional):**

Optionally, install the **.NET Test Explorer extension** for a convenient Testing view UI to run the Core tests. Tests can also be run from the command line using `dotnet test`.

### Packaging & Marketplace

**Build the backoffice assets:**

```bash
cd src/UmbracoPrism.Client
npm install
npm run build
```

**Pack the NuGet package:**

```bash
dotnet pack src/UmbracoPrism.Core/UmbracoPrism.Core.csproj -c Release -o artifacts
```

**Marketplace metadata:**

See [umbraco-marketplace.json](umbraco-marketplace.json) for the listing metadata (icon, screenshots, tags, description).

**Accessibility (WCAG) checks:**

Storybook test runner runs axe checks (WCAG 2.0/2.1 A/AA) via
[src/UmbracoPrism.Client/.storybook/test-runner.ts](src/UmbracoPrism.Client/.storybook/test-runner.ts).

To opt out for a specific story, set `parameters: { a11y: { disable: true } }` in your `.stories.ts` file:

```typescript
export const MyStory = {
  render: (args) => <MyComponent {...args} />,
  parameters: {
    a11y: { disable: true }  // Disables WCAG checks for this story
  }
};
```

### Local Authentication Walkthrough

#### 1. Azure Setup

**Entra ID:** Create App Registration. Redirect URI: `https://localhost:[PORT]/signin-oidc`.

**Key Vault:** Add secret (e.g., `tenant-a-secret`) with Client Secret.

**Permissions:** Grant **Key Vault Secrets User** to your identity.

#### 2. Local Auth

```bash
az login --allow-no-subscriptions
```

Allows `SecretVaultService` to access Key Vault in local dev.

#### 3. Tenant Setup

In **Prism Dashboard** (backoffice):
- **Hostname:** `localhost:[PORT]`
- **Entra Tenant ID:** Directory ID
- **Entra Client ID:** App Registration ID
- **Secret Key Name:** `tenant-a-secret`

For **generic OIDC production tenants**, enter the provider authority/client ID plus the Key Vault secret name that should be resolved at runtime. The dashboard does not round-trip raw OIDC client secrets through edit responses; production updates are reference-based, and only the seeded localhost Keycloak demo exposes an inline replace field.

#### 4. Downstream API Auth

If your Prism frontend needs to call a secure backend (e.g., a "Member Dashboard" API), Prism can flow the current tenant’s identity and access token to that downstream system.

#### 1. Backend API: Enabling Prism Auth

In your downstream ASP.NET Core API, register the Prism authentication handler. This allows the API to accept multi-tenant tokens from any CIAM tenant registered in your system.

```csharp
// In your API's Program.cs
builder.Services.AddPrismAuthentication(builder.Configuration);

```

#### 2. Backend API: Resolving the Tenant

Use the Prism identity extensions to resolve which brand the user belongs to. This ensures data isolation at the API level.

```csharp
app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user) =>
{
    // Resolves the tenant from config (default) or a custom resolver
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));

    if (tenant == null) return Results.Unauthorized();

    return Results.Ok(new { 
        Brand = tenant.DisplayName,
        Code = tenant.Code 
    });
}).RequireAuthorization();

```

#### 3. Frontend: Calling the API

From your Umbraco site, use `IPrismContext` to automatically generate the correct Authorization header containing the user's `access_token`.

```csharp
public async Task<string> GetMemberDataAsync()
{
    using var client = new HttpClient();
    // Automatically handles token extraction and refresh logic
    client.DefaultRequestHeaders.Authorization = await PrismContext.GetAuthorizationHeaderAsync();

    return await client.GetStringAsync("https://your-api.com/api/backoffice/me");
}

```

---

### Sample Projects

**`UmbracoPrism.TestSite`** — Reference Umbraco v17 site. Shows OIDC setup, tenant branding, downstream API calls. Pre-configured tenant definitions for local auth.

**`UmbracoPrism.MockBackOffice`** — Minimal API. Shows `AddPrismAuthentication` and multi-tenant data isolation.

→ See [Local Authentication Walkthrough](#local-authentication-walkthrough)

---

## Stack

* **Umbraco:** v17.0+
* **.NET:** 10.0
* **Auth:** Stateless OIDC (Entra), Azure Key Vault, Managed Identity
* **Mobile:** Capacitor, TypeScript, Storybook

---

## Phone Auth via Cloudflare Tunnel

For Entra sign-in on mobile, use HTTPS tunnel (Entra requires `https://` or `http://localhost` only).

### No Domain (Temporary URL)

```bash
brew install cloudflared
cloudflared tunnel --url https://localhost:44345
```

Or use helper:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Add redirect URI in Entra:
```
https://<random>.trycloudflare.com/signin-oidc
```

Helper script auto-rotates stale trycloudflare URIs.

### Stable Hostname (Custom Domain)

```bash
cloudflared tunnel login
cloudflared tunnel create prism-dev
cloudflared tunnel route dns prism-dev prism-dev.<your-domain>
```

Create `~/.cloudflared/config.yml`:

```yml
tunnel: <tunnel-id>
credentials-file: /Users/<you>/.cloudflared/<tunnel-id>.json

ingress:
  - hostname: prism-dev.<your-domain>
    service: https://localhost:44345
    originRequest:
      noTLSVerify: true
      httpHostHeader: localhost:44345
  - service: http_status:404
```

Run:

```bash
cloudflared tunnel run prism-dev
```

Redirect URI:
```
https://prism-dev.<your-domain>/signin-oidc
```
