# Customising Service Blueprint UI & Theme

A guide to customising the appearance and behavior of service blueprint forms in Umbraco.Prism.

**For context:**
- **Setting up service blueprints?** Start with [Setting Up a Prism Service Blueprint](./service-blueprint-setup.md)
- **Understanding validation?** See [Form Validation](./service-request-forms-validation.md)
- **Using GDS components?** See [GDS Design System Components](./service-blueprint-gds-components.md)

---

## Overview

Prism provides sensible defaults for service blueprint rendering: GDS (GOV.UK Design System) styling, accessibility built-in, responsive layout. You can customise at three levels:

1. **CSS variables** — Change colors, spacing, fonts without touching code
2. **Razor partial overrides** — Replace step templates to customize HTML/markup
3. **Custom CSS** — Add your own styles on top of GDS

---

## CSS Variables & Theme Customization

The easiest way to customize Prism service blueprints: override CSS variables. All Prism styling uses custom properties (`--prism-*`), making it trivial to apply your brand colors, fonts, and spacing.

### Supported CSS Variables

#### Colors

```css
/* Primary service-blueprint colors */
--prism-primary-color: #0b3e6f;                /* Blue - primary buttons, links, accents */
--prism-success-color: #00703c;                /* Green - success states, valid checks */
--prism-warning-color: #b10e1e;                /* Red - warnings, errors, destructive actions */
--prism-neutral-color: #505a5f;                /* Grey - secondary text, disabled states */

/* Input styling */
--prism-input-border-color: #b1b4b6;           /* Border around text inputs */
--prism-input-bg-color: #ffffff;               /* Background of input fields */
--prism-input-text-color: #0b3e6f;             /* Text inside inputs */
--prism-focus-color: #ffdd00;                  /* Focus ring color (yellow accessibility standard) */

/* Panel backgrounds (step types) */
--prism-panel-question-border-color: #0b3e6f; /* Question step border (blue) */
--prism-panel-question-bg: #f3f6f9;            /* Question step background */

--prism-panel-confirmation-border-color: #00703c; /* Confirmation step border (green) */
--prism-panel-confirmation-bg: #f3faf5;        /* Confirmation step background */

--prism-panel-timeline-border-color: #505a5f;  /* Timeline step border (grey) */
--prism-panel-timeline-bg: #fafbfc;            /* Timeline step background */

/* Error styling */
--prism-error-border-color: #b10e1e;           /* Error field border */
--prism-error-bg-color: #fef7f5;               /* Error field background */
--prism-error-text-color: #b10e1e;             /* Error message text */

/* Check-answers (review) styling */
--prism-check-answers-dt-color: #505a5f;       /* Review field label colour */
--prism-check-answers-item-border: 1px solid #b1b4b6; /* Review field separator */
```

#### Spacing & Layout

```css
--prism-panel-padding: 1.5rem;                 /* Padding inside panels */
--prism-field-margin-bottom: 1.5rem;           /* Space between fields */
--prism-button-gap: 1rem;                      /* Space between buttons */
--prism-max-width: 960px;                      /* Max width of form container */
```

#### Typography

```css
--prism-font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
--prism-font-size-base: 1rem;
--prism-font-size-lg: 1.25rem;                 /* Step title font size */
--prism-font-size-sm: 0.875rem;                /* Helper text font size */
--prism-line-height: 1.5;
```

### How to Override CSS Variables

Create a custom CSS file in your Umbraco project and reference it in your Master layout. Override any variables you want to change:

**File:** `wwwroot/css/my-service-blueprint-theme.css`

```css
/* My custom theme overrides */
:root {
    /* Brand colors */
    --prism-primary-color: #d32f2f;             /* My brand red instead of blue */
    --prism-success-color: #388e3c;             /* My brand green */
    
    /* Custom spacing */
    --prism-panel-padding: 2rem;                /* More breathing room */
    --prism-field-margin-bottom: 2rem;
    
    /* Custom font */
    --prism-font-family: Georgia, serif;        /* Serif font */
    --prism-font-size-base: 1.1rem;             /* Slightly larger base */
}
```

**File:** `Views/Master.cshtml`

```cshtml
<head>
    <!-- Prism default styles (GDS-based) -->
    <link rel="stylesheet" href="~/css/govuk-frontend.min.css">
    <link rel="stylesheet" href="~/css/prism-service-blueprints.min.css">
    
    <!-- Your custom theme overrides -->
    <link rel="stylesheet" href="~/css/my-service-blueprint-theme.css">
</head>
```

**Result:** All Prism service blueprint UI automatically uses your colors, fonts, and spacing.

---

## Overriding Razor Partials

For deeper customization—changing HTML structure, adding custom elements, reorganizing sections—override the step-type partials.

### Partial File Locations

Prism looks for partials in this order:

1. **Your app's Partials folder** — `Views/Partials/_WorkflowStep-{stepType}.cshtml`
2. **Prism package defaults** — Built-in to UmbracoPrism.Core

To customize a step, copy the default partial to your app and modify it. Umbraco's partial resolver automatically uses your version.

### Available Partials to Override

#### 1. `_WorkflowStep-question.cshtml`

Renders a `question` step — form fields for data collection.

**Location to copy from:** Prism package defaults
**Location to override:** `Views/Partials/_WorkflowStep-question.cshtml`

**Model:**
```csharp
public class PrismServiceRequestViewModel
{
    public string StateDisplayName { get; set; }      // e.g., "Tell us about your enquiry"
    public IEnumerable<FormSection> FieldGroups { get; set; }  // Field groups to render
    public IEnumerable<ServiceBlueprintAction> AvailableActions { get; set; }  // Submit, Save Draft, etc.
    public IReadOnlyList<ServiceBlueprintProblem> Problems { get; set; }  // Validation errors
    public IReadOnlyDictionary<string, string> FormValues { get; set; }  // Submitted values for repopulation
}
```

**Example override:**
```cshtml
@model PrismServiceRequestViewModel

<div class="govuk-width-container">
    <div class="govuk-main-wrapper">
        <!-- Custom header -->
        <div class="govuk-panel govuk-panel--blue">
            <h1 class="govuk-panel__title">@Model.StateDisplayName</h1>
            <p class="govuk-panel__body">Please provide the following information</p>
        </div>

        <!-- Error summary -->
        @if (Model.Problems.Any())
        {
            <div class="govuk-error-summary">
                <h2 class="govuk-error-summary__title">There is a problem</h2>
                <ul class="govuk-list govuk-error-summary__list">
                    @foreach (var problem in Model.Problems)
                    {
                        <li><a href="#@problem.FieldKey">@problem.Message</a></li>
                    }
                </ul>
            </div>
        }

        <!-- Form -->
        <form method="post">
            @foreach (var fieldGroup in Model.FieldGroups)
            {
                <fieldset class="govuk-fieldset">
                    <legend class="govuk-fieldset__legend govuk-fieldset__legend--l">
                        <h2 class="govuk-fieldset__heading">@fieldGroup.DisplayName</h2>
                    </legend>

                    @foreach (var field in fieldGroup.Fields)
                    {
                        <!-- Render field based on type (text, email, select, etc.) -->
                        @Html.Partial("_WorkflowField-" + field.FieldType, field)
                    }
                </fieldset>
            }

            <!-- Action buttons -->
            <div class="govuk-button-group">
                @foreach (var action in Model.AvailableActions)
                {
                    <button type="submit" name="Action" value="@action.Key" class="govuk-button">
                        @action.Label
                    </button>
                }
            </div>
        </form>
    </div>
</div>
```

#### 2. `_WorkflowStep-check-answers.cshtml`

Renders a `check-answers` step — read-only review of all submitted data with "Change" links.

**Location to override:** `Views/Partials/_WorkflowStep-check-answers.cshtml`

**Example override:**
```cshtml
@model PrismServiceRequestViewModel

<div class="govuk-width-container">
    <div class="govuk-main-wrapper">
        <h1 class="govuk-heading-l">@Model.StateDisplayName</h1>
        <p>Check your answers before submitting.</p>

        <!-- Display all field values in a definition list -->
        <dl class="govuk-summary-list">
            @foreach (var fieldGroup in Model.FieldGroups)
            {
                <h2 class="govuk-heading-m">@fieldGroup.DisplayName</h2>
                @foreach (var field in fieldGroup.Fields)
                {
                    var value = Model.FormValues.ContainsKey(field.FieldKey) 
                        ? Model.FormValues[field.FieldKey] 
                        : "(Not provided)";

                    <div class="govuk-summary-list__row">
                        <dt class="govuk-summary-list__key">@field.Label</dt>
                        <dd class="govuk-summary-list__value">@value</dd>
                        <dd class="govuk-summary-list__actions">
                            <a class="govuk-link" href="#@field.FieldKey">Change</a>
                        </dd>
                    </div>
                }
            }
        </dl>

        <!-- Action buttons -->
        <div class="govuk-button-group">
            @foreach (var action in Model.AvailableActions)
            {
                <button type="submit" name="Action" value="@action.Key" class="govuk-button">
                    @action.Label
                </button>
            }
        </div>
    </div>
</div>
```

#### 3. `_WorkflowStep-status-timeline.cshtml`

Renders a `status-timeline` step — shows progress and current status.

**Location to override:** `Views/Partials/_WorkflowStep-status-timeline.cshtml`

#### 4. `_WorkflowStep-task-list.cshtml`

Renders a `task-list` step — shows tasks with individual completion statuses.

**Location to override:** `Views/Partials/_WorkflowStep-task-list.cshtml`

#### 5. `_WorkflowStep-confirmation.cshtml`

Renders a `confirmation` step — success message, reference number, next steps.

**Location to override:** `Views/Partials/_WorkflowStep-confirmation.cshtml`

**Example override:**
```cshtml
@model PrismServiceRequestViewModel

<div class="govuk-width-container">
    <div class="govuk-main-wrapper">
        <div class="govuk-panel govuk-panel--confirmation">
            <h1 class="govuk-panel__title">@Model.StateDisplayName</h1>
            <p class="govuk-panel__body">
                Your application reference is <strong>@Model.InstanceId</strong>
            </p>
        </div>

        <h2 class="govuk-heading-m">What happens next?</h2>
        <p>We'll review your application and contact you within 5 working days.</p>

        <div class="govuk-button-group">
            @foreach (var action in Model.AvailableActions)
            {
                <a href="/" class="govuk-button">@action.Label</a>
            }
        </div>
    </div>
</div>
```

---

## Overriding Field Partials

Each field type has its own partial template. Override these to customize how specific fields are rendered.

### Field Partial Locations

- `Views/Partials/_WorkflowField-text.cshtml`
- `Views/Partials/_WorkflowField-email.cshtml`
- `Views/Partials/_WorkflowField-number.cshtml`
- `Views/Partials/_WorkflowField-textarea.cshtml`
- `Views/Partials/_WorkflowField-select.cshtml`
- `Views/Partials/_WorkflowField-radio.cshtml`
- `Views/Partials/_WorkflowField-checkbox.cshtml`
- `Views/Partials/_WorkflowField-checkboxes.cshtml`
- `Views/Partials/_WorkflowField-date-input.cshtml`
- `Views/Partials/_WorkflowField-file-upload.cshtml`

### Example: Custom Text Field Partial

**File:** `Views/Partials/_WorkflowField-text.cshtml`

```cshtml
@model FieldRenderPayload

<div class="govuk-form-group @(Model.HasError ? "govuk-form-group--error" : "")">
    <label class="govuk-label" for="@Model.FieldKey">
        @Model.Label
        @if (Model.Required)
        {
            <span class="govuk-hint">(required)</span>
        }
    </label>

    @if (!string.IsNullOrEmpty(Model.HintText))
    {
        <div id="@(Model.FieldKey)-hint" class="govuk-hint">@Model.HintText</div>
    }

    @if (Model.HasError)
    {
        <p id="@(Model.FieldKey)-error" class="govuk-error-message">
            <span class="govuk-visually-hidden">Error:</span> @Model.ErrorMessage
        </p>
    }

    <input class="govuk-input @(Model.HasError ? "govuk-input--error" : "")"
           id="@Model.FieldKey"
           name="fields[@Model.FieldKey]"
           type="text"
           maxlength="@Model.MaxLength"
           @(Model.Required ? "required" : "")
           @(Model.ReadOnly ? "readonly" : "")
           value="@(Model.Value ?? Model.DefaultValue ?? "")" />
</div>
```

---

## Advanced: Custom Styling for Specific Step Types

You can apply custom CSS based on the step type currently displayed. The `PrismServiceRequestViewModel` includes the `StepType` property, allowing you to apply conditional styling based on which step is active.

**File:** `Views/Partials/_WorkflowStep-question.cshtml`

```cshtml
@model PrismServiceRequestViewModel

<div class="govuk-width-container service-blueprint-step service-blueprint-step--@Model.StepType.ToLower()">
    <!-- Step content -->
</div>
```

**File:** `wwwroot/css/my-service-blueprint-theme.css`

```css
/* Custom styling for question steps */
.service-blueprint-step--question {
    background-color: #f3f6f9;
    border-left: 4px solid #0b3e6f;
    padding: 2rem;
}

/* Custom styling for confirmation steps */
.service-blueprint-step--confirmation {
    text-align: center;
    background-color: #f3faf5;
    border-radius: 8px;
}

.service-blueprint-step--confirmation h1 {
    color: #00703c;
    font-size: 2rem;
}
```

---

## Adding Custom JavaScript

If you need custom behavior (e.g., analytics tracking, custom validation, dynamic field updates), add JavaScript in your layout or partial.

**File:** `Views/Master.cshtml`

```cshtml
<body>
    @RenderSection("content", required: true)

    <!-- Prism core scripts -->
    <script src="~/js/govuk-frontend.min.js"></script>
    <script>window.GOVUKFrontend.initAll();</script>

    <!-- Your custom service-blueprint logic -->
    <script src="~/js/service-blueprint-custom.js"></script>
</body>
```

**File:** `wwwroot/js/service-blueprint-custom.js`

```javascript
// Track form submissions for analytics
document.addEventListener('submit', (e) => {
    if (e.target.classList.contains('service-blueprint-form')) {
        const formData = new FormData(e.target);
        const action = formData.get('Action');
        console.log(`User submitted service-blueprint action: ${action}`);
        // Send to analytics provider
    }
});

// Example: Auto-save draft periodically
setInterval(() => {
    const form = document.querySelector('.service-blueprint-form');
    if (form && form.querySelector('[name="Action"][value="save-draft"]')) {
        // Optional: submit a "save-draft" action periodically
    }
}, 60000); // Every 60 seconds
```

---

## Accessibility Considerations

When customizing, keep these accessibility principles in mind:

1. **Color contrast** — Ensure text meets WCAG AA standards (4.5:1 for normal text)
2. **Focus indicators** — Never remove focus rings; use `--prism-focus-color` to style them
3. **Error messages** — Link errors to fields using `aria-describedby`
4. **Semantic HTML** — Use `<fieldset>` and `<legend>` for grouping, `<label>` for every input
5. **Skip links** — Add a "skip to main content" link (see GDS documentation)
6. **ARIA labels** — For complex components, add `aria-label`, `aria-live`, etc.

Example:
```cshtml
<div class="govuk-form-group">
    <label for="email" class="govuk-label">Email address</label>
    <input type="email" 
           id="email" 
           name="email"
           class="govuk-input"
           aria-describedby="email-hint email-error" />
    <div id="email-hint" class="govuk-hint">Use your work email</div>
    <div id="email-error" class="govuk-error-message">Invalid email format</div>
</div>
```

---

## Summary: Customization Approaches

| Customization | Level | Effort | When to Use |
|---------------|-------|--------|-----------|
| CSS variables override | Light | 5 mins | Quick brand change (colors, fonts, spacing) |
| Partial override | Medium | 30 mins | Restructure a step type's HTML |
| Field partial override | Medium | 20 mins | Change how a specific field renders |
| Custom CSS + JavaScript | Heavy | Hours | Advanced interactions, analytics, custom logic |

**Recommended starting point:** Override CSS variables. If you need more control, then override partials.

---

**Next steps:**
- [Form Validation](./service-request-forms-validation.md) — understand validation layers
- [GDS Components](./service-blueprint-gds-components.md) — available form elements and design patterns
