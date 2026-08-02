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
2. **Razor partial overrides** — Replace shell or component templates to customize HTML/markup
3. **Custom CSS** — Add your own styles on top of GDS

---

## CSS Variables & Theme Customization

The easiest way to customize Prism: override CSS variables. All Prism styling uses custom properties (`--prism-*`). The reference implementation's own variables live in `src/UmbracoPrism.TestSite/wwwroot/branding/*.css` (colors, typography, layout, forms) — a real host defines its own equivalent stylesheet setting the same variable names.

### Supported CSS Variables (the reference implementation's actual set)

#### Colors (`prism-colors.css`)

```css
--prism-text: #0b0c0c;
--prism-muted: #505a5f;
--prism-primary: #1d70b8;
--prism-primary-contrast: #ffffff;
--prism-accent: #00703c;
--prism-link: #1d70b8;
--prism-focus: #ffdd00;               /* Focus ring color (yellow accessibility standard) */
--prism-surface: #ffffff;
--prism-surface-alt: #f3f2f1;
--prism-border: #b1b4b6;
--prism-success: #00703c;
--prism-warning: #f47738;
--prism-danger: #d4351c;
```

#### Forms (`prism-forms.css` — mostly derived from the color variables above)

```css
--prism-form-group-spacing: 20px;
--prism-input-border: 2px solid var(--prism-text);
--prism-input-border-radius: 0;
--prism-input-padding: 8px 12px;
--prism-input-font-size: 19px;
--prism-input-focus-outline: 3px solid var(--prism-focus);
--prism-label-font-size: 19px;
--prism-label-font-weight: 700;
--prism-hint-color: var(--prism-muted);
--prism-hint-font-size: 16px;
--prism-required-color: var(--prism-danger);
--prism-button-font-size: 19px;
--prism-button-padding: 8px 16px 7px;
--prism-actions-gap: 1rem;

/* Confirmation panel */
--prism-panel-confirmation-border-color: var(--prism-success);
--prism-panel-confirmation-bg: color-mix(in srgb, var(--prism-success) 8%, var(--prism-surface));
--prism-panel-confirmation-padding: 1.5rem;

/* Check-answers (the "review" shell) */
--prism-review-dt-color: var(--prism-muted);
--prism-review-item-border: 1px solid var(--prism-border);

/* Errors */
--prism-error: var(--prism-danger);
--prism-error-bg: color-mix(in srgb, var(--prism-danger) 8%, var(--prism-surface));
--prism-error-border: var(--prism-danger);
```

#### Layout (`prism-layout.css`)

```css
--prism-page-max: 960px;
--prism-page-gutter: 30px;
--prism-section-gap: 30px;
--prism-radius: 0;
```

#### Typography (`prism-typography.css`)

```css
--prism-font-body: "GDS Transport", -apple-system, BlinkMacSystemFont, "Segoe UI", "Helvetica Neue", Arial, sans-serif;
--prism-font-display: "GDS Transport", -apple-system, BlinkMacSystemFont, "Segoe UI", "Helvetica Neue", Arial, sans-serif;
--prism-heading-weight: 700;
--prism-body-weight: 400;
--prism-text-size-xl: 48px;
--prism-text-size-lg: 24px;
--prism-text-size-md: 19px;
--prism-text-size-sm: 16px;
```

### How to Override CSS Variables

Create a custom CSS file in your Umbraco project and reference it in your layout, after Prism's own branding stylesheet so your overrides win.

**File:** `wwwroot/css/my-service-blueprint-theme.css`

```css
:root {
    --prism-primary: #d32f2f;
    --prism-accent: #388e3c;
    --prism-form-group-spacing: 28px;
    --prism-font-body: Georgia, serif;
}
```

**File:** `Views/Shared/Master.cshtml` (see `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml` for the real reference layout)

```cshtml
<head>
    <link rel="stylesheet" href="/css/govuk-frontend.min.css" />
    <link rel="stylesheet" href="/branding/prism-branding.css" />

    <!-- Your custom theme overrides, after Prism's own -->
    <link rel="stylesheet" href="~/css/my-service-blueprint-theme.css">
</head>
```

**Result:** All Prism service blueprint UI automatically uses your colors, fonts, and spacing.

---

## Overriding Razor Partials

For deeper customization — changing HTML structure, adding custom elements, reorganizing sections — override the shell or component partials. They ship compiled inside the [`Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco) package (a real Razor Class Library, `AddRazorSupportForMvc`), but **not** at the path a naive "RCL override" assumption would suggest: ASP.NET Core resolves a precompiled view at an exact virtual path immediately and never checks whether your app also defines a runtime-compiled view at that identical path, so if the package's own defaults lived at the same path you'd place an override, your override would be silently ignored. Wayfinder.Umbraco avoids that by keeping its own built-in catalog at a package-internal path (`~/Views/Partials/_WayfinderComponents/`, `~/Views/Partials/_WayfinderFields/`) and having `Wayfinder.Umbraco.Services.ComponentPartialResolver` check your app's own `~/Views/Partials/Components/`/`~/Views/Partials/Fields/` path *first*, explicitly, falling back to the package's own default only if nothing's there — a genuine host-wins override, just implemented rather than inherited from the framework for free. A new override file is picked up on your app's next restart (the resolver caches its decision per component/field type for the process lifetime, so a render is never slowed down by a filesystem check).

### Shell Partials

The shell rendered for a stage is inferred from its authored components (see [Client rendering](../design/service-request-forms-engine-client.md#shell-selection)), not authored as a separate field:

| Shell | Partial |
| --- | --- |
| `question` | `Views/Partials/_Stage-Question.cshtml` |
| `check-answers` | `Views/Partials/_Stage-Review.cshtml` |
| `confirmation` | `Views/Partials/_Stage-Completion.cshtml` |
| `status-timeline` | `Views/Partials/_Stage-StatusTimeline.cshtml` |
| `task-list` | `Views/Partials/_Stage-TaskList.cshtml` |
| `waiting` | `Views/Partials/_Stage-Waiting.cshtml` |

**Model:** `ServiceRequestPageViewModel` (`Wayfinder.Umbraco.Models`) — real properties, not a `FieldGroups`-based model:

```csharp
public class ServiceRequestPageViewModel
{
    public string InstanceId { get; set; }
    public int StateVersion { get; set; }
    public string BlueprintKey { get; set; }
    public string ReturnUrl { get; set; }
    public string StepType { get; set; }
    public string StateDisplayName { get; set; }
    public IReadOnlyList<ComponentRenderPayload> Components { get; set; }
    public IReadOnlyList<ServiceRequestAction> AvailableActions { get; set; }
    public IReadOnlyList<ServiceRequestProblem> Problems { get; set; }
    public string Nonce { get; set; }
    public IReadOnlyDictionary<string, string> FormValues { get; set; }
    public string? LiveModelJson { get; set; }
    public int? PollAfterMs { get; set; }
    // FieldErrors and AllFields are computed from Components/Problems
}
```

**The real `_Stage-Question.cshtml`** (from `Wayfinder.Umbraco`) — the pattern any override should follow: the `<wayfinder-stage-form>`/`<wayfinder-component>` tag helpers do the real work, so a shell override is mostly about structure, not re-implementing field rendering:

```cshtml
@model Wayfinder.Umbraco.Models.ServiceRequestPageViewModel

<wayfinder-stage-form instance-id="@Model.InstanceId"
                   state-version="@Model.StateVersion"
                   blueprint-key="@Model.BlueprintKey"
                   return-url="@Model.ReturnUrl"
                   nonce="@Model.Nonce">

    <wayfinder-error-summary problems="@Model.Problems" />

    @foreach (var component in Model.Components)
    {
        <wayfinder-component component="@component"
                          errors="@Model.FieldErrors"
                          values="@Model.FormValues"
                          return-url="@Model.ReturnUrl"
                          instance-id="@Model.InstanceId"
                          state-version="@Model.StateVersion"
                          blueprint-key="@Model.BlueprintKey"
                          nonce="@Model.Nonce" />
    }

    <div class="govuk-button-group">
        @foreach (var action in Model.AvailableActions)
        {
            var btnClass = action.Style switch
            {
                "primary" => "govuk-button",
                "destructive" => "govuk-button govuk-button--warning",
                _ => "govuk-button govuk-button--secondary"
            };
            <button type="submit" name="Action" value="@action.ActionKey" class="@btnClass" data-module="govuk-button">
                @action.Label
            </button>
        }
    </div>

</wayfinder-stage-form>
```

`<wayfinder-stage-form>` (`StageFormTagHelper`) writes the antiforgery token and the hidden `InstanceId`/`StateVersion`/`BlueprintKey`/`ReturnUrl`/`Nonce` fields for you — a shell override doesn't need to rebuild that plumbing.

---

## Overriding Component and Field Partials

Each authored component `type` dispatches to its own partial by naming convention (kebab-case → PascalCase) — see [Customising rendering](./service-blueprint-setup.md#customising-rendering) for the full mechanism.

### Field Partial Locations

`Views/Partials/Fields/` in your own app (one per input field type — `text`, `number`, `decimal`, `select`, `radio`, `checkboxlist`, `date`, `email`, `textarea`, `boolean`, `slider`, `file-upload`, `guidance-checklist`). Wayfinder.Umbraco's own built-in versions of these live at `Views/Partials/_WayfinderFields/` inside the package — see the note above on why the two paths are deliberately different:

- `_Component-Text.cshtml`
- `_Component-Email.cshtml`
- `_Component-Number.cshtml`
- `_Component-Decimal.cshtml`
- `_Component-Textarea.cshtml`
- `_Component-Select.cshtml`
- `_Component-Radio.cshtml`
- `_Component-Boolean.cshtml`
- `_Component-Checkboxlist.cshtml`
- `_Component-Date.cshtml`
- `_Component-FileUpload.cshtml`
- `_Component-GuidanceChecklist.cshtml`
- `_Component-Slider.cshtml`
- `_Component-Default.cshtml` (fallback for an unrecognised type)

### Example: Custom Text Field Partial

**File:** `Views/Partials/Fields/_Component-Text.cshtml`

Every field partial receives `Wayfinder.Umbraco.Models.FieldContext` — pre-built ARIA attributes, CSS classes, and the resolved display value, so partials stay declarative. This is the real built-in partial:

```cshtml
@model Wayfinder.Umbraco.Models.FieldContext
<div class="@Model.WrapperClass"@Html.Raw(Model.WrapperAttrs)>
    @await Html.PartialAsync("~/Views/Partials/Fields/_ComponentLabel.cshtml", Model)
    <input class="govuk-input@(Model.ReadOnlyCssClass)@(Model.HasFieldError ? " govuk-input--error" : "")"
           type="text"
           id="@Model.Field.FieldKey"
           name="fields[@Model.Field.FieldKey]"
           value="@Model.DisplayValue"
           data-label="@Model.Field.Label"@Html.Raw(Model.DescribedBy)@Html.Raw(Model.RequiredAttr)@Html.Raw(Model.AriaRequired)@Html.Raw(Model.AriaInvalid)@Html.Raw(Model.ReadOnlyAttr)@Html.Raw(Model.MinLengthAttr)@Html.Raw(Model.MaxLengthAttr)@Html.Raw(Model.PatternAttr) />
    @if (Model.Field.ReadOnly)
    {
        <input type="hidden" name="fields[@Model.Field.FieldKey]" value="@Model.DisplayValue" />
    }
</div>
```

`FieldContext`'s other pre-built properties: `HasFieldError`/`FieldError`, `HintId`/`ErrorId`/`DescribedBy`, `AriaRequired`/`AriaInvalid`/`ReadOnlyAttr`, `MinLengthAttr`/`MaxLengthAttr`/`PatternAttr`/`MinAttr`/`MaxAttr`/`StepAttr`.

---

## Adding Custom JavaScript

If you need custom behavior (e.g., analytics tracking, custom validation, dynamic field updates), add JavaScript in your layout or partial. GOV.UK Frontend v5 ships as an ES module — load it as one, not a classic script:

**File:** `Views/Shared/Master.cshtml`

```cshtml
<body>
    @RenderSection("content", required: true)

    <script type="module">
        import { initAll } from '/js/govuk-frontend.min.js';
        initAll();
    </script>

    <!-- Your custom service-blueprint logic -->
    <script src="~/js/service-blueprint-custom.js" defer></script>
</body>
```

**File:** `wwwroot/js/service-blueprint-custom.js`

```javascript
// Track form submissions for analytics
document.addEventListener('submit', (e) => {
    if (e.target.matches('form')) {
        const formData = new FormData(e.target);
        const action = formData.get('Action');
        console.log(`User submitted service-request action: ${action}`);
        // Send to analytics provider
    }
});
```

---

## Accessibility Considerations

When customizing, keep these accessibility principles in mind:

1. **Color contrast** — Ensure text meets WCAG AA standards (4.5:1 for normal text)
2. **Focus indicators** — Never remove focus rings; use `--prism-focus` to style them
3. **Error messages** — Link errors to fields using `aria-describedby` (already pre-built on `FieldContext.DescribedBy`)
4. **Semantic HTML** — Use `<fieldset>` and `<legend>` for grouping, `<label>` for every input
5. **Skip links** — Add a "skip to main content" link (see GDS documentation)
6. **ARIA labels** — For complex components, add `aria-label`, `aria-live`, etc.

---

## Summary: Customization Approaches

| Customization | Level | When to Use |
|---------------|-------|-----------|
| CSS variables override | Light | Quick brand change (colors, fonts, spacing) |
| Shell partial override | Medium | Restructure a stage shell's HTML |
| Component/field partial override | Medium | Change how a specific component or field renders |
| Custom CSS + JavaScript | Heavy | Advanced interactions, analytics, custom logic |

**Recommended starting point:** Override CSS variables. If you need more control, then override partials.

---

**Next steps:**
- [Form Validation](./service-request-forms-validation.md) — understand validation layers
- [GDS Components](./service-blueprint-gds-components.md) — available form elements and design patterns
