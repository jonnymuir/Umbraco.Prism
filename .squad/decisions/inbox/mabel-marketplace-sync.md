---
timestamp: 2026-05-23T14:36:30.529+01:00
category: documentation
status: completed
---

# Marketplace Documentation Sync — Core-vs-TestSite Clarity

## Summary
Regenerated MARKETPLACE.md to reflect the Core-vs-TestSite architectural simplification completed during the vinyl/core boundary integration. The marketplace description now clearly distinguishes reusable Prism Core features from TestSite reference implementation examples.

## Problem
The `marketplace-description` CI check was failing because MARKETPLACE.md had become out of date with respect to README.md. The README had been updated with Core-vs-TestSite architectural clarifications (introducing 🔵 Core labels, Notification Infrastructure section, and separation of "Core provides" vs "Your app extends with"), but MARKETPLACE.md was stale.

## Solution
Ran `npm run generate:marketplace` to regenerate MARKETPLACE.md from the updated README.md using the existing `scripts/generate-marketplace-readme.mjs` transformation script.

### What Changed in MARKETPLACE.md
- **New introduction:** Explicitly states "Prism is a NuGet package" and clarifies that "TestSite is a reference implementation showing how to extend Prism for a business domain (vinyl records)"
- **Feature labels:** Added 🔵 Core badges to features provided by Prism Core:
  - Multi-Tenant Web — One Instance, Hundreds of Brands (🔵 Core)
  - Produce Mobile — Generate Apps from Backoffice (🔵 Core)
  - Notification Infrastructure — Extend for Your Business Logic (🔵 Core)
- **New Notification Infrastructure section:** Explains the extensible notification platform pattern:
  - Generic `IPrismNotificationService`
  - Config-driven event handling
  - Subscription persistence and rate limiting
  - Examples of extending with business-specific handlers (vinyl back-in-stock)
- **Refactored Features list:** Separated into "Prism Core provides" and "Your app extends with" for clarity
- **Updated Architecture section:** Added "Prism Core provides" subsection clarifying which components ship with Core

## Verification
✅ `npm run check:marketplace` now passes locally
✅ All changes were generated automatically from README.md (no manual edits to marketplace content)
✅ Marketplace description accurately reflects the Core-vs-TestSite architectural boundary established during vinyl/core integration

## Alignment with Team Decisions
This sync directly implements the documentation implications of `.squad/decisions.md` entries related to:
- **mabel-vinyl-core-boundary.md** — Documenting the architectural split between Core (reusable) and TestSite (reference)
- **mabel-host-guidance-docs.md** — Philosophy that Core is extensible, hosts/apps add business logic

## Publishing Note
When MARKETPLACE.md is merged to main, the marketplace sync endpoint (`https://marketplace.umbraco.com/sync/umbracoprism`) will pick up the new content automatically for NuGet.org display.

---
**Decision Owner:** Mabel (Technical Writer & Release Manager)
