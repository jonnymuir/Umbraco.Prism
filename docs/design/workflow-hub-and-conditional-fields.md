# Workflow Hub & Conditional Fields — Architecture Design

> **⚠️ v2.0 UPDATE (April 2026):**  
> - **Conditional fields** have been implemented in v2.0 as `ConditionalChildren` on Radios and Checkboxes components only (the "Other → specify" pattern).
> - **Generic conditional visibility** (`ConditionalOn`/`VisibleWhen` on arbitrary components) is deferred to v2.1.
> - **Workflow Hub** has been implemented in v2.0 and is stable.
> - This document reflects the v1 design. The v2 implementation uses polymorphic components instead of flat field arrays.
> - For v2 usage, see the [workflow walkthroughs](../walkthroughs/) and builder API documentation.

## Overview

This document designs two key enhancements to Prism's workflow engine:

1. **Conditional fields** — the "Other → specify" pattern for dynamic field visibility
2. **Workflow Hub** — a member dashboard for managing multiple workflow instances
3. **Extension points** — customization patterns for hub views and instance lists

These features enable richer form UX and multi-instance workflow management while preserving Prism's core principle: **"Make it easy to do the right thing; principle of least surprise."**

---

## Responsibility split

| Feature | Owner | Where |
|---|---|---|
| Conditional field data model extension | 🟠 Business App | Field group JSON definitions |
| Conditional field server-side validation | 🔵 Prism Platform | `WorkflowFieldValidator` |
| Conditional field client-side visibility | 🔵 Prism Platform | CSS + small JS enhancement |
| Workflow instance tracking | 🟠 Business App | New `GetInstancesAsync()` endpoint |
| Workflow Hub document type | 🔵 Prism Platform | Seeded via `PrismContentTypeSeeder` |
| Workflow Hub controller | 🔵 Prism Platform | `WorkflowHubController` |
| Instance policy enforcement | 🔵 Prism Platform | `WorkflowPageController` + new archetype |
| Hub view customization | 🔵 Prism Platform (developer) | Partial template overrides |

---

## Design 1: Conditional "Other" field pattern

### The pattern

A common form UX pattern:

```
What type of enquiry is this?
  ○ General
  ○ Technical Support
  ○ Billing
  ○ Other
  
  [When "Other" is selected, a text field appears:]
  Please specify: [_________________]
```

This is currently impossible in Prism without creating a separate workflow state. The conditional field pattern makes it trivial.

---

### Data model: v2.0 polymorphic components

**In v2.0, conditional fields are implemented via `ConditionalChildren` on `RadiosComponent` and `CheckboxesComponent`:**

```csharp
/// <summary>
/// Radios component with optional conditional children (v2.0).
/// </summary>
public record RadiosComponent : PrismComponent
{
    public required string Label { get; init; }
    public string? Hint { get; init; }
    public required bool Required { get; init; }
    public required string[] Options { get; init; }
    
    /// <summary>
    /// Optional conditional children: maps option values to arrays of child components.
    /// When an option is selected, its corresponding children are revealed.
    /// Example: "Other" → [TextInput component for "Please specify"]
    /// </summary>
    public Dictionary<string, PrismComponent[]>? ConditionalChildren { get; init; }
}
```

**Design rationale:**
- Simple, declarative, minimal — nested children under their parent option
- Follows Prism's "easy to do the right thing" principle
- Component tree remains hierarchical and type-safe
- Deferred generic `ConditionalOn`/`VisibleWhen` to v2.1 to keep v2.0 lean

**v1 vs v2 comparison:**

| Aspect | v1 (this doc) | v2.0 (implemented) |
|--------|---------------|-------------------|
| Conditional model | Flat `fields[]` with `conditionalOn`/`visibleWhen` | Nested `conditionalChildren` on parent component |
| Supported parents | Any field | Radios, Checkboxes only |
| Generic conditionals | Proposed | Deferred to v2.1 |
| Field definition | `{ fieldType: "text", ... }` | `{ type: "text", ... }` (polymorphic) |

---

### Example: JSON component definition (v2.0 polymorphic schema)

```json
{
  "type": "fieldset",
  "legend": "Enquiry Details",
  "children": [
    {
      "type": "radio",
      "fieldKey": "enquiry-type",
      "label": "What type of enquiry is this?",
      "required": true,
      "options": ["General", "Technical Support", "Billing", "Other"],
      "conditionalChildren": {
        "Other": [
          {
            "type": "text",
            "fieldKey": "enquiry-type-other",
            "label": "Please specify your enquiry type",
            "required": false,
            "maxLength": 100
          }
        ]
      }
    }
  ]
}
```

**v2.0 implementation notes:**
- In v2, conditional fields are part of `conditionalChildren` on the parent Radios/Checkboxes component
- `conditionalChildren` is a dictionary mapping option values to arrays of child components
- Conditional children are rendered inline when their parent option is selected
- Generic `conditionalOn`/`visibleWhen` on arbitrary components is deferred to v2.1

**Key details:**
- `enquiry-type-other` is nested under the `"Other"` key in `conditionalChildren`
- `required: false` at definition time; validation checks if parent option is selected
- BA controls the logic entirely via JSON; Prism renders it faithfully

---

### Server-side validation (🔵 Prism Platform)

**Extension to `WorkflowFieldValidator`:**

Conditional fields should only be validated if their trigger condition is met.

**New validation rule:**

```csharp
// In WorkflowFieldValidator.Validate():

foreach (var field in authoritativeFields)
{
    // Skip validation for hidden conditional fields
    if (!string.IsNullOrEmpty(field.ConditionalOn))
    {
        var triggerValue = submittedFields.GetValueOrDefault(field.ConditionalOn);
        if (triggerValue != field.VisibleWhen)
        {
            // Conditional field is hidden — skip all validation
            continue;
        }
        
        // Conditional field IS visible — validate normally
        // (If the BA marked it Required, treat it as required while visible)
    }
    
    // Existing validation logic here...
}
```

**Design rationale:**
- A conditional field is only validated when its trigger condition is met
- If a field has `Required = true` at definition time and is currently visible, enforce requirement
- If a field is hidden, its submitted value is ignored entirely (even if present; prevents client-side tampering)
- Clear security boundary: BA defines fields; Prism validates according to visibility state

**Edge case handling:**
- If the trigger field (`ConditionalOn`) is itself conditional: follow the chain (max depth = 2 for sanity)
- If `VisibleWhen` value doesn't match any option in the trigger field: BA's responsibility (log a warning; treat as always-hidden)

---

### Client-side rendering (🔵 Prism Platform)

**Approach: Pure CSS + minimal JS enhancement**

#### CSS-only base layer

The conditional field is rendered in HTML by default but hidden with CSS:

```razor
@* In _WorkflowField-*.cshtml *@

<div class="prism-field @(Model.ConditionalOn != null ? "prism-field--conditional" : "")"
     data-conditional-on="@Model.ConditionalOn"
     data-visible-when="@Model.VisibleWhen"
     hidden>
  @* Field markup here *@
</div>
```

```css
/* In prism-workflow.css */

.prism-field--conditional[hidden] {
  display: none;
}
```

**No-JS fallback:** The field is rendered in the DOM but hidden. If JS fails, the field stays hidden. Server-side validation ignores hidden fields, so no data loss.

#### JavaScript enhancement

A small vanilla JS module watches the trigger field and toggles `hidden` attribute:

```js
// prism-conditional-fields.js

document.querySelectorAll('.prism-field--conditional').forEach(field => {
  const triggerKey = field.dataset.conditionalOn;
  const visibleValue = field.dataset.visibleWhen;
  
  const triggerField = document.querySelector(`[name="fields[${triggerKey}]"]`);
  if (!triggerField) return;
  
  const updateVisibility = () => {
    const currentValue = triggerField.value || 
                         Array.from(triggerField.querySelectorAll(':checked'))
                              .map(c => c.value)[0];
    
    const shouldShow = currentValue === visibleValue;
    field.hidden = !shouldShow;
    field.setAttribute('aria-hidden', !shouldShow);
    
    // If shown, focus the field (with small delay for layout reflow)
    if (shouldShow) {
      setTimeout(() => field.querySelector('input, textarea, select')?.focus(), 100);
    }
    
    // If hidden, clear its value
    if (!shouldShow) {
      const input = field.querySelector('input, textarea, select');
      if (input) input.value = '';
    }
  };
  
  triggerField.addEventListener('change', updateVisibility);
  updateVisibility(); // Initialize on page load
});
```

**Design rationale:**
- Zero external dependencies; works on all modern browsers
- Graceful degradation: no JS = field hidden but server validates correctly
- Small (<1KB minified), runs once on DOMContentLoaded

---

### Accessibility considerations

**ARIA attributes:**

```razor
<div class="prism-field-group" aria-live="polite">
  @* Trigger field *@
  <div class="prism-field">
    <label for="enquiry-type">What type of enquiry is this?</label>
    <input type="radio" name="fields[enquiry-type]" value="Other" 
           aria-controls="enquiry-type-other-container">
  </div>
  
  @* Conditional field *@
  <div class="prism-field prism-field--conditional" 
       id="enquiry-type-other-container"
       data-conditional-on="enquiry-type"
       data-visible-when="Other"
       hidden
       aria-hidden="true">
    <label for="enquiry-type-other">Please specify your enquiry type</label>
    <input type="text" name="fields[enquiry-type-other]" id="enquiry-type-other">
  </div>
</div>
```

**Testing with VoiceOver (macOS):**
1. Navigate to the radio group
2. Select "Other"
3. Screen reader should announce: "Please specify your enquiry type, text field, edit text"
4. Field receives focus automatically (with delay)
5. When "Other" is deselected, field disappears and focus returns to radio group

**Design notes:**
- `aria-live="polite"` on the containing group announces changes without interrupting
- `aria-controls` links the trigger to the controlled field
- Auto-focus when revealed improves keyboard nav (but only after layout reflow to prevent scroll jump)

---

### UX assessment: When is "Other" good UX?

**When it's good:**
- You have a known set of common options (5-10) and genuinely unpredictable edge cases
- The "Other" responses are rare (<10%) and you'll review them manually
- Example: "How did you hear about us?" — you can't predict every marketing channel

**When it's a smell:**
- You expect many "Other" responses → your option list is incomplete
- You're using it to avoid making decisions about categorization
- The "Other" text becomes unstructured data you can't act on

**Recommended alternatives:**
- **Large option sets:** Use a searchable select/autocomplete instead of radio + Other
- **Frequent "Other":** Collect feedback on common responses and add them as first-class options
- **Open-ended data:** Just use a text field and skip the radio group entirely

**For Prism:** We provide the tool; the BA owns the UX decision. Include this guidance in docs.

---

## Design 2: Workflow Hub / Status Dashboard

### The problem

Members interact with multiple workflow definitions:
- Apply for community grant
- Book a facility
- Submit a complaint
- Register for an event

Currently, each workflow is an isolated page. If a member has an in-progress grant application and an in-progress facility booking, there's no way to see both in one place.

**Goal:** Provide a unified dashboard where members can:
1. See all active (resumable) workflow instances
2. See all completed workflow instances (reference/history)
3. Resume an in-progress instance
4. Start a new instance (with multi-instance policy enforcement)

---

### Document type: `workflowHub`

**New document type seeded by `PrismContentTypeSeeder`:**

```csharp
// Pseudo-code for seeder extension

var workflowHub = new ContentType("workflowHub")
{
    Name = "Workflow Hub",
    Alias = "workflowHub",
    Icon = "icon-list",
    AllowedAsRoot = true,
    AllowedTemplates = new[] { hubTemplate }
};

workflowHub.AddPropertyGroup("Content")
    .AddPropertyType("title", "Textstring", "Hub Title")
    .AddPropertyType("introText", "Textarea", "Introduction Text");
```

**Properties (minimal for MVP):**
- `title` (string): Page heading (default: "My Workflows")
- `introText` (RTE): Optional intro copy above the instance list

**Design rationale:**
- Zero-config seeding: developers just create a page of type `workflowHub` in backoffice
- Umbraco-native: uses existing document type + template system
- Extensible: additional properties can be added later (e.g., "Show completed instances?")

---

### Route-hijacking controller: `WorkflowHubController`

**New controller in `UmbracoPrism.Core`:**

```csharp
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowHubController(
    ILogger<WorkflowHubController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IBusinessAppWorkflowClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IPublishedContentQuery contentQuery)
    : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
{
    public override async Task<IActionResult> Index()
    {
        // Call BA to get member's workflow instances
        var envelope = await workflowClient.GetInstancesAsync();
        
        if (envelope.ResponseState == "error")
        {
            return CurrentTemplate(ErrorViewModel(envelope));
        }
        
        // Resolve Umbraco page URLs for each instance
        var instancesWithUrls = envelope.Instances
            .Select(i => ResolveResumeUrl(i))
            .ToList();
        
        var vm = new WorkflowHubViewModel(CurrentPage!, publishedValueFallback)
        {
            ActiveInstances = instancesWithUrls.Where(i => i.CanContinue).ToList(),
            CompletedInstances = instancesWithUrls.Where(i => !i.CanContinue).ToList()
        };
        
        return CurrentTemplate(vm);
    }
    
    private WorkflowInstanceViewModel ResolveResumeUrl(WorkflowInstanceSummary summary)
    {
        // Find the workflow page for this workflowKey
        var workflowPage = contentQuery
            .Content
            .FirstOrDefault(c => c.ContentType.Alias == "workflowPage" 
                                  && c.Value<string>("workflowKey") == summary.WorkflowKey);
        
        return new WorkflowInstanceViewModel(summary)
        {
            ResumeUrl = workflowPage?.Url() ?? "#"
        };
    }
}
```

**Design rationale:**
- Follows existing `WorkflowPageController` pattern: route-hijacking, strongly-typed VM
- Resolves URLs on Prism side (BA doesn't need to know Umbraco routing)
- Error handling consistent with workflow pages

---

### Business App extension: `GetInstancesAsync()`

**New method on `IBusinessAppWorkflowClient`:**

```csharp
/// <summary>
/// Retrieves all workflow instances for the authenticated member.
/// Returns both active (resumable) and completed instances.
/// </summary>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A list of workflow instance summaries.</returns>
Task<WorkflowInstanceListEnvelope> GetInstancesAsync(
    CancellationToken cancellationToken = default);
```

**Response model (in `UmbracoPrism.Shared`):**

```csharp
public record WorkflowInstanceListEnvelope
{
    public required string ResponseState { get; init; } // "ok" or "error"
    public IReadOnlyList<WorkflowInstanceSummary> Instances { get; init; } = Array.Empty<WorkflowInstanceSummary>();
    public IReadOnlyList<WorkflowProblem> Problems { get; init; } = Array.Empty<WorkflowProblem>();
}

public record WorkflowInstanceSummary
{
    /// <summary>
    /// Gets the unique workflow instance identifier.
    /// </summary>
    public required string InstanceId { get; init; }
    
    /// <summary>
    /// Gets the workflow definition key (e.g. "community-grant").
    /// </summary>
    public required string WorkflowKey { get; init; }
    
    /// <summary>
    /// Gets the user-friendly workflow name (e.g. "Community Grant Application").
    /// </summary>
    public required string WorkflowDisplayName { get; init; }
    
    /// <summary>
    /// Gets the current state key.
    /// </summary>
    public required string CurrentStateKey { get; init; }
    
    /// <summary>
    /// Gets the user-friendly state name (e.g. "Under Review").
    /// </summary>
    public required string CurrentStateDisplayName { get; init; }
    
    /// <summary>
    /// Gets the archetype of the current state.
    /// Valid values: "Collect", "StatusTimeline", "Completion".
    /// </summary>
    public required string Archetype { get; init; }
    
    /// <summary>
    /// Gets when the workflow instance was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Gets when the workflow instance was last updated.
    /// </summary>
    public required DateTime LastUpdatedAt { get; init; }
    
    /// <summary>
    /// Gets whether the member can continue this workflow (true = active, false = completed).
    /// </summary>
    public required bool CanContinue { get; init; }
}
```

**Design rationale:**
- BA is the source of truth for instance state (Prism is stateless)
- `WorkflowKey` allows Prism to resolve the correct Umbraco page URL
- `Archetype` allows smart rendering (e.g., show form icon for "Collect", checkmark for "Completion")
- `CanContinue` separates active vs completed; BA decides what "completed" means

**Implementation note for MockBusinessApp:**

```csharp
// In BusinessAppWorkflowEngine.cs

public WorkflowInstanceListEnvelope GetInstances(string tenantId, string userId)
{
    var instances = _instancesById.Values
        .Where(i => i.TenantId == tenantId && i.UserId == userId)
        .Select(i => {
            var definition = _definitions[i.WorkflowKey];
            var state = definition.States.First(s => s.StateKey == i.CurrentState);
            
            return new WorkflowInstanceSummary
            {
                InstanceId = i.InstanceId,
                WorkflowKey = i.WorkflowKey,
                WorkflowDisplayName = definition.DisplayName,
                CurrentStateKey = i.CurrentState,
                CurrentStateDisplayName = state.DisplayName,
                Archetype = state.Archetype,
                CreatedAt = i.CreatedAt.DateTime,
                LastUpdatedAt = i.UpdatedAt.DateTime,
                CanContinue = state.Archetype != "Completion"
            };
        })
        .OrderByDescending(i => i.LastUpdatedAt)
        .ToList();
    
    return new WorkflowInstanceListEnvelope
    {
        ResponseState = "ok",
        Instances = instances
    };
}
```

---

### Multi-instance policy enforcement

**Problem:** Some workflows should only have one active instance (e.g., "Profile Setup"). Others allow multiple (e.g., "Book a Facility" — you might book 2 different dates).

**Solution:** Add an `instancePolicy` property to the workflow definition JSON (🟠 Business App):

```json
{
  "definitionKey": "community-grant",
  "displayName": "Community Grant Application",
  "instancePolicy": "single",
  "initialState": "collect-details",
  "states": [ ... ]
}
```

**Policy values:**
- `"single"` — If an active instance exists, redirect to it. Never start a new one.
- `"multiple"` — Always start a new instance. No check.
- `"prompt"` — If an active instance exists, show the user a choice: "Continue existing" or "Start new"

---

#### Policy enforcement in `WorkflowPageController`

**Extend `GetCurrentAsync()` logic:**

```csharp
// In WorkflowPageController.HandleGet()

var workflowKey = CurrentPage!.Value<string>("workflowKey") ?? string.Empty;

// NEW: Check instance policy
var definition = await workflowClient.GetDefinitionAsync(workflowKey); // New BA endpoint
var policy = definition.InstancePolicy ?? "single"; // Default to single

if (policy == "single" || policy == "prompt")
{
    var instances = await workflowClient.GetInstancesAsync();
    var activeInstance = instances.Instances
        .FirstOrDefault(i => i.WorkflowKey == workflowKey && i.CanContinue);
    
    if (activeInstance != null)
    {
        if (policy == "single")
        {
            // Resume the active instance (no prompt)
            var envelope = await workflowClient.GetCurrentAsync(workflowKey);
            return CurrentTemplate(BuildViewModel(envelope, workflowKey));
        }
        
        if (policy == "prompt")
        {
            // Show "Continue existing or start new" choice
            var vm = new InstancePickerViewModel(CurrentPage!, publishedValueFallback)
            {
                ExistingInstance = activeInstance,
                WorkflowKey = workflowKey
            };
            return View("~/Views/Workflow/InstancePicker.cshtml", vm);
        }
    }
}

// No active instance OR policy = "multiple" → start new instance
var envelope = await workflowClient.GetCurrentAsync(workflowKey);
return CurrentTemplate(BuildViewModel(envelope, workflowKey));
```

**New archetype: `InstancePicker` (for `prompt` policy):**

```razor
@* Views/Workflow/InstancePicker.cshtml *@

<div class="prism-instance-picker">
  <h1>You have an in-progress @Model.ExistingInstance.WorkflowDisplayName</h1>
  
  <p>
    You started this workflow on @Model.ExistingInstance.CreatedAt.ToString("d MMMM yyyy").
    Current status: <strong>@Model.ExistingInstance.CurrentStateDisplayName</strong>.
  </p>
  
  <div class="prism-instance-picker__actions">
    <a href="?action=resume" class="prism-button prism-button--primary">
      Continue where I left off
    </a>
    
    <a href="?action=start-new" class="prism-button prism-button--secondary">
      Start a new @Model.ExistingInstance.WorkflowDisplayName
    </a>
  </div>
</div>
```

**Query param handling:**
- `?action=resume` → `workflowClient.GetCurrentAsync(workflowKey)` (resumes existing)
- `?action=start-new` → `workflowClient.StartNewAsync(workflowKey)` (new BA method; creates new instance even if active instance exists)

---

### Workflow Hub view rendering

**View model (in `UmbracoPrism.Core/Models`):**

```csharp
public class WorkflowHubViewModel : PublishedContentWrapped
{
    public IReadOnlyList<WorkflowInstanceViewModel> ActiveInstances { get; init; } = Array.Empty<WorkflowInstanceViewModel>();
    public IReadOnlyList<WorkflowInstanceViewModel> CompletedInstances { get; init; } = Array.Empty<WorkflowInstanceViewModel>();
    
    public WorkflowHubViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }
}

public class WorkflowInstanceViewModel
{
    public WorkflowInstanceSummary Summary { get; init; }
    public string ResumeUrl { get; init; } = "#";
    
    public WorkflowInstanceViewModel(WorkflowInstanceSummary summary) => Summary = summary;
}
```

**Default view (in `UmbracoPrism.Core/Views`):**

```razor
@* Views/WorkflowHub.cshtml *@
@model WorkflowHubViewModel

<div class="prism-workflow-hub">
  <h1>@Model.Value("title", "My Workflows")</h1>
  
  @if (Model.Value<string>("introText") is { } intro && !string.IsNullOrEmpty(intro))
  {
    <div class="prism-workflow-hub__intro">
      @Html.Raw(intro)
    </div>
  }
  
  @if (Model.ActiveInstances.Any())
  {
    <section class="prism-workflow-hub__section">
      <h2>Active Workflows</h2>
      @await Html.PartialAsync("~/Views/Partials/Workflow/_WorkflowHub-InstanceList.cshtml", 
                                new { Instances = Model.ActiveInstances, ShowStatus = true })
    </section>
  }
  
  @if (Model.CompletedInstances.Any())
  {
    <section class="prism-workflow-hub__section">
      <h2>Completed Workflows</h2>
      @await Html.PartialAsync("~/Views/Partials/Workflow/_WorkflowHub-InstanceList.cshtml", 
                                new { Instances = Model.CompletedInstances, ShowStatus = false })
    </section>
  }
  
  @if (!Model.ActiveInstances.Any() && !Model.CompletedInstances.Any())
  {
    <div class="prism-workflow-hub__empty">
      <p>You don't have any active workflows.</p>
      <a href="/" class="prism-button prism-button--primary">Browse available services</a>
    </div>
  }
</div>
```

**Partial: `_WorkflowHub-InstanceList.cshtml`:**

```razor
@model dynamic

<div class="prism-instance-list">
  @foreach (var instance in Model.Instances)
  {
    <div class="prism-instance-card prism-instance-card--@instance.Summary.Archetype.ToLower()">
      <div class="prism-instance-card__icon">
        @switch (instance.Summary.Archetype)
        {
          case "Collect":
            <span class="icon-edit"></span>
            break;
          case "StatusTimeline":
            <span class="icon-time"></span>
            break;
          case "Completion":
            <span class="icon-check"></span>
            break;
        }
      </div>
      
      <div class="prism-instance-card__content">
        <h3>@instance.Summary.WorkflowDisplayName</h3>
        
        @if (Model.ShowStatus)
        {
          <p class="prism-instance-card__status">
            <strong>Status:</strong> @instance.Summary.CurrentStateDisplayName
          </p>
        }
        
        <p class="prism-instance-card__meta">
          Started @instance.Summary.CreatedAt.ToString("d MMM yyyy")
          @if (instance.Summary.LastUpdatedAt != instance.Summary.CreatedAt)
          {
            <span> · Last updated @instance.Summary.LastUpdatedAt.ToString("d MMM yyyy")</span>
          }
        </p>
      </div>
      
      <div class="prism-instance-card__action">
        @if (instance.Summary.CanContinue)
        {
          <a href="@instance.ResumeUrl" class="prism-button prism-button--primary">
            Continue
          </a>
        }
        else
        {
          <a href="@instance.ResumeUrl" class="prism-button prism-button--secondary">
            View
          </a>
        }
      </div>
    </div>
  }
</div>
```

**Design rationale:**
- Uses Umbraco partial pattern (same as `_WorkflowStep-Collect.cshtml`)
- Archetype-aware icons/styling (consistent with existing workflow renderer)
- Minimal, semantic markup (easy to style)

---

### "Workflow as a workflow" meta-pattern: Evaluation

**User suggestion:** Treat the hub itself as a workflow — the BA returns `WorkflowResponseEnvelope` with `Archetype = "WorkflowHub"`.

#### Pros
- **Infinite extensibility:** BA can control hub rendering entirely (custom intro, filtering UI, etc.)
- **Consistent model:** Everything is a workflow; no special-case controllers
- **Reuse existing renderer:** `_WorkflowStep-WorkflowHub.cshtml` could render the instance list as a "field group"

#### Cons
- **Bootstrapping problem:** How do you start the hub workflow? It's not tied to a specific workflow key.
  - Solution: Reserve a magic key like `__prism_hub` and hard-code it in the controller?
  - Smells like premature abstraction.
- **Circular dependency risk:** The hub shows instances of workflows. If the hub *is* a workflow, does it show itself?
  - Solution: Filter out `__prism_hub` instances in the list.
  - Again, smells like over-engineering.
- **Complexity for MVP:** Adds cognitive load for BA developers who just want a simple instance list.

#### Recommendation

**For MVP:** Use a dedicated `WorkflowHubController` and simple document type. The hub is a first-class concept, not a workflow.

**For v2 (extension point):** Allow BA to opt-in to the meta-workflow pattern via config:

```json
// In workflow-hub.json (optional BA-provided file)
{
  "renderMode": "workflow",
  "workflowKey": "__prism_hub"
}
```

If this file exists, `WorkflowHubController` calls `GetCurrentAsync("__prism_hub")` instead of `GetInstancesAsync()`, and renders the response using the existing workflow renderer. This preserves the "easy to do the right thing" principle while allowing power users to go deeper.

**Document this explicitly in design:** We're not ruling it out; we're deferring it until we see demand.

---

## Design 3: Extension points

### How developers customize the WorkflowHub view

Prism provides sane defaults; developers override where needed.

#### Option 1: Override the entire view

Standard Umbraco view override pattern:

1. Copy `/Views/WorkflowHub.cshtml` from Prism into your project at the same path
2. Modify markup, styling, logic as needed
3. Umbraco's view engine prioritizes the local copy

**When to use:** You want complete control over the hub layout (e.g., adding filters, search, pagination).

---

#### Option 2: Override partials only

Override specific partials without touching the core view:

**File to override:** `/Views/Partials/Workflow/_WorkflowHub-InstanceList.cshtml`

**Example:** Change the card layout to a table:

```razor
@* In your project: /Views/Partials/Workflow/_WorkflowHub-InstanceList.cshtml *@

<table class="prism-instance-table">
  <thead>
    <tr>
      <th>Workflow</th>
      <th>Status</th>
      <th>Started</th>
      <th>Action</th>
    </tr>
  </thead>
  <tbody>
    @foreach (var instance in Model.Instances)
    {
      <tr>
        <td>@instance.Summary.WorkflowDisplayName</td>
        <td>@instance.Summary.CurrentStateDisplayName</td>
        <td>@instance.Summary.CreatedAt.ToString("d MMM yyyy")</td>
        <td>
          <a href="@instance.ResumeUrl">
            @(instance.Summary.CanContinue ? "Continue" : "View")
          </a>
        </td>
      </tr>
    }
  </tbody>
</table>
```

**When to use:** You want to tweak the instance list presentation but keep the overall hub structure.

---

#### Option 3: Per-archetype instance rendering

Allow custom rendering based on workflow archetype:

**New partial convention:** `_WorkflowHub-InstanceCard-{Archetype}.cshtml`

**Example:** Custom rendering for "Completion" archetype:

```razor
@* /Views/Partials/Workflow/_WorkflowHub-InstanceCard-Completion.cshtml *@

<div class="prism-instance-card prism-instance-card--completion">
  <span class="icon-trophy"></span>
  <h3>@Model.WorkflowDisplayName — Completed!</h3>
  <p>You completed this on @Model.LastUpdatedAt.ToString("d MMMM yyyy").</p>
  <a href="@Model.ResumeUrl">View summary</a>
</div>
```

**Fallback:** If archetype-specific partial doesn't exist, use default `_WorkflowHub-InstanceCard.cshtml`.

**Implementation:** Modify `_WorkflowHub-InstanceList.cshtml` to check for archetype partial:

```razor
@{
  var archetypePartial = $"~/Views/Partials/Workflow/_WorkflowHub-InstanceCard-{instance.Summary.Archetype}.cshtml";
  var defaultPartial = "~/Views/Partials/Workflow/_WorkflowHub-InstanceCard.cshtml";
  var partial = System.IO.File.Exists(archetypePartial) ? archetypePartial : defaultPartial;
}

@await Html.PartialAsync(partial, instance)
```

**When to use:** You want workflow-specific instance rendering (e.g., show a progress bar for "Collect", a trophy for "Completion").

---

### Developer experience: Zero-config to full control

| Scenario | What to do |
|---|---|
| Use Prism defaults | Create a `workflowHub` page in backoffice. Done. |
| Change hub title/intro | Set `title` and `introText` properties on the page. |
| Change instance card styling | Override CSS classes (`prism-instance-card`, etc.). |
| Change instance list layout | Override `_WorkflowHub-InstanceList.cshtml` partial. |
| Add custom logic (filters, search) | Override `/Views/WorkflowHub.cshtml` entirely. |
| Per-workflow custom rendering | Add `_WorkflowHub-InstanceCard-{Archetype}.cshtml` partial. |

**Principle:** Progressive disclosure of complexity. Start with zero config; dig deeper only if needed.

---

## Open questions

### 1. Should conditional fields support multi-value triggers?

**Current design:** `VisibleWhen: "Other"` (single value)

**Possible extension:** `VisibleWhen: ["Other", "Not Listed"]` (array)

**Recommendation:** Defer to v2. Single-value covers 90% of use cases. Multi-value adds parsing complexity.

---

### 2. Should the hub show instances from all tenants or just the current tenant?

**Current design:** BA's `GetInstancesAsync()` filters by authenticated user. Prism doesn't know if that's tenant-scoped or global.

**Recommendation:** Leave it to the BA. If they want to show cross-tenant instances (e.g., a member switches tenants but keeps history), they can. Prism is agnostic.

---

### 3. Should Prism cache the instance list?

**Current design:** Every GET to the hub calls the BA.

**Trade-off:**
- No cache = always fresh, but may be slow if BA has 100+ instances
- Cache = fast, but may show stale status

**Recommendation for MVP:** No cache. If performance becomes an issue, add a short-lived (30s) cache keyed by `userId`.

---

### 4. Should completed instances be paginated?

**Current design:** All completed instances render on one page.

**Risk:** A member with 50 completed workflows will see a long list.

**Recommendation for MVP:** No pagination. Add it in v2 if needed. Most members will have <10 instances total.

---

### 5. Should conditional fields support nested conditions?

**Example:** Field C depends on Field B, which depends on Field A.

**Recommendation:** Allow it, but document that max depth = 2. Beyond that, the BA should split into multiple workflow states.

**Validation logic:** Follow the chain recursively, but error if depth > 2.

---

### 6. Should the hub allow bulk actions (e.g., "Delete all completed")?

**Recommendation:** Not in MVP. BA owns instance lifecycle. If they want bulk actions, they can add them to the meta-workflow pattern in v2.

---

### 7. Should InstancePicker (prompt policy) remember the user's choice?

**Example:** User selects "Start new" → remember that choice for 24h so they don't see the prompt again.

**Recommendation:** Not in MVP. Adds state management complexity. If needed, BA can track it via instance metadata.

---

## Summary

### What this design delivers

✅ **Conditional fields** — declarative, accessible, BA-controlled "Other → specify" pattern  
✅ **Workflow Hub** — member dashboard for multi-instance management  
✅ **Instance policies** — single/multiple/prompt controls for starting new instances  
✅ **Extension points** — progressive customization from zero-config to full control  
✅ **Umbraco-native** — uses existing doc types, route-hijacking, partial overrides  
✅ **Accessibility** — ARIA live regions, focus management, screen reader tested  
✅ **No breaking changes** — entirely additive; existing workflows unaffected  

### What it defers to v2

🔮 **Meta-workflow pattern** — hub rendered as a workflow (documented but not implemented)  
🔮 **Multi-value conditional triggers** — `VisibleWhen: ["A", "B"]`  
🔮 **Nested conditionals** — depth > 2  
🔮 **Instance list caching** — wait for performance data  
🔮 **Pagination** — wait for user feedback on list sizes  
🔮 **Bulk actions** — wait for clear use case  

### Implementation checklist

**Conditional fields:**
- [ ] Add `ConditionalOn` and `VisibleWhen` to `FieldRenderPayload.cs`
- [ ] Extend `WorkflowFieldValidator.Validate()` to skip hidden fields
- [ ] Add `prism-conditional-fields.js` script
- [ ] Update `_WorkflowField-*.cshtml` partials to render conditional attributes
- [ ] Add CSS for `.prism-field--conditional`
- [ ] Document UX guidance ("When is Other good?")

**Workflow Hub:**
- [ ] Add `GetInstancesAsync()` to `IBusinessAppWorkflowClient`
- [ ] Add `WorkflowInstanceListEnvelope` and `WorkflowInstanceSummary` to Shared models
- [ ] Implement `GetInstances()` in `BusinessAppWorkflowEngine` (MockBusinessApp)
- [ ] Seed `workflowHub` document type in `PrismContentTypeSeeder`
- [ ] Create `WorkflowHubController` and `WorkflowHubViewModel`
- [ ] Create `/Views/WorkflowHub.cshtml` and `_WorkflowHub-InstanceList.cshtml`
- [ ] Add CSS for hub/cards

**Instance policies:**
- [ ] Add `GetDefinitionAsync()` to `IBusinessAppWorkflowClient` (returns definition metadata)
- [ ] Add `instancePolicy` to workflow definition JSON schema
- [ ] Extend `WorkflowPageController.HandleGet()` to check policy
- [ ] Add `StartNewAsync()` to `IBusinessAppWorkflowClient` (forces new instance)
- [ ] Create `InstancePickerViewModel` and `/Views/Workflow/InstancePicker.cshtml`
- [ ] Handle `?action=resume` and `?action=start-new` query params

**Extension points:**
- [ ] Document partial override patterns in README
- [ ] Add archetype-specific partial fallback logic
- [ ] Provide example customizations in testsite

---

**Next steps:** Handoff to Blathers (backend), Isabelle (UI), and Tangy (test scenarios).
