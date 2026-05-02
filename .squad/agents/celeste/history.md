# Celeste — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**User:** Jonny Muir

## Team Context

- Tom Nook: Architecture, scope, code review
- Isabelle: Web Components, Storybook, UI logic
- Blathers: Backend services, middleware, authentication
- Tangy: Test strategy and reliability coverage
- Copper: Security engineering (CIA, tenant isolation)
- Scribe: Session logging and decisions

## Learnings

- User requested stronger XML-style documentation discipline across code.
- Documentation must support multi-tenant and security-critical reasoning, not generic summaries.
- Security-sensitive flows should document tenant boundaries, trust assumptions, and failure behavior explicitly.
- Practical baseline works best when focused on public/protected API surface in Auth, Services, Middleware, and boundary Models before private internals.
- Parameter and return tags should be explicit for request/tenant/security context values to improve IntelliSense safety during integration work.
- Docs-only passes should stay behavior-neutral and be validated with full build plus Core tests to keep risk minimal.
- **2026-05-02: devcontainer.json timing nuance:** `customizations.vscode.settings` in devcontainer.json is applied *late* in VS Code startup (sometimes after `postAttachCommand` runs). When relying on editor associations (like `workbench.editorAssociations`), place them in `.vscode/settings.json` at the workspace root instead — this loads earlier and reliably. Keep devcontainer settings as a belt-and-braces fallback for robustness.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.
- 2026-04-14: Created `docs/DEPLOYMENT_SECURITY.md` to document deployment boundaries, environment variable hygiene, and pre-flight checks. Key learnings: security-critical guides must cover the "why" (security design intent) not just the "what" to help developers reason about risk. Backchannel URL pattern is subtle—the guide explains that it changes metadata fetch URL but not trust anchor, so developers understand it's safe when combined with strict issuer validation. Added checklist format for pre-deployment verification to make guidance immediately actionable.
- 2026-01-24: Completed XML documentation for new workflow DX classes. Focus on actionable, developer-specific language: explain *what* developers override and *why* (e.g., PrePopulateFields is called after workflow state retrieval but before nonce generation). Used `<remarks>` for pattern guidance, `<example>` for builder fluent APIs, and `<returns>` to describe return values contextually. Builder classes benefit from concrete usage examples showing typical chaining patterns. ViewModel docs explained non-obvious properties like StepType (not Archetype), Nonce (tamper-proof binding), and FieldErrors (convenience property). Controller docs emphasized the full pattern (antiforgery + nonce + PRG + validation) with bulleted responsibilities so integrators understand the boilerplate they inherit. All docs validated with `dotnet build` — no XML doc warnings.

---

## 2026-04-21T20:58:11Z: Workflow API Documentation Session

**Scope:** XML documentation for all new public API surface

**Changes:**
- Added comprehensive XML docs to PrismWorkflowPageController<TViewModel>
  - Class-level docs with inheritance patterns
  - Method docs for GET/POST handlers
  - Virtual method docs for customization points
  - Example usage for integrators
  
- Added XML docs to PrismWorkflowViewModel
  - Base class purpose and extensibility
  - StepType property documentation
  
- Added XML docs to WorkflowDefinitionBuilder
  - Fluent API pattern documentation
  - Method docs with parameters and constraints
  - Example workflows with inline comments
  
- Added XML docs to FieldGroupBuilder and WorkflowFieldBuilder
  - Field creation methods
  - Type and validation documentation
  - Usage patterns with examples

**Documentation Standards:**
- All public members documented
- Parameter constraints clearly stated
- Return values described
- Code examples included for complex APIs
- Remarks for integrator guidance
- Cross-references between related classes

**Result:** ✅ Zero doc warnings, full IntelliSense coverage, consistent style

**Reference:** `.squad/orchestration-log/2026-04-21T20:58:11Z-celeste.md`

