# Isabelle — History Archive (Pre-2026-04-22)

This file contains archived history entries from before 2026-04-22. Recent entries (2026-04-22 onward) are in the main `history.md`.

## Phase 1 UI Security Hardening (2026-03-28)

**Context:** Security audit identified dangerous debug/demo surfaces exposed inappropriately.

**Implementation:**
1. PrismDebugTagHelper — environment gating (IWebHostEnvironment check)
2. DownstreamDemoController — endpoint gating + URL allowlist
3. View documentation added to HomePage.cshtml and MemberDashboard.cshtml
4. Comprehensive test coverage: 5 new security tests added

**Key Learnings:**
- Environment-based gating: `environment.IsDevelopment() || config.GetValue<bool>("Prism:EnableFeature", false)`
- URL allowlist pattern for token-forwarding endpoints (always validate)
- TagHelper suppression (`output.SuppressOutput()`) is cleaner than conditional rendering
- Test-driven security for regression prevention

**Files Modified:**
- src/UmbracoPrism.Core/TagHelpers/PrismDebugTagHelper.cs
- src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs
- src/UmbracoPrism.TestSite/Views/HomePage.cshtml
- src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml
- src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs

**Configuration Options:**
- Prism:EnableDebugPanel (bool)
- Prism:EnableDownstreamDemo (bool)
- Prism:DownstreamDemo:AllowedUrls (string[])

## Session: GDS View Layer Phase 1 Completion (2026-04-20)

**Status:** ✅ Complete — All views GDS-compliant, 416 tests passing

**Delivered:**
- npm & Build Infrastructure: govuk-frontend 5.9.0 + MSBuild target
- Master layout updates with GDS template classes
- Workflow step partials (_WorkflowStep-Question, -Review, -Completion, -StatusTimeline, -TaskList)
- TagHelper refactoring (PrismFieldTagHelper → govuk-* classes, PrismErrorSummaryTagHelper)
- View dispatch updates
- Backward compatibility with Prism CSS

## Earlier Sessions (Pre-2026-04-20)

- Generic OIDC secret refactor (UI alignment)
- Tenant modal secret editing (replace-only surface)
- GDS notification banner + details component accessibility patterns
- Mobile nav schema + component boundary considerations
- MockBusinessApp admin workflow page C# string interpolation patterns

---

**Archive Note:** These entries document important learning and pattern decisions. Refer to git history and main decisions.md for specific commit SHAs and implementation details.
