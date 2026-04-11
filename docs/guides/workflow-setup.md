# Setting Up a Prism Workflow

A complete guide to building and deploying a multi-step workflow form using Umbraco.Prism.

## Overview

A Prism workflow is a multi-step form engine that connects your Umbraco website to a Business App (a separate .NET web API). The Business App defines the workflow structure—steps, fields, validation rules—as JSON files. Umbraco renders the forms and handles authentication.

**Architecture:**

```
Umbraco TestSite              Business App
─────────────────            ──────────────
workflowPage content node    workflow-seeds/retirement-quote.json
    ↓ workflowKey property       ↓ defines states, fields, transitions
WorkflowPageController       WorkflowEngine
    ↓ GET/POST                   ↓ returns WorkflowResponseEnvelope
IBusinessAppWorkflowClient   
    ↓ HTTP Bearer token
Views/Partials/_WorkflowStep-{Archetype}.cshtml
    ↓
Rendered form
```

Workflows support complex scenarios: multi-step data collection, review steps, approval workflows, status tracking, and completion states. Each step can validate, branch logic, or trigger backend actions.

## Prerequisites

Before setting up a workflow, ensure:

1. **Prism is installed** in your Umbraco 17 package
2. **Your Umbraco members are authenticated** using the `PrismMemberCookie` authentication scheme (Entra tenant configured)
3. **Your Business App is running** and accessible via HTTP(S) from the Umbraco server
4. **Business App client is configured** in Umbraco's `appsettings.json` with the correct endpoint URL and Entra token

## Step 1: Define the Workflow in Your Business App

A workflow is a JSON file stored in your Business App's `workflow-seeds/` directory.

**Create a new file:** `workflow-seeds/my-workflow.json`

### Complete Example: Retirement Quote Workflow

```json
{
  "definitionKey": "retirement-quote",
  "displayName": "Retirement Quote Request",
  "version": 1,
  "initialState": "collecting-info",
  "states": [
    {
      "stateKey": "collecting-info",
      "displayName": "Tell us about yourself",
      "archetype": "Collect",
      "allowedActions": ["submit", "save-draft"],
      "fieldGroupKeys": ["personal-details", "request-details"]
    },
    {
      "stateKey": "review-details",
      "displayName": "Check your answers",
      "archetype": "Review",
      "allowedActions": ["submit", "back"],
      "fieldGroupKeys": []
    },
    {
      "stateKey": "under-review",
      "displayName": "Your quote is being prepared",
      "archetype": "StatusTimeline",
      "allowedActions": [],
      "fieldGroupKeys": []
    },
    {
      "stateKey": "complete",
      "displayName": "Your quote is ready",
      "archetype": "Completion",
      "allowedActions": ["start-another"],
      "fieldGroupKeys": []
    }
  ],
  "transitions": [
    { "fromState": "collecting-info", "toState": "review-details", "action": "submit" },
    { "fromState": "collecting-info", "toState": "collecting-info", "action": "save-draft" },
    { "fromState": "review-details", "toState": "collecting-info", "action": "back" },
    { "fromState": "review-details", "toState": "under-review", "action": "submit" },
    { "fromState": "under-review", "toState": "complete", "action": "approve", "requiresRole": "reviewer" }
  ],
  "fieldGroups": [
    {
      "fieldGroupKey": "personal-details",
      "displayName": "Personal Information",
      "fields": [
        {
          "fieldKey": "full-name",
          "label": "Full name",
          "fieldType": "text",
          "required": true,
          "hint": "Enter your first and last name"
        },
        {
          "fieldKey": "date-of-birth",
          "label": "Date of birth",
          "fieldType": "date",
          "required": true
        },
        {
          "fieldKey": "email",
          "label": "Email address",
          "fieldType": "email",
          "required": true,
          "hint": "We'll use this to send your quote"
        }
      ]
    },
    {
      "fieldGroupKey": "request-details",
      "displayName": "Quote Details",
      "fields": [
        {
          "fieldKey": "retirement-age",
          "label": "What age do you want to retire?",
          "fieldType": "number",
          "required": true
        },
        {
          "fieldKey": "investment-type",
          "label": "Investment preference",
          "fieldType": "select",
          "required": true,
          "options": [
            { "key": "conservative", "label": "Conservative" },
            { "key": "balanced", "label": "Balanced" },
            { "key": "aggressive", "label": "Aggressive" }
          ]
        },
        {
          "fieldKey": "additional-info",
          "label": "Anything else we should know?",
          "fieldType": "textarea",
          "required": false
        }
      ]
    }
  ]
}
```

### Workflow Definition Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `definitionKey` | string | Yes | Unique identifier matching the filename (without `.json`). Used in Umbraco `workflowKey` property. |
| `displayName` | string | Yes | Human-readable name shown in admin interfaces. |
| `version` | number | Yes | Version number for your workflow; increment when you modify structure. |
| `initialState` | string | Yes | The `stateKey` to start the workflow in. |
| `states` | array | Yes | Array of workflow states (see State Properties below). |
| `transitions` | array | Yes | Array of allowed transitions between states (see Transition Properties). |
| `fieldGroups` | array | Yes | Array of field groupings for data collection (see Field Group Properties). |

### State Properties

Each state in the `states` array:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `stateKey` | string | Yes | Unique identifier for the state (used in transitions). |
| `displayName` | string | Yes | Title shown to the user (e.g., "Check your answers"). |
| `archetype` | string | Yes | Rendering type: `Collect`, `Review`, `StatusTimeline`, or `Completion`. |
| `allowedActions` | array | Yes | List of action keys that can be triggered in this state. |
| `fieldGroupKeys` | array | Yes | Which field groups to display in this state. Empty array `[]` for non-data states. |

### Transition Properties

Each transition defines a path through the workflow:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `fromState` | string | Yes | Starting state (`stateKey`). |
| `toState` | string | Yes | Destination state (`stateKey`). |
| `action` | string | Yes | Action key (e.g., "submit", "back", "save-draft"). Must be in the `allowedActions` of `fromState`. |
| `requiresRole` | string | No | If specified, only users with this role can trigger this transition. For backend validation. |

### Field Group Properties

Each field group in `fieldGroups`:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `fieldGroupKey` | string | Yes | Unique identifier (referenced in state `fieldGroupKeys`). |
| `displayName` | string | Yes | Legend/heading shown above the field group. |
| `fields` | array | Yes | Array of fields (see Field Properties). |

### Field Properties

Each field in a field group:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `fieldKey` | string | Yes | Unique identifier for the field (used when collecting values). |
| `label` | string | Yes | Label shown in the form. |
| `fieldType` | string | Yes | Input type (see Field Types below). |
| `required` | boolean | No | Whether the field must be filled (default: false). |
| `hint` | string | No | Helper text displayed below the label. |
| `options` | array | No | For `select`, `radio`, or `checkboxlist` fields. Array of `{ key, label }` objects. |

### Field Types Reference

| Type | HTML Element | Example |
|------|--------------|---------|
| `text` | `<input type="text">` | Name, username, postal code |
| `email` | `<input type="email">` | Email address with validation |
| `number` | `<input type="number">` | Integer values (age, quantity) |
| `decimal` | `<input type="number" step="0.01">` | Monetary amounts, percentages |
| `date` | `<input type="date">` | Date picker |
| `datetime` | `<input type="datetime-local">` | Date and time picker |
| `select` | `<select>` | Dropdown list (requires `options`) |
| `radio` | Radio buttons | Single choice from list (requires `options`) |
| `checkboxlist` | Checkboxes | Multiple selections (requires `options`) |
| `textarea` | `<textarea>` | Multi-line text (comments, descriptions) |
| `boolean` | Checkbox | Yes/no toggle |

## Step 2: Register the Workflow

The Business App's `WorkflowEngine` automatically discovers all JSON files in the `workflow-seeds/` directory at startup. No code changes needed.

**File location:** `src/UmbracoPrism.MockBusinessApp/workflow-seeds/retirement-quote.json`

**How it works:**
1. Business App starts up
2. `WorkflowEngine` scans `workflow-seeds/` directory
3. Each `.json` file is loaded and indexed by `definitionKey`
4. When Umbraco calls `GET /api/workflow/{key}/current`, the engine returns the workflow state

To reload workflows without restarting, restart the Business App.

## Step 3: Set Up the Umbraco Document Type

Create a new document type in Umbraco's backoffice to represent workflow pages.

1. **Create document type `workflowPage`:**
   - Go to **Settings > Document Types > Create**
   - Name: `Workflow Page`
   - Alias: `workflowPage` (must match the controller name convention)
   - Icon: Choose an icon (e.g., a form icon)

2. **Add a property:**
   - Name: `Workflow Key`
   - Alias: `workflowKey`
   - Type: **Text string**
   - Description: "The key of the workflow definition (e.g., 'retirement-quote')"
   - Required: Yes

3. **Optional properties** you may want to add:
   - `workflowDescription` (Text string) — description of the form
   - `successRedirectUrl` (Text string) — URL to redirect to after completion

4. **Save the document type**

Note: The `WorkflowPageController` requires this exact alias. If you rename it, also rename the controller file (Umbraco route-hijacking uses filename conventions).

## Step 4: Publish the Content Node

Create and publish a content page using the `workflowPage` document type.

1. **Create a content node:**
   - Go to **Content**
   - Right-click the root and select **Create > Workflow Page**
   - Name: "Retirement Quote Request" (or your form title)

2. **Fill in the properties:**
   - `workflowKey`: `retirement-quote` (must match the JSON `definitionKey` exactly)
   - Any other properties you added (description, redirect URL, etc.)

3. **Publish the page:**
   - Click **Save and Publish**

4. **Note the URL:**
   - Umbraco assigns a URL like `/retirement-quote-request/`
   - This is the public URL where members will visit the form

## Step 5: Test It

Visit the published content page from a browser where you're authenticated as an Umbraco member (with a valid `PrismMemberCookie`).

### Expected Flow

1. **GET request:** Browser requests the page → `WorkflowPageController.Index()` → Calls Business App → Renders the first state (`collecting-info`)
2. **Form display:** Partials render based on archetype (e.g., `_WorkflowStep-Collect.cshtml`)
3. **Fill form:** Member completes fields
4. **POST request:** Form submits → Controller validates → Calls Business App advance endpoint → Redirects to page (PRG pattern)
5. **Next state:** Page reloads, controller fetches new state, new partial renders
6. **Completion:** Final state (`Completion` archetype) shows success message

### Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "No workflow key configured" | `workflowKey` property is empty | Set it in Umbraco backoffice |
| "Could not start workflow" | Business App offline or key not found | Check Business App is running; verify JSON file exists and `definitionKey` matches |
| Form doesn't look right | Partial not found | Ensure `_WorkflowStep-{Archetype}.cshtml` exists in `Views/Partials/` |
| Form won't submit | Antiforgery validation failed | Ensure `@Html.AntiForgeryToken()` is in the partial; check security headers |
| Member redirected to login | Not authenticated | Member must have valid `PrismMemberCookie` session |

## Archetype Reference

Choose an archetype based on what the user should do in each state:

| Archetype | Purpose | Renders | Examples |
|-----------|---------|---------|----------|
| `Collect` | Data entry form | Input fields from `fieldGroupKeys` | Name, email, preferences |
| `Review` | Confirm and submit | Read-only display of collected data + submit action | "Check your answers" step |
| `StatusTimeline` | Waiting state | Status message (no form) | "Under review", "Processing..." |
| `Completion` | Success state | Completion message (no form) | "Quote sent!", "Application approved" |

The view dispatch looks for a partial file named `_WorkflowStep-{Archetype}.cshtml` in `Views/Partials/`. Default partials ship with Prism but you can override them.

## Action Styles

Actions define how buttons appear. Use the appropriate style for intent:

| Style | Appearance | Use For |
|-------|-----------|---------|
| `primary` | Bold, main colour | Primary next step ("Continue", "Submit", "Approve") |
| `secondary` | Secondary colour | Alternative action ("Go back", "Save draft", "Cancel") |
| `destructive` | Red/alert colour | Dangerous action ("Delete", "Reject", "Cancel") |

## Complete Workflow JSON Template

Use this as a starting point for new workflows:

```json
{
  "definitionKey": "my-workflow",
  "displayName": "My Workflow",
  "version": 1,
  "initialState": "step-1",
  "states": [
    {
      "stateKey": "step-1",
      "displayName": "Step 1: Collect Info",
      "archetype": "Collect",
      "allowedActions": ["continue", "save"],
      "fieldGroupKeys": ["personal"]
    },
    {
      "stateKey": "step-2",
      "displayName": "Step 2: Review",
      "archetype": "Review",
      "allowedActions": ["submit", "back"],
      "fieldGroupKeys": []
    },
    {
      "stateKey": "complete",
      "displayName": "Complete",
      "archetype": "Completion",
      "allowedActions": [],
      "fieldGroupKeys": []
    }
  ],
  "transitions": [
    { "fromState": "step-1", "toState": "step-2", "action": "continue" },
    { "fromState": "step-1", "toState": "step-1", "action": "save" },
    { "fromState": "step-2", "toState": "step-1", "action": "back" },
    { "fromState": "step-2", "toState": "complete", "action": "submit" }
  ],
  "fieldGroups": [
    {
      "fieldGroupKey": "personal",
      "displayName": "Personal Information",
      "fields": [
        {
          "fieldKey": "name",
          "label": "Full name",
          "fieldType": "text",
          "required": true
        }
      ]
    }
  ]
}
```

## Next Steps

- **Customise the UI:** See [Customising Workflow UI](./workflow-customisation.md) for CSS theming and partial overrides
- **Add validation:** Implement server-side validation in your Business App
- **Add transitions:** Extend the workflow with approval steps, role-based routing
- **Monitor usage:** Check Umbraco logs for workflow state transitions and errors
