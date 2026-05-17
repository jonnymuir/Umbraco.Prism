# Brewster — Archived History

## Pre-2026-05-10 Sessions

### 2026-05-02: Downstream Demo HTML Validation Fix
- Fixed false-positive where DownstreamDemoController treated HTML responses as success
- Added Content-Type validation for JSON only
- Tangy found Codespaces port-forwarding pages broke dashboard UI
- Commit: da7ddc9

### 2026-05-03: Startup Health-Check Issues (3 fixes)
1. **Pre-Forward Critical Ports** — Added forwardPorts array to devcontainer.json (3000, 15135, 44345, 7245, 8443)
2. **Status Server Recovery** — Fixed Node process not surviving Codespace suspension
3. **URL Regression** — Changed `tr -d '/'` to `sed 's|/*$||'` to preserve https://

### 2026-05-03: Codespaces Recovery Scripts
- `scripts/codespaces/stop.sh` — Graceful AppHost shutdown
- `scripts/codespaces/refresh.sh` — Fast recovery cycle (~90s)
- `scripts/codespaces/health-check.sh` — Readiness probes
- Updated CODESPACES.md with full recovery section

### 2026-05-04: Walkthrough Discoverability Implementation
- Removed TestSite stub views
- Restructured member dashboard
- Exposed workflow admin URL from MemberDashboardController
- Decision: "Walkthrough Discoverability — All Workflow Types Reachable from Dashboard"

### 2026-05-16: P1 Prereq — TestSite Workflow Stub Views Removal
- workflowPage.cshtml and workflowHub.cshtml already gitignored
- Physical deletion was correct action (no git rm needed)
- TestSiteViewModelBindingTests: 4/4 passed
- Core suite: 690/690 passed

### 2026-05-16: Workflow Editor V1 — Umbraco Integration Design
- Hybrid hosting approach (v17 backoffice section + Lit Web Component)
- Public/Member/Back-stage surfaces mapped
- workflowPage and workflowHub stable Core-owned types
- Auth boundary: Umbraco backoffice gates editor, PrismMemberCookie gates member
- Doc: `docs/design/workflow-editor-v1/03-umbraco-integration.md`

### 2026-05-16: V1 Workflow Editor Backoffice Section Scaffold
- Files: umbraco-package.json, web-components/prism-workflow-editor-host.js, README.md
- Manifest shape: 5 extensions (section, sectionSidebarApp, menu, menuItem, dashboard)
- Lit element reads PrismWorkflowEditorConfig.authoringBaseUrl
- 4-second fetch probe for reachability
- Tests: WorkflowEditorManifestTests (4 assertions)
