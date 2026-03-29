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

