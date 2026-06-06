# Playwright CI Infrastructure Issue

## Status
PR #91 fixes identified, but CI/CD infrastructure issue remains unresolved.

## Root Causes Addressed
1. **WorkflowTuiService crash** — Fixed with `Console.IsInputRedirected` guard
2. **Workflow seed heading mismatches** — Fixed community-enquiry.json
3. **Incomplete test** — Fixed community-enquiry.walkthrough.spec.ts to fill form fields

## Outstanding Issue
**Aspire HTTP listeners not starting in CI**
- Error: "listener not listening; no HTTP response"
- Occurs during readiness probe (Aspire dashboard, TestSite, MockBusinessApp all fail to respond)
- This happens BEFORE tests even run
- Affects both `localhost-auth-playwright` and `planning-workflow-editor-smoke` jobs

## Hypothesis
This is an **infrastructure/environment issue**, not code:
- Docker might not be properly available
- Keycloak pre-pull might be timing out
- CI runner might be resource-constrained
- Aspire AppHost startup might need more time or debugging

## Recommended Next Steps
1. Check CI runner logs for Docker daemon issues
2. Verify port availability on CI runner
3. Increase Aspire startup timeout if needed
4. Investigate if recent changes to Program.cs or AppHost inadvertently broke startup

## PR Status
- Core tests: ✅ PASS
- Storybook tests: ✅ PASS
- Playwright tests: ❌ BLOCKED on infrastructure

Code changes are sound; issue is environmental.
