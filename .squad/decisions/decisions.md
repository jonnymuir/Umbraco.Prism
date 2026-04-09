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

