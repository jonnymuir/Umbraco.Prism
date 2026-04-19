# Mabel — History

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **My scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

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
