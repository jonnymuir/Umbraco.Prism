# Interactive Walkthrough — "Apply for Planning Permission"

Once your stack is running (Codespaces or local), this guide walks you through the demo workflow end-to-end, explaining what Umbraco.Prism and the Umbraco backoffice are doing behind the scenes at each step.

> **Start here:** spin up the stack via [Codespaces](../README.md#try-it-now--no-install-required) or [local setup](../README.md#try-the-demo--local-setup), then come back to this guide.

---

## Part 1: Log In and Start the Workflow

### Step 1: Navigate to the TestSite

1. Open the TestSite in your browser:
   - **Codespaces:** Click the forwarded link from the terminal (or `https://{CODESPACE_NAME}-44345.app.github.dev`)
   - **Local:** `https://localhost:44345`

   You see a branded homepage.

2. 💡 **What's happening:** Umbraco.Prism's middleware resolved your hostname to a tenant (seeded as `localhost` for local dev, or `{CODESPACE_NAME}.app.github.dev` for Codespaces). This tenant configuration is stored in the Umbraco backoffice **Prism Dashboard** under **Settings** and includes the tenant's branding, OIDC authority (Keycloak), and tenant ID.

### Step 2: Log In

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

### Step 3: Find and Click "Apply for Planning Permission"

1. On the **My Workflows** page, find the tile labeled **Apply for Planning Permission** and click it.

   The page title changes to **Describe your project** and you see a form with three fields.

2. ✅ **What you're about to do:** You are about to start a new workflow instance — a stateful conversation between you and the system that collects information, validates it, shows it back to you, and then submits it.

---

## Part 2: Walk Through the Workflow Steps

Each step collects information or presents a review screen. Let's fill in the form as we go.

### Step 1: "Describe your project"

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

### Step 2: "Type of work"

**What you see:**
- Form title: **Type of work**
- Radio buttons to select the primary work type

**What to select:**
- Choose **Extension or alteration**

**Click Continue**

1. 💡 **Field group reference:**
   - This step uses the `work-type-info` field group (defined in `workflow-seeds/field-groups/work-type-info-v1.json`).
   - The workflow definition (in `planning-notification-v1.json`) specifies that the `work-type` state includes the `work-type-info` field group.
   - The Umbraco client sent your filled-in `project-name`, `project-description`, and `property-address` values to the workflow engine along with an action `continue`.
   - The workflow engine validated those fields, stored them in the in-memory instance, transitioned to the `work-type` state, and returned the new state's field group definitions.

### Step 3: "Timeline and cost"

**What you see:**
- Form title: **Timeline and cost**
- Date field: **When do you plan to start?** (required, three separate day/month/year inputs)
- Number field: **Estimated duration in weeks** (required)
- Currency field: **Estimated cost of works** (required, displayed with £ prefix)

**What to enter:**
- **Start date:** day `15`, month `6`, year `2025`
- **Duration:** `12` (weeks)
- **Estimated cost:** `25000` (the system will display as £25000)

**Click Continue**

1. 💡 **What's happening:**
   - The date field uses the GDS `date-input` step type, rendering three separate inputs with IDs `proposedStartDate-day`, `proposedStartDate-month`, and `proposedStartDate-year`. The workflow engine combines these into a single `D/M/YYYY` value stored under the base field key.
   - The currency field stores the raw number and prepends the `£` prefix when displaying the value in the summary.
   - When you click Continue, the TestSite POSTs the filled values to `/api/workflow/planning-notification/advance` with action `continue`, and the engine transitions to the next state.

### Step 4: "Affected parties"

**What you see:**
- Form title: **Affected parties**
- Checkboxes: Select all that apply

**What to select:**
- Check **Neighbouring properties**

**Click Continue**

1. ✅ **Multi-select fields:**
   - These are checkboxes defined in the `affected-parties-info` field group.
   - The engine stores which boxes were checked.
   - Validation ensures at least one is selected (if `required: true`).

### Step 5: "Check your answers"

**What you see:**
- Form title: **Check your answers**
- A summary table showing all the information you entered, grouped by field:
  - **Project details:** Project name, description, address
  - **Work type:** Selected type
  - **Timeline and cost:** Start date, estimated duration, estimated cost
  - **Affected parties:** Checked options
- Two buttons: **Back** (to edit) and **Submit**

1. 💡 **The check-answers step type:**
   - The workflow definition specifies `stepType: "check-answers"` for this state.
   - The TestSite rendered a special partial (`_WorkflowStep-Review.cshtml`) that displays a read-only summary instead of input fields.
   - The workflow engine aggregates all field groups from every previous step and presents them together — none of your earlier answers are lost.
   - This is a UX checkpoint: users confirm their data is correct before submission.

2. ✅ **What you can do:**
   - **Edit:** Click **Back** to return to the previous step, make changes, and click **Continue** again. The workflow instance remembers your earlier answers, so you land back on the **Affected parties** step with your previous choices still selected.
   - **Submit:** Click **Submit** to finalize the workflow.

**Click Submit**

1. 💡 **Submitting the workflow:**
   - The TestSite POSTs to `/api/workflow/planning-notification/advance` with action `submit`.
   - The workflow engine validates that all required fields are filled, transitions to the `complete` state, and marks the instance as finished.
   - The Business App stores the instance in its in-memory state (for this demo; in production, it would persist to a database and possibly trigger downstream actions like sending emails or creating records).

### Step 6: "Application received"

**What you see:**
- Form title: **Application received**
- A confirmation panel:
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

## Part 3: Behind the Scenes — How Umbraco.Prism Powers This Workflow

### The Workflow Definition

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
    { "stateKey": "check-answers", "displayName": "Check your answers", "stepType": "check-answers", ... },
    { "stateKey": "complete", "displayName": "Application received", "stepType": "confirmation", ... }
  ],
  "transitions": [
    { "fromState": "project-details", "toState": "work-type", "action": "continue" },
    ...
  ]
}
```

**What this means:**
- **definitionKey:** The unique workflow identifier (used in the URL: `/api/workflow/planning-notification/...`).
- **displayName:** Human-readable name shown to users.
- **instancePolicy:** `"single"` means only one active instance per user/workflow.
- **states:** Each state is a step in the workflow, with a `stepType` (`question`, `check-answers`, `confirmation`) and allowed actions (`continue`, `back`, `submit`).
- **transitions:** Defines which state follows each action (e.g., "from `project-details`, if action is `continue`, go to `work-type`").

### Field Groups

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
    }
  ]
}
```

**What this means:**
- **groupKey:** Unique identifier for this group (referenced in the workflow state).
- **fields:** Array of field definitions, each with a `fieldType` (`text`, `textarea`, `date-input`, `currency`, `radios`, `checkboxes`), validation rules, and UI metadata.

### The Workflow Engine

**Location:** `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`

This is the core engine that powers workflow logic. It:

1. **Loads seed data at startup:** Reads all JSON files from `workflow-seeds/` into memory as `WorkflowDefinitionFile` and `FieldGroupFile` objects.
2. **Maintains instance state:** Stores each user's workflow instances in a `ConcurrentDictionary<string, WorkflowInstanceState>` keyed by `{tenantId}:{userId}:{workflowKey}`.
3. **Handles GetCurrent:** Returns the current state of a workflow instance, or creates a fresh one if none exists.
4. **Handles Advance:** Validates the user's input against the current state's field group, transitions to the next state, and returns the new state's definition.
5. **Tracks completed instances:** Once a workflow reaches a terminal state (e.g., `complete`), it marks the instance as finished but keeps it in memory so the UI can display the confirmation.

### Integration: How Umbraco Calls the Engine

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

### The Response Envelope

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

### Rendering: How Umbraco Displays the Workflow

**Location:** `src/UmbracoPrism.TestSite/Views/WorkflowPage.cshtml`

1. The TestSite controller fetches the current state via `BusinessAppWorkflowClient.GetCurrentAsync()`.
2. It passes the `WorkflowResponseEnvelope` to `WorkflowPage.cshtml`.
3. The view maps the `StepType` to a partial view:
   - `question` → `_WorkflowStep-Question.cshtml` (renders form inputs)
   - `check-answers` → `_WorkflowStep-Review.cshtml` (renders read-only summary)
   - `confirmation` → `_WorkflowStep-Completion.cshtml` (renders success message)
4. Each partial uses the field definitions to generate HTML form fields.

### Umbraco Backoffice Content

Log into the Umbraco backoffice to see how this workflow is wired:

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

### Keycloak: Identity and Authorization

When you logged in at the start, Keycloak issued tokens. Here's what happened:

1. **Authentication:** Keycloak verified your username and password against its user store.
2. **Token issuance:** Keycloak created:
   - `id_token`: Contains claims about your identity (subject ID, email, etc.)
   - `access_token`: An OAuth 2.0 JWT that proves you're authorized to call APIs
3. **Token storage:** The TestSite stored the `id_token` in a secure cookie and the `access_token` in memory.
4. **API authorization:** When the TestSite calls the workflow engine, it includes the `access_token` as a Bearer token in the HTTP Authorization header.
5. **Token validation:** The MockBusinessApp validates the token's signature using Keycloak's public key (fetched from `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration`).
6. **Tenant resolution:** The token includes a `tenantId` claim (set by Prism during token exchange) that the Business App uses to isolate workflow instances by tenant.

---

## Exploring Further

### View the Workflow Definition

In your Codespace or local terminal:

```bash
cat src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification-v1.json
```

You'll see the full state machine definition. Try changing a field label or adding a new field, restarting the AppHost, and see it reflected in the workflow UI.

### View the Engine Logs

While running the workflow, watch the MockBusinessApp logs in the Aspire Dashboard:

1. Open the Aspire Dashboard (`https://localhost:17214` or Codespaces forwarded URL)
2. Click **Resources** and find **MockBusinessApp**
3. Click **View Logs** to see real-time engine activity

### Edit the Backoffice

Log into the Umbraco backoffice and explore:

1. Navigate to **Content**
2. Find the **Get in Touch** or planning permission page
3. Edit the page name or description and click **Save**
4. Publish the page and see the change reflected on the frontend

### Test with Multiple Browsers or Tabs

Open the TestSite in two separate browsers or private windows (both logged in as `demo@prism.local`):

1. Start the workflow in Browser A.
2. Go to the workflow in Browser B.
3. Both should see the same active instance (because `instancePolicy: "single"` and the engine uses `{tenantId}:{userId}:{workflowKey}` as the lookup key).
4. Complete the workflow in Browser B.
5. Go back to Browser A — you'll see the completion page (the instance is shared).
