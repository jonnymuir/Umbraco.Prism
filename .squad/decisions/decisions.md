# Decisions

## Decision: Direct Main Branch Workflow

**Date:** 2026-03-28  
**Agent:** Copilot (via user directive)  
**Status:** Active

### What Was Decided

Work directly on main branch, one issue at a time. No pull requests — commits go straight to main after work is complete.

### Why This Matters

Solo developer project context. PR overhead is unnecessary when individual is responsible for all decisions and testing. Enables faster iteration while maintaining code quality through direct validation and commit practices.

---

## Decision: PrismDeviceCredential Schema Choices (Issue #12)

**Author:** Blathers (Backend Dev)  
**Date:** 2026-03-28  
**Issue:** #12 — Phase 1: prismBiometricTokens DB table + migration

### Type Choices

| Field | Type | Rationale |
|---|---|---|
| `TenantId` | `nvarchar(450)` | Logical tenant string identifier (not int FK) per issue spec; matches Umbraco identity column sizing |
| `UserId` | `nvarchar(450)` | Entra OID stored as string; 450 is Umbraco's standard for identity keys |
| `DeviceId` | `nvarchar(64)` | Client UUID as string, 36 chars + headroom |
| `TokenHash` | `nvarchar(512)` | SHA-256 hex (64 chars) with headroom for algorithm prefixing |
| `RegisteredAt` | `datetime2` + `getutcdate()` default | UTC enforced; `datetime2` is higher precision than `datetime` |
| `FailedAttempts` | `int` + default `0` | Rate-limiting counter; int sufficient for any realistic limit |
| `Platform` | `nvarchar(50)` | Bounded enum-like values ('ios', 'android'); validated at application layer |

### Index Rationale

| Index | Type | Rationale |
|---|---|---|
| `(TenantId, DeviceId)` | UNIQUE | Enforces one credential entry per device per tenant at DB level |
| `(TenantId, UserId)` | Non-unique | Supports listing/revoking all devices for a user within a tenant |
| `(TokenHash)` | Non-unique | Exchange endpoint hashes the incoming JWT and looks up the record; hot path |

### Composite Index Approach

The Umbraco `[Index]` NPoco annotation only supports single-column indexes. Composite indexes were created via `Database.Execute()` raw SQL inside the migration class. This is consistent with the Umbraco migration pattern and safe because the `TableExists` guard ensures idempotency.

### What Was Deferred

- `RefreshTokenEnc` field (from the original design doc SQL) is not in this phase; the issue spec omits it and it belongs to the `/exchange` service implementation, not the registry schema.
- Per-tenant expiry configuration (7–90 day range) is an application-layer concern; the `ExpiresAt` column stores the computed value set at registration time.

---

## Decision: Native Biometric Platform Configuration

**Date:** 2026-01-25  
**Author:** Kicks (Mobile Native Specialist)  
**Context:** Issues #20, #21 — iOS and Android biometric platform config in MobileBundleService

### Decision

The `MobileBundleService` now conditionally injects platform-specific biometric configuration into generated mobile app bundles when the `BiometricAuthEnabled` flag is set to true.

### iOS Configuration
- **Info.plist Key:** `NSFaceIDUsageDescription` with usage string
- **Injection Method:** `plutil -insert` command in bootstrap-ios.sh script
- **When:** After `npx cap add ios` but before app build/run
- **Rationale:** FaceID requires explicit usage description in Info.plist for App Store approval; TouchID does not

### Android Configuration
- **Manifest Permission:** `android.permission.USE_BIOMETRIC`
- **Injection Method:** `sed` insertion before `<application>` tag in bootstrap-android.sh script
- **When:** After `npx cap add android` but before app build/run
- **API Level:** Targets API 28+ (BiometricPrompt API); no need for deprecated `USE_FINGERPRINT` permission

### Plugin Dependencies
When `BiometricAuthEnabled` is true, package.json includes:
- `@aparajita/capacitor-biometric-auth@^7.0.0` — biometric authentication prompts
- `@aparajita/capacitor-secure-storage@^7.0.0` — secure Keychain/Keystore access

**Plugin Selection Rationale:** `@aparajita` packages chosen over `@capacitor-community` alternatives for:
- Capacitor 7 compatibility
- Active maintenance
- Superior iOS Keychain and Android Keystore mapping
- Consistent API surface from same author

### Implementation Details

Both iOS and Android bootstrap scripts follow this pattern:
1. Check if the platform-specific file exists
2. Check if the required entry is already present (idempotent)
3. If not present, inject using platform-appropriate tool (`plutil` for iOS plist, `sed` for Android XML)
4. Provide clear feedback to developer

This approach ensures the scripts can be run multiple times without duplication and gracefully handle cases where the platform hasn't been added yet.


---

## Decision: Workflow Forms Engine Redesign — Element Types as Step Definitions

**Author:** Tom Nook (Lead)  
**Date:** 2025-07-16  
**Status:** Proposed  
**Design Doc:** `docs/design/workflow-forms-engine-redesign.md`

### Context

The original Workflow Forms Engine implementation used custom `PrismFieldGroupDefinition` tables with JSON schema to define form fields. This created:

1. A parallel schema system duplicating what Umbraco already provides
2. Custom Lit components (`prism-workflow-collect.ts`, etc.) tightly coupled to that schema
3. MockBackOffice referencing `UmbracoPrism.Core` and pulling in Umbraco management API controllers

Jonny Muir raised the key insight: workflow steps should be defined as **Umbraco Element Types** — the same pattern used by Block List and Block Grid.

### Decision

**Replace custom field-group schema with Umbraco Element Types.**

#### What This Means

1. **Each workflow step** references an Umbraco Element Type by alias (e.g., `workflowPersonalDetails`)
2. **Element Type properties** ARE the form fields — defined in standard Umbraco Document Types UI
3. **Umbraco property editors** (TextString, DateTime, DropDown, TrueFalse) determine input rendering
4. **Umbraco validation** (mandatory, regex) works out of the box
5. **WorkflowController** uses `IContentTypeService` to introspect Element Types and generate render payloads
6. **Bespoke Lit components** are replaced by a dynamic form renderer driven by payload

#### What Stays the Same

- Workflow state machine (definitions, instances, tasks tables)
- Response envelope model (`responseState: ask_now | wait | complete | error`)
- WorkflowController REST endpoints
- Client orchestrator (`workflow-orchestrator.ts`, `workflow-api-client.ts`)
- Optimistic concurrency (`stateVersion`)
- Tenant isolation

#### What Gets Deleted

- `PrismFieldGroupDefinitionSchema.cs` and related migrations
- `prism-workflow-shell.ts`, `prism-workflow-collect.ts`, `prism-workflow-completion.ts`
- `workflow-index.ts`

#### MockBackOffice Fix

Assembly isolation already applied — `AddControllers()` now only scans MockBackOffice's own assembly, preventing Umbraco management API controllers from being picked up.

### Rationale

1. **Umbraco-native** — Content editors use familiar Document Types UI to define workflow steps
2. **Property editors for free** — No custom input components needed
3. **Validation for free** — Mandatory/regex from Umbraco's standard property validation
4. **Consistent** — Same pattern as Block List/Block Grid
5. **Less code** — Delete 6+ client files, 1 schema table

### Migration Impact

- **Medium effort** — Requires server-side render service, client dynamic renderer
- **Non-breaking** — Workflow instance/task tables unchanged
- **TestSite** — Requires Element Types to be created in Umbraco

### Open Questions

1. Element Type naming convention
2. Which property editors to support in v1
3. How to seed Element Types for demo

---

## Decision: Element Types as Workflow Step Definitions — Umbraco v17 Platform Analysis

**Author:** Brewster (Umbraco Platform Specialist)  
**Date:** 2025-01-26  
**Status:** Implementation Ready  
**Context:** Platform architecture and Element Types API integration for Workflow Forms Engine redesign

### Summary

Element Types provide built-in property editor infrastructure, validation, and versioning. The migration path is clear, and no new Umbraco services need registration — `IContentTypeService` and `IDataTypeService` are already available via DI.

**Verdict:** ✅ This approach is **sound and Umbraco-native**.

### Key Technical Findings

#### Element Type Creation (Code-First)

An Element Type is identical to a Document Type except for one flag:
```csharp
IsElement = true  // Makes it an Element Type instead of a Document Type
```

- Cannot be created as content nodes in the content tree
- Cannot have templates assigned
- CAN be used as block list/grid items and **workflow step definitions**

#### Property Editor Support

Standard Umbraco property editors are fully supported:
- TextString, TextArea (string inputs)
- DateTime (date/time inputs)
- Dropdown (fixed or dynamic lists)
- TrueFalse (boolean inputs)
- Rich Text Editor (HTML content)
- Content Picker (cross-content references)
- Media Picker (file selection)

#### Built-In Validation

Element Types inherit standard Umbraco validation:
- **Mandatory** flag on each property
- **Regex patterns** for text fields
- **Min/Max** constraints for numeric fields
- **Custom validators** via property editor configuration

#### DI Requirements

Both `IContentTypeService` and `IDataTypeService` are already registered in Umbraco's DI container. No additional setup required.

#### Deterministic Data Type Creation

Fixed GUIDs ensure consistent element type creation across environments:
```csharp
private static readonly Guid TextboxDataTypeKey = new("7a1b2c3d-4e5f-6789-0abc-def123456789");
```

### TestSite Seeding Strategy

Leverage existing `PrismContentTypeSeeder` pattern:
1. Create Element Types with fixed GUIDs
2. Add Property Groups and Property Types
3. Guard with `contentTypeService.Get(alias)` to ensure idempotency
4. Log creation for verification

### Migration Path

1. **Phase 1:** Seed Element Types code-first
2. **Phase 2:** Extend WorkflowDefinition to reference Element Type alias
3. **Phase 3:** Deprecate PrismFieldGroupDefinition table
4. **Phase 4:** Background job to migrate legacy definitions to Element Types

---

## Decision: Backend Implementation Plan — Workflow Forms Engine Redesign

**Author:** Blathers (Backend Dev)  
**Date:** 2025-01-26  
**Status:** Ready for Implementation  
**Context:** Concrete backend changes for Element Types integration

### WorkflowController Refactor

#### Current State
```
GET /api/workflow/{workflowKey}/state/{instanceId}
  → loads workflowDefinition
  → loads state machine
  → renders ask_now response with PrismFieldGroupDefinition schema
```

#### New State
```
GET /api/workflow/{workflowKey}/state/{instanceId}
  → loads workflowDefinition
  → loads state machine
  → loads Element Type by alias
  → introspects properties via IContentTypeService
  → generates property descriptor payload
  → renders ask_now response
```

### Key Service Changes

#### IComponentService for Media URLs

**Problem:** Media properties in Element Types require absolute URLs for the client to render images/files.

**Solution:** Use `IComponentService` with media URL caching:
- Register `IComponentService` in DI (new for v17 media handling)
- Cache media URLs in `_cache` to avoid repeated lookups
- Attach media URLs to payload descriptors

**Code Pattern:**
```csharp
var mediaUrl = await _componentService.GetMediaUrlAsync(mediaId);
// Include in propertyDescriptor
```

#### IContentTypeService for Element Type Introspection

**Current Usage:** Load Document Types for content authoring  
**New Usage:** Load Element Types to generate form render payloads

**API:**
```csharp
var elementType = contentTypeService.Get("workflowPersonalDetails");
foreach (var prop in elementType.PropertyTypes)
{
    // PropertyType exposes:
    // - Alias, Name, Description
    // - Mandatory, ValidationRegex
    // - DataType (which contains PropertyEditor info)
}
```

### Payload Generation Logic

The WorkflowController now generates a **property descriptor array** from Element Type metadata:

```csharp
public record PropertyDescriptor(
    string Alias,
    string Label,
    string? Description,
    string EditorUiAlias,  // e.g., "Umb.PropertyEditorUi.TextBox"
    bool Mandatory,
    Dictionary<string, object> EditorConfig,  // min/max, etc.
    object? DefaultValue
);
```

The client uses this to dynamically render form fields.

### Validation Framework

**Server-side validation** performed after form submission:
1. Load Element Type
2. For each submitted property, validate against Element Type definition
3. Check mandatory fields, regex patterns, data type constraints
4. Return validation errors in `responseState: error` envelope

**Client-side validation** performed during form interaction:
1. Use property descriptors to show placeholder/required indicators
2. Live regex validation as user types
3. Show error messages before form submission

### Migration Strategy

#### Phase 1: Dual-Stack
- Both `PrismFieldGroupDefinition` and Element Types coexist
- New workflows use Element Types
- Legacy workflows still use PrismFieldGroupDefinition

#### Phase 2: Deprecation
- Old form endpoints return deprecation headers
- Encourage customers to migrate

#### Phase 3: Removal
- Delete PrismFieldGroupDefinition table, schema, and old endpoints

### IComponentService Initialization

**Challenge:** Media picker and media handling require `IComponentService`, which needs to be initialized with cache settings.

**Solution:** Register in Composition startup:
```csharp
composition
    .Services
    .AddScoped<IComponentService, ComponentService>();
```

This is already done in MockBackOffice's composition setup.

---

## Decision: Frontend Implementation Strategy — Dynamic Form Renderer

**Author:** Isabelle (Frontend Dev)  
**Date:** 2025-01-26  
**Status:** Ready for Implementation  
**Context:** Client-side component architecture for Element Types-based workflow forms

### Component Architecture

#### Replace Bespoke Components

**Delete:**
- `prism-workflow-shell.ts` — Shell wrapper
- `prism-workflow-collect.ts` — Form field collector (highly coupled to old schema)
- `prism-workflow-completion.ts` — Completion screen
- `workflow-index.ts` — Index wrapper

**Create:**
- `dynamic-form-renderer.ts` — Universal renderer for any Element Type
- `form-field.ts` — Single field component (reusable across property editors)
- `form-section.ts` — Group fields into sections

#### Dynamic Form Renderer

Accepts a `propertyDescriptor[]` and renders form fields based on `EditorUiAlias`:

```typescript
render(propertyDescriptors: PropertyDescriptor[]) {
  return html`
    ${propertyDescriptors.map(prop => this.renderField(prop))}
  `;
}

private renderField(prop: PropertyDescriptor) {
  switch (prop.editorUiAlias) {
    case 'Umb.PropertyEditorUi.TextBox':
      return html`
        <input 
          type="text" 
          .value=${this.formValues[prop.alias] || ''} 
          @input=${(e: Event) => this.onFieldChange(prop.alias, e)}
        />
      `;
    case 'Umb.PropertyEditorUi.DateTime':
      return html`
        <input 
          type="datetime-local"
          .value=${this.formValues[prop.alias] || ''}
          @change=${(e: Event) => this.onFieldChange(prop.alias, e)}
        />
      `;
    // ... handle other property editors
  }
}
```

### Form State Management

#### State Object

```typescript
private formValues: Map<string, unknown> = new Map();
private formErrors: Map<string, string> = new Map();
private isDirty: boolean = false;
private isSubmitting: boolean = false;
```

#### Validation Lifecycle

1. **Field blur:** Client-side regex validation (if available)
2. **Before submit:** Check mandatory fields, format validation
3. **After submit:** Server-side validation via WorkflowController
4. **Error display:** Show server validation errors if returned

### Workflow Orchestration Integration

#### Current Orchestrator

The `workflow-orchestrator.ts` already handles:
- State machine progression
- Response envelope parsing (`ask_now | wait | complete | error`)
- Task submission and retry logic
- Tenant isolation headers

**No changes required here** — the orchestrator remains unchanged.

#### Form Submission Flow

```typescript
async submitWorkflow() {
  this.isSubmitting = true;
  
  // 1. Client-side validation
  if (!this.validateForm()) {
    this.formErrors = this.getValidationErrors();
    this.isSubmitting = false;
    return;
  }

  // 2. Submit to WorkflowController
  const response = await this.workflowApiClient.submitResponse(
    this.workflowKey,
    this.instanceId,
    this.formValues,  // Map<string, unknown>
    this.stateVersion
  );

  // 3. Handle response
  if (response.responseState === 'error') {
    this.formErrors = response.validationErrors;
  } else {
    this.orchestrator.handleStateChange(response);
  }

  this.isSubmitting = false;
}
```

### Property Editor Handling

#### Map Editor UI Aliases to Input Types

| EditorUiAlias | Input Type | Component |
|---|---|---|
| `Umb.PropertyEditorUi.TextBox` | `<input type="text">` | `form-field` |
| `Umb.PropertyEditorUi.DateTime` | `<input type="datetime-local">` | `form-field` |
| `Umb.PropertyEditorUi.Toggle` | `<input type="checkbox">` | `form-field` |
| `Umb.PropertyEditorUi.RadioButtonList` | `<fieldset><input type="radio">` | `form-field` |
| `Umb.PropertyEditorUi.Dropdown` | `<select>` | `form-field` |
| `Umb.PropertyEditorUi.Textarea` | `<textarea>` | `form-field` |
| `Umb.PropertyEditorUi.RichText` | Rich text editor | `rich-text-field` |

### Testing Strategy

#### Unit Tests

- **PropertyDescriptor parsing:** Verify correct mapping of Element Type metadata to descriptors
- **Form renderer:** Test field rendering for each EditorUiAlias
- **Validation:** Test client-side regex and mandatory field checks
- **State management:** Test form value updates, dirty state tracking

#### Fixture Generation

Mock property descriptors for test scenarios:
```typescript
const textFieldDescriptor = createPropertyDescriptor({
  alias: 'firstName',
  label: 'First Name',
  editorUiAlias: 'Umb.PropertyEditorUi.TextBox',
  mandatory: true
});

const dateFieldDescriptor = createPropertyDescriptor({
  alias: 'birthDate',
  label: 'Date of Birth',
  editorUiAlias: 'Umb.PropertyEditorUi.DateTime',
  mandatory: false
});
```

#### Integration Tests

- **Workflow end-to-end:** Submit form, verify state machine progression
- **Error handling:** Verify error display and retry logic
- **Orchestrator coordination:** Verify form renderer integrates correctly with existing orchestrator

### Performance Considerations

#### Render Performance

- **Lazy load** property editors only when visible (for large forms)
- **Memoize** descriptor parsing to avoid re-computation
- **Virtual scrolling** for workflows with 100+ fields

#### Network Performance

- **Cache descriptors** in localStorage to avoid re-fetching
- **Batch validation** requests if multiple forms in a workflow
- **Progressive enhancement** — show form while validating in background

### Mobile Responsiveness

#### Breakpoints

- **< 600px:** Stack form fields vertically, full-width inputs
- **600px–1200px:** 1-column layout with wider fields
- **> 1200px:** Multi-column layout (if Element Type defines groups)

#### Touch Interactions

- Larger tap targets for mobile
- Native date picker for datetime fields (`<input type="date">` on mobile)
- Avoid hover-based interactions

### Accessibility (WCAG 2.1)

- **Labels:** Every form field has associated `<label>` with `for` attribute
- **Error messages:** Linked to fields with `aria-describedby`
- **Mandatory indicators:** Visual marker + `aria-required="true"`
- **Keyboard navigation:** All fields tabbable, sensible tab order
- **Screen readers:** Announce field type, mandatory status, error messages


---

## Decision: Field Group API Endpoints

**Date:** 2026-04-21  
**Author:** Blathers (Backend Dev)  
**Status:** ✅ Implemented

### Context

The workflow admin UI at `/admin/workflow` allows editing workflow definitions via inline JSON editors. Jonny requested the same capability for field groups, so admins can view and modify field configurations (fieldType, label, options, content, etc.) alongside workflow definitions.

### Decision

Added field group API endpoints to `MockBusinessApp` following the exact same pattern as the existing workflow definition endpoints:

#### API Endpoints

- `GET /admin/workflow/field-group/{key}/json`
  - Returns FormSectionDefinition as pretty-printed camelCase JSON
  - 404 if field group not found
  - Same key validation regex as definition endpoints: `^[a-zA-Z0-9\-]+$`

- `PUT /admin/workflow/field-group/{key}`
  - Deserializes FormSectionDefinition from request body
  - Updates in-memory field group (no file persistence)
  - Returns 200 with `{updated: key}` on success, 404 if not found
  - BadRequest for invalid key format or malformed JSON

#### Engine Methods

Added three methods to `BusinessAppWorkflowEngine`:

- `GetFieldGroup(string key)` — returns field group or null
- `GetAllFieldGroups()` — returns all loaded field groups
- `UpdateFieldGroup(string key, FormSectionDefinition updated)` — replaces in-memory field group

### Rationale

1. **Consistency:** Field group endpoints mirror definition endpoints exactly (validation, error handling, JSON serialization)
2. **Minimal scope:** No changes to UI, tests, or Core library — backend-only
3. **In-memory only:** Matches existing definition update behavior (no file persistence)
4. **Security:** Same key validation prevents path traversal/injection

### Consequences

- Admins can now view and edit field group JSON inline at `/admin/workflow`
- Updates are in-memory only — restart reverts to seed files
- Isabelle built the UI to consume these endpoints in parallel
- No breaking changes to existing code

---

## Decision: Inline Field Group Editing in Workflow Admin

**Date:** 2026-04-21  
**Decided by:** Isabelle (Frontend Dev)  
**Status:** ✅ Implemented

### Context

MockBusinessApp admin workflow UI extension. Users were confused because workflow definitions show `fieldGroupKeys: ["about-you-with-context"]` but the admin page never revealed the actual field structure inside those groups.

### Decision

Extended `/admin/workflow` GET handler to display and edit field groups inline alongside workflow definitions. The UI now shows a "Field Groups" table within each definition card, listing all field groups referenced by states with their display names, field counts, and edit buttons.

### Implementation

1. **Data fetching:** Added `GetAllFieldGroups()` call at handler initialization to build a dictionary of field groups by key
2. **UI surface:** New "Field Groups" section in definition cards with light purple background (#f4f0fb) to distinguish from state/transition tables
3. **Modal reuse:** Extended existing ACE editor modal with `currentEditorType` variable ('definition' | 'field-group') to support both resource types
4. **JS functions:**
   - `openEditor(key)` — sets type to 'definition', updates modal title, fetches definition JSON
   - `openFieldGroupEditor(key)` — sets type to 'field-group', updates modal title, fetches field group JSON
   - `saveDefinition()` — dispatches PUT to correct endpoint based on `currentEditorType`

### Rationale

Inline visibility solves the two-level architecture confusion without requiring navigation to separate pages or opening multiple JSON files.

### Coordination

- Blathers provided field group API endpoints
- No merge conflicts — isolated changes to GET handler
- Clean build, all tests passing

---

## Decision: v1.7.1 Security Release — Notes Format

**Date:** 2026-04-06  
**Author:** Mabel (Technical Writer & Release Manager)  
**Status:** ✅ Applied

### Question

How should security release notes be written for a patch release with critical auth fixes?

### Context

v1.7.1 security fix includes:
- ID token signature validation using per-tenant JWKS
- Nonce validation with hard failure on mismatch
- Structured logging instead of console debug output

Previous releases used feature-driven language. This release needed a security-focused approach.

### Decision

Security release notes should prioritize **security implications** over implementation details:

1. **What's protected:** Explain the vulnerability or attack vector being closed (e.g., "replay attacks," "token forgery")
2. **How it works:** One sentence on the mechanism (e.g., "per-tenant JWKS validation")
3. **User impact:** Mention if deployment action is required (none in this case — automatic)

### Format Applied

```markdown
## Security Improvements

- **ID token signature validation:** ID tokens are now cryptographically validated using per-tenant JWKS endpoints. Signatures must match the tenant's current key set; invalid signatures are rejected with a 401 response.
- **Nonce validation enforcement:** Nonce values in ID tokens are validated against the original authorization request nonce. Mismatches are treated as a hard failure and prevent token acceptance, closing the window for replay attacks.
- **Structured logging for auth flows:** Replaced debug console output (which inadvertently exposed tenant information) with structured logging via `ILogger<T>`. Auth flows now emit proper telemetry without exposing sensitive data to stdout.
```

### Reasoning

1. **"Security Improvements" section:** Signals to users that this is a security-relevant release
2. **Bold summary lines:** Scannable — developers can skim titles and decide relevance
3. **Mechanism + implication:** Not just "we added nonce validation" but "prevents replay attacks"
4. **Hard failure language:** "Treated as a hard failure" signals that mismatches are not silently ignored
5. **Logging as security:** Exposing tenant info to stdout is a data leak; replacing it with structured logging is a security improvement

### Scope

This format applies to all future security patch releases. Feature releases continue to use "New Features" / "Bug Fixes & Improvements" sections.

---

## Decision: Media URL Generation Fix (Workflow Media Picker)

**Author:** Blathers (Backend Dev)  
**Date:** 2026-04-06  
**Issue:** Media URLs from media picker properties not resolving correctly in workflow form payloads

### Problem

When a workflow step includes a media picker property, the rendered form shows a media ID (e.g., `media: 1234`) but the client cannot construct a URL to display the asset.

### Solution

Use `IComponentService.GetMediaUrlAsync(mediaId)` during payload generation in WorkflowController:

```csharp
if (property.DataType.EditorUiAlias == "Umb.PropertyEditorUi.MediaPicker")
{
    var mediaUrl = await componentService.GetMediaUrlAsync((int)property.Value);
    descriptor.DefaultValue = mediaUrl;  // Client receives absolute URL
}
```

**Service Registration:** Already included in MockBackOffice composition.

---

## Decision: Frontend Bug Fixes — UI Polish

**Author:** Isabelle (Frontend Dev)  
**Date:** 2026-04-06  
**Status:** ✅ Deployed

### 1. Media Picker Mobile — Touch Input Handling

**Issue:** Media picker on mobile devices didn't support touch-triggered file dialogs.

**Fix:** Added `touch-action: manipulation` CSS and tested with iOS/Android file pickers.

---

### 2. Headline Padding — Layout Consistency

**Issue:** Workflow step headlines had inconsistent padding across breakpoints.

**Fix:** Standardized to `padding: 1rem 1.5rem` on all breakpoints (mobile, tablet, desktop).

---

### 3. Picker MIME Filter — File Type Validation

**Issue:** Media picker accepted files that didn't match the Element Type property editor configuration.

**Fix:** Added client-side MIME type filter based on property descriptor's `editorConfig.allowedTypes`.

---

### 4. WebKit Timing — Animation Smoothness

**Issue:** CSS animations jittered on Safari/WebKit due to timer resolution.

**Fix:** Added `-webkit-backface-visibility: hidden` and used `requestAnimationFrame` instead of `setInterval`.

---

### 5. Maximised Scroll — Overflow Handling

**Issue:** Form scrolling was blocked when workflow panel was maximized (fullscreen mode).

**Fix:** Added `overflow-y: auto; max-height: calc(100vh - 4rem)` to form container.

---

### 6. Workflow Client Phase 4 — Integration Testing

**Author:** Isabelle (Frontend Dev)  
**Date:** 2025-04-09  
**Status:** ✅ In Progress

#### Scope
End-to-end testing of workflow client against new Element Types backend:
- Form rendering with property descriptors
- Validation and error handling
- State machine progression
- Mobile responsiveness

#### Test Cases
1. ✅ Single-field form (text input)
2. ✅ Multi-field form with validation
3. ✅ Media picker field
4. ✅ Date/time field
5. ✅ Error state and retry
6. ⏳ Mobile viewport rendering
7. ⏳ Accessibility compliance (WCAG 2.1)
8. ⏳ Performance benchmarks

#### Fixtures
Property descriptors generated for testing:
- Personal Details Step (firstName, lastName, dateOfBirth, agreedToTerms)
- Contact Information Step (email, phone)
- Media Upload Step (profilePhoto)

---

# Blathers — Aspire startup prerequisite guard

## Context

The VS Code **C#: Aspire (Full Stack)** launch path could fail with an opaque Aspire runtime exception:

- `Property CliPath: The path to the DCP executable used for Aspire orchestration is required.`
- `Property DashboardPath: The path to the Aspire Dashboard binaries is missing.`

In practice, this occurred when local machine prerequisites were missing, especially the Aspire workload.

## Decision

Add an explicit prerequisite validation step to the repo-owned VS Code full-stack launch flow before AppHost starts.

## Conventions

- Keep the fix in repository launch/task configuration instead of relying on developers to remember a manual workaround.
- Validate the Aspire workload before AppHost launch and fail with a direct setup instruction if it is missing.
- Validate Docker availability in the same preflight because the full stack depends on container orchestration for Keycloak.
- Document the exact `CliPath` / `DashboardPath` symptom in local dev docs so developers can map the exception to the missing prerequisite quickly.

## Why

Aspire tooling binaries are an external machine prerequisite and cannot be bundled by the AppHost project itself. Repo-level preflight validation is the smallest reliable fix: it preserves the existing launch flow on correctly configured machines while turning a confusing runtime crash into an actionable setup message.

---

## Decision: Workflow Model Naming Cleanup

**Date:** 2026-01-22  
**Author:** Blathers (Backend Dev)  
**Status:** ✅ Complete  
**Scope:** C# workflow models, Business App engine, TestSite controllers

### Decision

Rename workflow types and state values to use clear, ubiquitous language that reflects their actual purpose in the workflow engine.

### Rationale

The user confirmed a naming directive: **use clear, ubiquitous language** across all workflow models. The previous names (`WorkflowRenderPayload`, `FieldGroupRenderPayload`, `WorkflowStateFile`, `FieldGroupFile`) were technically accurate but not intuitive. The new names better describe what each concept represents:

- **StepContent** — the content to render for one step of a workflow (clearer than "render payload")
- **FormSection** — a logical section of a form within a step (clearer than "field group render payload")
- **StepDefinition** — defines one step in a workflow seed file (clearer than "workflow state file")
- **FormSectionDefinition** — defines a form section in a seed file (clearer than "field group file")

String state values also renamed for clarity:
- **"render"** — render this step to the user now (clearer than "ask_now")
- **"defer"** — defer this step, don't render it yet (clearer than "wait")

### Changes

#### Type Renames
1. `WorkflowRenderPayload` → `StepContent`
2. `FieldGroupRenderPayload` → `FormSection`
3. `WorkflowStateFile` → `StepDefinition`
4. `FieldGroupFile` → `FormSectionDefinition`

#### String Value Renames
1. `"ask_now"` → `"render"`
2. `"wait"` → `"defer"`

#### Files Updated
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/WorkflowDefinitionFile.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`
- `src/UmbracoPrism.TestSite/Models/WorkflowViewModel.cs`
- `src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs`

### Impact

- **Backend:** Type renames across shared models, Business App engine, and TestSite
- **Frontend:** No impact — view models and Razor views use the updated types seamlessly
- **JSON Seeds:** No changes needed — seed files use string keys, not type names
- **Tests:** All 420 Core tests passing

### Validation

- ✅ Build succeeded with 0 errors
- ✅ All 420 tests passing
- ✅ Comprehensive grep search confirmed all usages updated

### Additional Work

**Date-Input Year Validation:** Added explicit year range check (1900-2100 inclusive) to `WorkflowFieldValidator.cs` for `date-input` field type, with 4 new test cases.

---

## Decision: Playwright E2E Test Patterns for GDS Workflow Journeys

**Date:** 2026-04-20  
**Author:** Tangy (Tester)  
**Status:** ✅ Implemented

### Context

Writing comprehensive E2E tests for the planning-notification-v1 workflow required establishing test patterns for multi-step GDS Design System workflows that integrate with the existing localhost Aspire test infrastructure.

### Decisions

#### 1. Test File Organization

**Decision:** Use separate test files per workflow type, excluded from default Storybook config, included in localhost-auth config.

**Pattern:**
```typescript
// tests/workflow-{workflow-name}.spec.ts
test.describe('{Workflow} GDS journey behavioural contracts', () => {
  test.describe.configure({ mode: 'serial' });
  // ... tests
});
```

**Rationale:** Each workflow is a distinct user journey deserving its own test suite. Serial mode required because tests share LiveAppHost lifecycle and state. Clear naming convention makes it easy to identify workflow tests vs component tests.

#### 2. Selector Strategy for GDS Components

**Decision:** Always use semantic selectors that target rendered HTML, never web component tags or CSS classes.

**Preferred selector hierarchy:**
1. `getByRole` (buttons, links, headings, checkboxes, radios)
2. `getByLabel` (form inputs)
3. `getByText` (summary values, error messages)
4. Locator with semantic attributes (`role="alert"`, `.govuk-summary-list`)

**Examples:**
```typescript
// ✅ Good
await page.getByRole('heading', { name: 'Check your answers' });
await page.getByLabel('Project name').fill('...');
await page.locator('[role="alert"]').first();

// ❌ Avoid
await page.locator('prism-field'); // web component tag
await page.locator('.prism-input'); // CSS class
await page.locator('input[name="fields[projectName]"]'); // implementation detail
```

**Rationale:** Tests survive component refactoring. Mirrors user interaction. Enforces accessibility (semantic HTML is a prerequisite).

#### 3. Date Input Field Targeting

**Decision:** Target GDS date-input sub-fields by their generated IDs: `{fieldKey}-day`, `{fieldKey}-month`, `{fieldKey}-year`.

**Pattern:**
```typescript
await page.locator('#proposedStartDate-day').fill('15');
await page.locator('#proposedStartDate-month').fill('6');
await page.locator('#proposedStartDate-year').fill('2025');
```

**Rationale:** GDS Design System standard structure (3 separate inputs). IDs are stable contract (generated from fieldKey). No semantic alternative (no label per sub-input).

#### 4. Error Validation Testing

**Decision:** Always verify both error summary AND field-level errors. Check `role="alert"` on error summary.

**Pattern:**
```typescript
// Submit invalid form
await page.getByRole('button', { name: 'Continue' }).click();

// Verify error summary (GDS accessibility requirement)
const errorSummary = page.locator('[role="alert"]').first();
await expect(errorSummary).toBeVisible();
await expect(errorSummary).toContainText('There is a problem');

// Verify field-level error
await expect(page.locator('.prism-field-error').first()).toBeVisible();
```

**Rationale:** GDS Design System requires both summary and field-level errors. `role="alert"` is accessibility requirement (screen reader announcement). Tests enforce full error UX.

#### 5. Conditional Field Reveal Testing

**Decision:** Test both reveal and hide behaviour. Use `toBeVisible()` and `toBeHidden()`, not attribute checks.

**Pattern:**
```typescript
// Select option with conditional field
await page.getByRole('radio', { name: 'Other' }).check();
const conditionalField = page.getByLabel('Describe the type of work');
await expect(conditionalField).toBeVisible();

// Select different option
await page.getByRole('radio', { name: 'Extension or alteration' }).check();
await expect(conditionalField).toBeHidden();
```

**Rationale:** `toBeVisible()` checks display, visibility, and hidden attribute (comprehensive). Tests both directions (show/hide) catches one-way bugs. Playwright's retry logic handles animation/transition timing.

#### 6. Check-Answers Summary Validation

**Decision:** Verify submitted values appear in the GDS summary list, not the entire HTML structure.

**Pattern:**
```typescript
const summaryList = page.locator('.govuk-summary-list');
await expect(summaryList.getByText('Garden extension')).toBeVisible();
await expect(summaryList.getByText('£25000')).toBeVisible();
```

**Rationale:** Scoped locator prevents false positives from header/footer. Tests user-facing value display. Allows for layout changes without breaking tests.

#### 7. Workflow Routing and Seeding

**Decision:** Seed workflow pages in `WorkflowPageSeeder` with stable URLs and workflow keys. Use constants in `TestSiteSeedContract`.

**Pattern:**
```csharp
// TestSiteSeedContract.cs
public const string PlanningWorkflowKey = "planning-notification";
public const string PlanningWorkflowPageUrl = "/apply-for-planning";

// WorkflowPageSeeder.cs
private void EnsurePlanningWorkflowPage() {
    var page = contentService.Create(
        TestSiteSeedContract.PlanningWorkflowPageName,
        Constants.System.Root,
        TestSiteSeedContract.WorkflowPageAlias
    );
    page.SetValue("workflowKey", TestSiteSeedContract.PlanningWorkflowKey);
    SaveAndPublishIfNeeded(page, ...);
}
```

**Rationale:** Tests use stable URLs instead of dynamic Umbraco routes. Seeding is idempotent (development-only, runs on app start). Constants centralized for reuse across controllers, tests, documentation.

#### 8. Test Lifecycle and AppHost Management

**Decision:** Reuse existing `LiveAppHost` pattern for localhost workflow tests. One instance per test file, serial test execution.

**Pattern:**
```typescript
const appHost = new LiveAppHost();

test.beforeAll(async () => {
  await appHost.start();
});

test.afterAll(async () => {
  await appHost.stop();
});
```

**Rationale:** LiveAppHost already handles Aspire stack (Keycloak, TestSite, MockBusinessApp). Serial mode avoids test isolation issues (shared workflow state). Restarting Aspire per test is too slow (5+ minutes startup time).

#### 9. Playwright Config Separation

**Decision:** Maintain two Playwright configs — default for Storybook, localhost-auth for live app tests.

**Pattern:**
```typescript
// playwright.config.ts (default)
testIgnore: ['**/localhost-auth-session.spec.ts', '**/workflow-gds-journey.spec.ts']

// playwright.localhost-auth.config.ts
testMatch: /(localhost-auth-session|workflow-gds-journey)\.spec\.ts/
```

**Rationale:** Default config runs fast Storybook tests (no external dependencies). Localhost-auth config explicitly opts into slow tests (requires Aspire stack). Clear separation prevents accidental CI slowdown.

### Future Considerations

1. **Multi-workflow test efficiency**: If we add many workflow test files, consider a shared AppHost fixture at suite level instead of file level.
2. **Dynamic workflow discovery**: Current approach seeds specific workflows. Future: test framework could discover all workflow definitions and generate test scaffolds.
3. **Conditional field mapping**: Manual mapping of conditional fields in tests. Future: data-driven approach using workflow seed JSON.
4. **Change-answer navigation**: Current test manually progresses through all steps after changing an answer. Workflow may support direct navigation to check-answers in future.

### Related Files

- `src/UmbracoPrism.Client/tests/workflow-gds-journey.spec.ts`
- `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`
- `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs`
- `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`

---

## Decision: Interactive Walkthrough Guide in README

**Date:** 2026-04-16  
**Author:** Mabel (Technical Writer & Release Manager)  
**Status:** Complete  
**Scope:** Developer onboarding, documentation, user experience

### Problem

New users landing on the Umbraco.Prism repository had quick-start instructions but no guided tour showing them how to actually *use* the demo workflow. Developers unfamiliar with the system couldn't see:

- The end-to-end user experience of filling out a multi-step workflow
- How the workflow definition, field groups, engine, and Umbraco views connect
- What Keycloak authentication does behind the scenes
- How Umbraco backoffice content relates to the frontend workflow

### Solution

Created a comprehensive **Interactive Walkthrough** section in README.md that:

1. **Walks users through the demo workflow step-by-step** with concrete data to enter at each step, making it immediately runnable
2. **Explains what's happening at each step** with callouts showing:
   - OIDC authentication flow details
   - Workflow state transitions and field validation
   - Instance management and data persistence
3. **Maps the user experience to the code** by showing:
   - The workflow definition JSON structure
   - Field group references and validation rules
   - How the BusinessAppWorkflowEngine processes requests
   - How BusinessAppWorkflowClient integrates with Umbraco
   - How Umbraco Razor partials render different step types
4. **Provides bonus exploration sections** for users who want to dig deeper:
   - How to view workflow definitions
   - How to check engine logs
   - How to edit content in the backoffice
   - How to test multi-browser scenarios

### Structure

The walkthrough is organized as three distinct parts, allowing users to read at different depths:

- **Part 1: Log In and Start** — Quick, actionable steps (3-5 minutes)
- **Part 2: Walk Through Steps** — Guided workflow execution with concrete data (10-15 minutes)
- **Part 3: Behind the Scenes** — Deep-dive architecture and code explanations (optional, 15+ minutes)

### Style Decisions

- **Callouts:** Used emoji-based callouts (💡 for learning, ✅ for features, ℹ️ for reference) to break up dense text and highlight different types of information
- **Code examples:** Showed real JSON from `planning-notification-v1.json` and field group files so developers can reference actual code
- **Tone:** Developer-first ("here's what this means to you"), active voice, present tense
- **Concrete vs. abstract:** Always paired abstract concepts with concrete steps ("Click here, enter 'Extension'") before explaining why

### Related Files

- `/README.md` — Main walkthrough section added after credentials table
- `/ASPIRE_DEV.md` — Added callout linking quick-start users to the README walkthrough
- `.squad/agents/mabel/history.md` — Learnings and session record

### Impact

- **Reduces onboarding friction:** Users can get from "clone repo" to "completed workflow demo" in 15-20 minutes
- **Clarifies architecture:** Developers understand the connection between workflow JSON, field groups, the engine, and Umbraco views
- **Enables self-service learning:** Users don't need to ask teammates how to run the demo
- **Supports future contributors:** Contributors can understand the workflow system well enough to extend it

### Alternatives Considered

1. **Separate walkthrough document** — Rejected; keeping it in README maximizes visibility and reduces doc fragmentation
2. **Video walkthrough** — Rejected; text-based guide is easier to maintain and works in all environments (including Codespaces terminal)
3. **Interactive tour** — Rejected; out of scope; text guide is sufficient for initial onboarding

### Maintenance

- The walkthrough should be updated if:
  - Workflow definition changes (e.g., new steps, different field types)
  - Field group structure evolves
  - OIDC flow changes (e.g., different scopes or claims)
  - Umbraco backoffice UI changes significantly
- Version it in git alongside code changes; don't let docs drift

---

## Decision: Extend PrismFieldTagHelper with Content Field Types

**Date:** 2026-04-22  
**Author:** Blathers (Backend Developer)

### Decision

`PrismFieldTagHelper` is extended with four non-input GDS content component field types (`inset-text`, `warning-text`, `details`, `notification-banner`). These are declared directly in field group JSON alongside form fields — no new Razor partials or tag helpers are needed.

### Context

The existing `PrismFieldTagHelper` handled all form input rendering. There was no mechanism to inject GDS content components (callout boxes, expandable details, warning banners) inline within a field group. Without this, authors had to use separate Razor partials or bespoke views, breaking the self-contained field group model.

### Why This Approach

- **Single rendering surface**: All field-group content flows through one tag helper. Authors define layout entirely in JSON.
- **Zero view changes**: No Razor partial changes required in TestSite or MockBusinessApp views.
- **GDS fidelity**: HTML output matches the GOV.UK Design System component markup exactly.
- **Validation safety**: Content types are excluded from field validation by type string check — no new model properties needed.

### Implications

- A `Content` string? property is added to `FieldRenderPayload` (Shared) and `FieldFile` (MockBusinessApp). Other consumers of these models may receive a null `Content` property harmlessly.
- Content field types that have null/empty `Content` render nothing — fail-safe behaviour.
- The validator skips content field types entirely: they contribute no user-submitted value and are never treated as required.
- The existing `govuk-form-group` wrapper is bypassed via early-return before the outer `<div>` is added.
- `details` uses `Label` as the summary text (fallback: "More information"). `notification-banner` uses `Label` as the title (fallback: "Important").


---

## Decision: Screenshot Policy — Content-Aware Default

**Date:** 2026-05-04  
**Agents:** Isabelle (Screenshot & Visual Fidelity), Mabel (Documentation Specialist)  
**Status:** Implemented

### Summary

Walkthrough screenshot policy corrected to default to **showing the whole useful screen**, with selective cropping only for exceptionally tall pages (>2200px). This ensures documentation shows complete functionality context rather than truncated viewport-sized captures.

### Problem Solved

Earlier policy had drifted toward viewport-first default (`fullPage: false`), which made fresh captures too cropped to show full functionality. Team feedback clarified the intended rule: default to complete screen context.

### Decision

**Default behavior:** Full page (all scrollable content visible)
- Shows complete functionality of the screen
- Applies to form pages, check-answers pages, summary pages
- Readers see everything available on the page

**Constrain only when:**
- Page is exceptionally tall (>2200px)
- Full-page height creates visual clutter without adding documentation value
- Smaller crop makes guidance clearer without hiding necessary content

### Implementation

- **Control point:** `tests/walkthroughs/support/walkthrough.ts` (single policy location)
- **Per-step override:** `screenshotSelector` for location-based crops; `screenshotMaxHeight` for height caps
- **Workflow override:** `SCREENSHOT_FULL_PAGE=1` for forced full-page captures

### Files Updated

- `tests/walkthroughs/support/walkthrough.ts` — JSDoc comments and content-aware logic
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — "Screenshot Heights" section
- Screenshot capture layer — helper suppression timing and frame validation

### Impact

- **Walkthrough authors:** Use full-page default; only explicitly set `fullPage` when narrative requires it
- **Next captures:** Follow new default policy
- **Team clarity:** Policy now consistent across skill documentation and code comments

---

## Decision: Approval Workflow Narratives in Walkthroughs

**Date:** 2026-05-04  
**Author:** Mabel (Documentation Specialist)  
**Status:** Implemented

### Summary

Updated four walkthrough documents to provide complete, step-by-step guided demonstration of the approval/reviewer handoff pattern. Moved from brief explanations of "approval is needed" to full narratives showing user submission → waiting state → operator review → user outcomes.

### Problem Solved

Workflows are correctly defined with `requiresRole: "reviewer"` transitions, but documentation didn't explain them where readers need to understand them — in service walkthroughs. Readers finishing walkthroughs didn't understand:
- How workflows continue after user submission
- Why intermediate states exist
- What role/permission is needed to advance them
- Why forms/transitions behave the way they do

### Narratives Implemented

**Pattern:** Four-part walkthrough structure for workflows with approval steps

1. **Part 1:** End-user submission (form → waiting/processing state)
2. **Part 2:** Operator review (admin panel → viewing definition → performing approval/rejection)
3. **Part 3:** Return-to-user confirmation (what user sees after approval)
4. **Part 4:** Production patterns (webhook vs manual approval vs operator interface)

### Workflows Updated

#### Payment Demo
- Explicit step-by-step guide for accessing admin panel from dashboard
- Workflow definition JSON showing `requiresRole: "reviewer"`
- Explanation of why `waiting` component + `single` instance policy work together
- Three production patterns (Stripe webhook, operator interface, system role)

#### Community Enquiry
- `under-review` explained as non-terminal, waiting state
- State machine showing both `approve` and `request-changes` transitions
- Cycle loops: `collecting-details` ↔ `under-review` ↔ `collecting-details`
- What "request changes" means for user (form returns with answers)

#### Information Request
- Urgency field tied to reviewer workflow (triage queue, SLAs)
- State machine showing transitions gated by `requiresRole: "reviewer"`
- SLA examples: Standard (7 days), Urgent (2 days), Critical (same day)

#### README & Workflow Administration
- README.md: Updated note positioning admin panel as "reviewer role simulator" in local demo
- Workflow Administration.md: Expanded Part 2b with full step-by-step walkthrough of handoff

### Files Changed

- `docs/walkthroughs/payment-demo.md`
- `docs/walkthroughs/community-enquiry.md`
- `docs/walkthroughs/information-request.md`
- `docs/walkthroughs/README.md`
- `docs/walkthroughs/workflow-administration.md`

### Documentation Principles Applied

- **Executable specs alignment** — Narratives coordinate with workflow definitions and test specs
- **Guided demonstration** — Each reads as step-by-step walkthrough someone could follow in running app
- **Conceptual coherence** — Admin panel positioned as "reviewer actor" in service flow
- **Production grounding** — "Production Patterns" section shows why architecture matters in real systems
- **User-centric outcomes** — Always shows what changes for original user after approval/feedback

### Why This Matters

1. **Onboarding clarity** — Developers understand not just "what approval is" but "how it flows end-to-end"
2. **Test authoring** — Specs have clear narratives; easier to write related edge case tests
3. **Design grounding** — Product team sees exactly where approval/review points are and why they exist
4. **Production mapping** — Each section labeled "Production Patterns" makes clear how demo harness maps to real operator workflows

---

## Decision: Waiting-State Walkthroughs Prove Original Page Advances

**Date:** 2026-05-04  
**Agent:** Tangy (Tester)  
**Status:** Implemented

### Summary

Executable specs for walkthroughs that pause in waiting or under-review state now keep the original member-facing page open while a second page follows the reviewer route. This proves the waiting page moves on automatically after approval without needing manual refresh.

### Pattern

1. Complete member journey until waiting page is visible
2. Open second page/tab for supporting checks:
   - Inspect **My Workflows** if needed
   - Follow discoverable dashboard route to **Workflow Admin**
3. Perform reviewer action there
4. Return to or foreground original waiting page
5. Assert that it advances without manual refresh step in spec

### Why

- Teaches the whole mechanism, not just the operator half
- Keeps service walkthroughs honest about what member actually experiences
- Provides stronger regression coverage for waiting-step polling/reload behaviour

### Implementation

- `payment-demo.walkthrough.spec.ts` — Full approval flow with user page staying open and auto-advancing
- Multi-page pattern — simultaneous browser tabs for user and admin routes
- Auto-advance validation — confirms page updates without explicit refresh trigger

### Impact

- Walkthrough tests now demonstrate complete member experience
- Regression suite covers polling/reload behaviour
- Documentation aligned with actual end-to-end workflow behavior

