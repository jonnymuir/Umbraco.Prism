## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## 2026-05-16T13:20:33 | Workflow Editor V1 — Umbraco Integration

**Editor hosting:** Hybrid model — v17 backoffice section (`prism-workflow-editor`) wrapping Lit/Web Component
**Surface mapping:** (1) Public: unauthenticated content shells, (2) Member: `PrismMemberCookie`-protected pages, (3) Back-stage: MockBusinessApp reviewer surface
**DocType strategy:** `workflowPage` and `workflowHub` stable Core-owned; V1 adds `workflowLanding`
**Auth boundary:** Umbraco backoffice gates editor, PrismMemberCookie gates member, MockBusinessApp role gates reviewer
**Key file:** `docs/design/workflow-editor-v1/03-umbraco-integration.md`

## 2026-05-16T23:17:22 | V1 Workflow Editor Backoffice Section Scaffold

**Files shipped:**
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/umbraco-package.json` — v17 package manifest
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js` — Lit element
- `src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs` — 4 file-shape assertions

**Manifest:** 5 extensions (section, sectionSidebarApp, menu, menuItem, dashboard)
**Dev base-URL:** Reads `window.PrismWorkflowEditorConfig?.authoringBaseUrl` → 4-second fetch probe for reachability
**No build step:** Umbraco v17 resolves `@umbraco-cms/backoffice/*` at runtime; plain .js ESM files load directly

---

**📚 Older sessions (pre-2026-05-10) archived to `history-archive.md` to keep active history under 15KB.**
