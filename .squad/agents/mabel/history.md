# mabel History (Summary)

## Latest Updates

See history-archive.md for full history.

**2026-05-17 | Workflow Editor V1 Doc Polish for Clarity & Terminology**
- Reviewed all five design docs (README, 01-authoring-ux, 02-runtime-projection, 03-umbraco-integration, 04-agentic-surfaces) for consistency and clarity per user directive: "Keep it simple — workflow editor, workflow engine, forms engine."
- Fixed critical terminology errors:
  - 01-authoring-ux.md: "nodes = states" → "nodes = authored stages" with runtime clarification; "state's properties" → "stage's properties" throughout
  - 02-runtime-projection.md: Added opening "Key distinction" callout to reinforce Authored Model (stages) ≠ Runtime (states)
  - README.md: Rewrote TL;DR to explicitly name the three operational products (workflow editor, workflow engine, forms engine) aligned with user's mental model; added runtime counterpart column to repo mapping table
  - 03-umbraco-integration.md: Added clarifying note about Forms Engine vs Workflow Engine responsibilities across surfaces
- All changes preserve substantive architecture; only clarity and terminology consistency improved.
- No contradictions found requiring resolution; docs are architecturally sound.

**2026-05-17 | Design & Execution Artifact Structure Recommendation**
- Analyzed portfolio structure: docs, issues, decisions.md bridging pattern.
- Recommended complementary use of `/docs/design/` (narrative spine) + GitHub issues (execution units) + decisions.md (bridge).
- Key finding: Current Workflow Editor V1 spine is exemplary; scale horizontally without new structure.
- Lightweight maintenance: issue body copies doc snippet (5 min), decisions.md bumped at PR merge (2–3 min).
- Deliverable: `.squad/decisions/inbox/mabel-design-artifact-structure.md` — full recommendation with cross-linking template and issue granularity guidance.
- No file changes; recommendations only. Awaiting squad approval before extraction as team skill.

**2026-05-08 | v1.9.1 Release Completion**
- Published release v1.9.1 with marketplace-generated NuGet package readme
- Tagged commit 8b78831 (chore: release v1.9.1 with marketplace packaging)
- UmbracoPrism.1.9.1.nupkg successfully published to NuGet.org
- MARKETPLACE.md configured as PackageReadmeFile in UmbracoPrism.Core.csproj
- marketplace sync endpoint triggered at https://marketplace.umbraco.com/sync/umbracoprism
- GitHub Release v1.9.1 published with NuGet package asset
- Notes: Tag required re-push due to GitHub Actions timing issue with tag propagation

**Recommendations (No Changes Yet):**
- Crop homepage screenshot to ~2200–2400px (tooling)
- Create workflow-administration.md walkthrough (6+ admin panel screenshots)
- Capture missing backoffice screenshots (5 total across 5 walkthroughs)
- Verify/hide mobile helper if visible in walkthrough sessions (tooling)
- Document shared screenshot dependencies in skills

**Full audit report:** `.squad/agents/mabel/audit-walkthrough-docs-2026-05-04.md`

**Work Product:**
- Comprehensive audit identifying 6 issues (1 high, 3 medium, 2 low priority)
- Clear decision matrix: what's tooling vs. documentation vs. product
- Verification checklist for implementation
- No production files modified; recommendations only


## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.


**2026-05-08T05:26:48.026Z — Squad Sync:** v1.9.1 release recorded in decisions.md.
### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch — Terminology polish and docs/issues coordination

- Polished terminology and cross-links across all five workflow-editor-v1 design documents for consistency (stage vs. state, three operational products named explicitly, engine roles clarified).
- Produced two decisions merged to `.squad/decisions.md`:
  1. **mabel-workflow-editor-doc-polish.md** — Terminology corrections (authored stages ≠ runtime states; explicit naming: workflow editor, workflow engine, forms engine).
  2. **mabel-design-artifact-structure.md** — Defined docs/issues/decisions coordination pattern: docs are source of truth (non-time-bound narrative), issues are execution units (2–5 day tasks), decisions.md bridges them. Lightweight cross-linking rules (5 min per issue, 2 min per doc update) and hygiene rules documented.
- All five design docs now internally coherent with consistent terminology. Structure pattern documented for future teams (see `.squad/skills/docs-issues-structure/SKILL.md` recommendation).
