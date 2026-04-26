# Workflow Form Validation

> **⚠️ v2.0 Schema Update:** This guide is being updated for v2.0. JSON examples still use the legacy v1 schema (`fieldType`, flat `fields[]`). See [walkthroughs/](../walkthroughs/) for v2.0 examples with the polymorphic component model.

Form validation in Prism is automatic once the package is installed. You get a complete, multi-layer validation stack with zero configuration needed. This guide explains what happens automatically (🔵 Prism Platform), what you define in your Business App (🟠 Your Business App), and how errors are displayed to users.

**Prism's design principle:** Make it easy to do the right thing; principle of least surprise. Install the package and validation just works.

**For context:**
- **Setting up workflows?** Start with [Setting Up a Prism Workflow](./workflow-setup.md)
- **Theming forms and customizing UI?** See [Customising Workflow UI](./workflow-customisation.md)

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

---

## Declaring Field Constraints

Constraints are defined in your Business App's field group JSON files. Here's the complete set:

### Field Constraint Properties

```json
{
  "groupKey": "contact-details",
  "displayName": "Contact Details",
  "version": 1,
  "fields": [
    {
      "fieldKey": "full-name",
      "label": "Full name",
      "fieldType": "text",
      "required": true,
      "maxLength": 100,
      "minLength": 2
    },
    {
      "fieldKey": "email-address",
      "label": "Email address",
      "fieldType": "email",
      "required": true
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
      "fieldKey": "postcode",
      "label": "UK postcode",
      "fieldType": "text",
      "required": true,
      "pattern": "^[A-Z]{1,2}\\d[A-Z\\d]?\\s?\\d[A-Z]{2}$"
    },
    {
      "fieldKey": "message",
      "label": "Your message",
      "fieldType": "textarea",
      "required": true,
      "maxLength": 5000,
      "minLength": 10
    },
    {
      "fieldKey": "enquiry-type",
      "label": "What can we help with?",
      "fieldType": "select",
      "required": true,
      "options": [
        "General enquiry",
        "Technical support",
        "Partnership",
        "Other"
      ]
    },
    {
      "fieldKey": "newsletter",
      "label": "Subscribe to our newsletter",
      "fieldType": "checkbox",
      "required": false
    }
  ]
}
```

### Constraint Reference

| Property | Type | Field Types | Behavior |
|----------|------|-------------|----------|
| `required` | boolean | all | If true, field must not be empty. Empty is `""`, `null`, or all whitespace. |
| `maxLength` | number | `text`, `textarea`, `email` | Maximum character length enforced client- and server-side. |
| `minLength` | number | `text`, `textarea`, `email` | Minimum character length. |
| `min` | number | `number`, `date-input` | Minimum numeric or date value. |
| `max` | number | `number`, `date-input` | Maximum numeric or date value. |
| `pattern` | string | `text`, `email` | Regex pattern for validation (e.g., `"^[0-9]{3}-[0-9]{2}-[0-9]{4}$"` for SSN). |
| `options` | array | `select`, `radio`, `checkbox`, `checkboxes` | Whitelist of allowed values (plain strings). Submitted values must match exactly. |
| `errorMessage` | string | all | Custom error message shown if validation fails (optional). |

---

## How Validation Works: Step-by-Step

### Example: User Submits Invalid Data

1. **User fills out form** (all fields visible on screen)
   ```
   Full name: "J"              ← Too short (min: 2)
   Email: "not-an-email"       ← Invalid email format
   Age: "16"                   ← Below minimum (min: 18)
   Message: ""                 ← Required field is empty
   Enquiry type: "spam"        ← Not in options array
   ```

2. **Browser HTML5 validation kicks in** (before submission)
   - Field `age` shows "Please enter a number that is greater than or equal to 18"
   - Field `email` shows "Please include an @ in the email address"
   - Field `message` shows "Please fill out this field"
   - But `full-name` passes HTML5 (no minLength in simple cases), and `enquiry-type` passes (not a known value to browser)

3. **User clicks Submit** (POST request sent)

4. **Prism's server-side validator runs** (in WorkflowPageController)
   - Checks nonce → valid ✅
   - Checks field keys → all present and whitelisted ✅
   - Checks constraints:
     - `full-name` minLength 2 → "J" is 1 char → ❌ **Error: too short**
     - `email-address` email format → "not-an-email" → ❌ **Error: invalid email**
     - `age` min 18 → 16 → ❌ **Error: too low**
     - `message` required → "" → ❌ **Error: required**
     - `enquiry-type` options whitelist → "spam" not in list → ❌ **Error: invalid selection**

5. **Validation fails; form is NOT submitted to Business App**
   - Problems are serialized to TempData
   - User is redirected back to the form (PRG pattern)

6. **Form re-renders with errors displayed**
   - Error summary appears at the top
   - Each field with an error shows red border + error message
   - Form values are pre-filled (user doesn't re-type everything)
   - User can fix and try again

### Example: Validation Passes, But Business App Returns an Error

Imagine the user submits valid data, but the Business App rejects it:

```csharp
// User submitted:
{
  "email-address": "duplicate@example.com",  ← Already registered
  "enquiry-type": "Partnership"
}

// Prism's structural validation passes ✅
// Form is submitted to Business App POST /api/workflow/advance

// Business App validation runs:
// "This email is already registered in our system"
// Responds with WorkflowResponseEnvelope:
{
  "responseState": "error",
  "problems": [
    {
      "fieldKey": "email-address",
      "message": "This email is already registered in our system. Try logging in or use a different email.",
      "code": "duplicate_email"
    }
  ]
}

// Prism displays the error to the user, form re-renders
```

---

## Conditional Field Validation

Fields with conditional visibility (show only if another field has a certain value) are automatically validated only when they're visible.

### Example: Conditional Fields

```json
{
  "fields": [
    {
      "fieldKey": "enquiry-type",
      "label": "Type of enquiry",
      "fieldType": "select",
      "required": true,
      "options": ["General", "Technical", "Partnership", "Other"]
    },
    {
      "fieldKey": "technical-details",
      "label": "Describe the technical issue",
      "fieldType": "textarea",
      "required": true,
      "minLength": 20,
      "maxLength": 5000,
      "conditionalOn": "enquiry-type",
      "visibleWhen": ["Technical"]
    }
  ]
}
```

**Behavior:**
- If user selects "General" → `technical-details` is hidden, and validation is skipped (even though it's marked `required`)
- If user selects "Technical" → `technical-details` is shown, and `required` + `minLength` validation applies
- If user selects "Technical", then switches to "General", then back to "Technical" → field retains its value and can be re-validated

**Server-side:** When the form is posted, Prism checks which fields were visible based on the submitted data, and only validates those fields.

---

## Custom Business App Validation

Your Business App can reject submissions for reasons beyond field structure. Examples:

- Email already registered
- Dates out of logical order
- Regional restrictions
- Business rule violations

### Implementing Custom Validation

**In your Business App's POST `/api/workflow/advance` endpoint:**

```csharp
public IActionResult Advance(string workflowKey, string instanceId, string action, [FromBody] IDictionary<string, object?> fields)
{
    // 1. Check structural constraints (Prism already did this, but you can double-check)
    // ...

    // 2. Implement custom business logic validation
    var email = fields?["email-address"]?.ToString();
    var startDate = DateTime.TryParse(fields?["start-date"]?.ToString(), out var sd) ? sd : (DateTime?)null;
    var endDate = DateTime.TryParse(fields?["end-date"]?.ToString(), out var ed) ? ed : (DateTime?)null;

    var problems = new List<WorkflowProblem>();

    // Check: email uniqueness
    if (!string.IsNullOrEmpty(email) && EmailAlreadyRegistered(email))
    {
        problems.Add(new WorkflowProblem
        {
            FieldKey = "email-address",
            Message = "This email is already registered. Try logging in or use a different email.",
            Code = "duplicate_email"
        });
    }

    // Check: date order
    if (startDate.HasValue && endDate.HasValue && startDate > endDate)
    {
        problems.Add(new WorkflowProblem
        {
            FieldKey = "end-date",
            Message = "End date must be after start date",
            Code = "invalid_date_range"
        });
    }

    // If validation failed, return error response (DO NOT advance state)
    if (problems.Count > 0)
    {
        return Ok(new WorkflowResponseEnvelope
        {
            ResponseState = "error",
            Problems = problems
        });
    }

    // 3. Validation passed — advance to next state
    var nextStep = AdvanceWorkflow(workflowKey, instanceId, action, fields);
    return Ok(nextStep);
}
```

### Error Response Format

```csharp
public class WorkflowResponseEnvelope
{
    public string ResponseState { get; set; }  // "success" or "error"
    public string InstanceId { get; set; }
    public string StateKey { get; set; }
    public IReadOnlyList<WorkflowProblem> Problems { get; set; }
    public RenderPayload? Render { get; set; }  // Null if error
}

public class WorkflowProblem
{
    public string? FieldKey { get; set; }       // Optional — ties error to a field
    public string Message { get; set; }        // User-facing error message
    public string? Code { get; set; }          // Machine-readable error code (optional)
}
```

**Prism will:**
1. Display field-level errors next to their inputs (if `FieldKey` is set)
2. Add global errors to the error summary
3. Re-render the form with the user's submitted values pre-filled
4. NOT advance the workflow state

---

## Validation Best Practices

### 1. Make Error Messages User-Friendly

❌ Bad:
```
"maxLength constraint violated"
"pattern validation failed"
```

✅ Good:
```
"Name must be 100 characters or fewer"
"Please enter a valid UK postcode (e.g., SW1A 1AA)"
```

### 2. Use Specific Field Keys

When returning errors from your Business App, include the `fieldKey` so Prism can display the error next to the relevant field:

```csharp
problems.Add(new WorkflowProblem
{
    FieldKey = "email-address",  // ← Include this
    Message = "This email is already registered"
});
```

### 3. Validate Early, but Keep Business Logic in the App

- **Prism validates:** Format, length, required, constraints (HTML5 + server)
- **Your Business App validates:** Business rules, uniqueness, relationships

### 4. Repopulate Forms on Error

Prism automatically repopulates form fields with submitted values when validation fails (POST-redirect-GET pattern). Users don't re-type everything.

### 5. Log Validation Failures

In your Business App, log validation errors for debugging and analytics:

```csharp
if (problems.Count > 0)
{
    _logger.LogWarning(
        "Workflow validation failed: {WorkflowKey}, Instance: {InstanceId}, Errors: {@Problems}",
        workflowKey, instanceId, problems);
}
```

---

## Testing Validation

### Unit Testing Field Constraints

Test your field group definitions directly:

```csharp
[Fact]
public void EmailField_ShouldRequireValidEmail()
{
    var field = new FieldDefinition
    {
        FieldKey = "email",
        FieldType = "email",
        Required = true
    };

    var result = Validator.Validate(new[] { field }, new { email = "not-an-email" });
    Assert.False(result.IsValid);
    Assert.Contains("email", result.Errors.Keys);
}
```

### Integration Testing Workflows

Test the full flow (form submission → validation → Business App):

```csharp
[Fact]
public async Task CompleteWorkflow_WithValidData_ShouldSucceed()
{
    var formData = new Dictionary<string, string>
    {
        { "fields[full-name]", "John Doe" },
        { "fields[email-address]", "john@example.com" },
        { "fields[message]", "I have a question about your service" }
    };

    var response = await client.PostAsync("/workflow-page", new FormUrlEncodedContent(formData));
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Contains("/workflow-page", response.Headers.Location.ToString());
}

[Fact]
public async Task CompleteWorkflow_WithInvalidData_ShouldShowErrors()
{
    var formData = new Dictionary<string, string>
    {
        { "fields[full-name]", "" },  // Required field missing
        { "fields[email-address]", "invalid" },
        { "fields[message]", "" }  // Required field missing
    };

    var response = await client.PostAsync("/workflow-page", new FormUrlEncodedContent(formData));
    var html = await response.Content.ReadAsStringAsync();
    Assert.Contains("govuk-error-summary", html);  // Error summary rendered
    Assert.Contains("error-message", html);  // Field-level errors rendered
}
```

---

## Summary: Validation Layers

| Layer | Owner | When | What |
|-------|-------|------|------|
| HTML5 client-side | Browser | Before submission | Type checks, length limits, pattern matching |
| Nonce tamper-proofing | 🔵 Prism | On submission | Prevents field injection / removal |
| Structural validation | 🔵 Prism | Before Business App | Required, whitelist, constraints |
| Business logic validation | 🟠 Your App | After Prism | Uniqueness, relationships, business rules |
| Error display | 🔵 Prism | After validation | GDS error summary + field-level errors |

Each layer is independent. All must pass for the workflow to advance.

---

**Next steps:**
- [Customising Workflow UI](./workflow-customisation.md) — override partials, adjust CSS
- [GDS Components](./workflow-gds-components.md) — available form elements and design patterns
