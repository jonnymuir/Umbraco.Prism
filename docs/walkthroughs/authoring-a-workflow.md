# Walkthrough — Authoring a New Workflow

A developer-focused guide to writing a new workflow definition from scratch using the Prism fluent builder API, understanding the polymorphic JSON model, seeding it into the MockBusinessApp, and verifying it end-to-end with hot reload.

> **Prerequisites:** Familiarity with C# and JSON. Stack running via [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup). Read at least one of the existing walkthroughs (e.g., [Planning Notification](planning-notification.md)) to understand the user-facing experience you're building toward.

---

## Overview

Every workflow in Prism is described by a **workflow definition** — a directed graph of states connected by transitions, where each state holds a tree of **polymorphic components** (form fields, panels, fieldsets, and more). Definitions live in two forms that are equivalent and interchangeable:

| Form | Location | When to use |
|---|---|---|
| **JSON seed file** | `src/UmbracoPrism.MockBusinessApp/workflow-seeds/` | Simple definitions; no compile step; hot-reloadable |
| **Fluent builder** | `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs` | Complex logic; reusable helpers; IntelliSense; type-safe |

This walkthrough builds a **Leave Request** workflow from scratch, using the fluent builder, then showing its equivalent JSON form. The four demo workflows ([Community Enquiry](community-enquiry.md), [Payment Demo](payment-demo.md), [Planning Notification](planning-notification.md), [Information Request](information-request.md)) are living examples of this exact model.

---

## Part 1: The Polymorphic JSON Model

### The `type` Discriminator

Every component in a workflow state is a JSON object with a `type` field that tells the renderer which component to display. This is the polymorphic discriminator:

```json
{ "type": "text",      "fieldKey": "name",    "label": "Full name",        "required": true }
{ "type": "textarea",  "fieldKey": "reason",  "label": "Reason for leave", "required": true }
{ "type": "radios",    "fieldKey": "duration", "label": "Duration",         "options": ["Half day", "Full day", "Multiple days"] }
{ "type": "date",      "fieldKey": "start-date", "label": "Start date",     "required": true }
{ "type": "panel",     "heading": "Request submitted" }
{ "type": "body",      "content": "Your manager will review this within one working day." }
```

Available `type` values:

| Type | C# class | Use |
|---|---|---|
| `text` | `TextInputComponent` | Single-line text |
| `textarea` | `TextareaComponent` | Multi-line text |
| `email` | `EmailComponent` | Email address |
| `tel` | `TelComponent` | Telephone number |
| `number` | `NumberInputComponent` | Integer |
| `decimal` | `DecimalInputComponent` | Decimal number |
| `select` | `SelectComponent` | Dropdown |
| `radios` | `RadiosComponent` | Radio buttons |
| `checkboxes` | `CheckboxesComponent` | Checkbox group |
| `date` | `DateInputComponent` | Day/month/year |
| `boolean` | `BooleanComponent` | Single yes/no checkbox |
| `fieldset` | `FieldsetComponent` | Container with legend |
| `summary-list` | `SummaryListComponent` | Check-answers table |
| `panel` | `PanelComponent` | Confirmation panel |
| `body` | `BodyComponent` | Paragraph text |
| `heading` | `HeadingComponent` | Heading (level 1–6) |
| `inset-text` | `InsetTextComponent` | Highlighted inset |
| `warning-text` | `WarningTextComponent` | Warning callout |
| `details` | `DetailsComponent` | Collapsible reveal |
| `notification-banner` | `NotificationBannerComponent` | Info/success/warning banner |
| `waiting` | `WaitingComponent` | Polling / long-running state |

### `children[]` — Container Components

`fieldset` and `summary-list` components have a `children` array that holds nested components:

```json
{
  "type": "fieldset",
  "legend": "Leave details",
  "children": [
    { "type": "date", "fieldKey": "start-date", "label": "Start date", "required": true },
    { "type": "date", "fieldKey": "end-date",   "label": "End date",   "required": true }
  ]
}
```

💡 **What's happening:** The renderer walks the component tree depth-first. When it encounters `fieldset`, it wraps the `children` in a `<fieldset>` element with the optional `legend`. This nesting is what gives the GDS forms their visual grouping — you can see it live in [Community Enquiry](community-enquiry.md)'s "About You" section.

### `conditionalChildren` — Conditional Reveals

`radios` and `checkboxes` support a `conditionalChildren` object mapping option values to child component arrays:

```json
{
  "type": "radios",
  "fieldKey": "duration",
  "label": "Duration",
  "options": ["Half day", "Full day", "Multiple days"],
  "conditionalChildren": {
    "Multiple days": [
      { "type": "date", "fieldKey": "end-date", "label": "Last day of leave", "required": true }
    ]
  }
}
```

When the user selects "Multiple days", the `end-date` field is revealed inline. Selecting any other option hides it again. The workflow engine only validates `end-date` when it's visible.

✅ **What you can do:** Chain any number of conditional reveals. Each key in `conditionalChildren` matches an exact option value. The values are case-sensitive.

---

## Part 2: The Fluent Builder API

The fluent builder (`src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`) generates the same JSON model in C# with full IntelliSense:

### Building the Leave Request Workflow

```csharp
using UmbracoPrism.Shared.Builders;

var workflow = new WorkflowDefinitionBuilder()
    .Key("leave-request")
    .DisplayName("Request Annual Leave")
    .Version(1)
    .StartsAt("details")
    .InstancePolicy("multiple")

    // Step 1: collect leave details
    .AddState("details", s => s
        .DisplayName("Tell us about your leave")
        .InsetText("This request will be sent to your line manager for approval.")
        .Fieldset(f => f
            .Legend("Leave dates", "l")
            .DateInput("start-date", "Start date", required: true)
            .Radios("duration", "Duration",
                new[] { "Half day", "Full day", "Multiple days" },
                required: true,
                conditional: c => c
                    .When("Multiple days", o => o
                        .DateInput("end-date", "Last day of leave", required: true))))
        .Textarea("reason", "Reason for leave",
            required: true, hint: "Optional — your manager can see this.", maxLength: 500)
        .Checkboxes("cover-arranged", "Cover arranged?",
            new[] { "Yes, a colleague is covering my responsibilities" },
            required: false))

    // Step 2: check answers
    .AddState("check-answers", s => s
        .DisplayName("Check your request")
        .SummaryList(sl => sl
            .Title("Leave request")
            .ChangeStateKey("details")
            .Children(c => c
                .DateInput("start-date", "Start date")
                .Radios("duration", "Duration", new[] { "Half day", "Full day", "Multiple days" })
                .DateInput("end-date", "Last day of leave")
                .Textarea("reason", "Reason for leave")
                .Checkboxes("cover-arranged", "Cover arranged?",
                    new[] { "Yes, a colleague is covering my responsibilities" }))))

    // Step 3: confirmation
    .AddState("submitted", s => s
        .DisplayName("Request submitted")
        .Panel("Request submitted")
        .Body("Your manager will review this within one working day."))

    .AddTransition("details",       "check-answers", "continue")
    .AddTransition("check-answers", "details",       "back")
    .AddTransition("check-answers", "submitted",     "submit")

    .Build();
```

💡 **What's happening:** `.Build()` returns a `WorkflowDefinitionFile` — the same strongly-typed object that `System.Text.Json` deserializes from the seed JSON files. You can serialize it back to JSON with `JsonSerializer.Serialize(workflow)` and drop the result into the seed folder — both paths are equivalent.

The CRTP pattern (`ComponentCollectionBuilder<TSelf>`) means every builder method returns the most-derived type, so chaining works on both `StateBuilder` and `FieldsetBuilder` without casting.

---

## Part 3: JSON Seeds — How They Are Loaded

### Seed file location

```
src/UmbracoPrism.MockBusinessApp/workflow-seeds/
├── community-enquiry.json
├── information-request.json
├── payment-demo.json
└── planning-notification.json
```

Each file matches the pattern `{definitionKey}.json`. The engine scans this directory at startup using `Directory.EnumerateFiles(seedsPath, "*.json")` and deserializes each file into a `WorkflowDefinitionFile`.

### Adding your new seed

1. Create `src/UmbracoPrism.MockBusinessApp/workflow-seeds/leave-request.json`.
2. Paste the JSON equivalent of your builder definition (or serialize it with `JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true })`).
3. Restart the MockBusinessApp (or rely on hot reload — see below).

### Minimal seed skeleton

```json
{
  "definitionKey": "leave-request",
  "displayName": "Request Annual Leave",
  "version": 1,
  "instancePolicy": "multiple",
  "initialState": "details",
  "states": [
    {
      "stateKey": "details",
      "displayName": "Tell us about your leave",
      "components": [
        {
          "type": "inset-text",
          "content": "This request will be sent to your line manager for approval."
        },
        {
          "type": "fieldset",
          "legend": "Leave dates",
          "children": [
            { "type": "date", "fieldKey": "start-date", "label": "Start date", "required": true },
            {
              "type": "radios",
              "fieldKey": "duration",
              "label": "Duration",
              "required": true,
              "options": ["Half day", "Full day", "Multiple days"],
              "conditionalChildren": {
                "Multiple days": [
                  { "type": "date", "fieldKey": "end-date", "label": "Last day of leave", "required": true }
                ]
              }
            }
          ]
        },
        {
          "type": "textarea",
          "fieldKey": "reason",
          "label": "Reason for leave",
          "hint": "Optional — your manager can see this.",
          "required": true,
          "maxLength": 500
        }
      ]
    },
    {
      "stateKey": "check-answers",
      "displayName": "Check your request",
      "components": [
        {
          "type": "summary-list",
          "title": "Leave request",
          "changeStateKey": "details",
          "children": [
            { "type": "date",    "fieldKey": "start-date", "label": "Start date" },
            { "type": "radios",  "fieldKey": "duration",   "label": "Duration", "options": ["Half day", "Full day", "Multiple days"] },
            { "type": "date",    "fieldKey": "end-date",   "label": "Last day of leave" },
            { "type": "textarea","fieldKey": "reason",     "label": "Reason for leave" }
          ]
        }
      ]
    },
    {
      "stateKey": "submitted",
      "displayName": "Request submitted",
      "components": [
        { "type": "panel", "heading": "Request submitted" },
        { "type": "body",  "content": "Your manager will review this within one working day." }
      ]
    }
  ],
  "transitions": [
    { "fromState": "details",       "toState": "check-answers", "action": "continue" },
    { "fromState": "check-answers", "toState": "details",       "action": "back" },
    { "fromState": "check-answers", "toState": "submitted",     "action": "submit" }
  ]
}
```

---

## Part 4: Hot Reload During Development

The MockBusinessApp watches the `workflow-seeds/` directory. In development mode, saving a seed file triggers an in-process reload — no restart needed.

### Enabling hot reload

1. Start the AppHost with `dotnet watch`:
   ```bash
   cd src/UmbracoPrism.AppHost
   dotnet watch
   ```
2. Open the TestSite in your browser (`https://localhost:44345`).
3. Make a change to a seed file — for example, rename a field label or add a new step.
4. Save the file. The MockBusinessApp reloads the definition in the background.
5. Refresh the browser (no full stack restart needed).

💡 **What's happening:** The file watcher calls `IWorkflowSeedLoader.ReloadAsync()`, which re-reads all JSON files and replaces the in-memory definition dictionary. Any in-flight workflow *instances* keep their state (fields already filled in) but will pick up the new definition from the next state onward. Completed instances are unaffected.

✅ **What you can do during hot reload:**
- **Rename a field label** — reflected immediately on next page load.
- **Add a new state/transition** — new path becomes available to users.
- **Change field validation rules** — server-side validation uses the reloaded definition.
- **Restructure fieldset children** — the renderer picks up the new tree.

> ⚠️ **Instance compatibility:** If you rename a `fieldKey` that already has user data collected, the existing instance will have an orphaned key. For development this is fine (use the test reset API: `DELETE /api/test/reset`). In production, treat field key renames as a schema migration requiring a version bump.

---

## Part 5: Validation

### Client-side Validation

Client-side validation is driven by the field definitions the server returns:

- **`required: true`** → HTML5 `required` attribute; the browser blocks submission if blank.
- **`maxLength`** → HTML5 `maxlength` attribute + a live character counter rendered below the field.
- **`pattern`** → HTML5 `pattern` attribute; validated on blur and submit.
- **`min` / `max`** on number inputs → HTML5 `min`/`max` attributes; spinner enforces range.
- **Conditional reveals** → Hidden fields are removed from the DOM, so they are never submitted or validated when invisible.

💡 **What's happening:** The TestSite Razor partials (`_WorkflowStep-Question.cshtml`) read the component tree returned by the engine and emit the corresponding HTML attributes. Character counters are implemented as a Lit web component (`prism-char-count`) that wraps each textarea and updates on every `input` event.

### Server-side Validation

When you POST to `/api/workflow/{key}/advance`, the MockBusinessApp engine:

1. Fetches the current state's component tree.
2. Iterates every visible field (conditional fields not triggered are excluded).
3. Checks `required`, `minLength`, `maxLength`, `min`, `max`, and `pattern` against the submitted value.
4. Passes the value through `PrismSanitizer.Sanitize()` — which HTML-encodes user input and strips any inline event attributes — before storing it.
5. If any check fails, returns `WorkflowResponseEnvelope { IsValid = false, ErrorMessages = [...] }`.
6. The TestSite re-renders the step with the error messages displayed above the relevant fields (GDS error summary + inline field errors).

✅ **What you can do:** Add custom server-side validation by implementing `IWorkflowStepValidator` in the MockBusinessApp and registering it with DI. Your validator receives the current state key and the submitted field map and can return custom error messages.

---

## Part 6: Wiring to the TestSite

After seeding the workflow, create a Umbraco content page that links to it:

1. Log into the Umbraco backoffice at `https://localhost:44345/umbraco`.
   - Username: `admin@prism.local`
   - Password: `PrismLocal!12345`
2. Navigate to **Content** and create a new child page under **Home**.
3. Set the document type to **Workflow Page**.
4. Set the **Workflow Key** property to `leave-request` (must match `definitionKey` in the seed).
5. Set the **URL segment** to `leave-request`.
6. Click **Save and Publish**.
7. Navigate to `https://localhost:44345/leave-request` — you'll see your workflow.

<!-- manual capture: Umbraco backoffice content editing requires manual authentication and navigation to the Workflow Key property -->

💡 **What's happening:** The Umbraco `WorkflowPageController` reads the **Workflow Key** property from the content node and passes it to `BusinessAppWorkflowClient.GetCurrentAsync()`. The client calls the MockBusinessApp at `https://localhost:7245/api/workflow/leave-request/current` using the current user's bearer token.

---

## Schema Quick Reference

For the full component schema, including all optional properties and their types, see:
- [Workflow GDS Components Guide](../guides/workflow-gds-components.md)
- [Workflow Forms Validation Guide](../guides/workflow-forms-validation.md)

### Common optional properties (all input components)

| Property | Type | Effect |
|---|---|---|
| `hint` | `string` | Hint text shown below the label |
| `required` | `bool` | Marks the field required |
| `conditionalOn` | `string` (fieldKey) | Only render this component when another field has a value |
| `visibleWhen` | `string` (value) | The value the `conditionalOn` field must have |

---

## Related Walkthroughs

The four demo workflows demonstrate every component type in real use:

| Walkthrough | Notable components |
|---|---|
| [Community Enquiry](community-enquiry.md) | `fieldset`, `select`, `radios` + conditional reveal, `checkboxes` |
| [Payment Demo](payment-demo.md) | `decimal` with prefix, `check-answers`, `waiting` panel |
| [Planning Notification](planning-notification.md) | `date`, `currency`, `file`, multi-state flow |
| [Information Request](information-request.md) | `select`, `radios` + urgency conditional, `textarea` |

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`authoring-a-workflow.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/authoring-a-workflow.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.squad/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
