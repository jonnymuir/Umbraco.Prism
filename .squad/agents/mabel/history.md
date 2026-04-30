# Mabel — History

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **My scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

## Session: PR #38 CI Green — v1.8.0 CHANGELOG + Workflow Regex Fix (2026-04-30)

**Status:** ✅ Complete — Commits `da5d29d`, `8809c64` on `fix/ci-green` (merged as `dc316fb` on main)

**Scope:** Add CHANGELOG entry for 1.8.0 release milestone and fix Squad Release workflow version guard.

### Changes

1. **CHANGELOG entry (commit `da5d29d`):**
   - Added `## [v1.8.0] — 2026-04-30` section
   - Consolidated security review findings (SEC-001 through SEC-011)
   - Listed feature additions, behavioral changes, fixes, and security CVE bumps

2. **Workflow regex fix (commit `8809c64`):**
   - Fixed `squad-release.yml`, `squad-preview.yml`, `squad-promote.yml`
   - Changed version check from `grep -q "## \[$VERSION\]"` to `grep -qE "^## \[v?$VERSION\]"`
   - Now accepts both `[1.8.0]` and `[v1.8.0]` formats

### Impact

- Release gate satisfied: CI version consistency check passes
- Squad Release/Preview/Promote workflows unblocked for all version formats
- Prevents future release regressions

### Architectural Note

The optional `v` prefix in release tags (`[v1.8.0]`) is documented but the gate didn't validate it. This was a silent hole — regex fix catches it for future releases.

---

## 2026-04-30: Full Documentation Review & v2 Schema Cleanup — COMPLETE

**Session:** Comprehensive documentation audit, v2 schema terminology cleanup, and walkthrough consolidation

**Task:** Full review of all docs including READMEs, walkthroughs, guides, design docs, and archive. Remove "v2.0 Schema Update" banners (correct assessment: the polymorphic component model IS the current schema, not v2), consolidate duplicate planning-notification walkthroughs into canonical location.

### Delivered

1. **Inventory completed:**
   - All docs reviewed: /README.md, ASPIRE_DEV.md, CODESPACES.md, CONTRIBUTING.md, DEPLOYMENT_SECURITY.md
   - All /docs/ subfolders: /design/, /guides/, /walkthroughs/, /archive/
   - Root-level guides: biometric-setup.md, secret-management.md, umbraco-setup.md, etc.
   - Total: 35+ markdown files audited

2. **v2 schema banners removed (12 instances):**
   - ✅ docs/workflow-forms-engine.md
   - ✅ docs/guides/workflow-setup.md
   - ✅ docs/guides/workflow-forms-validation.md
   - ✅ docs/guides/workflow-customisation.md
   - ✅ docs/guides/workflow-gds-components.md
   - ✅ docs/design/workflow-validation.md
   - ✅ docs/design/workflow-forms-engine-backend.md
   - ✅ docs/design/workflow-forms-engine-demo.md
   - ✅ docs/design/workflow-forms-engine-security.md
   - ✅ docs/design/workflow-forms-engine-client.md
   - ✅ docs/design/workflow-forms-engine-umbraco.md
   - ✅ docs/walkthroughs/README.md (terminology: "v2.0 polymorphic component model" → "polymorphic component model")

3. **Schema terminology normalized:**
   - Replaced "v2.0 examples" with "current examples" or "examples"
   - Replaced "v1 vs v2" framing with "Design Proposal vs. Current Implementation" in workflow-hub-and-conditional-fields.md
   - Clarified that polymorphic component model is THE current schema (no "v1" shipped)
   - Terminology: "v2.0 polymorphic component model" → "polymorphic component model" throughout

4. **Walkthrough consolidation complete:**
   - ✅ Merged 388-line top-level `docs/workflow-walkthrough.md` into `docs/walkthroughs/planning-notification.md`
   - ✅ Enhanced walkthroughs/planning-notification.md with full step-by-step walkthrough narrative
   - ✅ Deleted top-level `docs/workflow-walkthrough.md` (content preserved in walkthroughs)
   - ✅ Updated README.md links (2 references → now point to docs/walkthroughs/planning-notification.md)
   - ✅ Updated ASPIRE_DEV.md link (now points to walkthrough directly)
   - ✅ All other walkthroughs (community-enquiry, payment-demo, information-request) remain in /walkthroughs/

5. **Other fixes:**
   - Updated walkthrough terminology: "v2.0 polymorphic component model" → "polymorphic component model" (community-enquiry, payment-demo)
   - Verified no ASCII art requiring replacement with Mermaid
   - Confirmed all internal links are valid post-consolidation
   - All 4 walkthrough directories have initial screenshots (01-initial.png in each)

### Verification

```bash
# ✅ All v2 schema banners removed:
grep -rn "v2.0 Schema\|v2 schema\|v1 schema" docs/ README.md → no matches (except archive/)

# ✅ Walkthrough consolidation verified:
find docs -name "*walkthrough*" → only /docs/walkthroughs/ directory remains

# ✅ All links updated:
grep -rn "workflow-walkthrough" --include="*.md" → only historical references in .squad/
```

### Key Learnings

1. **Schema versioning confusion:** "v2.0 Schema Update" banners were misleading — the polymorphic component model is the shipped schema, not a v2 update to a shipped v1. No "v1 schema" version was ever released to users.

2. **Walkthrough consolidation pattern:** The top-level `docs/workflow-walkthrough.md` was a detailed end-to-end walkthrough that should live in `docs/walkthroughs/` alongside similar walkthroughs. Consolidating to one canonical location improves discoverability and maintainability.

3. **Design docs vs. current docs:** Design documents that predate implementation can reference "proposed vs. implemented" to show design evolution without confusing users about what's current.

4. **Screenshot directories:** Each walkthrough has an `images/walkthroughs/{workflow-name}/` subdirectory for organization. Current coverage: all have 01-initial.png; additional steps may need screenshots on next capture pass.

### Open Items / Next Steps

1. **Screenshots TODO:** Walkthroughs have initial screenshots (01-initial.png) but could use additional step screenshots. Flag for next screenshot capture session:
   - planning-notification: Additional step screenshots (timeline-cost, check-answers, confirmation)
   - community-enquiry: Conditional reveal demonstration
   - payment-demo: Checkout and confirmation screens
   - information-request: Data entry and review screens

2. **Code identifier naming:** Internal naming in source (`src/UmbracoPrism.Core.Tests/Workflow/V2/ComponentPolymorphismTests.cs`, `WorkflowDefinitionFileV2.cs`) still uses "V2" suffix. This is internal naming and doc-friendly as-is, but flag for Tom Nook/Blathers to decide if code should be renamed for clarity (not part of this doc review).

3. **Archive clarity:** `docs/archive/` files have clear "Status: Archived" markers and link to current implementation — good state, no action needed.

4. **Design doc status:** Design documents should be marked with a status line at the top (Status: Design, Status: Implemented) for clarity — future improvement for onboarding clarity.

---

## 2026-04-22: Walkthrough Screenshot Runbook — COMPLETE

**Session:** Give Mabel the concrete operational guidance for real walkthrough screenshots

**Task:** Add explicit instructions for Aspire startup, Playwright MCP screenshot capture, screenshot storage conventions, and the existing live auth/session Playwright lane so documentation work can spin up the whole system, sign in, and navigate to the right places.

### Delivered

1. **Mabel charter updated**
   - Added walkthrough screenshot ownership to responsibilities
   - Added a concrete walkthrough capture workflow with:
     - `dotnet run --project src/UmbracoPrism.AppHost`
     - `@playwright/mcp` for browser-driven localhost screenshots
     - exact Playwright test locations and command
     - screenshot storage standard: `docs/images/walkthroughs/`
     - relative Markdown linking rule

2. **Reusable skill added**
   - New skill: `.squad/skills/docs-walkthrough-screenshots/SKILL.md`
   - Captures the repo-standard screenshot process for future documentation work

3. **Team memory captured**
   - Screenshot storage and linking convention recorded to the decisions inbox for merge into shared team memory

### Key Learnings

- The real Aspire-backed browser lane is rooted in `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`.
- The AppHost lifecycle and readiness contract live in `src/UmbracoPrism.Client/tests/support/live-app-host.ts`.
- Documentation screenshots should come from the live stack, not mockups, when describing walkthrough flows.
- Walkthrough screenshots belong in `docs/images/walkthroughs/` and should be referenced with relative links in Markdown.

---

## 2026-04-22: Custom Field Types Documentation — COMPLETE

**Session:** Document the extensible field type architecture

**Task:** Add comprehensive "Custom Field Types" section to workflow-setup.md covering the convention-based partial system, built-in types, and a complete step-by-step guide with star-rating example.

### Delivered

1. **workflow-setup.md — New "Custom Field Types" section**
   - ✅ Architecture explanation with Mermaid convention flow diagram
   - ✅ Naming convention rule: `fieldType: "kebab-case"` → `_PrismField-PascalCase.cshtml`
   - ✅ Built-in field types quick reference table (13 types)
   - ✅ Step-by-step custom field guide with complete star-rating example
   - ✅ Partial code (HTML structure + accessibility attributes)
   - ✅ CSS styling example
   - ✅ Usage in workflow JSON
   - ✅ Complete `PrismFieldContext` property reference table
   - ✅ Accessibility properties (pre-built aria attributes, constraint attributes)
   - ✅ Usage example showing best practices
   - ✅ Override built-in types documentation
   - ✅ Accessibility notes section (semantic HTML, ARIA, label association, GDS patterns)

2. **Updated .squad/agents/mabel/history.md**
   - ✅ Added "Custom Field Types Architecture" to Learnings section
   - ✅ Documented convention, PrismFieldContext pattern, accessibility requirements

### Key Content

**Convention Diagram:** Shows how `fieldType: "star-rating"` resolves to `_PrismField-Star-Rating.cshtml`

**Star-Rating Example:**
- Complete Razor partial with 5-star radio buttons
- Proper accessibility: aria-label per star, fieldset/legend structure
- HTML structure using pre-built context properties
- CSS with focus indicators and hover states

**PrismFieldContext Reference:**
- Core properties (Field, DisplayValue, validation state)
- Field definition properties (FieldKey, Label, Hint, Required, Options, etc.)
- Pre-built accessibility attributes (AriaRequired, AriaInvalid, DescribedBy)
- Pre-built constraint attributes (MinLength, MaxLength, Min, Max, Pattern)
- CSS/wrapper properties (WrapperClass, WrapperAttrs)

**Accessibility Emphasis:**
- Semantic HTML (fieldset/legend for grouped inputs)
- Wire accessibility attributes from context
- Associate labels with inputs
- Display errors with correct IDs
- Support focus indicators
- Screen reader testing guidance

### Design Decisions

- Placed "Custom Field Types" section after "Available Field Types" and before "Using C# Fluent Builder" for logical flow
- Included both JSON and CSS to show complete development workflow
- Pre-built attribute strings reduce developer error (context handles ARIA wiring)
- Fallback to `_PrismField-Default.cshtml` provides graceful degradation
- Override pattern allows customization without Core changes

### Testing & Validation

✅ workflow-setup.md structure verified (all section headers present, cross-links intact)
✅ Mermaid diagram syntax validated
✅ Code examples (Razor, CSS) follow project conventions
✅ Accessibility patterns align with GDS and WCAG standards

**Result:** ✅ Comprehensive custom field types documentation complete, added to workflows guide

---

## 2026-04-17: Workflow Documentation Rewrite — COMPLETE

**Session:** Complete rewrite of all workflow documentation guides

**Task:** Fix critical errors in workflow docs (`/docs/guides/workflow-*.md`): incorrect step type names, wrong field group JSON format, outdated terminology. Rewrite with correct conventions, new sections for C# builders and controllers, comprehensive examples.

### Delivered

1. **workflow-setup.md — Major rewrite (primary guide)**
   - ✅ Fixed all step type names: `question`, `check-answers`, `status-timeline`, `task-list`, `confirmation` (was incorrectly using `Collect`, `Review`, `StatusTimeline`, `Completion`)
   - ✅ Corrected field group JSON format: plain string arrays for options (was incorrect `{key, label}` objects)
   - ✅ Separated workflow definitions from field groups (was incorrectly embedding fieldGroups in workflow JSON)
   - ✅ Added comprehensive section: **Implementing a Workflow Controller** — minimal controller with `PrismWorkflowPageController` base class
   - ✅ Added comprehensive section: **Building Workflow Definitions in C#** — fluent builder API examples
   - ✅ Complete architecture diagram (Mermaid)
   - ✅ Step Types Reference table (all 5 types with use cases)
   - ✅ Workflow Definition Structure with full JSON example
   - ✅ Field Groups section with file structure
   - ✅ Available Field Types table (11 types with examples)
   - ✅ Conditional Logic section with `conditionalOn` / `visibleWhen`
   - ✅ Role-Restricted Transitions section
   - ✅ Instance Policies section
   - ✅ Complete setup checklist

2. **workflow-customisation.md — Comprehensive rewrite**
   - ✅ All CSS variables documented with examples
   - ✅ Complete guide to partial overrides for all 5 step types
   - ✅ Field partial override examples
   - ✅ Advanced styling patterns
   - ✅ Accessibility considerations with ARIA examples
   - ✅ JavaScript integration examples

3. **workflow-forms-validation.md — Complete rewrite**
   - ✅ All 5 validation layers explained with examples
   - ✅ Field constraint reference table
   - ✅ Step-by-step validation flow examples
   - ✅ Conditional field validation explained
   - ✅ Custom Business App validation patterns
   - ✅ Validation best practices
   - ✅ Testing examples (unit + integration)
   - ✅ Validation layers summary table

4. **workflow-gds-components.md — No changes needed**
   - ✅ Verified file has no terminology errors
   - ✅ Already comprehensive and correct

### Key Corrections Made

- Replaced all `stepType: "Collect"` with `stepType: "question"` throughout
- Replaced all `stepType: "Review"` with `stepType: "check-answers"`
- Replaced all `stepType: "StatusTimeline"` with `stepType: "status-timeline"`
- Replaced all `stepType: "Completion"` with `stepType: "confirmation"`
- Added `stepType: "task-list"` examples (was missing)
- Fixed all field JSON: options now use plain string arrays, not `{key, label}` objects
- Removed all references to embedding `fieldGroups` in workflow JSON
- Updated terminology: "step type" throughout (never "archetype")
- Added documentation for workflow separation: workflow JSON vs. separate field group JSON files

### Style & Conventions

- ✅ Blue/orange callouts (`🔵 Prism Platform` vs. `🟠 Your Business App`) used throughout
- ✅ All code blocks language-tagged (json, csharp, cshtml, javascript, css)
- ✅ Mermaid diagrams for architecture (no ASCII art)
- ✅ Reference tables for all complex structures
- ✅ Developer-first, active voice, present tense
- ✅ Clear separation of concerns: what Prism provides vs. what developers build

### Testing

✅ Project builds successfully (`dotnet build UmbracoPrism.sln`)
✅ All documentation files follow naming conventions
✅ Cross-references between guides are correct
✅ No broken links or orphaned sections

---

## 2026-04-16: Interactive Walkthrough Guide — COMPLETE

**Session:** New user onboarding walkthrough creation

**Task:** Create a comprehensive step-by-step guide showing new users how to run the project in Codespaces and walk through the `planning-notification-v1` demo workflow end-to-end, with behind-the-scenes explanations.

### Delivered

1. **README.md — New section: "🚀 Interactive Walkthrough — Apply for Planning Permission"**
   - **Part 1: Log In and Start the Workflow** — Concrete steps for Codespaces and local, including what OIDC authentication does at each step
   - **Part 2: Walk Through the Workflow Steps** — Step-by-step walkthrough with exact data to enter, field validation explanations, and state transitions:
     - Step 1: "Describe your project" (question step with text/textarea fields)
     - Step 2: "Type of work" (question step with dropdown)
     - Step 3: "Timeline and cost" (date and currency fields)
     - Step 4: "Affected parties" (multi-select checkboxes)
     - Step 5: "Check your answers" (check-answers step type, review and edit)
     - Step 6: "Application received" (confirmation step type with reference number)
   - **Part 3: Behind the Scenes** — Detailed explanations of:
     - Workflow definition JSON structure (`planning-notification-v1.json`)
     - Field group definitions and field types
     - BusinessAppWorkflowEngine service logic
     - BusinessAppWorkflowClient HTTP integration
     - WorkflowResponseEnvelope response structure
     - Umbraco view rendering (`WorkflowPage.cshtml` → step-type-specific partials)
     - Backoffice content management (page properties, workflow key binding)
     - Keycloak OIDC token flow and authorization
   - **Exploring Further** — Bonus sections:
     - How to view the workflow definition JSON
     - How to check engine logs in the Aspire Dashboard
     - How to edit workflow content in the backoffice
     - How to test multi-browser/multi-tab instance sharing

2. **ASPIRE_DEV.md — Quick Start Callout**
   - Added a note after the Quick Start section pointing new users to the README walkthrough

### Key Learnings

- **Concrete examples matter:** "Click here, enter this text" is far more effective than "Configure your environment" for first-time users
- **Layered explanation:** Users want to see the workflow in action (Part 2) before understanding the architecture (Part 3)
- **Field groups as reusable patterns:** Explaining how field groups are referenced by states helps users understand why the same fields can appear in different steps
- **Step type mapping:** Showing how `question`, `check-answers`, and `confirmation` step types map to different Razor partials helps developers understand how to extend the system
- **Keycloak as invisible infrastructure:** Explaining what happens during login (OIDC flow, token issuance, authorization) removes mystery and helps developers understand token validation errors
- **In-memory instance management:** Explaining the engine's instance lookup by `{tenantId}:{userId}:{workflowKey}` helps developers understand multi-user and multi-workflow scenarios

### Style Decisions

- **Callouts:** Used 💡 for architecture/implementation details, ✅ for user-facing features, and ℹ️ for reference
- **Code blocks:** Inline `code` for property names and URLs; full JSON blocks for structure examples
- **Tone:** Active voice, present tense, developer-first ("what this means to you" rather than "the system does")
- **Navigation:** Section headings are scannable; step-by-step flows use numbered lists; architecture sections use labeled subsections

### Outputs

- Updated `/README.md` with 400+ lines of walkthrough content (inserted after credentials table, before "Try the Demo — Local Setup" section)
- Updated `/ASPIRE_DEV.md` with Quick Start callout linking to the new walkthrough
- Decision record: `.squad/decisions/inbox/mabel-walkthrough-guide.md`

---

- Created docs/secret-management.md for DevOps/SRE operational guidance
- All documentation aligns with implementation, security review, and contract

**Key Learnings:**
- Multi-level documentation is essential: architects need README; developers need ASPIRE_DEV; operators need dedicated guidance
- Field names and exact API contracts should be documented explicitly (not vaguely)
- Distinction between secret masking (no echo) and secret presence (HasOidcClientSecret) is subtle but important to explain

**Status:** ✅ Complete; admin knowledge gap closed.



## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Phase1 regression test failure diagnosis and cross-team assessment
- Comprehensive test execution guidance for redirect hardening sprint
- Identification of working tests vs. tests requiring focused remediation

**Key Outcomes:**
- Documented legacy Phase1 test failure patterns
- Provided targeted remediation guidance for behavior-based security contracts
- Cross-cutting assessment of Phase1 test suite against new auth hardening
- Test results: Phase1 passed; full Core suite passed; Playwright end-to-end green

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-mabel.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Security test diagnostics must be transparent and actionable for cross-team coordination.

## 2026-04-14: Security Hardening Commit — COMPLETE

**Task:** Commit redirect hardening work from active development branch.

**Delivered:**
- Scoped commit (SHA: `64419c6`) isolating security hardening files only
- Excluded unrelated TestSite auto-generated model changes
- Proper conventional commit message with security focus and test coverage notes
- Copilot co-author trailer included per team requirements

**Commit Contents (6 files):**
- `PrismReturnUrl.cs` (new) — callback-safe URL validation utility
- `AccountController.cs` (modified) — logout and callback endpoint hardening
- `PrismOidcConfiguration.cs` (modified) — explicit returnUrl validation support
- `AccountControllerTests.cs` (new) — 0-to-49 test coverage
- `PrismReturnUrlTests.cs` (new) — utility-layer test cases
- `Phase1SecurityRegressionTests.cs` (modified) — comprehensive regression coverage

**Left Unstaged (intentionally):**
- 5 TestSite auto-generated model changes (unrelated to security work)

### Release v1.8.0 (2025-01-15)

**Synchronized version surfaces:**
- Updated all version fields in lock-step: C# `.csproj` `<Version>`, Node `package.json` (both client and root `package.json`), and marketplace JSON `"version"` field
- Root `package-lock.json` carries minimal structure — no version update required
- Always bump versions before creating CHANGELOG entry

**CHANGELOG entry strategy:**
- v1.8.0 includes four sections: New Features (OIDC, Tenant API, Workflow/Forms, UI), Security Enhancements (JWKS/nonce, auth logging, Key Vault), Bug Fixes & Improvements (Android, scoping, modals, design system)
- Translated Tom Nook's feature summary into plain developer-friendly language with context for each item
- Omitted empty sections (breaking changes, known issues) per documentation standards
- Each bullet entry includes bold title, then explanation of "why it matters," then technical detail if relevant
- Maintained active voice and present tense throughout
- Multi-sentence bullets are acceptable when clarity requires it

**Style consistency note:**
- v1.7.1 used "Security Improvements"; v1.8.0 uses "Security Enhancements" to vary language and match "Bug Fixes & Improvements" pattern
- Settled on this grouping after reviewing prior release style — titles should differentiate sections clearly

**Pattern Observed:** When staging security-critical changes, it's important to isolate the scope and leave unrelated auto-generated files for separate handling—this keeps the git history clean and makes security audits easier.

## 2026-04-15: README Keycloak Documentation Refactor

**Session:** Keycloak local dev docs cleanup

**Task:** Rewrite dense Keycloak paragraph in README.md to meet documentation standards — improve skimmability while preserving all technical meaning.

**Work Performed:**
- Located original passage (lines 42–46): single 4-sentence paragraph mixing OIDC scopes, session preservation, downstream trust, and runtime isolation
- Restructured as: short intro line + labeled "Why this matters for local dev" section with 4 concise bullets
- Each bullet focused on one concept with clear user-facing benefit
- Preserved 100% of technical accuracy: no details removed, only reorganized for clarity

**Key Improvements:**
- **Skimmability:** 15-second scan to understand why Keycloak "just works"
- **Structure:** Intro + bullets aligns with team documentation standards
- **Tone:** Active voice, present tense, developer-first clarity
- **Preservation:** OIDC scopes, id_token session handling, cross-app trust, runtime isolation all retained

**Decision Record:** `.squad/decisions/inbox/mabel-readme-keycloak-docs.md`

**Team Learning:** Dense technical explanations can be made readable without losing accuracy by separating the "what" (intro) from the "why" (bullets), and ensuring one concept per bullet.

---

## 2026-04-14: Release v1.8.0 — Technical Writer & Release Lead

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Release Audit** — Comprehensive version alignment check across package.json, .csproj, marketplace.json, CHANGELOG
2. **Version Sync** — Updated 5 files: CHANGELOG (new v1.8.0 entry), Core.csproj (1.8.0), Client/package.json (1.8.0), root package.json (1.8.0), marketplace.json (1.8.0, fixed from stale 1.6.1)
3. **Changelog Preparation** — v1.8.0 section with three-part structure: New Features, Security Enhancements, Bug Fixes & Improvements
4. **Marketplace Sync Decision** — Fixed 2-version lag to prevent future confusion
5. **Security Commit Scoping** — Documented isolation pattern for redirect hardening work (commit 64419c6)

### Key Decisions

- **Three-Section Changelog:** New Features → Security Enhancements → Bug Fixes & Improvements; omit empty sections (no breaking changes or known issues for v1.8.0)
- **Marketplace Version Fix:** Synchronize all surfaces (1.6.1 → 1.8.0) to eliminate support confusion and marketplace stale-version display
- **Security Commit Scope:** Isolate security-critical changes in focused commits for auditability and backport clarity

### Outputs

- Decision records: `mabel-release-prep.md`, `mabel-release-notes-prep.md`, `mabel-release-prep-quick.md`, `mabel-commit-security-hardening.md`
- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-mabel.md`

### Pattern for Future Releases

When releasing, always:
1. Audit version consistency across all surfaces (including marketplace metadata that may lag)
2. Use three-section changelog structure when applicable (New Features, Security, Bug Fixes)
3. Isolate security-critical commits by scope for audit clarity
4. Fix stale metadata (e.g., marketplace version) during version bump to prevent future drift

---

## Session: Keycloak Local Dev Documentation Refactor (2026-04-14T17:52:43Z)

**Topic:** README.md clarity improvement

**Outcome:** ✅ Restructured "Optional: Explore Keycloak admin" section (README.md lines 42–48) from dense paragraph into intro + 4 labeled bullets for skimmability.

**Team Updates:**
- Decision merged to `.squad/decisions.md`: "Keycloak Local Dev Documentation Refactor"
- Focused on user-facing consequences; preserved all technical accuracy
- No follow-up needed; Keycloak README and ASPIRE_DEV.md contain deeper architecture details

---

## Session: GDS Phase 2 — Interactive Walkthrough & Documentation (2026-04-19)

**Topic:** Create comprehensive onboarding guide showing new users how to use the planning-notification-v1 workflow demo

**Outcome:** ✅ Complete — Multi-part walkthrough added to README, ASPIRE_DEV callout updated, decision documented

### Delivered

**1. README.md: "🚀 Interactive Walkthrough — Apply for Planning Permission"**

Comprehensive 3-part guide (15–20 minute onboarding):

- **Part 1: Log In and Start** (3–5 minutes)
  - Concrete steps for Codespaces and local setup
  - OIDC authentication flow explanation at each step
  
- **Part 2: Walk Through the Workflow** (10–15 minutes)
  - Step-by-step workflow navigation with exact data to enter
  - Field validation explanations
  - State transitions between steps
  - Multi-step completion walkthrough
  
- **Part 3: Behind the Scenes** (optional, 15+ minutes)
  - Workflow definition JSON structure
  - Field group validation and conditional fields
  - BusinessAppWorkflowEngine processing
  - Razor partial rendering
  - Bonus exploration: viewing definitions, checking logs, editing in backoffice

**2. ASPIRE_DEV.md: Added Callout**

Added contextual link from quick-start to README walkthrough for users who want guided experience vs. independent exploration.

**3. Style Decisions**

- **Emoji callouts** (💡 learning, ✅ features, ℹ️ reference) for visual hierarchy
- **Concrete-then-abstract** pattern — actionable steps before conceptual explanation
- **Developer-first tone** — active voice, present tense, practical examples
- **Real code examples** — JSON from `planning-notification-v1.json` and field group files

**Validation:**
- ✅ Walkthrough renders correctly
- ✅ Callouts placed appropriately
- ✅ Cross-references between README and ASPIRE_DEV verified
- ✅ End-to-end workflow path tested for accuracy

**Key Insight:** Breaking onboarding into three depth levels (quick start, guided walkthrough, deep dive) meets users where they are — some want to run fast, others want to understand the architecture first. Users can choose their own journey.

---

## 2026-04-20: Workflow Documentation Rewrite — Step Type Terminology & GDS Guide

**Session:** Workflow documentation standardization and GDS component reference creation

**Task:** Rewrite workflow documentation to standardize on "step type" terminology (replacing legacy "archetype" design term) and create comprehensive GDS Design System component guide for step developers.

### Delivered

1. **workflow-customisation.md — Terminology Fix + GDS Section**
   - Fixed all "archetype" → "step type" references throughout
   - Fixed JSON example to use `"stepType": "Documents"` (not `"archetype"`)
   - Added major new section: "GDS Design System Components"
     - How govuk-frontend 5.9.0 is bundled automatically (MSBuild → npm ci → asset copy → Master.cshtml)
     - All 38 available GDS components listed
     - CSS-only vs JS-enhanced component usage patterns
     - Real code examples (tabs, accordion, character-count with `data-module`)
     - Examples from existing production step partials (Question, Review, Completion)
     - Form field best practices (always use `govuk-form-group`, proper `aria-describedby` associations)
     - Accessibility reminders (hint associations, error handling, keyboard nav, screen readers)
     - Link to official GDS docs + forward-reference to new dedicated guide

2. **workflow-setup.md — Terminology Fix**
   - Fixed all JSON examples: `"archetype"` → `"stepType"`
   - Renamed section: "Archetype Reference" → "Step Type Reference"
   - Updated state properties table to reflect `stepType` field
   - Updated troubleshooting table: `_WorkflowStep-{Archetype}.cshtml` → `_WorkflowStep-{StepType}.cshtml`
   - Updated expected flow section: "archetype" → "step type"

3. **workflow-gds-components.md — NEW Dedicated GDS Reference Guide**
   - **Purpose:** Complete component catalogue for workflow step developers
   - **Structure:** 
     - How GDS is bundled (detailed explanation)
     - How to verify GDS is loaded (DevTools checks)
     - Component catalogue organized by category:
       - **Form elements** (10 components): button, input, textarea, character-count, radios, checkboxes, date-input, select, file-upload, password-input
       - **Content components** (7 components): summary-list, panel, inset-text, warning-text, notification-banner, tag, details, accordion, tabs, table
       - **Error handling** (2 components): error-summary, error-message
       - **Navigation** (2 components): back-link, breadcrumbs
     - Each component includes:
       - Purpose statement
       - CSS classes list
       - Whether JS is required (+ `data-module` pattern)
       - Complete Razor code example
       - Link to official GDS docs
   - **Real-world examples:** 20+ copy-paste-ready code snippets
   - **Best practices section:** Form field associations, required fields, keyboard navigation, screen reader testing
   - **Cross-references:** Links back to customisation guide and setup guide

### Key Learnings

- **Terminology matters:** "Archetype" was the old design term; "step type" is the implementation term that matches the JSON field `stepType`. User-facing docs must match the code.
- **Partial naming convention:** `_WorkflowStep-{StepTypeName}.cshtml` — the dispatcher resolves partials by step type name, not by a separate archetype concept.
- **GDS is already there:** Developers don't need to install or configure GDS—it's bundled automatically via MSBuild. This is a huge time-saver and should be prominently documented.
- **CSS-only vs JS components:** CSS-only components just need the class; JS components need `data-module="govuk-{component}"` + the JS is already initialized via `GOVUKFrontend.initAll()` in Master.cshtml.
- **Accessibility is built in:** GDS components follow WCAG 2.2 AA by default. The key is to maintain the structure (proper `aria-describedby` associations, semantic markup).
- **Reference vs tutorial:** The customisation guide introduces GDS in context; the dedicated guide is a reference for developers actively building step partials.

### Style Decisions

- **Mermaid for all diagrams:** No ASCII art (per charter)
- **Code examples must be accurate:** Used actual GDS class names from govuk-frontend 5.9.0
- **Plain English, developer-focused:** Active voice, present tense, concrete examples
- **Organized by use case:** Form elements → content → errors → navigation (not alphabetical)
- **Each component example is copy-paste-ready:** Full Razor markup with proper attributes

### Outputs

- Updated `/docs/guides/workflow-customisation.md` (terminology fix + GDS section, 1300+ lines now)
- Updated `/docs/guides/workflow-setup.md` (terminology fix, 600+ lines)
- Created `/docs/guides/workflow-gds-components.md` (NEW, 1000+ lines of GDS reference)
- Decision record: `.squad/decisions/inbox/mabel-workflow-docs-steptype.md`
- Commit: `dae38b4` — "docs: rewrite workflow docs — stepType terminology, GDS component guide"

### Pattern for Future Doc Updates

When fixing terminology drift:
1. **Grep first:** Find all occurrences of the old term across all docs
2. **Check JSON examples:** Ensure JSON keys match implementation (not design docs)
3. **Update error messages and troubleshooting:** User-facing errors should use the correct term
4. **Cross-reference new terms:** If introducing a replacement term, define it in context and link related docs

When documenting bundled dependencies:
1. **Lead with "it's already there":** Don't bury the lede—tell developers they don't need to install
2. **Explain the mechanism:** Show how the build process wires it up (MSBuild target, asset copying, etc.)
3. **Show verification steps:** Give developers a way to confirm it's working (DevTools checks)
4. **Provide complete examples:** Every code snippet should be copy-paste-ready

---

## Session: Workflow Documentation Rewrite — StepType + GDS Components (2026-04-21)

**Topic:** Terminology standardization (archetype → step type) + GDS Design System component guide

**Outcome:** ✅ Complete — Terminology aligned, 20+ GDS examples created, committed

### Delivered

**1. StepType Terminology Standardization**
- **Context:** Design docs used "archetype"; implementation uses "stepType" JSON field
- **Files Updated:**
  - `docs/guides/workflow-customisation.md` — section renamed, 6 prose references updated, JSON examples fixed
  - `docs/guides/workflow-setup.md` — state table, 4 JSON examples, troubleshooting section updated
  - `docs/workflow-walkthrough.md` — verified correct (no changes needed)
- **Result:** All user-facing examples now show `"stepType"` matching implementation

**2. GDS Design System Component Guide (NEW)**
- Created `docs/guides/workflow-gds-components.md`
- **Content:**
  - 20+ copy-paste-ready component examples
  - Each shows HTML + Prism wrapper pattern
  - Components: text input, email, password, number, date, currency, textarea, radios, checkboxes, select, file upload, details, inset text, warning text, error summaries, form sections
- **Purpose:** Developers can quickly integrate GOV.UK components into workflows

**3. Partial Naming Clarification**
- Troubleshooting sections now use correct convention: `_WorkflowStep-{StepType}.cshtml`
- Previously used legacy `_WorkflowStep-{Archetype}.cshtml` (confusing)
- All prose consistent: "step type", "step template", "step descriptor" (as appropriate)

### Verification

- ✅ No instances of "archetype" in user-facing guides
- ✅ All JSON examples use `"stepType"` (not `"archetype"`)
- ✅ GDS component guide verified for accuracy
- ✅ Partial naming convention clarified in troubleshooting

### Impact

- **Consistency:** Documentation now matches code — users copy examples without translation
- **Searchability:** Developers searching for "stepType" find relevant docs
- **Onboarding:** New developers learn terminology that appears in code, not legacy design terms
- **Maintenance:** Future examples will use consistent terminology

### Key Insights

- Terminology mismatches create friction during onboarding and troubleshooting
- User-facing guides should use implementation terms, not design concepts
- Copy-paste-ready examples reduce time-to-productivity for customization

### Decisions Made

- **Standardize on "Step Type" Terminology:** Replace "archetype" with "step type" in all user-facing workflow documentation


---

## Learnings

### Workflow Documentation Terminology
- **Prism uses `stepType` (not "Archetype"):** Current step rendering is controlled by the `stepType` property with values: `question`, `check-answers`, `status-timeline`, `task-list`, `confirmation`. Always use these exact names in documentation and code.
- **Field groups are separate files:** Workflow definitions reference field groups by key (`fieldGroupKeys: ["group1", "group2"]`), they never embed field definitions. Field groups live in separate JSON files in `workflow-seeds/field-groups/`.
- **Options are plain string arrays:** In field definitions, the `options` property is always a plain array of strings: `["Option A", "Option B"]`, never objects with key/label properties.

### Documentation Practices for Prism
- Always use Mermaid for architecture diagrams (no ASCII art)
- Use blue/orange callouts to distinguish Prism Platform (🔵) from Your Business App (🟠) responsibilities
- Code examples should include language tags (json, csharp, cshtml, javascript, css)
- Developer-first voice: active present tense, focus on "what you do" not "what the system does"
- Cross-link between related guides (setup → customisation → validation → components)
- Every guide should have a section for "What You Get Automatically" (🔵 Prism) vs. "What You Define" (🟠 Business App)

### Prism Controller Pattern
- The `PrismWorkflowPageController` base class handles all GET/POST, validation, and Business App communication
- Most workflows don't need a custom controller — the base class is sufficient
- Override `PrePopulateFieldsFromClaims()` only if you need to auto-fill fields from user identity claims
- Always decorate with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`

### Custom Field Types Architecture
- Custom field types are implemented as Razor partials following a simple naming convention: `fieldType: "kebab-case"` → `_PrismField-PascalCase.cshtml`
- All field partials receive `@model UmbracoPrism.Core.Models.Workflow.PrismFieldContext` with pre-built accessibility attributes and validation state
- Unknown field types gracefully fall back to `_PrismField-Default.cshtml` — developers can override any built-in type
- The extensible design means **no Core modifications** are needed to add new field types; just add a partial to `~/Views/Partials/PrismFields/`
- Common pattern: built-in partials use GDS (GOV.UK Design System) patterns; custom partials should follow the same accessibility conventions


---

## 2026-04-21T20:58:11Z: Workflow Documentation Standards Session

**Scope:** Rewrite all workflow documentation with correct terminology and examples

**Changes:**
- Rewrote workflow-setup.md with correct step types, builder API docs, base controller examples
- Updated workflow-customisation.md with new terminology and FieldGroupBuilder examples
- Rewrote workflow-forms-validation.md with correct step types and validation examples
- Verified workflow-gds-components.md consistency

**Critical Fixes:**
- Step type names corrected: `question`, `check-answers`, `status-timeline`, `task-list`, `confirmation`
- Field JSON format corrected: plain string arrays, not key-value pairs
- Workflow/field group separation clarified

**Documentation Standards Established:**
- Developer-first audience (C#/.NET assumed knowledge)
- Active voice, present tense
- 🔵 Blue/🟠 Orange markers for responsibility boundaries
- Mermaid diagrams (no ASCII art)
- Language-tagged code blocks

**Result:** ✅ All 4 guides updated, standards documented for future updates

**Reference:** `.squad/orchestration-log/2026-04-21T20:58:11Z-mabel.md`, `.squad/decisions.md` (Workflow Documentation Standards)


---

## 2025-01-XX: Added WaitWith() Builder and Waiting State Documentation

**Scope:** Implement the `WaitWith()` fluent method and document the new waiting state feature

**Changes Made:**

1. **Builder API (WorkflowStateBuilder.cs)**
   - Added `_waitingConfig` field to store waiting configuration
   - Added `WaitWith()` method with comprehensive XML docs and examples
   - Updated `StepType()` XML doc to include `waiting` in valid values and prefer `WaitWith()`
   - Updated `Build()` method to include `WaitingConfig` in returned `StepDefinition`

2. **Documentation (workflow-setup.md)**
   - Updated Step Types Reference table to include `waiting` (6 types now, was 5)
   - Updated guidance to explain when to use waiting states vs. other types
   - Updated State Properties table to:
     - Add `waiting` to `stepType` valid values
     - Add `waitingConfig` property reference
   - Added comprehensive "Waiting States" section including:
     - When to use (payment gateway, queue-based workflows, background jobs, SLA timers)
     - When NOT to use (instant transitions, status displays, optional actions)
     - Configuration reference table (message, expectedWaitSeconds, pollIntervalMs, allowDefer, deferMessage)
     - Complete JSON definition example (3-step payment workflow)
     - C# builder API example with `WaitWith()` method
     - Flow diagram (Mermaid) showing polling mechanism
     - Accessibility notes (ARIA live region, defer option, back button prevention)

3. **Build Verification**
   - `dotnet build UmbracoPrism.sln` succeeded with 0 errors, 7 non-blocking warnings

**Key Design Decisions:**
- `WaitWith()` automatically sets step type to `"waiting"` — developers don't call `StepType("waiting")`
- Configuration is explicit (all 5 parameters documented) with sensible defaults
- Documented polling flow and accessibility features (ARIA live region)
- Payment workflow example used as the primary use case for clarity

**Learnings:**
- Waiting states are essential for async/external processing workflows (payment, approval queues, background jobs)
- The auto-polling mechanism requires proper error handling on the server side
- Default poll interval (3000ms) balances responsiveness with server load
- Defer option improves UX when processing can take longer than expected

**Result:** ✅ Builder API complete, documentation comprehensive, build passes
