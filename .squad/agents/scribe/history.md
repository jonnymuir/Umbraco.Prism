# Project Context

- **Project:** Umbraco.Prism
- **Created:** 2026-03-22

## Core Context

Agent Scribe initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-03-22
📝 2026-03-22: Copilot instructions file completed and verified. Orchestration logs written. Decisions consolidated. Ready for deployment.

## Learnings

- **Multi-project structure:** Core is a NuGet package; Client is web components (Lit/Storybook); TestSite and MockBackOffice are reference implementations.
- **Testing split:** .NET Core uses XUnit (with Moq, FluentAssertions); Client uses Playwright with Storybook test runner and axe for WCAG compliance.
- **Mobile feature complexity:** MobileBundleService (27KB) generates full Capacitor starter bundles with tenant-specific config; requires Node.js 22.17.1 and npm.
- **Middleware-first architecture:** PrismTenantMiddleware resolves tenant from hostname; PrismBrandingMiddleware injects CSS variable overrides dynamically.
- **Stateless identity:** No local Members; all auth deferred to Entra ID (CIAM) with secrets stored in Azure Key Vault per tenant.
- **Admin policy enforcement:** Tenant management restricted to Umbraco users in configured groups (default: admin); enforced via PrismAdminHandler.
- **CI/CD:** Separate workflows for testing (ci-tests.yml) and packaging (package-release.yml); client build must complete before Core pack.

---

## 2026-04-03T11:42:28Z — Phase 1 Notifications Backend Documentation & Handoff

**Task:** Post-delivery documentation and team history update

### Deliverables

1. ✅ **Orchestration Log:** `.squad/orchestration-log/2026-04-03T11:42:28Z-blathers.md`
   - Complete Phase 1 deliverables summary
   - Build status (0 errors, 0 warnings)
   - 8 key implementation decisions
   - Handoff notes for next phases

2. ✅ **Session Log:** `.squad/log/2026-04-03T11:42:28Z-phase1-notifications-backend.md`
   - Brief summary of Phase 1 completion
   - All deliverables (FirebaseAdmin, 2 migrations, service, controller, handler)
   - Key decisions documented

3. ✅ **Decision Merge:** `.squad/decisions/inbox/blathers-phase1-notifications.md` → `.squad/decisions/decisions.md`
   - 8 decisions merged into main decisions file
   - Inbox file deleted
   - Deduplication: No pre-existing conflicts

4. ✅ **Agent History Updates:**
   - Blathers: Appended Phase 1 completion record with full technical details
   - Scribe: This entry (documentation and handoff coordination)

### Coordination Notes

**Copied to Blathers History:**
- Deliverables (code, migrations, service, API, handler, composer)
- Build status and technical gotchas solved
- 8 key decisions and rationale
- Ready for next phases (testing, UI, rate limiting, analytics)

**Decision Inbox:** Fully cleared. No pending decisions awaiting scribe coordination.

**Team Impact Summary:**
- Blathers: Phase 1 shipped, ready for testing phase
- Isabelle (Frontend): May need components for subscription UI (Phase 2+)
- Tangy (Testing): Unit tests, integration tests ready to start
- Copper (Security): API auth model, FCM credential storage review deferred to implementation phase
- Celeste (Documentation): XML docs and Firebase setup docs deferred to Phase 2+
- Mabel (Release Management): v1.0.0 notation pending (Phase 2+ release cycle)

---

## Documentation Restructure Session (2026-04-03T15:41:14Z)

### Summary

Processed Mabel's documentation restructuring work. Merged 3 pending decisions from inbox → decisions.md. Cleaned up decision tracking.

### Actions Completed

1. ✅ **Decision Inbox Merge:** 3 files processed
   - `mabel-cloudflare-maintenance.md` → decisions.md (Cloudflare-only maintenance handling)
   - `blathers-di-scopefactory.md` → decisions.md (IServiceScopeFactory pattern for BackgroundService)
   - `kicks-android-bootstrap-fix.md` → decisions.md (perl for manifest injection, Gradle 8.14 upgrade)
   - No duplicates found
   - Inbox files deleted

2. ✅ **Logs Written:**
   - Session log: `/squad/log/2026-04-03T15:41:14Z-docs-restructure.md`
   - Orchestration log: `/squad/orchestration-log/2026-04-03T15:41:14Z-mabel.md`

3. ✅ **Agent Histories Updated:**
   - Mabel: Appended README restructure and docs index work with context
   - Scribe: This entry

### Decision Impact

These three decisions span different domains:
- **Blathers (Backend):** DI ScopeFactory pattern applies to BackgroundService usage
- **Kicks (Mobile):** Android bootstrap improvements for cross-platform scripting
- **Jonny/Cloudflare:** Clarifies maintenance responsibility boundary (no app code changes)

All decisions are orthogonal and improve long-term project hygiene.

---

## Post-Agent Documentation Session (2026-04-12T01:29:29Z)

### Summary

Processed background session output from Blathers (Aspire workload permissions investigation). Created orchestration and session logs, merged decision from inbox to main decisions file, and archived old entries.

### Actions Completed

1. ✅ **Orchestration Log:** `2026-04-12T01:29:29Z-blathers.md`
   - Summary of Blathers' Aspire workload investigation
   - Root cause: .NET SDK at `/usr/local/share/dotnet` owned by `root:wheel` requires elevated privileges
   - Implementation: Updated README.md, ASPIRE_DEV.md, scripts/validate-aspire-prereqs.mjs
   - Validation status and impact documented

2. ✅ **Session Log:** `2026-04-12T01:29:29Z-aspire-workload-permissions.md`
   - Brief summary of Aspire workload permissions fix
   - Outcome: elevated command path documented and tested

3. ✅ **Decision Merge:** `blathers-workload-permissions.md` → `decisions.md`
   - Decision: Document elevated Aspire workload install on macOS protected SDK paths
   - Conventions established for preflight validation messaging
   - Standing effect: Developers now receive clear elevated-command guidance
   - Inbox file deleted

4. ✅ **Archive Maintenance:** `decisions-archive.md` created
   - Archived two entries older than 30 days (2025-07-22, 2025-07-15)
   - decisions.md reduced by 2 old entries to improve size management
   - Archive file preserves historical record

5. ✅ **Team Notifications:**
   - Blathers: Existing handoff already in history from background session
   - No cross-agent updates needed; this is dev-experience only

### Decision Impact

The archived decisions remain valid but are now in historical record:
- **2025-07-22:** uui-input accessibility label pattern (continues to apply)
- **2025-07-15:** Test philosophy as behavioral contracts (continues to apply)

These principles remain standing even after archiving.

---
