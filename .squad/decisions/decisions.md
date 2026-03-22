# Decisions

## Decision: Comprehensive Copilot Instructions Created

**Date:** 2026-03-22  
**Agent:** Docsmith (Documentation Specialist)  
**Status:** Implemented

### What Was Decided

Created `.github/copilot-instructions.md` as a central reference for future Copilot sessions working on Umbraco Prism.

### Why This Matters

Umbraco Prism is a complex multi-tenancy package with:
- **Mixed stack:** .NET 10 Core + Node.js 22 Client (web components)
- **Multiple testing frameworks:** XUnit (C#) + Playwright (TypeScript)
- **Architectural subtlety:** Middleware-driven tenant resolution, stateless OIDC, mobile app generation
- **Team conventions:** Not obvious from a single file (scattered across Middleware/, Services/, Auth/)

Future Copilot sessions will spend less time exploring and more time implementing, reducing rework and ensuring consistency.

### What Was Included

1. **Build/Test/Lint Commands** (284 lines total)
   - All commands that actually exist in CI workflows and local development
   - Prerequisites (Node.js 22.17.1, .NET 10.0.x)
   - How to run single tests (XUnit filter syntax, Playwright UI mode)

2. **High-Level Architecture**
   - 7 interconnected layers (Runtime, Identity, Persistence, Services, Authorization, Backoffice, Sample Projects)
   - Cross-references to physical file locations (Services/, Middleware/, Persistence/)
   - Diagram-free but explicit: describes responsibilities and integration points

3. **Key Conventions**
   - Code organization (why each folder exists)
   - Naming rules (IPrismXxx, XxxService, PrismXxxMiddleware)
   - Database/migration patterns
   - Mobile feature conventions (Produce Mobile, safe-area support)
   - Admin policy reasoning
   - Secrets management (Key Vault per tenant)

4. **Common Tasks & Reference Tables**
   - How to add a new service
   - How to run tests locally
   - Debugging mobile bundles
   - Local Entra sign-in walkthrough
   - Accessibility requirements
   - Dependencies table (versions, notes)
   - File/directory reference table

### Integration with Existing Project

- No .github/copilot-instructions.md existed; created from scratch
- Drew from: README.md (architecture, features), package.json (build script names), .csproj files (test frameworks, versions), CI workflows (actual command syntax)
- Verified against: src/ structure, Services/, Middleware/, Persistence/ organization

### Follow-Up

- Scribe history updated with high-level learnings
- File is self-contained; no dependencies on external docs
- Can be incrementally improved as project evolves (add sections as new patterns emerge)

### Rationale

This document is **not** generic Copilot advice (no "make atomic commits" or "use descriptive variable names"). It is purely project-specific, answering: "What is the shape of this codebase, and how does work get done here?"
