# Workflow Form Validation

Form validation in Prism is automatic once the package is installed. You get a complete, multi-layer validation stack with zero configuration needed. This guide explains what happens automatically (🔵 Prism Platform), what you define in your Business App (🟠 Your Business App), and how errors are displayed to users.

**Prism's design principle:** Make it easy to do the right thing; principle of least surprise. Install the package and validation just works.

**For context:**
- **Setting up workflows?** Start with [Setting Up a Prism Workflow](./workflow-setup.md) to define your workflow structure and states
- **Theming forms and customizing UI?** See [Customising Workflow UI](./workflow-customisation.md) for CSS variables and partial overrides

## What You Get Automatically

Prism implements five independent layers of validation, each serving a specific purpose:

### 1. HTML5 Client-Side (🔵 Prism Platform)

The Prism tag helpers emit HTML5 validation attributes directly from your field definitions. These provide instant feedback before form submission:

- **Required fields:** `required` attribute
- **Text constraints:** `minlength` and `maxlength` attributes
- **Pattern matching:** `pattern` attribute (regex validation)
- **Numeric bounds:** `min` and `max` attributes for number fields
- **Type validation:** `type="email"`, `type="number"`, `type="date"` enforce format validation

Browsers handle these natively—no JavaScript needed. Tap a required field and leave it blank: the browser shows the error immediately.

### 2. Tamper-Proofing Nonce (🔵 Prism Platform)

Every form submission is protected by a cryptographic nonce. When the form is rendered, Prism:

1. Generates a unique nonce
2. Caches the server-authoritative field definitions under that nonce
3. Embeds the nonce in a hidden form field

When the form is posted back, the nonce is validated and the cached definitions are retrieved. This prevents attackers from:
- Injecting new fields into the form
- Removing validation constraints
- Bypassing required field markers

The nonce is invisible to users and requires no configuration.

### 3. Server-Side Structural Validation (🔵 Prism Platform)

After the form is submitted, Prism runs structural validation before your Business App is called. This layer:

- **Validates the nonce** — rejects if expired or invalid; redirects to GET
- **Whitelist field keys** — rejects any fields not in the authoritative definition
- **Checks required fields** — rejects if required fields are missing
- **Validates types** — coerces values (e.g., "123" → 123 for number fields)
- **Validates options whitelist** — for radio, select, and checkbox lists; rejects out-of-list values
- **Validates constraints** — applies minLength, maxLength, pattern, min, and max rules

This happens in `IWorkflowFieldValidator`, which is called before your Business App receives the request. If validation fails, the form redisplays with error messages—your Business App never sees bad data.

### 4. Business App Validation (🟠 Your Business App)

After Prism's structural layer passes, your Business App can perform domain-specific validation. Examples:

- "This email is already registered"
- "The start date must be before the end date"
- "We're not accepting applications in your region right now"
- "Only renewal cases can be processed in this workflow"

Your Business App returns validation errors in the response, and Prism displays them alongside field-level errors.

### 5. Error Display (🔵 Prism Platform)

Prism renders errors using GDS (GOV.UK Design System) patterns, which are accessible and user-friendly:

- **Error summary** — top of the form lists all errors as clickable links
- **Field-level errors** — each field with an error gets a red border and error text
- **Accessibility** — error text is linked to the input via `aria-describedby`; focus is moved to the error summary
- **User guidance** — errors explain what went wrong and how to fix it

## Declaring Field Constraints in Your Business App

Constraints are defined in your Business App's field group JSON files. Here's what you can declare:

```json
{
  "groupKey": "contact-details",
  "displayName": "Your Contact Information",
  "version": 1,
  "fields": [
    {
      "fieldKey": "full-name",
      "label": "Full name",
      "fieldType": "text",
      "required": true,
      "minLength": 2,
      "maxLength": 100,
      "hint": "Enter your first and last name"
    },
    {
      "fieldKey": "email-address",
      "label": "Email address",
      "fieldType": "email",
      "required": true,
      "hint": "We'll use this to contact you"
    },
    {
      "fieldKey": "message",
      "label": "Your message",
      "fieldType": "textarea",
      "required": true,
      "minLength": 10,
      "maxLength": 500
    },
    {
      "fieldKey": "age",
      "label": "How old are you?",
      "fieldType": "number",
      "required": true,
      "min": 18,
      "max": 120
    },
    {
      "fieldKey": "phone",
      "label": "Phone number",
      "fieldType": "text",
      "required": false,
      "pattern": "^[0-9\\-\\+\\s\\(\\)]{10,20}$",
      "hint": "Format: +44 1234 567890 or 01234 567890"
    },
    {
      "fieldKey": "enquiry-type",
      "label": "What can we help with?",
      "fieldType": "radio",
      "required": true,
      "options": [
        "General enquiry",
        "Technical support",
        "Partnership opportunity"
      ]
    },
    {
      "fieldKey": "subscribe",
      "label": "Subscribe to our newsletter",
      "fieldType": "boolean",
      "required": false
    }
  ]
}
```

### Constraint Reference

🟠 **Your Business App** — Define these in your field group JSON:

| Constraint | Type | Field Types | Example |
|-----------|------|-------------|---------|
| `required` | boolean | All | `"required": true` |
| `minLength` | integer | text, textarea, email | `"minLength": 5` |
| `maxLength` | integer | text, textarea, email | `"maxLength": 100` |
| `pattern` | regex string | text, email | `"pattern": "^[A-Z][a-z]+$"` |
| `min` | number | number, decimal | `"min": 0` |
| `max` | number | number, decimal | `"max": 999.99` |
| `options` | array | radio, select, checkboxlist | `"options": ["Yes", "No"]` |

### Real Example: "Get in Touch" (Community Enquiry)

The testsite includes a "Get in Touch" workflow (`community-enquiry`). Its enquiry message field demonstrates minLength and maxLength:

```json
{
  "fieldKey": "message",
  "label": "Tell us more",
  "hint": "Please give us enough detail to help you (20–500 characters)",
  "fieldType": "textarea",
  "required": true,
  "minLength": 20,
  "maxLength": 500
}
```

Users must type between 20 and 500 characters. Browsers enforce this client-side; Prism enforces it server-side.

## Tag Helpers

🔵 **Prism Platform** — Three tag helpers work together to render forms with automatic validation. You use them in your Razor views—no JavaScript needed.

### `<prism-workflow-form>`

Wraps the entire form and handles submission:

```cshtml
<prism-workflow-form instance-id="@Model.InstanceId"
                     state-version="@Model.StateVersion"
                     workflow-key="@Model.WorkflowKey"
                     return-url="@Model.ReturnUrl"
                     nonce="@Model.Nonce">
    <!-- content goes here -->
</prism-workflow-form>
```

This renders as a standard HTML `<form>`, automatically:
- Sets method to `POST`
- Adds the antiforgery token (CSRF protection)
- Embeds the nonce and metadata as hidden fields
- Sets `novalidate` (Prism handles validation, not the browser's native popup errors)

### `<prism-error-summary>`

Displays errors at the top of the form in GDS style:

```cshtml
<prism-error-summary problems="@Model.Problems" />
```

Renders as:
```html
<div class="prism-error-summary" role="alert">
    <h2 class="prism-error-summary__title">There is a problem</h2>
    <ul class="prism-error-summary__list">
        <li><a href="#full-name">Full name is required</a></li>
        <li><a href="#email-address">Email address is already registered</a></li>
    </ul>
</div>
```

Each error (if it's a field error) is a link that jumps to that field.

### `<prism-field>`

Renders an individual form field with all constraints, errors, and accessibility attributes:

```cshtml
<prism-field field="@fieldDefinition" errors="@Model.FieldErrors" />
```

The field helper automatically:
- Emits the correct HTML input type (text, email, number, date, etc.)
- Adds `required`, `minlength`, `maxlength`, `pattern`, `min`, `max` attributes
- Renders hints with `aria-describedby`
- Adds error text with `aria-invalid="true"`
- Handles radio buttons, checkboxes, select dropdowns, and text areas

### Complete Example

Here's the full `_WorkflowStep-Collect.cshtml` partial from the testsite:

```cshtml
@model UmbracoPrism.TestSite.Models.WorkflowViewModel

<prism-workflow-form instance-id="@Model.InstanceId"
                     state-version="@Model.StateVersion"
                     workflow-key="@Model.WorkflowKey"
                     return-url="@Model.ReturnUrl"
                     nonce="@Model.Nonce">

    <prism-error-summary problems="@Model.Problems" />

    @foreach (var group in Model.FieldGroups)
    {
        <fieldset class="prism-workflow__fieldset">
            <legend class="prism-legend">@group.DisplayName</legend>
            @foreach (var field in group.Fields)
            {
                <prism-field field="@field" errors="@Model.FieldErrors" />
            }
        </fieldset>
    }

    <div class="prism-workflow__actions">
        @foreach (var action in Model.AvailableActions)
        {
            var btnClass = action.Style switch
            {
                "primary" => "prism-button prism-button--primary",
                "destructive" => "prism-button prism-button--destructive",
                _ => "prism-button prism-button--secondary"
            };
            <button type="submit" name="Action" value="@action.ActionKey" class="@btnClass">
                @action.Label
            </button>
        }
    </div>

</prism-workflow-form>
```

This renders a complete, validated, accessible form with error summary and field-level errors. Prism handles all the structural HTML; you just provide the data model.

## Business App Validation Responses

After Prism's structural validation passes, your Business App is called to process the form data. If your Business App detects additional validation problems, return them in this format:

```json
{
  "responseState": "validation_error",
  "problems": [
    {
      "fieldKey": "email-address",
      "message": "This email is already registered.",
      "code": "duplicate"
    },
    {
      "fieldKey": "start-date",
      "message": "Start date must be in the future.",
      "code": "date_invalid"
    },
    {
      "fieldKey": null,
      "message": "Submissions are temporarily paused. Please try again tomorrow.",
      "code": "unavailable"
    }
  ]
}
```

### Response Fields

🟠 **Your Business App** — Return these fields in your validation response:

| Field | Type | Required | Purpose |
|-------|------|----------|---------|
| `responseState` | string | Yes | Set to `"validation_error"` to signal validation failure |
| `problems` | array | Yes | List of problems (see below) |

### Problem Object

| Field | Type | Required | Purpose |
|-------|------|----------|---------|
| `fieldKey` | string or null | Yes | Field to associate error with. Use null for form-wide errors. |
| `message` | string | Yes | Error message shown to the user (e.g., "This email is already registered.") |
| `code` | string | No | Machine-readable error code (e.g., "duplicate", "unavailable") for logging or business logic |

Prism merges Business App validation errors with field-level errors and redisplays the form with all errors visible.

## Multi-Server / Production Configuration

### Default: Single Server (Development)

Out of the box, Prism uses in-memory caching for nonces:

```csharp
// In Program.cs (default)
builder.Services.AddDistributedMemoryCache();
```

This is fine for development and single-server deployments. Each instance has its own nonce cache; nonces don't expire until the configured TTL.

### Production: Multi-Server Deployments

🟠 **Your Configuration** — If you run multiple Umbraco instances (load-balanced), you must use a shared distributed cache. Replace the in-memory cache with Redis or SQL Server:

**Redis:**
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
```

Then add the connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

**SQL Server:**
```csharp
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Cache");
    options.SchemaName = "dbo";
    options.TableName = "DistributedCache";
});
```

Run the cache initialization:
```bash
dotnet sql-cache create "Server=.;Database=PrismCache;..." dbo DistributedCache
```

### Nonce TTL Configuration

🔵 **Prism Platform** — Nonce expiry is configurable. The default is 2 hours; increase this for slow multi-step workflows:

```json
{
  "Prism": {
    "Workflow": {
      "NonceExpiry": "02:00:00"
    }
  }
}
```

Change the timespan to suit your workflow. Example: `"06:00:00"` for 6 hours.

If a user's nonce expires while they're filling out a form, the form redisplays with an error asking them to refresh the page (which generates a new nonce).

## The Testsite Demo

The Umbraco TestSite includes a "Get in Touch" workflow at `/get-in-touch` that demonstrates all validation features:

- **8 field types:** text, email, number, date, select, radio, checkboxlist, boolean
- **Constraint validation:** minLength/maxLength on message field
- **Required fields:** multiple required fields with visual indicators
- **Multi-step workflow:** data collection → under review → completion
- **Error display:** submit with missing required fields to see error summary and field-level errors

Visit the page as an authenticated member to test the full workflow. Try:
1. Submitting with required fields empty → see error summary
2. Entering a message under 20 characters → see inline error
3. Correcting errors and resubmitting → workflow advances

## What You DON'T Need to Do

🔵 **Prism Platform** — These are handled automatically:

- ❌ **Antiforgery tokens** — Prism adds `__RequestVerificationToken` automatically
- ❌ **Nonce generation** — Prism generates and caches nonces on GET; validates on POST
- ❌ **Nonce validation** — Prism rejects expired nonces and redirects to GET
- ❌ **Field key whitelist** — Prism enforces the authoritative field list; rejects injected fields
- ❌ **Required field checking** — Prism validates required fields server-side
- ❌ **Type coercion and constraint checking** — Prism applies minLength, maxLength, pattern, min, max rules
- ❌ **Option whitelist validation** — Prism rejects invalid select/radio/checkbox values
- ❌ **GDS-style error display** — Prism renders accessible error summaries and field-level errors
- ❌ **Accessibility attributes** — Prism emits `aria-invalid`, `aria-describedby`, `aria-required`, focus management

You focus on domain validation (business rules) in your Business App. Prism handles the security, structure, and UI.
