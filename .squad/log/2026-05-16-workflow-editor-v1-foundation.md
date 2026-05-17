# Session Log: Workflow Editor V1 Foundation Wave

**Date:** 2026-05-16  
**Requester:** Jonny Muir  
**Goal:** Start V1 implementation following the design (three-plane architecture, deterministic projection, proposal-first agent loop)

## Wave Dispatch

Four agents dispatched in parallel, each owning one plane/contract:

- **brewster-1** (Umbraco integration) — P1 issue clearance
- **blathers-1** (Authored model + projection) — core serialization and deterministic projection contract
- **isabelle-1** (Authoring UX) — four Web Components and Storybook stories
- **tangy-1** (Test seam) — C# fixture tests, Playwright agent-loop and keyboard specs

## Outcomes

✅ **brewster-1:** P1 cleared (TestSite stub views untracked gitignored files deleted from disk, no commit)

✅ **blathers-1:** Committed `24374f2` (authored model + deterministic projection slice) + `0d74ab8` (history update)
- AuthoredWorkflow record graph (AuthoredStage, AuthoredField, AuthoredTransition, AuthoredHandoff, WaitingMetadata, StageKind, FieldType)
- IWorkflowProjector + WorkflowProjector: five-stage pipeline (validate, normalise, emit, checksum) producing WorkflowDefinitionFile
- Canonical JSON options (camelCase, no indent, DefaultIgnoreCondition.Never, UnsafeRelaxedJsonEscaping) lock determinism; SHA-256 checksum on every projection
- planning.workflow.json fixture (4 stages: declaration → application-form → check-answers → submitted) as source of truth
- 19 passing serialization, determinism, and shell-inference tests

✅ **isabelle-1:** Committed `6e97d81`
- Four Lit 3 Web Components: prism-workflow-graph (dual-mode canvas), prism-step-inspector (read-only detail), prism-conversation-pane (role=log), prism-proposal-diff (focus-trapped modal)
- Storybook 8 stories with play functions and axe a11y config per design §4.6
- TypeScript interfaces: AuthoredWorkflow, ProposalEnvelope, stub data
- All components pass tsc and vite build cleanly

✅ **tangy-1:** Committed `916045e`
- PlanningWorkflowFixtureTests (4 skipped, awaiting Blathers' fixture — now satisfied)
- planning-workflow-agent-loop.spec.ts (2 skipped pending component stories, 2 fixme for wave 2)
- workflow-graph-keyboard.spec.ts (2 skipped pending dual-mode story, 3 passed asserting WCAG 2.1.1 + 4.1.2)
- All suites green: dotnet test 690 passed + 4 skipped; playwright 4 passed + 10 skipped

## Build and Test State

- **Build:** ✅ All four agents' commits merge cleanly; tsc and vite build pass
- **Tests:** ✅ dotnet test suite: 690 passed, 4 skipped; playwright suite: 4 passed, 10 skipped
- **Design:** ✅ V1 design docs now canonical in `docs/design/workflow-editor-v1/`

## Decisions Recorded

Blathers' decision on V1 foundation (namespace, fixture format, JSON canonicalization, check-answers population) merged from inbox into decisions.md under 2026-05-16 section.

## Next Steps

**Wave 2 (patch service, agent MCP tools, backoffice hosting):**
1. Implement in-memory patch service (apply authored mutations)
2. Implement preview service (project with mutations)
3. Implement agent MCP tools (diff, validate, test)
4. Scaffold backoffice v17 extension (link from Business App)
5. Host agent loop on MockBusinessApp (receive NL requests, emit proposals)
6. Unskip Playwright tests as components move to live routing

**Wave 3 (collaborative editing, multi-tenant, advanced authoring):**
- Versioning, lifecycle, rollback
- In-flight instance migration
- Multi-tenant authoring
- Real-time collaborative editing
- Operator backstage UI contract
- Permission expressiveness
- Cross-workflow refactors

## Working Tree State

- ✅ Four orchestration-log entries created
- ✅ Session log appended
- ✅ Decisions merged from inbox
- ✅ Design docs committed in wave 1 commit (0697e92)
- ✅ Ready for final .squad/ commit
