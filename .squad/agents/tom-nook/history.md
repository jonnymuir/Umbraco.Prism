# Tom Nook — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Isabelle: Web Components, Storybook, Playwright UI tests
- Blathers: C# backend, services architecture, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

- Defined five concrete answers: tenant fields, raw-secret boundaries, management API contract, demo resolution, documentation obligations
- Established secure-by-default properties (vault-backed for production, repo-owned demo marker)

**Key Learnings:**
- Reference-based secrets is the unifying pattern for multi-provider auth systems
- Inline secrets only acceptable for transient dev-only flows with clear repo ownership
- API contracts should reflect the security model: no secret echo, only metadata and provider state
- Fresh-clone experience and production security are not in conflict when the demo is explicitly tagged

**Status:** ✅ Complete; handed off to implementation team.

---

## Session: 2026-04-14 — Release v1.8.0 Semver Recommendation

**Role:** Lead/Release decision authority.

**Scope Reviewed:**
- 19 new feature commits (OIDC, Keycloak, mobile models, workflow refinements, Backoffice pickers)
- 6 security hardening commits (redirect validation, OIDC secret handling, auth flows)
- Multiple internal refactors (workflow architecture, test coverage)

**Semver Decision:** MINOR bump → v1.8.0
- New public features (OIDC provider fields, models, endpoints) justify minor
- All new fields optional; defaults graceful
- Security hardening is non-breaking (stricter validation on malicious inputs only)
- No breaking changes in public contracts

**Key Principle Applied:**
- New backward-compatible functionality = MINOR (semver.org)
- Stricter validation improves security without breaking legitimate contracts
- No user confirmation required; all changes forward-compatible

**Status:** ✅ Recommendation documented; awaiting bump execution.



## 2026-04-14: Release v1.8.0 — Semver Analysis & Lead Sign-Off

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Commit Analysis** — Reviewed 92 commits since v1.7.1: ~20 feature, ~14 fix (including security), ~7 refactor, ~51 chore/docs
2. **Semver Recommendation** — MINOR bump justified: workflow forms engine (substantial feature), generic OIDC, mobile models, bearer token forwarding, media picker; no breaking changes
3. **Justification Documentation** — Provided detailed rationale: Why MINOR (not PATCH), Why not MAJOR, change composition breakdown
4. **Release Sign-Off** — High-confidence recommendation: Ready for tag creation and deployment

### Key Decisions

- **MINOR Bump (v1.7.1 → v1.8.0):** Workflow forms engine + multiple new features (OIDC, mobile models) exceed patch scope; no breaking changes support MINOR classification
- **Not PATCH:** Multiple user-facing features; patch reserved for bug fixes/tweaks
- **Not MAJOR:** No contract-breaking changes; all public APIs stable; new fields optional

### Outputs

- Decision records: `tom-nook-semver.md`, `tom-nook-semver-quick.md`
- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-tom-nook.md`

### Pattern for Future Semver Assessment

When assessing version bumps:
1. Count commits by type (feat/fix/refactor/chore) to gauge scope
2. Verify no breaking changes to public API contracts
3. Confirm all new features are backward-compatible
4. Document rationale for MAJOR/MINOR/PATCH choice per semver.org
5. High confidence in recommendation ensures smooth release

## Learnings

- 2026-07-08 — stepType removal architecture call: `StepType` should be removed from authored JSON (`StepDefinition`) and replaced by an engine-derived `shell` property on `StepContent`. The inference is deterministic: `waiting` component → waiting shell; all `summary-list` → check-answers; `panel` + no fieldsets → confirmation; `task-list` → task-list; otherwise → question. `WaitingConfig` becomes a first-class `waiting` component. `FieldFile` properties are unchanged; `fieldKey` remains the critical common property for all input-collecting components. Validation, GDS defaults, polling, transitions, and actions are all unaffected by this change.

- 2026-05-07 — Unified component model review: `StepType` on `StepDefinition` is a parallel classification system that should be removed. The component tree is self-describing. `WaitingConfig` belongs as a `waiting`-typed component in the tree, not as a sidecar. Shell rendering (form vs read-only vs polling) should be derived from component composition + `AvailableActions`, not from a step-type discriminator. This unblocks adding future component types without needing a new `StepType` each time.
- 2026-04-14 — Prism/Umbraco 17 fit review: the strongest pattern is **Umbraco owns authored routes and page shell; Prism owns tenant/auth/session plumbing; the Business App remains the workflow source of truth**. Keep `src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs`, `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs`, and `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs` aligned to that boundary.
- The current workflow UI stack is strongest where server components stay thin: `PrismWorkflowFormTagHelper`, `PrismFieldTagHelper`, and the archetype partials are a good Prism fit because they render contracts from the Business App rather than inventing workflow rules locally.
- Main architectural debt to watch: `TestSiteSeedContract`/seeded route fallbacks and content-tree scanning are useful demo stabilisers, but they can quietly couple dashboards and hubs to seeded paths instead of authored content structure if they leak beyond TestSite/demo concerns.
- User preference observed: review-only architecture work should synthesise strengths, design debt, coupling risks, and highest-value follow-up actions in human terms rather than drifting into implementation.
- 2026-04-22 — "Waiting" step type design: nested `WaitingConfig` object in `StepDefinition` is the cleanest pattern for optional step-type-specific configuration. **Superseded 2026-05-07 — `waiting` should be a component type, not a step type with a sidecar config object.** Lightweight JSON polling endpoint (`GET /api/prism/workflow/poll`) preferred over full page reload or SSE/WebSocket complexity remains valid.

## 2026-04-14T20:24:57Z: Architecture Review Session Complete

**Session:** Umbraco 17 and Prism-fit review (parallel with Brewster)

**Work Performed:**
1. Reviewed route-hijacked page pattern against Umbraco 17 idioms
2. Validated Business App boundary and workflow state externalization
3. Identified architectural strengths and coupling risks
4. Ranked follow-up actions by impact
5. Synthesized decision for team consensus

**Key Decision Made:**
- Approved current route-hijacked pattern as canonical for member dashboard, workflow pages, and hubs
- Confirmed good fit for Prism provided boundary stays explicit
- Documented architectural debt: demo coupling, tree scanning, unfinished surfaces

**Outcome:**
- ✅ Decision merged to `.squad/decisions.md`
- ✅ Session recorded in `.squad/log/2026-04-14T20:24:57Z-umbraco-review.md`
- ✅ Orchestration complete: `.squad/orchestration-log/2026-04-14T20:24:57Z-tom-nook.md`

**Status:** Review-only session complete; awaiting team consensus and planning for follow-up sprints

## Learnings

- 2026-04-23 — Workflow schema cleanup design review: confirmed three issues Jonny raised. (1) `stepType: null` is pure System.Text.Json default-serialization noise — no `WhenWritingNull` is configured on any of the four workflow `JsonSerializerOptions` call sites (`BusinessAppWorkflowEngine`, `BusinessAppWorkflowClient`, `MockBusinessApp/Program.cs` ×2). (2) `PrismComponentDefinition` is a 16-slot anaemic-union god-record where most properties are `null` for any given `type` — pure C#-shape smell that bleeds into JSON without `WhenWritingNull`. (3) State-level `WaitingConfig` is dead weight: `EffectiveWaitingConfig` already prefers the `waiting` component; no current seed authors the sidecar. (4) Fields-vs-components asymmetry is real — `_PrismField-{Type}.cshtml` and `_PrismComponent-{Type}.cshtml` are two parallel dispatch systems for the same idea, and `inset-text`/`body`/`heading` already exist in both vocabularies (canary smell).

- 2026-04-23 — Recommended path: **Option 1 (minimal cleanup) now, Option 2 (polymorphic split) deferred to v2.0 schema**. Charter value applied: *defer perfection*. Option 1 fixes the wire format Jonny actually sees in one day (delete two record properties, add `WhenWritingNull` globally) without schema-breaking changes. Option 2 (`[JsonPolymorphic]` hierarchy where fields *are* components — `TextInputComponent`, `DecimalInputComponent`, etc., with `fieldset.children` instead of `fields[]`) is the right destination because the view layer is already polymorphic via `_PrismComponent-{Type}.cshtml`, but it's a 100+ test rewrite and an authored-JSON migration — only worth doing alongside another v2 breaking change. Proposal written to `.squad/decisions/inbox/tom-nook-workflow-schema-cleanup.md`.

- 2026-07-08 — Produced full executable rollout plan for Option 2 (workflow schema v2). Key design calls: (1) `PrismComponent` abstract record with `[JsonPolymorphic]` + `[JsonDerivedType]`, discriminator = `"type"` string (same author vocab). (2) `FieldsetComponent.Children: PrismComponent[]` replaces `PrismComponentDefinition.Fields: FieldFile[]` — fields are components. (3) `SummaryListComponent.FieldRefs: string[]` — engine resolves labels from definition tree; flagged as high-risk, needs P3 prototype before commitment. (4) `conditionalFields` on FieldFile → `conditionalChildren: Dictionary<string, PrismComponent[]>` on `RadiosComponent`/`CheckboxesComponent`. (5) `PrismComponentRenderPayload` stays flat for P3–P4; typed children decision deferred to P5 start. (6) `PrismFieldTagHelper` deleted in P5; all 11 `_PrismField-*.cshtml` renamed to `_PrismComponent-*.cshtml`. (7) 6-phase plan: P1 additive types, P2 migrator, P3 engine v2 reads, P4 builder v2 API, P5 view collapse, P6 release. Target ≤610 tests at v2.0. First commit: P1 types only — zero existing files changed. Plan filed to `.squad/decisions/inbox/tom-nook-workflow-v2-rollout.md`.

---

## 📌 2026-04-26: DIRECTIVE UPDATE — Solo Project, Main-Only Workflow

**Captured by:** Scribe  
**Effective:** 2026-04-26 onwards

### Changes to Squad Operations

Jonny Muir issued explicit directive (2026-04-26T07:28:51Z):

> *"This is a solo project. Work directly on `main` — no feature branches, no PR ceremony, no merge overhead."*

**For Tom Nook (and all squad agents):**

1. **DO NOT create `feature/*` or `squad/*` branches** except for issue-driven work explicitly requested
2. **Commit directly to `main`** for routine architectural planning and decisions
3. **No PR gate or Coordinator merge step** needed
4. If/when other contributors join, user will revisit

**Implications:**
- Next spawns should work directly on main
- Feature branches are overhead in single-developer context
- Routing.md and templates referencing PR workflows are documentation only

---

## 📌 2026-04-26: Workflow Schema v2.0 Rollout Plan Approved

**Authored by:** Tom Nook (Lead/Architect)  
**Status:** ✅ Approved for execution (Jonny Muir sign-off)  
**Document:** `.squad/decisions/inbox/tom-nook-workflow-v2-rollout.md` (merged to decisions.md)

**Executive Summary:**
- 6-phase rollout (P1–P6)
- Phase 1: Abstract `PrismComponent` base + sealed derived types (zero existing files changed, additive only)
- Design: `[JsonPolymorphic]` with `"type"` discriminator (same author vocab)
- Target: ≤610 tests at v2.0 (vs 557 current)
- Risk flag: `SummaryListComponent.FieldRefs` needs P3 prototype validation

**Next Action:** Await sprint planning. When Coordinator assigns v2.0 work, this plan is executable.


## 2026-04-26: v2.0 Design Doc Audit — Implications & Gaps

**Session:** Solo design audit (no code changes)  
**Requested by:** Jonny Muir  
**Context:** Validate v2 component design implications across 9 design docs before Blathers completes P1 implementation

### Work Performed

1. **Read 9 design documents** — workflow-forms-engine.md, -redesign.md, -backend.md, -client.md, -umbraco.md, -demo.md, -security.md, workflow-hub-and-conditional-fields.md, workflow-validation.md
2. **Audited v1 schema coupling** — Identified sections describing `fields[]`, `fieldType`, `conditionalFields`, `FieldGroupKey`, `PrismComponentDefinition` anaemic union
3. **Confirmed Jonny's mental model** — Yes, fields BECOME components (not just "represented by"); `FieldFile` is deleted; everything is `PrismComponent[]` tree
4. **Analyzed conditional fields upgrade** — v1 `conditionalFields: Dict<string, FieldDefinition[]>` → v2 `ConditionalChildren: Dict<string, PrismComponent[]>` allows ANY components in reveal branches (genuine power upgrade)
5. **Surfaced 8 new design gaps** — Component-tree validation traversal, generic conditional visibility, summary-list + hidden fields, fieldset-level validation, auth checks on tree, depth limits, doc rewrites, redesign doc obsolescence
6. **Produced delta report** — Per-doc impact table, conditional fields deep-dive, newly-surfaced gaps, recommended doc rewrite order (P3–P6), sign-off confirmation

### Key Findings

- **7 of 9 docs require rewrite** (ranging from light touch to heavy)
- **workflow-hub-and-conditional-fields.md most v1-coupled** — entire Design 1 section describes field-level `ConditionalOn`/`VisibleWhen` pattern that doesn't exist in v2 (v2 has `ConditionalChildren` on radios/checkboxes only)
- **workflow-forms-engine-redesign.md obsolete** — proposes Umbraco Element Types, superseded by polymorphic components; mark as archived
- **No showstoppers** — v2 design is sound; gaps are addressable in P3 prototype

### Newly-Surfaced Design Gaps

1. Component-tree validation traversal (P3 work)
2. Generic conditional visibility on non-input components (decide in P3: defer or implement)
3. Summary-list + conditionally-hidden fields (already flagged P3 blocker)
4. Component-tree authorization checks (add `AuthorizedRoles` to `InputComponent`?)
5. Fieldset-level validation rules (defer to v2.1)
6. Conditional children depth limit (warn in migrator/builder)
7. Umbraco doc JSON examples (rewrite in P5/P6)
8. Redesign doc obsolescence marker

### Outputs

- **Inbox memo:** `.squad/decisions/inbox/tom-nook-v2-design-doc-audit.md`
- **Sign-off to Jonny:** Confirmed fields → components; conditional fields get more powerful; 7 docs need rewrite (P5/P6); no showstoppers; 8 gaps surfaced

### Status

✅ Audit complete. Awaiting Scribe merge to decisions.md. No code changes (per directive). Doc rewrites deferred to P5/P6 per rollout plan.

### Learnings Added

- 2026-04-26 — v2 design doc audit surprise: **workflow-hub-and-conditional-fields.md is the most deeply v1-coupled doc**, not the backend or client docs. Entire Design 1 section describes field-level `ConditionalOn`/`VisibleWhen` pattern. In v2, conditionals are a dict on radios/checkboxes (`ConditionalChildren`), not a base property on all components. This is a **feature regression** unless we add generic conditional support (Option A: base class properties) in P3. Recommend: defer generic conditionals to v2.1; radios `ConditionalChildren` covers 80% ("Other → specify" pattern).

- 2026-04-26 — v2 doc audit methodology: most docs assumed high-level principles would survive, but the **vocabulary shift is pervasive**. "Field group", "field key", "field type", "options whitelist" appear in 7 of 9 docs. The validation and security docs are less coupled because they talk about *contracts* (nonce, response envelope, problems array), not *shapes*. Architecture lesson: design docs age better when they describe *protocols* rather than *schemas*.


---

## Session: v2.0 Design Audit & Scope Refinement (2026-04-26)

**Topic:** Audit 9 workflow design documents against v2 component plan; surface design gaps; confirm mental model alignment

**Outcome:** ✅ Complete — Audit memo in inbox; generic ConditionalOn deferral confirmed by user; v2 rollout plan updated

### Key Context for Next Session

**Generic ConditionalOn deferral confirmed by user.**

User (Jonny Muir) approved deferring generic `ConditionalOn` + `VisibleWhen` on arbitrary components to v2.1. v2.0 ships with `ConditionalChildren` on Radios/Checkboxes only (canonical "Other → specify" pattern, ~80% of use cases).

**Rationale:**
- Keeps v2 MVP scope tight; enables earlier ship date
- Avoids tree-traversal complexity in v2.0
- v2.1 can implement generic Option A (base class properties) with full traversal infrastructure

**For P3 prototype phase:** Focus on ConditionalChildren rendering/validation (in scope). Skip generic conditionals. Tree traversal for validation/authorization is separate concern (add to P3 scope per audit gaps).

### Delivered (Audit)

**9 workflow design documents audited** against v2 component plan:
1. workflow-forms-engine.md — ✅ Light touch needed (§1-2, §5 stable; §3-4 deferred to P5/P6)
2. workflow-forms-engine-redesign.md — ⚠️ OBSOLETE (archive with pointer to v2 plan)
3. workflow-forms-engine-backend.md — ⚠️ Major rewrite (models, services, C# signatures)
4. workflow-forms-engine-client.md — ⚠️ Major rewrite (tree rendering logic)
5. workflow-forms-engine-umbraco.md — ⚠️ Major rewrite (JSON examples for v2 schema)
6. workflow-forms-engine-security.md — ✅ Minor touch (tree traversal notes; security logic stable)
7. workflow-hub-and-conditional-fields.md — ⚠️ Major rewrite (§1 Design 1 obsolete; §2 Design 2 stable)
8. workflow-validation.md — ✅ Minor touch (tree traversal notes; validation logic stable)
9. workflow-forms-engine-demo.md — ✅ Minor touch (data model update; mostly stable)

### Key Findings

**Confirmed Mental Model:**
- ✅ Fields BECOME first-class components (no `fields[]` array)
- ✅ `FieldFile` deleted; all inputs are sealed PrismComponent descendants
- ✅ `FieldsetComponent.Children: PrismComponent[]` replaces `FieldFile[]`
- ✅ No asymmetry — a "field" and a "component" are the same thing

**Conditional Fields Upgrade:**
- v1: `conditionalFields: { [optionValue]: FieldDefinition[] }` on Radios field
- v2: `ConditionalChildren: Dictionary<string, PrismComponent[]>` on RadiosComponent
- Each branch can reveal ANY components (not just input fields) — genuine upgrade

**8 Design Gaps Surfaced:**
1. Component-tree validation traversal — V2 needs recursive walk for InputComponents (P3 work)
2. Component-tree authorization checks — Role-based field visibility in trees (P3 work)
3. Generic conditional visibility — Defer to v2.1 (approved by user)
4. Summary-list + conditionally-hidden fields — (P3 blocker already flagged)
5. Fieldset-level validation — Can FieldsetComponent have validation rules? (defer to v2.1)
6. Conditional children depth limit — Warn if nesting exceeds depth 2 (P2 migrator or P4 builder)
7. Umbraco integration JSON examples — All seed workflows use v1; rewrite P5-P6 (doc debt, not design gap)
8. Redesign doc obsolescence — workflow-forms-engine-redesign.md superseded (archive with pointer)

### Recommended Doc Rewrite Order

- **P1-P2:** No changes (docs describe v1 runtime)
- **P3:** Add "v2 in progress" banner to 4 major docs
- **P4:** Update code examples (builder v2 API ships)
- **P5:** Update client docs (view layer collapse)
- **P6:** Final rewrite; remove banners; archive obsolete doc

### Audit Memo

Location: `.squad/decisions/inbox/tom-nook-v2-design-doc-audit.md` (merged into decisions.md)

All 9 docs reviewed. No showstoppers — the polymorphic design is sound. Path forward is clear.

### Next Phase

P1 implementation complete (Blathers). P2 migrator next. Design audit informs P3 prototype scope.
