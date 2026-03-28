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
