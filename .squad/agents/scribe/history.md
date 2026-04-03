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
