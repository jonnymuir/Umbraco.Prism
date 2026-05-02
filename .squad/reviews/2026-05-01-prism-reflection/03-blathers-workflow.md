# Workflow review — Blathers

_2026-05-01T08:57:29+01:00_

---

## Verdict

The workflow engine is architecturally coherent and GDS-aligned in intent, but it carries three visible scars from rapid evolution: a flat `PrismComponentRenderPayload` bag that contradicts the elegant design-time hierarchy it services; a hardcoded business rule buried in the generic `Advance()` method that belongs in the workflow definition, not the engine; and a stringly-typed `Dictionary<string, object?>` advance contract that forces JsonElement workarounds into the render path. The service designer path is genuinely good when using the C# builder — honest, fluent, discoverable. The JSON-first path is workable but requires implicit knowledge of type discriminator spellings and has zero schema enforcement. For business users, feedback is functional but not transparent. The engine is long-lasting in skeleton; the flesh needs trimming.

---

## The service designer's journey (walk through authoring a workflow)

**Route 1 — C# builder (recommended, discoverable):** A service designer with C# competence opens `WorkflowDefinitionBuilder` (`src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`), chains `.Key()`, `.StartsAt()`, `.AddState()`, `.AddTransition()`, `.Build()`. IntelliSense guides every step. The fluent API covers all 16 component types. They drop the resulting `WorkflowDefinitionFile` into the engine and go. This is genuinely good. The abstract CRTP base `ComponentCollectionBuilder<TSelf>` is an implementation-cleverness tax that the caller never sees — tolerable.

**Route 2 — JSON seed (current production path):** The designer writes a file in `src/UmbracoPrism.MockBusinessApp/workflow-seeds/`. The format is undocumented beyond example files. They must know that `RadiosComponent` serialises as `"type": "radio"` but the validator code checks `"radios"` and `"radio"` interchangeably, and the view partial is `_Component-Radio.cshtml`. `CheckboxesComponent` serialises as `"type": "checkboxlist"` but the validator uses `"checkboxlist"` or `"checkboxes"`. There is no JSON Schema file, no schema validation on load, and no error on unknown fields (it just silently ignores them). A typo in a discriminator produces an empty component tree, not an exception.

**Route 3 — registering the workflow on an Umbraco page:** The designer must go into the Umbraco backoffice, find the page, and set a `workflowKey` property to match the definition key. This step is invisible from the builder and the seed file. It is not documented in any file adjacent to the seeds.

The authoring journey is code-first, honest for Route 1, obscure for Routes 2 and 3.

---

## The business user's journey (filling in, getting feedback, rejection)

The PRG pattern works: submit, validate, redirect, repopulate. Errors travel via TempData (`WorkflowProblems`) and surface as a GDS error summary plus inline field-level errors (`FieldErrors` dictionary). Error messages are human-friendly: _"Full name is required."_ The `WorkflowFieldValidator` (`src/UmbracoPrism.Core/Services/Workflow/WorkflowFieldValidator.cs`) is the server-side source of truth and it is thorough — whitelist, required, type, options, constraints, all in order, no cascade.

Where it breaks down: there is a hardcoded business rule in `BusinessAppWorkflowEngine.Advance()` (lines 304–336, `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`). If `enquiry-type == "Technical support"` and the message lacks a version number, URL, or error code, the engine rejects the submission with `"diagnostic-info-required"`. The error message is relatively good. But: (a) this rule is invisible to the service designer — it cannot be found in the workflow seed; (b) it is hardcoded to a specific field key and option value; (c) a business user who reads the field's hint will find no indication that this constraint exists. The hint and the label are the only channels of explanation, and neither is populated on that field. The business user gets a rejection they cannot anticipate.

---

## Architecture honesty

### The Component design-time vs render-time split: working or accidental?

The split is deliberate and justified in principle: `PrismComponent` (`src/UmbracoPrism.Shared/Models/Workflow/Components/PrismComponent.cs`) is the rich typed hierarchy used for authoring and serialisation; `PrismComponentRenderPayload` (`src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs`) is what the engine sends to the Core controller after resolving values. The design-time types enforce type safety via sealed records; the render payload adds runtime concerns (saved values, sanitised content, accordion sections, task status).

But `PrismComponentRenderPayload` is a 20-property flat bag. Properties like `AccordionSections`, `TaskSections`, `PollIntervalMs`, `AllowDefer`, `DeferMessage`, `Level`, `BannerType` are all nullable and only meaningful for one component type each. A `Type = "body"` payload carries `Fields = Array.Empty<FieldRenderPayload>()` as a default. There is no discriminated union on the render side. The split is working, but the render-side is accidental — it grew by addition. A typed render hierarchy (mirroring the design-time one) would be smaller, safer, and eliminates the nullability fog.

### The advance API contract: clean or leaky?

Leaky. `BusinessAppWorkflowClient.AdvanceAsync()` sends `Dictionary<string, object?>` as `FieldValues`. When the JSON body is deserialised by ASP.NET Core in the MockBusinessApp's controller, `object?` values arrive as `System.Text.Json.JsonElement`. The engine's `GetDisplayValue()` method (`BusinessAppWorkflowEngine.cs`, line 878) explicitly special-cases this: `System.Text.Json.JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString()`. This is a workaround for a contract problem. The advance payload should be typed (a named DTO with `Dictionary<string, string> FieldValues`) rather than relying on `object?` and downstream casting. The comment on line 875 says it openly — that's honest self-awareness, but the root cause should be fixed not annotated.

---

## Rams scorecard (10 principles)

| # | Principle | Rating | Note |
|---|-----------|--------|------|
| 1 | Innovative | ✅ | Polymorphic component model over a CMS is genuinely new design space |
| 2 | Makes a product useful | ✅ | GDS components, nonce-based tamper protection, PRG — all serve real user needs |
| 3 | Aesthetic | ⚠️ | Design-time types are clean; render-time flat bag is not |
| 4 | Makes a product understandable | ⚠️ | `InferStepType()` is implicit magic; string enums for policy/action/step type are invisible contracts |
| 5 | Unobtrusive | ✅ | Base controller pattern is correct; override only what's domain-specific |
| 6 | Honest | ⚠️ | The GET-that-posts (`GetCurrentAsync` uses `POST`), JsonElement workaround, and hardcoded business rule are dishonest seams |
| 7 | Long-lasting | ⚠️ | Skeleton is sound; hardcoded rule and flat render bag are accumulation scars |
| 8 | Thorough down to last detail | ⚠️ | `"PARTIAL"` sentinel for date validation is clever but fragile; `"checkboxlist"` vs `"checkboxes"` inconsistency; no schema on JSON seeds |
| 9 | Environmentally friendly | ✅ | In-memory engine is appropriate for a mock; no observable resource waste |
| 10 | As little design as necessary | ❌ | `PrismComponentRenderPayload` is a 20-property bag; `ActionLabel()`/`ActionStyle()` lookup tables belong in the seed, not the engine; duplicate conditional-reveal mechanisms (tree-based `ConditionalChildren` + flat `ConditionalOn`/`VisibleWhen` pair) |

---

## Three things to fix / simplify (prioritised)

### 1. Evict the hardcoded business rule from the engine

**File:** `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`, lines 304–336

The `enquiry-type == "Technical support"` regex validator is a domain rule disguised as framework code. It is invisible to service designers, untestable in isolation, and proves the engine is not generic. Replace with a declarative `"rules"` array on the step definition (or a strategy hook the MockBusinessApp registers) so rules live in the workflow seed alongside the fields they govern. This is fix #1 because it directly harms business users and locks down the engine.

### 2. Collapse the `PrismComponentRenderPayload` bag into typed render DTOs

**File:** `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs`

`PrismComponentRenderPayload` has 20+ nullable properties serving 10+ component types. Create a sealed render hierarchy mirroring `PrismComponent`: `FieldsetRenderPayload`, `SummaryListRenderPayload`, `BodyRenderPayload`, etc., all derived from an abstract `PrismComponentRenderBase`. The `BuildComponents()` method in `BusinessAppWorkflowEngine.cs` already switches on type — the switch arms would each return the right subtype. Views and tag helpers would receive the correct shape with no nullability guessing.

### 3. Replace string enums with typed constants or C# enums

**Files:** `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs` (`InstancePolicy`), `src/UmbracoPrism.Core/Models/Workflow/WorkflowResponseEnvelope.cs` (`ResponseState`, `Style`), `src/UmbracoPrism.Shared/Extensions/PrismComponentExtensions.cs` (`InferStepType` return values)

`InstancePolicy = "single"`, `ResponseState = "render"`, `Style = "primary"` — these are unenforceable string contracts spread across C#, JSON, and Razor. Introduce `PrismInstancePolicy`, `WorkflowResponseState`, and `WorkflowActionStyle` as `enum` or `static class` constant holders. The `InferStepType()` convention (presence of `PanelComponent` → `"confirmation"`) should be the only implicit magic remaining; all other string contracts should become compile-time symbols.
