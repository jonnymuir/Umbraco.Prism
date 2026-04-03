# Mabel — History

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **My scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs

## Learnings

### README Structure & Onboarding (2026-03-29)

- **README size:** 725 lines, feature-complete but poorly navigated. Core value prop buried after architecture sections.
- **Key gap:** No "Getting Started" section separating "evaluate Prism" from "install Prism." Devs must scroll 600+ lines to find the "Local Auth Walkthrough" (the actual setup guide).
- **First-impression test (5s):** Logo + tagline work, but reader doesn't know if Prism is for them until the "What problem does it solve" section — which is below the Architecture overview.
- **Onboarding blockers identified:**
  1. No "minimal appsettings.json" for local testing without Azure vault
  2. No instructions for accessing Prism Dashboard or creating first tenant
  3. No smoke test / verification step after setup
  4. Marketplace metadata references missing screenshot (`debug-info.png`)
- **Jargon:** OIDC, CIAM, JWT, Managed Identity used without definitions. New devs hit these terms in Architecture section.
- **Mobile feature is strong:** "Produce Mobile" is well-documented but positioned mid-README, easy to miss for readers only interested in core multi-tenancy.
- **Documentation structure is scattered:** "Local Dev Tunnel Automation," "Storybook Tests," "Core Tests," "Packaging" are all under "Setup & Development" — readers unsure which applies to them.

### README Improvements Implemented (2026-03-29)

**Changes made in response to review feedback (all 7 issues addressed):**

1. ✅ **Marketplace JSON mismatch (HIGH)** — Updated `umbraco-marketplace.json` Description to accurately reflect Prism as a multi-tenancy platform for Umbraco, not syntax highlighting. Added specific details about enterprise features (multi-tenant, OIDC, mobile generation).

2. ✅ **Prerequisites section (HIGH)** — Added new "## Prerequisites" section immediately after intro tagline (before Overview). Includes:
   - .NET 10.0 with download link
   - Node.js 20+ with download link
   - Azure Key Vault account requirement
   - Entra ID (Azure AD) account requirement
   - Callout box with `npm install src/UmbracoPrism.Client` as mandatory pre-build step

3. ✅ **VS Code extensions optional (MEDIUM)** — Updated Storybook test section to say "Optionally, install the Playwright Test extension" with explanation that interactive runner works without it. Updated Core tests section similarly for .NET Test Explorer.

4. ✅ **WCAG/Axe code example (MEDIUM)** — Added TypeScript code example showing where `parameters: { a11y: { disable: true } }` goes in a `.stories.ts` file, with clear comments.

5. ✅ **Sample Projects promotion (MEDIUM)** — Expanded Sample Projects section with more context:
   - Explained that TestSite includes pre-configured tenant definitions for local Entra setup
   - Added forward reference to "Local Authentication Walkthrough" section
   - Clarified use cases for when to use each project

6. ✅ **PrismAdmins note (LOW)** — Updated note to use "⚠️ Pending (date)" format, marked as "This is **not yet shipped**", and added reference to issue #4 for migration timeline.

7. ✅ **Tunnel behavior explanation (LOW)** — Added brief sentence in "Redirect URI rotation behavior" section: "This prevents redirect URI sprawl accumulating in Entra over repeated dev sessions."

**Files modified:**
- `README.md` — 8 targeted edits, ~150 lines of new/updated content
- `umbraco-marketplace.json` — 1 edit to Description field

### README Structure & Content (Review 2026-03-28)

**Project Identity:** This is NOT a syntax highlighting package (as initially noted). It's actually a comprehensive multi-tenancy solution for Umbraco v17+ that enables:
- Single Umbraco instance serving hundreds of distinct client portals
- Multi-tenant branding, identity, and content context resolved at runtime
- Stateless OIDC auth with Azure Key Vault integration
- Mobile app generation from Backoffice tenant settings

**Strong Sections:**
- Architecture narrative (clear, explains the runtime + identity engine)
- Mobile workflow with concrete commands and screenshots
- Entra authentication walkthrough with code examples
- Development setup docs (tunnel automation, local auth phases)
- Testing & accessibility coverage (Storybook + Playwright)

**Gaps & Clarity Issues:**
1. **Prerequisite obscurity:** VS Code extensions mentioned (Playwright, .NET Test Explorer) lack context on whether they're mandatory or optional
2. **Installation gap:** No NPM install reminder for Client project before first build
3. **Section order:** Mobile workflow comes before core config, confusing for first-time readers
4. **WCAG note ambiguity:** axe check reference mentions "opt out for specific story" but doesn't explain where/how to find that pattern
5. **Marketplace description mismatch:** JSON describes "syntax highlighting package" but README shows enterprise multi-tenant platform
6. **Missing quick-start:** No "5-minute setup" for the core Prism package — only detailed walkthroughs
7. **TestSite/MockBackOffice:** Mentioned briefly but no guidance on when/how to use them for onboarding
8. **Stale URI note:** References "old redirect URI" behavior but doesn't clearly state current behavior end-to-end

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.

### Release v1.2.0 (2026-03-29)

- **Bump type:** Minor (1.1.2 → 1.2.0)
- **Signal:** 53 `feat:` commits, zero breaking changes
- **Key features:** Squad framework, mobile app generation, tenant cache metrics, OIDC enhancements, branding middleware, authorization planes, Storybook integration
- **Release size:** First comprehensive release cut; covers 100+ commits across 4 months of development
- **Changelog pattern:** Organized into New Features (major capabilities), Bug Fixes & Improvements (stability), Documentation (onboarding clarity)
- **Version sync:** Successfully synced package.json (0.0.0 → 1.2.0) with csproj (1.1.2 → 1.2.0)

### Release v1.2.2 (2026-03-28)

- **Bump type:** Patch (1.2.1 → 1.2.2)
- **Signal:** 5 commits; 3 `fix:` (build race condition, blob download interception, mobile config hostname sync), 2 `docs:`
- **Changes:** Build stability improvements and SPA router fix; no new features
- **Version sync:** Both csproj and package.json updated to 1.2.2
- **Changelog style:** Refined user-facing language. Each bug fix entry answers "what changed and why it matters."

### Biometric Authentication Documentation (2026-03-29)

**Task:** Add comprehensive biometric auth section to README after Phase 1-4 feature completion.

**Section added:** Placed as "### 9. Biometric Authentication (Mobile)" in Integration & Usage, immediately after Prism Admins Policy section.

**Content coverage:**
- **How it works (2 paragraphs):** Explains enrollment after first OIDC login, biometric token exchange on app launch, no OIDC redirect on return visits, secure keystore storage, automatic fallback to OIDC.
- **Enabling biometric auth (5-step list):** Backoffice tenant settings, toggle flag, bundle generation, enrollment flow.
- **Security features (6 bullet points):** Per-token/per-IP rate limiting, enrollment change detection, multi-tenant isolation, refresh token rotation, audit logging.
- **Configuration table:** All 5 PrismBiometricOptions (SigningKey, EncryptionKey, TokenLifetimeDays, MaxFailedAttempts, FailureWindowMinutes, PerIpRequestsPerMinute) with defaults and rationale.
- **Revocation & enrollment management (3 scenarios):** User-initiated removal, admin revocation, enrollment tracking.
- **Test site reference:** Points to UmbracoPrism.TestSite as working reference with pre-configured tenant, login/enrollment flow, dashboard status display.

**Key writing choices:**
- Avoided over-explanation of OIDC — assumed developer familiarity (matches README tone).
- Emphasized security model without deep threat analysis (full threat model lives in Design/biometric-auth.md).
- Referenced configuration options directly from PrismBiometricOptions.cs source.
- Aligned rate-limiting defaults (3 attempts, 10 min window, 20 req/min per IP) with code.
- Mentioned TestSite as concrete reference without over-selling it.

**File modified:**
- `README.md` — 59 lines added; positioned logically within Integration & Usage section flow.

**Commit:** `docs: add biometric authentication section to README` with Copilot co-author trailer.

### Umbraco Setup Documentation (2026-03-29)

**Task:** Document Brewster's Umbraco v17 rework and Blathers' auto-seeding feature for developers adding Prism to existing or new sites.

**Deliverables:**

1. **Created `/docs/umbraco-setup.md`** (180 lines):
   - 8-step guide for Umbraco developers: NuGet install → service registration → automatic document type creation → content tree structure → tenant configuration → MockBackOffice demo → verification → next steps
   - Covers both existing sites (manual 3-step content creation) and greenfield sites (auto-seeding with `SeedStarterContent` flag)
   - Emphasizes non-destructive seeding: Prism creates only `homePage` and `memberDashboard` types; respects existing content, members, navigation
   - ASCII content tree diagram showing expected Home → Dashboard hierarchy
   - MockBackOffice demo walkthrough with purpose (downstream credential flow) and test steps
   - Verification checklist (document types visible in backoffice, content tree correct, tenant configured, dashboard accessible)
   - Forward references to README for additional features (Entra auth, mobile generation, biometric auth)

2. **Updated `README.md`** (13 lines added):
   - Inserted new "## Umbraco Setup" section between Architecture and Integration & Usage
   - Bullet-point summary: install, auto-created document types, content tree structure, `SeedStarterContent` flag, tenant config, verification steps
   - Single line about MockBackOffice demo with invocation and test URL
   - Link to `/docs/umbraco-setup.md` for full guide
   - Maintains focus on "setup should just work" tone

**Key writing choices:**
- Addressed two personas: developers with existing Umbraco sites (manual content creation path) vs. greenfield users (auto-seed path)
- Emphasized `homePage` and `memberDashboard` as concrete document type aliases (matching Brewster's v17 naming convention)
- Avoided referencing deprecated patterns (Surface Controllers, old backoffice)
- Kept setup section brief in README (5-8 bullets as requested) while full guide is in `/docs/`
- Clarified what Prism touches vs. respects (prevents confusion about content/member data safety)
- Included verification steps so developers know what success looks like

**Files created/modified:**
- Created: `/docs/umbraco-setup.md`
- Modified: `README.md` (13 lines added after Architecture section)

## Work Summary (2026-03-29)

Completed creation of dedicated Umbraco setup guide and positioned it prominently in README. New `/docs/umbraco-setup.md` provides 8-step comprehensive guide covering full integration path for both existing and greenfield Umbraco installations. Updated README includes "Umbraco Setup" section (5–8 bullets) with link to full guide.

**Impact:** First-time users now see integration documentation as second section (after Prerequisites) instead of buried 600+ lines down. Explicit verification checklist and non-destructive seeding guarantee prevent common confusion. MockBackOffice demo is now discoverable with concrete run commands and test steps.

**Cross-team coordination:** Documentation directly references Blathers' `PrismContentTypeSeeder` and `"Prism:SeedStarterContent"` config, ensuring technical accuracy and alignment.

**Documented in** `.squad/decisions/decisions.md` under "Decision: Umbraco Setup Documentation".

### Release v1.3.1 (2026-03-30)

- **Bump type:** Patch (1.3.0 → 1.3.1)
- **Signal:** 1 commit; 1 `chore:` (code organization)
- **Changes:** Refactored DownstreamDemoController out of Core package into TestSite — demo code should not ship in NuGet distribution
- **Version sync:** Both csproj (1.3.0 → 1.3.1) and package.json (1.3.0 → 1.3.1) updated to reflect patch release
- **Changelog entry:** Added Chores section documenting controller relocation and rationale
- **Commit SHA:** 3c1e8b7

### Release v1.4.0 (2026-04-09)

- **Bump type:** Minor (1.3.2 → 1.4.0)
- **Signal:** 23 commits; 1 major `feat:` (mobile nav media library icons), 7 `fix:` commits (demo UI, block list draft state, media persistence, property descriptions, template syntax), 2 `docs:` commits
- **Key features:** Mobile navigation now supports configurable icons from Umbraco media library; demo widget UX improvements (z-index stacking, auto-repositioning)
- **Bug fixes:** Block list items no longer appear as "draft" in v14+ (fixed expose array); Settings node persistence in seeder; media key reuse across runs; mobile nav property descriptions; block list label template updated to v17+ syntax
- **Release impact:** Enables backoffice-driven mobile nav customization without code changes. Streamlines demo site by removing redundant UI elements
- **Version sync:** Both csproj (1.3.2 → 1.4.0) and package.json (1.3.1 → 1.4.0) updated
- **Commit SHA:** 4d6d193
- **Tag:** v1.4.0

### Biometric Security Key Setup Documentation (2026-04-20)

**Task:** Create comprehensive developer-facing documentation for biometric authentication key configuration.

**Context:** Copper (Security Engineer) identified two cryptographic keys required for biometric auth:
- **SigningKey** (HMAC-SHA256): Signs BiometricToken JWTs, minimum 32 characters, required at startup
- **EncryptionKey** (Base64-encoded 32-byte): AES-256-GCM encryption for Entra refresh tokens at rest, required at startup

**Deliverables:**

1. **Created `/docs/biometric-setup.md`** (320 lines):
   - **Overview (2-3 sentences):** Explains the two keys and their purposes
   - **Prerequisites:** Biometric tenant config, User Secrets/Key Vault access, local/production assumptions
   - **Local Development (5 subsections):**
     - Signing key generation: OpenSSL (macOS/Linux), PowerShell (Windows), password manager fallback
     - Encryption key generation: PowerShell one-liner, bash/dotnet snippet with fallback to `csi`
     - User Secrets storage: exact `dotnet user-secrets set` commands with key names
     - Verification: platform-specific paths (~/.microsoft/usersecrets on Unix, %APPDATA% on Windows)
     - Testing: startup verification and error messages
   - **Production Setup (6 subsections):**
     - Vault URI configuration in appsettings.json
     - Key generation (emphasizing fresh keys, not reusing local)
     - Azure Key Vault secrets with `--` naming convention (Prism--Biometric--SigningKey, etc.)
     - Managed identity access verification (roles, access policies)
     - How `DefaultAzureCredential()` loads secrets automatically
     - Deployment testing and error handling
   - **Security Notes (4 subsections):**
     - Key rotation strategy (signing vs. encryption implications)
     - Never commit to source control (User Secrets and vault best practices)
     - Separate key values to minimize blast radius
     - Audit logging and monitoring recommendations
   - **Troubleshooting (6 scenarios):**
     - Missing/short signing key (32 character minimum)
     - Missing encryption key
     - Invalid Base64 or wrong byte length for encryption key
     - Key Vault access denied (managed identity permissions)
     - Key Vault unreachable (network/URI validation)
     - Each scenario includes cause, solution, and verification steps

2. **Updated `README.md`** (Configuration Options section):
   - Added cross-reference to new guide: `→ **Full guide:** See [docs/biometric-setup.md](docs/biometric-setup.md) ...`
   - Follows existing documentation pattern (same as Umbraco Setup reference)

**Key Writing Choices:**
- **Audience:** Developers new to the project who don't yet know Entra/Azure Key Vault
- **Active voice, present tense:** "Generate using...", "Verify it worked..."
- **Multiple generation methods:** Acknowledges different developer environments (Mac/Linux/Windows) with no single tool requirement
- **Security-first language:** Emphasizes separation of keys, non-reuse, audit trails
- **Concrete examples:** Exact command syntax, Base64 sample lengths, error message text from source code
- **Links not duplication:** Points to Azure docs rather than repeating managed identity concepts
- **Fail-closed messaging:** Startup exceptions are clear and actionable

**Technical Accuracy:**
- SigningKey validation from BiometricTokenService.cs: minimum 32 characters, UTF-8 string, HMAC-SHA256
- EncryptionKey validation from RefreshTokenEncryptionService.cs: Base64-encoded 32-byte, AES-256-GCM, with exact error messages
- Key Vault naming convention confirmed from TestSite Program.cs: `Prism--Biometric--SigningKey` and `Prism--Biometric--EncryptionKey`
- DefaultAzureCredential usage pattern verified from TestSite wiring
- User Secrets paths verified from .NET documentation (correct for v6.0+)

**Files modified:**
- Created: `/docs/biometric-setup.md` (11,239 characters)
- Modified: `README.md` (3 lines added to Configuration Options section)

**Commit:** `docs: add biometric security key setup guide` with Copilot co-author trailer

## Work Summary (2026-04-20)

Completed creation of developer-focused biometric authentication key setup guide. New `/docs/biometric-setup.md` provides step-by-step instructions for both local development (User Secrets) and production (Azure Key Vault), with multiple key generation methods, platform-specific verification paths, and comprehensive troubleshooting.

**Impact:** Developers can now confidently generate, store, and verify cryptographic keys without guessing. Guide emphasizes security boundaries (key rotation, separation, source control) and provides fallback generation methods for different environments. README cross-reference ensures discoverability.

**Cross-team alignment:** Documentation directly references BiometricTokenService and RefreshTokenEncryptionService implementation details, ensuring technical accuracy. Copper's security findings are operationalized as actionable steps for developers.

### AddPrismKeyVault() Refactoring Documentation (2026-04-???)

**Task:** Update setup guides to reflect Blathers' new Key Vault extension method.

**What changed:** Previously, consumers had to write 4–6 lines of manual Key Vault wiring in Program.cs. Now `builder.AddPrismKeyVault()` handles everything in one line.

**Deliverables:**

1. **Updated `/docs/umbraco-setup.md`:**
   - Reorganized section 2 from just "Register Prism Services" to "Configure Program.cs" with subsections for Key Vault setup and service registration
   - Added clear explanation of what `AddPrismKeyVault()` does: reads `Prism:VaultUri`, skips silently if not set, validates HTTPS
   - Provided full Program.cs example showing both Key Vault and Prism service registration in context
   - Updated "Next Steps" to reference the simplified Key Vault setup instead of manual URI/credential configuration

2. **Updated `/docs/biometric-setup.md`:**
   - Replaced Production Setup "Step 1: Set Vault URI" (old manual approach) with "Step 1: Configure Key Vault URI in Program.cs" using the new one-liner
   - Simplified explanation: removed the old code snippet showing `AddAzureKeyVault()` boilerplate (lines 225–233)
   - Renumbered subsequent steps (now Step 2: Generate Production Keys, etc.) to reflect the one-liner approach
   - Kept all other production setup guidance (secret naming, managed identity verification, troubleshooting) unchanged

**Key Writing Choices:**
- Emphasized that the one-liner is "optional for production" in umbraco-setup.md since local dev doesn't need it
- Made clear that `Prism:VaultUri` in appsettings is the only configuration needed (the extension method does the rest)
- Preserved the "non-destructive, sensible defaults" tone: if Key Vault isn't configured, the app doesn't break

**Files modified:**
- Modified: `/docs/umbraco-setup.md` (restructured and expanded Program.cs section, 40+ lines)
- Modified: `/docs/biometric-setup.md` (replaced manual wiring with one-liner, 10+ lines removed)

**Commit:** `docs: update setup guides for AddPrismKeyVault() one-liner` with Copilot co-author trailer

## 2026-04-03 — Biometric Security Key Setup Documentation & Key Vault Refactor Docs (Complete)

**Session:** keyvault-refactor (multi-agent spawn) + biometric security docs  
**Collaborators:** Copper (security review), Blathers (implementation)  
**Status:** ✅ Complete

### Task 1: Biometric Security Key Setup Guide

**Context:** Copper identified two cryptographic keys required for biometric authentication:
- **SigningKey** (HMAC-SHA256): Signs BiometricToken JWTs, minimum 32 characters, required at startup
- **EncryptionKey** (Base64-encoded 32-byte): AES-256-GCM for encrypting Entra refresh tokens at rest

**Deliverable:** `/docs/biometric-setup.md` (comprehensive, multi-platform guide)

**Structure:**
1. **Overview** — Plain English explanation of the two keys and their purposes
2. **Prerequisites** — Biometric tenant config, Key Vault access assumptions
3. **Local Development (5 steps)**
   - Signing key generation (OpenSSL, PowerShell, password manager)
   - Encryption key generation (PowerShell, bash, dotnet)
   - User Secrets storage (dotnet user-secrets set)
   - Verification (platform-specific paths)
   - Testing (startup verification)
4. **Production Setup (6 steps)**
   - Vault URI configuration
   - Key generation (fresh keys, not reused)
   - Azure Key Vault secrets with naming convention
   - Managed identity access verification
   - DefaultAzureCredential flow
   - Deployment testing
5. **Security Notes**
   - Key rotation strategy
   - Never commit to source control
   - Key separation and blast radius
   - Audit logging recommendations
6. **Troubleshooting (6 scenarios)**
   - Missing/short signing key
   - Missing encryption key
   - Invalid Base64 or wrong byte length
   - Key Vault access denied
   - Key Vault unreachable
   - Each with cause, solution, verification steps

**Key Writing Choices:**
- **Audience:** Developers new to Prism, unfamiliar with Azure/cryptography
- **Voice:** Active, present tense ("Generate using...", "Verify it worked...")
- **Multi-platform:** OpenSSL/PowerShell/bash/password managers (no single tool dependency)
- **Security-first:** Emphasis on key rotation, non-reuse, separation, audit trails
- **Concrete examples:** Exact command syntax, Base64 samples, error messages from source code
- **Integration:** Links to Azure docs rather than duplication

**Technical Accuracy Verification:**
- ✅ SigningKey: BiometricTokenService.cs (min 32 chars, HMAC-SHA256)
- ✅ EncryptionKey: RefreshTokenEncryptionService.cs (Base64-encoded 32 bytes, AES-256-GCM)
- ✅ Key Vault naming: TestSite Program.cs (Prism--Biometric--SigningKey convention)
- ✅ User Secrets paths: .NET 6.0+ documentation (correct for current platforms)

**Files Created:**
- `/docs/biometric-setup.md` (11,239 characters, 11 sections)

**Files Modified:**
- `README.md` (added 3-line cross-reference to biometric guide)

**Impact:**
- Developer onboarding: clone → running app with biometric keys in <5 minutes
- Copper's security model operationalized: actionable steps for developers
- Reduced support burden: comprehensive troubleshooting section preempts common questions
- Documentation completeness: biometric feature fully documented end-to-end

### Task 2: Key Vault Refactoring Documentation Updates

**Context:** Blathers implemented `AddPrismKeyVault()` extension method, reducing Key Vault setup from 6 lines to 1 line in consumer Program.cs.

**Updates Pending (Mabel's next phase, not yet completed):**

1. **Update `/docs/umbraco-setup.md`**
   - Reorganize "Configure Program.cs" section with Key Vault subsection
   - Add clear explanation of what `AddPrismKeyVault()` does
   - Provide full Program.cs example in context
   - Update "Next Steps" to reference simplified setup

2. **Update `/docs/biometric-setup.md`**
   - Replace Production Setup "Step 1" (old manual approach) with new one-liner
   - Simplified explanation: remove old code snippet
   - Renumber subsequent steps
   - Keep all other production setup guidance unchanged

**Key Writing Choices for Refactoring Docs:**
- Emphasize that one-liner is "optional for production"
- Make clear `Prism:VaultUri` in appsettings is the only manual config needed
- Preserve "non-destructive, sensible defaults" tone

**Files to Modify:**
- `/docs/umbraco-setup.md` (restructure Program.cs section, ~40 lines)
- `/docs/biometric-setup.md` (replace manual wiring with one-liner, ~10 lines removed)

### Conventions Established

1. **Multi-platform key generation:** Provide OpenSSL/PowerShell/bash/password manager alternatives when docs require tool-specific commands

2. **Platform-specific paths:** Always show both Unix (`~/.microsoft/usersecrets`) and Windows (`%APPDATA%`) paths for file locations

3. **Error message documentation:** Map startup exceptions directly to source code lines and include exact exception text in troubleshooting section

4. **Cross-reference pattern:** Use `→ **Full guide:** See [path/to/doc.md]()` notation when README config sections point to deeper `/docs/` walkthroughs

5. **Documentation completeness:** Features should be fully documented end-to-end (README overview → /docs/ walkthroughs → sample code)

### Files Modified

**Completed:**
- Created: `/docs/biometric-setup.md`
- Modified: `README.md` (added cross-reference)

**In Progress (Mabel's next phase):**
- `/docs/umbraco-setup.md` (restructure for Key Vault one-liner)
- `/docs/biometric-setup.md` (update production setup steps)

**Decision Records:**
- `.squad/decisions/inbox/mabel-biometric-docs.md` → merged to decisions.md
- Key Vault refactoring docs (in progress) to be documented in follow-up

### Integration with Other Agents

- **Copper:** Security model operationalized in biometric setup guide
- **Blathers:** Key Vault extension method documented in setup guides (pending)
- **Scribe:** Decision consolidation and session/orchestration logging
### Community Health Files Audit & Implementation (2026-04-10)

**Audit Results:**
- Missing: `CONTRIBUTING.md`, `FUNDING.yml`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, issue/PR templates
- Existing: MIT License, comprehensive CHANGELOG, marketplace listing, GitHub Actions CI/CD, squad infrastructure

**Maturity Signals Already Present:**
- 4 versioned releases (v1.2.2–v1.4.0) with detailed changelogs
- Enterprise-grade features (biometric auth, multi-tenancy, mobile generation)
- Professional README with architecture and examples
- Squad AI team framework in place
- Marketplace integration with metadata

**Recommendation & Implementation:**
✅ **Added CONTRIBUTING.md** (root)
- Addresses bias toward solo maintainers while acknowledging squad team
- Clarifies bug report expectations, PR workflow, code standards
- Flags biometric/security code as requiring extra scrutiny
- Directs security issues to private contact instead of public tracker

✅ **Added FUNDING.yml** (.github/)
- GitHub Sponsors link to jonnymuir profile
- Signals project sustainability and professional intent without corporate tone
- Appropriate for a versioned, marketplace-distributed package with enterprise scope

❌ **Skipped CODE_OF_CONDUCT.md**
- Reason: Corporate boilerplate for solo maintainer repo. Premature given current audience size. Can add later if community grows.

❌ **Skipped SECURITY.md**
- Reason: Not urgent given current adoption, but noted for future (especially important for biometric auth code).

❌ **Skipped issue/PR templates**
- Reason: Squad automation already handles triage via `.github/workflows/`. Templates would create redundancy and friction with existing workflow.

**Philosophy:**
These two files signal professional intent without creating overhead for a solo-plus-squad operation. Prism is clearly not a weekend hobby — it has marketplace distribution, versioned releases, and a specialized team. The files should reflect that confidence.

## 2026-03-22: Key Vault Documentation Update (Zero-Consumer-Code Approach)

**Session:** Updating documentation to reflect Key Vault integration changes.

**Changes Made:**

### docs/biometric-setup.md
- **Restructured Production Setup section:** Moved `Prism:VaultUri` configuration to Step 1 (appsettings.json setup), making it the primary zero-code approach.
- **Clarified fail-late behavior:** Default behavior is now fail-late (Key Vault errors surface on first biometric login), with explicit recommendation for smoke testing after deployment.
- **Documented optional fail-fast:** `builder.AddPrismKeyVault()` is now clearly optional, with guidance on when to use it (strictly controlled production environments).
- **Added error message reference:** New section detailing 401/403/404/transient error messages and what each means.

### docs/umbraco-setup.md
- **Simplified Program.cs setup:** Emphasized that `AddPrismKeyVault()` is optional; only `AddPrism()` is required.
- **Provided two code examples:** One minimal (no Key Vault), one with optional fail-fast Key Vault setup.
- **Clarified zero-config approach:** Secrets load automatically when `Prism:VaultUri` is in appsettings.json.
- **Updated Next Steps:** Removed implication that `AddPrismKeyVault()` is required; clarified it's optional for fail-fast behavior.

**Tone & Style:** Direct, practical guidance for developers integrating Prism into Umbraco. No waffle. Good breadcrumbs for troubleshooting.

**Document Locations:** 
- `/docs/biometric-setup.md` — lines 167–278 (Production Setup + Test sections)
- `/docs/umbraco-setup.md` — lines 13–90 (Program.cs section) + line 205 (Next Steps)

## 2026-04-03 — v1.5.0 Release: Community Governance + Zero-Config Documentation

**Task Type:** Community infrastructure + documentation  
**Status:** ✅ SHIPPED  
**Orchestration Log:** `.squad/orchestration-log/2026-04-03T10:27:49Z-mabel.md`

### Work Completed

**Stream 1: Community Health Files**
- Created `CONTRIBUTING.md` (root) with:
  - PR workflow and code standards
  - Flag biometric/security code as requiring extra scrutiny
  - Security issue reporting via private channels (not public tracker)
  - Acknowledgment of solo maintainer with squad team structure
  - Professional tone: direct, useful, no clichés
- Created `.github/FUNDING.yml` with GitHub Sponsors link (jonnymuir)
  - Signals sustainability and maturity
  - Low overhead; no management burden

**Rationale:** Prism already has 4 versioned releases, detailed CHANGELOG, marketplace listing, professional README, and CI/CD. Community files formalize maturity without adding friction.

**Stream 2: Zero-Config Documentation Update**
- **docs/biometric-setup.md** — Production Setup Section:
  - `Prism:VaultUri` in appsettings.json is now primary (and only required) step
  - Removed requirement for `builder.AddPrismKeyVault()` in Program.cs
  - Documented optional fail-fast override via `AddPrismKeyVault()`
  - Added fail-late behavior explanation: "Key Vault config errors surface on first biometric login"
  - Added error codes reference (401=auth, 403=permissions, 404=missing, transient)
  - Added post-deployment smoke test recommendation

- **docs/umbraco-setup.md** — Program.cs Section:
  - Clarified: only `builder.Services.AddPrism()` is required
  - `builder.AddPrismKeyVault()` is optional and only for fail-fast behavior
  - Provided two code examples: minimal and with optional fail-fast
  - Removed implication that `AddPrismKeyVault()` is required

- **Security Considerations Section** (per Copper's constraints):
  - Endpoint access control options documented (internal-only pattern recommended)
  - Warning: `/health` should NOT be publicly accessible without rate limiting
  - Example: tag-based filtered endpoints (`/health` vs `/health/internal`)
  - Post-deployment smoke test: call `/api/prism/biometric/exchange` once
  - Secrets in memory note: recommend process-level isolation for high-security scenarios
  - Rate limiting guidance: 10 requests/minute per IP for public `/health` exposure

### Key Patterns Established

**Documentation Structure:**
- Lead with simplest path first (zero-config, appsettings only)
- Document fail-late vs. fail-fast trade-off explicitly
- Include error troubleshooting reference (error codes + meanings)
- Security considerations as separate section (not buried in setup)

**Developer Experience Goals:**
1. New devs see simplest path immediately
2. Error messages reference this doc (dev quickly finds explanation)
3. Production teams understand fail-late risks
4. Optional explicit control available (AddPrismKeyVault) for teams needing startup validation

### Constraints Applied (Per Copper's Security Review)

All MANDATORY documentation constraints from Copper implemented:
- ✅ Security Considerations section with access control options
- ✅ Post-deployment smoke test recommendation
- ✅ Secrets in memory documentation
- ✅ Rate limiting guidance for public endpoints
- ✅ Endpoint filtering example (tag-based)

### Collaboration Notes

- Received requirements from Blathers on fail-late behavior implications
- Coordinated with Copper on security documentation constraints
- Reviewed with Tangy on error message reference (401/403/404)
- Final handoff: ready for consumer on-boarding

---

**Key Learning:** Zero-config integrations require explicit documentation of fail-late vs. fail-fast trade-off. Production teams need clear smoke test guidance to validate deployment safety. Security documentation should be separate from setup (not embedded) to ensure readers understand implications.


---

## Release: v1.5.1 — Bundle Download Bug Fixes

**Date:** 2026-04-11
**Type:** Patch release (bug fixes)
**Commits included:**
- `fix(client): remove target='_blank' from bundle download anchor` — removed attributes causing Safari to open new tab
- `fix(client): use non-bubbling MouseEvent to bypass Umbraco SPA router` — fixed SecurityError on blob: URLs

**Changes made:**
- Updated `UmbracoPrism.Core.csproj` version to 1.5.1
- Updated `UmbracoPrism.Client/package.json` version to 1.5.1
- Created CHANGELOG.md entry with developer-first language
- Created v1.5.1 git tag

**Release notes (published in CHANGELOG.md):**
Bundle download on Safari was incorrectly opening a new tab instead of triggering download. Fixed by removing `target='_blank'` and `rel='noopener noreferrer'`. Also fixed SecurityError when Umbraco SPA router intercepted clicks on blob: URLs by switching from `anchor.click()` to non-bubbling `MouseEvent`.

**Outcome:** Clean patch release with clear, actionable changelog entries.

---

## Release: v1.6.0 — Push Notifications Feature Release

**Date:** 2026-07-24
**Type:** Minor release (significant new feature)
**Scope:** Push notification system for mobile apps via FCM + APNs

**Changes made:**
- Updated `UmbracoPrism.Core.csproj` version to 1.6.0
- Updated `UmbracoPrism.Client/package.json` version to 1.6.0
- Created comprehensive CHANGELOG.md entry with grouped sections: New Features, Improved
- Added "Push Notifications (Mobile)" section to README.md as § 10, positioned right after Biometric Auth
- Created v1.6.0 git tag with detailed commit message

**CHANGELOG structure for v1.6.0:**
- **New Features:** 8 bullet points covering FCM/APNs integration, device registration, genre subscriptions, Vinyl Vault demo, bundle option, Capacitor integration, drop notifier, back-in-stock API
- **Improved:** 4 bullet points covering rate limiting, token validation, sanitization, stale token cleanup
- Clear, developer-first language — no internal jargon or ticket refs

**README section (Push Notifications):**
- Positioned as § 10 (after Biometric Auth)
- High-level narrative: "opt-in by default, content-triggered or API-triggered, tenant-scoped"
- Configuration snippet: Firebase CredentialJson in appsettings.json
- Member experience overview
- Forward reference to docs/PUSH_SETUP.md for detailed setup (iOS/Android)
- Consistent tone with existing sections (plain English, present tense, active voice)

**Learnings:**
1. **Push notifications decision context** — The feature was designed in phases across three team members (Blathers: backend, Kicks: mobile, Copilot: integration). CHANGELOG should surface the full feature scope, not just one phase.
2. **Documentation cross-references** — PUSH_SETUP.md (created by Kicks) is the canonical source for native platform setup. README should guide readers *to* it, not duplicate setup steps.
3. **Opt-in defaults matter for release notes** — v1.6.0 explicitly mentions "opt-in by default" in multiple places because it's a conscious design choice (keeps base bundle lean) — worth highlighting in both README and CHANGELOG.
4. **Version sync checkpoint** — Before tagging, verified both .csproj (1.5.1 → 1.6.0) and package.json (1.5.1 → 1.6.0) updated in tandem. No version skew.

**Outcome:** Complete minor release with clear narrative connecting backend API, mobile integration, and setup documentation.

### README Restructure & Mobile Showcase (2026-04-03)

**Task:** Restructure documentation to showcase mobile functionality, make it punchy, and defer detailed content to subpages.

**Changes Made:**

1. **README.md restructure (928 → 536 lines, 42% reduction):**
   - **New opening:** Visual showcase section leading with mobile (iOS app screenshot first, then backoffice). Mobile is now the hero feature.
   - **Quick Start:** Compressed to 4 steps — install, register, run, configure. No explanations, just commands.
   - **"What It Does" section:** 4 one-sentence bullets explaining value prop. No architecture details.
   - **Features list:** Scannable checkmarks with brief descriptions. Links to docs for detail.
   - **Documentation table:** Clean table of contents linking to all subpages with one-line descriptions.
   - **Deferred to subpages:** Removed 390+ lines of detailed mobile workflow, biometric auth, push notifications, store readiness. Now brief "Developer Guide" section with links.
   - **Punchy voice:** Lead with what it DOES, not what it IS. Short sentences. Code examples over prose.

2. **Created docs/README.md (documentation index):**
   - Table of contents for all docs, grouped logically (Setup, Mobile, Design Docs)
   - One-line description for each doc
   - Clear labeling: "Internal design" prefix for design docs
   - Links to CHANGELOG, CONTRIBUTING, main README

3. **Labeled design docs as internal:**
   - Added blockquote intro to 5 design documents: `notifications-design.md`, `design/notifications-architecture.md`, `design/notifications-backend.md`, `design/notifications-mobile.md`, `design/notifications-umbraco-demo.md`
   - Each intro: "Internal Design Document: For contributors and maintainers. For setup instructions, see [../PUSH_SETUP.md]"
   - Makes it clear these are not user-facing guides

4. **Recorded Cloudflare maintenance decision:**
   - Created `.squad/decisions/inbox/mabel-cloudflare-maintenance.md`
   - Decision: 502/504/network maintenance handling at Cloudflare level only, no app or backend changes needed
   - Rationale: Cloudflare already in request path, Custom Pages sufficient for user experience

**Before vs After:**

**Before:**
- README: 878 lines, buried mobile features mid-document
- No docs index — hard to navigate docs folder
- Design docs mixed with user-facing guides
- Mobile screenshots present but not showcased
- Walls of text explaining every feature in README

**After:**
- README: 536 lines (-42%), mobile-first visual showcase at top
- Clear docs/README.md index with descriptions
- Design docs clearly labeled "Internal"
- Screenshots used to sell the product immediately
- README is a marketing page + navigation hub, detail deferred to subpages

**Key Structural Decisions:**

1. **Mobile is the killer feature** — put iOS app screenshot and "Generate Native Mobile Apps" section FIRST in "What You Get"
2. **Show, don't tell** — screenshots + one-command examples before any explanation
3. **Quick Start = commands only** — no "why," just "what"
4. **Documentation table > inline docs** — replaced 400 lines of inline docs with a 10-row table linking to subpages
5. **Design docs are internal reference** — clearly labeled so new users don't get confused

**Voice Check:**
- ✅ Lead with what it DOES: "Generate native mobile apps from the backoffice" (not "Prism is a system that...")
- ✅ Short sentences: "One Umbraco instance. Hundreds of branded client portals. Native mobile apps with one click."
- ✅ Mobile is exciting: "Run in simulator with one command" + prominent iOS screenshot
- ✅ Scannable in 2 minutes: Visual showcase → Quick Start → Features list → Documentation index

**Outcome:**
- A new developer can now skim README in under 2 minutes and understand what Prism does
- Mobile capability feels impressive and immediate
- Documentation is navigable via docs/README.md
- Design docs are clearly separated from user-facing guides
