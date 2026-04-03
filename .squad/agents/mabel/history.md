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

