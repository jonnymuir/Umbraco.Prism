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

## 2026-05-02T11:55:13Z: Marketplace README Solution

**Problem:** GitHub renders HTML tags correctly, but the Umbraco Marketplace ingests README content as plain text — raw `<div>`, `<img>`, `<picture>` blocks appear as literal text, ruining the listing appearance.

**Investigation Findings:**
- Marketplace schema (https://marketplace.umbraco.com/umbraco-marketplace-schema.json) has NO separate field for marketplace-specific documentation — only `DocumentationUrl` field
- Schema supports: DocumentationUrl, Description, VideoUrl, Screenshots (structured), but no "MarketplaceReadmeUrl" or similar override
- Current umbraco-marketplace.json points DocumentationUrl to GitHub README anchor (`#readme`)
- README.md contains three decorative HTML blocks that render as plain text in marketplace:
  - Line 1–4: Centered logo with tagline (`<div align="center">`)
  - Line 100–103: Two side-by-side screenshot images (`<div align="center">` + `<img>` tags)
  - Line 135–137: Centered iOS screenshot (`<div align="center">` + `<img>`)

**Solution Implemented:**
1. Created MARKETPLACE.md with all HTML blocks replaced by markdown equivalents:
   - Removed `<div align="center">` containers
   - Converted `<img>` tags to markdown `![alt](url)` syntax
   - Added inline "Screenshots: [See on GitHub]" links where visual elements were removed
   - Made all links absolute (to https://github.com/...) for marketplace rendering
2. Updated umbraco-marketplace.json to point DocumentationUrl at:
   `https://raw.githubusercontent.com/jonnymuir/Umbraco.Prism/main/MARKETPLACE.md`
3. Kept README.md unchanged — developers still see full rich HTML experience

**Result:** 
- ✅ Marketplace now renders MARKETPLACE.md as clean, plain-text markdown with no stray tags
- ✅ All content, structure, and marketing intent preserved
- ✅ GitHub README unchanged; developers still see logo, centered images, and rich formatting
- ✅ No script/automation needed; both files remain in sync via manual edit

**Key Learning:** The Umbraco Marketplace schema does not support separate documentation URLs — it's single-intent. The workaround is to point the single `DocumentationUrl` at a marketplace-optimized variant and maintain the full-featured version separately.

---


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


## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

---

## 2026-05-15 | PASA Death Process Design Scaffold

**Objective:** Create a design document for a PASA (lifecycle termination) death-process workflow example, structured to absorb input from architecture, security, backend, frontend, and testing disciplines.

**Approach:**
1. Analyzed existing design docs (workflow-forms-engine.md, walkthroughs) to infer structure and tone
2. Created scaffold with explicit decision slots rather than speculative implementation details
3. Organized by discipline (Tom Nook, Copper, Blathers, Isabelle, Tangy) with role-specific guidance
4. Linked to Prism patterns (state machines, component mapping, security boundaries)

**Artifacts Created:**
- `docs/design/pasa-death-process.md` — Main design scaffold (13.5 KB)
  - State machine with proposed flow: request-confirmation → waiting-for-approval → executing → completed
  - Component mapping: `fieldset`, `summary-list`, `waiting`, `panel`
  - End-to-end narrative placeholder (Parts 1–4: initiate, approve, execute, complete)
  - Backend contract template: JSON workflow definition + `/advance` response schema
  - Security considerations: threat model, audit trail, tenant isolation
  - Testing strategy: executable specs + unit tests (placeholders)
  - Decision timeline: 4 phases (design → approval → implementation → documentation)
  
- `.squad/decisions/inbox/celeste-pasa-death-process.md` — Decision record documenting rationale and next steps

**Key Design Decisions:**
- **Scaffold over speculation:** Used explicit decision slots and open questions to flag unknowns rather than guess implementation details
- **Discipline-focused:** Organized questions for Tom Nook (architecture), Copper (security), Blathers (backend), Tangy (testing) so each can focus on their domain
- **Pattern reusability:** Structure (decision slots + narrative + backend contract + specs link) can be extracted as a template for future workflows

**Security Considerations Captured:**
- Role-based gates on approval transitions
- Nonce validation for destructive POST operations
- Audit trail requirements (who, when, outcome)
- Tenant isolation boundaries
- Threat model placeholder (unauthorized deletion, replay attacks, concurrent approvals, data exposure during cleanup)

**Validation:**
- Document follows Prism conventions (state machines, components, narrative structure)
- Links to existing design docs and walkthroughs for consistency
- Appendix includes role-specific guidance to guide team input

**Result:** ✅ Design scaffold ready for parallel team input. Awaiting Tom Nook (architecture), Copper (security), Blathers (backend), and Tangy (test strategy) to fill open questions. Target sync: 2026-05-16.

**Learning:** This scaffold approach — explicit decision slots + discipline-specific guidance — is reusable for future complex workflows. Candidate for extraction as `.squad/templates/design-doc-scaffold.md`.

## 2026-05-15: PASA Death Process Design Scaffold

Authored comprehensive design document scaffold at `/docs/design/pasa-death-process.md`. Integrated decision slots for all disciplines (architecture, security, backend, frontend, testing). Structured for parallel input. Scaffold includes open questions, proposed workflow, backend contracts, security considerations, testing strategy, documentation artifacts, decision timeline. Decision merged to shared registry.

