# Vision review — Tom Nook
_2026-05-01T08:57:29+01:00_

## Verdict

Prism is a genuinely coherent product when you see it from the outside: one Umbraco install, multiple branded portals, a workflow engine, a mobile launcher. The tag line holds. But internally it is three loosely-coupled pillars — a tenant/auth/branding engine, a GDS workflow runtime, and a mobile bundle generator — wired together by a single `PrismComposer.cs` that registers all of them unconditionally. Nobody who adds the package to run *only* multi-tenant branding gets branding: they also get `LimitedEditionDropNotifier`, `ExchangeRateLimitService`, `NotificationRateLimitService`, `BiometricTokenService`, and `RefreshTokenEncryptionService` injected into their container. That is honest about nothing. The seam between the pillars is invisible and therefore unobtrusive in the worst way — creators never know what they're carrying, and operators have no dial to turn. Prism is close to great. It needs to be split into honest feature registrations before it can truthfully call itself "as little design as possible".

---

## Rams Scorecard

| # | Principle | Score | Evidence |
|---|-----------|-------|----------|
| 1 | **Innovative** | ✅ | Stateless per-request OIDC swap via `PrismOidcConfiguration` + `IPostConfigureOptions<OpenIdConnectOptions>` is architecturally novel; most multi-tenant packages hard-code one identity provider. `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`. |
| 2 | **Makes a product useful** | ✅ | JSON seed files (`src/UmbracoPrism.MockBusinessApp/workflow-seeds/community-enquiry.json`) are genuine author-vocabulary: a content designer can describe a form in GDS terms without touching C#. That is real usefulness. |
| 3 | **Aesthetic** | ⚠️ | GDS partials are clean and component-typed; but `WorkflowRenderShellResolver.cs` derives shell type from heuristics over component lists (lines 22–44) — an author sees no surface for this, and the fallback chain is invisible beauty-debt. |
| 4 | **Makes a product understandable** | ⚠️ | `PrismComposer.cs` registers 20+ services in one 165-line method with no feature grouping beyond numbered comments (lines 27–163). An integrating developer cannot tell which services are core tenancy, which are workflow, and which are mobile. |
| 5 | **Unobtrusive** | ⚠️ | Tenant resolution is silent and correct. But `PrismTenantMiddleware.cs` logs a `LogWarning` for every request that doesn't match a tenant (line 52) — localhost developer workflows are drowned in noise. |
| 6 | **Honest** | ❌ | `OidcClientSecret` column exists in `PrismTenantSchema.cs` (line 103–106), marked "legacy, prefer provider/reference columns". Legacy columns that write nothing are quiet lies in the schema: any developer inspecting the DB sees a `OidcClientSecret` column and infers it is the right place to put a secret. |
| 7 | **Long-lasting** | ✅ | `[JsonPolymorphic]` + type discriminators in `PrismComponent.cs` (lines 9–32) is stable STJ-native polymorphism. It will survive .NET major versions without a custom converter. Seed-file JSON is version-tagged. |
| 8 | **Thorough down to the last detail** | ⚠️ | `WorkflowHubController.Index()` calls `IndexAsync().GetAwaiter().GetResult()` (line 44) — a sync-over-async deadlock risk that cuts against the careful async work everywhere else in the codebase. |
| 9 | **Environmentally friendly** | ⚠️ | `BusinessAppWorkflowEngine` is a singleton that holds all workflow instances in-memory (`ConcurrentDictionary`, line 23) and holds no eviction policy. For a demo this is fine; as a pattern it teaches consumers that "this is acceptable" for production. |
| 10 | **As little design as possible** | ❌ | `PrismComposer.cs` registers everything unconditionally. A site that wants only multi-tenant branding gets biometric services, push notification rate limiters, and a mobile bundle service injected. There is no opt-in surface. |

---

## Three Things Prism Gets RIGHT

### 1. The workflow component model is genuinely author-vocabulary
`src/UmbracoPrism.Shared/Models/Workflow/Components/PrismComponent.cs` uses `[JsonPolymorphic]` with discriminators that match GDS component names (`"fieldset"`, `"inset-text"`, `"warning-text"`, `"summary-list"`). A service designer writing `workflow-seeds/community-enquiry.json` sees GDS terms, not C# type names. The engine-side `BuildComponents` rendering pipeline and the TagHelper dispatch system honour these names end-to-end. This is the strongest manifestation of "makes a product understandable" in the whole codebase.

### 2. The tenant/auth seam is architecturally sound
`PrismTenantMiddleware` → `PrismBrandingMiddleware` → `PrismOidcConfiguration` is a clean request-scoped pipeline. The OidcConfiguration swaps authority/clientId/credential at runtime without mutating singleton options — a correct, hard problem solved correctly. `IPostConfigureOptions<OpenIdConnectOptions>` registered as singleton but operating per-options-instance is the right pattern here (`src/UmbracoPrism.Core/PrismComposer.cs`, lines 98–100). No auth state leaks between tenants by design.

### 3. The override-by-convention partial system
`PrismPartialsComposer.cs` wires a `CompositeFileProvider` so embedded GDS partials from the package are the fallback, and a consumer's own `Views/Partials/PrismFields/_Component-Text.cshtml` silently overrides them. The TagHelper dispatch (`PrismComponentTagHelper.cs`, lines 152–172) resolves at runtime via `ICompositeViewEngine.GetView`. No configuration, no inheritance, no code: add a file and it takes effect. This is "as little design as possible" applied perfectly to the extensibility surface.

---

## Three Things to Wrestle With

### 1. `PrismComposer.cs` is a monolith with no feature gates
**Problem:** All services — tenant resolution, OIDC, branding, workflow, biometrics, mobile bundle, push notifications, limited-edition drop notifier — are registered in a single `Compose()` method with no way to opt out. A developer installing Prism for multi-tenant branding alone carries the full stack.
**File:** `src/UmbracoPrism.Core/PrismComposer.cs`, lines 27–163.
**Suggestion:** Decompose into `AddPrismCore()` (tenancy + branding + auth), `AddPrismWorkflow()`, `AddPrismMobile()`, `AddPrismNotifications()` extension methods on `IUmbracoBuilder`. The composer calls all four for backward compat; consumers who know what they need can call one. This is not "add more design" — it is removing the implicit design that currently forces everything on everyone.

### 2. The workflow engine contract is only half-formal
**Problem:** `IBusinessAppWorkflowClient` in Core (`src/UmbracoPrism.Core/Services/IBusinessAppWorkflowClient.cs`) defines the Umbraco→BusinessApp HTTP contract, and `WorkflowResponseEnvelope` / `WorkflowDefinitionFile` live in Shared. But `BusinessAppWorkflowEngine` in MockBusinessApp has *its own* `WorkflowDefinitionFile` (`src/UmbracoPrism.MockBusinessApp/Services/WorkflowDefinitionFile.cs`), shadowing the Shared type. Any developer implementing a real business app must discover via runtime error that the authoritative schema is in `UmbracoPrism.Shared`, not in MockBusinessApp. The seam is invisible.
**Suggestion:** Delete `MockBusinessApp/Services/WorkflowDefinitionFile.cs` entirely; have MockBusinessApp reference the Shared type directly. If the two types genuinely differ, that difference should be named and owned, not silently doubled.

### 3. The `OidcClientSecret` column is an honest-design violation with security consequences
**Problem:** `PrismTenantSchema.cs` (line 103–106) retains an `OidcClientSecret` column "for migration compatibility". It is never written. Any developer inspecting the schema will infer it is the correct place to store a secret — which the security audit already established is wrong. The "correct" pattern (provider + reference columns) is described in prose comments, not enforced structurally.
**File:** `src/UmbracoPrism.Core/Persistence/PrismTenantSchema.cs`, lines 103–106.
**Suggestion:** Write a migration that removes the column. If an existing Prism install has data in it, surface a startup warning (not a silent no-op). "Long-lasting" and "honest" require removing things that tell lies, even when the lies are comfortable.

---

## The One Question I'd Put to Jonny

**Is MockBusinessApp a demo or a reference implementation?**

Right now it is both and neither. It ships production-grade JWT validation, sanitization, and concurrency control — but stores all state in a `ConcurrentDictionary` that evaporates on restart. It has its own `WorkflowDefinitionFile` type that shadows the Shared one. It is named "Mock" but linked to in the README as the thing that "powers the workflow engine."

If it is a demo: simplify it ruthlessly. Strip the JWT validation. Make the in-memory amnesia explicit and prominent. Its job is to show the HTTP contract, not simulate a production system.

If it is a reference implementation: rename it (`UmbracoPrism.WorkflowApp` or similar), give it a real persistence layer, and publish it as the template a department spins up alongside Prism. The workflow seed files are already good enough to ship as a product — the engine deserves the same.

The answer determines whether Prism is a library (Core + Shared) or a platform (Core + Shared + a runtime teams deploy). Rams would say: decide, then remove everything that doesn't serve the decision.
