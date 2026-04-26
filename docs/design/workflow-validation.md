# Workflow Form Validation — Architecture Design

> **⚠️ v2.0 Schema Update:** Validation architecture is largely unchanged in v2.0. Component types replace field types but validation rules apply identically.

## Overview

Prism workflow forms collect data across one or more steps, driven by a definition supplied by the **Business App**.  
This document designs the full validation stack: client-side, server-side, and business-app validation, together with a tamper-proof trust model for submitted form data.

---

## Responsibility split

| Layer | Owner | Runs when |
|---|---|---|
| HTML5 client-side hints | 🔵 Prism Platform | Immediately in browser |
| JavaScript validation (optional) | 🔵 Prism Platform (opt-in) | On input / before submit |
| Server-side structural validation | 🔵 Prism Platform | On every POST |
| Business logic validation | 🟠 Business App | On every POST (via API call) |
| Field definition trust / tamper-proofing | 🔵 Prism Platform | On every POST |

---

## 1. Client-side validation (browser)

### 1a. HTML5 native constraints

Prism renders fields from the `WorkflowFieldDefinition` model.  
Each field should map its metadata to HTML5 constraint attributes:

| Field property | HTML attribute |
|---|---|
| `Required = true` | `required` |
| `Type = "email"` | `type="email"` |
| `Type = "number"` | `type="number"` |
| `MinLength` | `minlength` |
| `MaxLength` | `maxlength` |
| `Pattern` | `pattern` |
| `Min` / `Max` (numeric) | `min` / `max` |

This gives immediate in-browser feedback at zero JS cost and is already accessible via the browser's built-in constraint validation API.

**Implementation:** Update `_WorkflowField.cshtml` to emit these attributes from the field definition model.

### 1b. JavaScript enhancement (optional, future)

Prism may later add a small vanilla-JS or Web Component layer to:
- Show inline error messages that match Prism's design system (`.prism-error-message`)  
- Prevent submit when the form is invalid, showing a summary error panel at the top

This is an **opt-in progressive enhancement** — the form must always work without JS.

---

## 2. Server-side structural validation (🔵 Prism Platform)

### What Prism must validate on every POST

On receiving a form POST in `WorkflowPageController`, before calling the Business App:

1. **Field key whitelist** — only field keys declared in the cached step definition are accepted; unknown keys are discarded.
2. **Required fields** — if a field is `Required`, its submitted value must be non-empty.
3. **Type constraints** — numeric/date fields are parsed; invalid input becomes a validation error.
4. **Max length** — string fields are truncated or rejected if they exceed `MaxLength`.
5. **Options whitelist (radio / select / checkboxlist)** — submitted value must be one of the declared `Options`; prevents injection of arbitrary values.

Validation errors are collected into `IEnumerable<WorkflowProblem>` and re-rendered in the form view without calling the Business App.

### Model

```csharp
public record WorkflowValidationResult(
    bool IsValid,
    IReadOnlyList<WorkflowProblem> Problems);
```

A new `WorkflowFieldValidator` static class (or service) performs this work.  
`WorkflowPageController.Index()` calls it before forwarding to `IWorkflowInstanceService`.

---

## 3. Business App validation

The Business App may apply domain-level rules that Prism cannot know (e.g. "retirement age must be ≥ 55 for this product tier").

### Protocol

The Business App's step endpoint already returns `WorkflowResponseEnvelope`.  
It should return `ResponseState = "validation_error"` and populate `Problems` with field-level messages when domain validation fails.

```json
{
  "responseState": "validation_error",
  "problems": [
    { "field": "retirementAge", "message": "Must be at least 55 for this plan." }
  ]
}
```

Prism re-renders the form step with these errors displayed next to the relevant fields.  
The field's `name` attribute (`fields[{key}]`) must match the `field` value in the problem — Prism maps them automatically.

---

## 4. Form definition trust model (tamper-proofing)

### The problem

The form definition (field names, types, options) is issued by the Business App.  
On a multi-step form, a malicious actor could craft a POST with unexpected field keys or values not present in the original definition.

### Chosen approach: **Server-side definition cache with a nonce**

This is the safest and simplest approach:

1. **On GET** (step render): Prism fetches the step definition from the Business App and stores it in `IDistributedCache` keyed by a random `nonce` (a `CryptoRandom` token).
2. **The nonce is embedded** in the form as a hidden field: `<input type="hidden" name="__prism_step_nonce" value="{nonce}" />`.
3. **On POST**: Prism reads the nonce, retrieves the step definition from cache, and validates the submitted data against it. If the nonce is missing, expired, or not found → reject with a 400 / redirect to step start.
4. **Cache TTL**: 30 minutes (configurable via `PrismWorkflowOptions`). Sufficient for a typical form session; expired nonces force the user back to the current step (a graceful GET).

### Multi-server / load-balanced deployments ⚠️

`IDistributedCache` is **safe for load-balanced / horizontally scaled deployments**, but only when backed by a shared external store. The registration determines safety:

| Registration | Safe across servers? | When to use |
|---|---|---|
| `services.AddDistributedMemoryCache()` | ❌ No — node-local, nonce not visible to other servers | Local dev / single-node only |
| `services.AddStackExchangeRedisCache(...)` | ✅ Yes | Production multi-server |
| `services.AddDistributedSqlServerCache(...)` | ✅ Yes | Production multi-server (SQL Server) |

**Prism's position:** Register `IDistributedCache` yourself using whatever provider your infrastructure uses. Prism only depends on the `IDistributedCache` abstraction — it has no opinion on the backing store. Umbraco sites on Azure commonly use the `Microsoft.Extensions.Caching.StackExchangeRedis` package with Azure Cache for Redis; AWS deployments typically use ElastiCache. Single-node or local dev falls back to the default in-memory provider.

> ⚠️ If you deploy to multiple servers **without** configuring a shared `IDistributedCache` provider, nonce lookups will fail on any server that didn't write the nonce, causing users to be bounced back to the start of the form step on POST. This is a graceful degradation (no data loss) but a poor UX. Configure a shared provider before going live with horizontally scaled Umbraco.

### Why not HMAC signing?

HMAC signing of a serialised definition embedded in the form is an alternative, but it:
- Leaks field names and options to the client (inspectable in source)
- Requires a stable signing key to be known at request time
- Is harder to revoke (e.g. if a workflow definition is updated mid-session)
- **Does not have the multi-server problem** — the signature is self-contained in the form — but the security trade-offs above outweigh this benefit for most cases.

The nonce + distributed cache model keeps the definition entirely server-side, is trivially revocable, and aligns with standard session/CSRF token patterns — as long as the cache is properly shared.

### Anti-CSRF

The existing Umbraco antiforgery token (`IAntiforgery.ValidateRequestAsync()`) protects against cross-site request forgery.  
The step nonce is an additional layer that ties the POST to a specific rendered step definition, not just a valid session.

### Nonce generation and storage

```csharp
// On GET — store definition and emit nonce
var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
await cache.SetStringAsync($"prism:step:{nonce}", JsonSerializer.Serialize(stepDefinition),
    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });
ViewBag.StepNonce = nonce;

// On POST — retrieve and validate
var nonce = Request.Form["__prism_step_nonce"];
var json = await cache.GetStringAsync($"prism:step:{nonce}");
if (json is null) { /* expired or tampered — redirect to step GET */ }
var stepDef = JsonSerializer.Deserialize<WorkflowStepDefinition>(json);
// validate submitted fields against stepDef ...
```

### Where does this live?

| Component | Location |
|---|---|
| Nonce generation + cache write | `WorkflowPageController` GET path |
| Nonce hidden field in form | `_WorkflowStep-*.cshtml` partials (Prism-owned template) |
| Nonce validation + definition retrieval | `WorkflowFieldValidator` |
| Cache abstraction | `IDistributedCache` — **must be shared across servers in production** |

---

## 5. Error display model

### Field-level errors

```html
<div class="prism-form-group prism-form-group--error">
  <label class="prism-label" for="field-retirementAge">Retirement Age *</label>
  <p class="prism-error-message" id="field-retirementAge-error">
    <span class="prism-visually-hidden">Error:</span>
    Must be at least 55 for this plan.
  </p>
  <input class="prism-input prism-input--error"
         type="number"
         id="field-retirementAge"
         name="fields[retirementAge]"
         aria-describedby="field-retirementAge-hint field-retirementAge-error"
         value="45" />
</div>
```

### Summary panel (top of form)

```html
<div class="prism-error-summary" role="alert" aria-labelledby="error-summary-title">
  <h2 id="error-summary-title">There is a problem</h2>
  <ul>
    <li><a href="#field-retirementAge-error">Must be at least 55 for this plan.</a></li>
  </ul>
</div>
```

GDS accessibility rules: focus the summary on page load when errors are present (JS enhancement).

### CSS

Add to `prism-forms.css`:
- `.prism-form-group--error` — left border in `var(--prism-danger)`
- `.prism-error-message` — bold, `var(--prism-danger)` colour
- `.prism-input--error` — border colour in `var(--prism-danger)`
- `.prism-error-summary` — bordered panel, `var(--prism-danger)` accent

---

## 6. Implementation order

1. **Nonce + server-side validation** (most important — tamper-proofing and structural checks)
2. **Business App `validation_error` response handling** (Prism re-renders with BA errors)
3. **HTML5 constraint attributes on fields** (zero-cost quick win)
4. **Error display CSS** (`prism-forms.css` additions)
5. **JS progressive enhancement** (optional, later)

---

## Open questions

- Should `WorkflowFieldDefinition` gain explicit `MinLength`, `MaxLength`, `Min`, `Max`, `Pattern` properties? Currently the model may not carry all constraint metadata. This needs a schema review.
- Multi-step validation: should Prism carry forward validated data from earlier steps in the nonce cache, so the Business App can validate the whole submission holistically on the final step?
- How should the Business App signal which *field* a validation error applies to, when the field key differs between the BA internal model and the Prism form key?
