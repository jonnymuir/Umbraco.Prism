# Tom Nook — Archived History (Pre-2026-05-16)

This archive contains entries prior to the design rewrite batch.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-08 | Post-Publish Release Review (v1.9.1)

**Task:** Verify post-publish state after 1.9.1 release work lands.

**Finding:** v1.9.1 tag was misaligned—positioned on commit 2951551 (Fix 1.9.0 package version sources) instead of correct commit 8b78831 (chore(release): bump version to 1.9.1 and update marketplace packaging). This blocked CI workflows.

**Action Taken:**
- Deleted remote v1.9.1 tag
- Repositioned tag to correct commit 8b78831
- Pushed corrected tag to GitHub

**Result:**
- ✅ Package Release workflow executed successfully (2026-05-08T05:26:46Z → 05:27:54Z)
- ✅ GitHub Release v1.9.1 created (published_at: 2026-05-08T05:27:51Z, draft=false, prerelease=false)
- ✅ NuGet package pushed (UmbracoPrism.1.9.1.nupkg artifact confirmed)
- ✅ MARKETPLACE.md updated with generated marketplace-friendly documentation

## 2026-05-15: PASA Death Process Baseline Decision

Produced foundational decision on case-scoped notifier model for death-process example. Confirmed notifier as authenticated workflow actor, deceased member as linked subject, no mandatory registration up front. Hybrid save/resume via verified-session + case-reference. Decision merged to shared registry.

### 2026-05-15T06:35:47.013+01:00 | PASA death-process design

- PASA's public guidance is strongest on **risk-based identity management** and member identity view across life events, but doesn't prescribe detailed bereavement journey. Notifier UX, optional-account posture, assisted-digital shape come from broader UK bereavement and service-design practice.
- Most reusable Prism pattern for third-party initiated casework is to separate **workflow actor** from **linked subject**. For bereavement reporting, notifier is actor and deceased member is matched server-side as subject.
- Save/resume for sensitive one-off reporting works best when service verifies contact channel early, creates case shell immediately after, and combines passwordless resume with case-reference recovery instead of forcing permanent registration.

## 2026-05-16 | Architecture Proposals & Reference Split Review

### 2026-05-16T10:59:37.438+01:00 | Workflow editor architecture proposal

- Jonny wants workflow editor effort grounded in Prism's existing workflow/forms/runtime, but designed for both human and AI/agent authoring/testing.
- Recommended: three-plane split — **authored model** (editor-native graph + component semantics), **runtime projection** (Prism-compatible `WorkflowDefinitionFile`), **agent surfaces** (MCP/skills/structured diff APIs) so AI doesn't become runtime authority.
- Planning application is best reference demo (spans rich citizen input, multi-step service, check-answers, cross-surface handoff).
- Prism pages stay content-owned shells; business app/runtime stays authoritative for state, transitions, validation, nonce-safe field contracts, render-shell inference.
- Key grounding paths documented.

### Learnings — 2026-05-16 Workflow Editor V1 spine

**V1 architecture invariants (locked):**
- Three planes — Authoring / Projection / Agent — with stable contracts; Prism runtime contract untouched.
- `WorkflowDefinitionFile` is projection *target*, never editor's primary model.
- Projection is pure, deterministic function.
- Every agent change is structured proposal bundle (no live-instance writes).
- NL generation and conversational refinement are first-class entry points via **general agents** (Copilot) + **workflow-specific MCP tools**.
- Planning app is single V1 reference demo.
- Authoring lives in Business App; Umbraco keeps public/member shells; v17 backoffice is thin link/embed.

**Deferred to V2:**
- Versioning / lifecycle / rollback semantics
- In-flight instance migration
- Multi-tenant authoring and real-time collaboration
- Operator backstage UI contract
- Permission expressiveness, routing, task-list authoring
- Agent autonomy ceiling
- Cross-workflow refactors

**Tensions resolved:**
- *Editor vs runtime coupling* → `WorkflowDefinitionFile` is projection target, not authored source
- *AI scope creep* → general agents do general work; workflow tools do workflow work
- *Where does authoring live?* → Business App owns workflow authoring; Umbraco gets thin link
- *NL generation vs safety* → all NL changes through proposal/validate/preview/approve loop
- *Conversational refinement* → layered proposals with provenance, not hidden mutations
