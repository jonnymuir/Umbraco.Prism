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

# tom-nook History (Summary)

## Latest Updates

See history-archive.md for full history.

   - **Phase 4 (Push/Manual):** Complete push-notifications; decide on authoring/tenant manual captures (Mabel + Tangy, 2 days)
   - **Phase 5 (Review):** Final navigation audit, SKILL.md updates (Tom Nook, 1 day)

4. **File-level cross-touch analysis:**
   - **memberDashboard.cshtml** — dashboard cards + admin link (Phase 1)
   - **walkthroughs/support/walkthrough.ts** — viewport + mobile-nav hiding (Phase 2)
   - **workflow-administration.walkthrough.spec.ts** (NEW) — ops walkthrough (Phase 3)
   - **docs/images/walkthroughs/**/*.png** — all regenerated (Phase 2)
   - **SKILL.md updates** — viewport standard, height rules (Phase 2–5)

5. **Strategic insight:**
   - Prism's admin surface (`/admin/workflow`) is fully functional but completely hidden from navigation. This is a UX debt, not an architecture flaw. Exposing the link (with proper role gating for production) unblocks ops documentation and makes the feature discoverable.
   - MockBusinessApp is both demo and reference implementation, creating a shadowing risk (same pattern repeated by real BusinessApp implementors). This is a separate tom-nook-prism-reflection decision already recorded.

**Decision:** Architecture proposal recorded in `.squad/decisions/inbox/tom-nook-walkthrough-discovery-2026-05-04.md`. No code changes in this pass. Ready for team review and sequencing.

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
- ⏳ NuGet indexing in progress (typically 15-60 minutes)
- ⏳ Umbraco Marketplace sync pending (occurs after NuGet indexing)

**Learnings:**
1. **Tag alignment is critical for release workflows.** GitHub Actions package-release.yml triggers on tag push but only processes tags that exist at proper commit point. Misalignment silently skips execution—CI status doesn't signal the root cause.
2. **Marketplace documentation generation is now part of CI.** The generate-marketplace-readme.mjs script (v1.9.1 addition) ensures MARKETPLACE.md stays in sync with README.md. package-release.yml includes `npm run check:marketplace` verification step—this guards against stale marketplace copy in published packages.
3. **Marketplace propagation delay is expected.** Umbraco Marketplace pulls package README from NuGet feed and ingests MARKETPLACE.md as the rendering source. Full propagation to marketplace.umbraco.com typically takes 30-90 minutes after package publication, not instant.

**Marketplace body status:** MARKETPLACE.md was generated and packaged. Once NuGet indexing completes, Marketplace will ingest the updated body from the package readme. User's goal is satisfied—the machinery is running on time.



**2026-05-08T05:26:48.026Z — Squad Sync:** Post-publish verification and tag correction recorded.

## Learnings

### 2026-05-15T06:35:47.013+01:00 | PASA death-process design

- PASA's public guidance is strongest on **risk-based identity management** and the need for a clear member identity view across life events, but it does not prescribe a detailed digital bereavement journey. The notifier UX, optional-account posture, and assisted-digital shape therefore need to come from broader UK bereavement and service-design practice.
- The most reusable Prism pattern for third-party initiated casework is to separate the **workflow actor** from the **linked subject**. For bereavement reporting, the notifier is the actor and the deceased member is matched server-side as the subject.
- Save/resume for sensitive one-off reporting works best when the service verifies a contact channel early, creates a case shell immediately after, and combines passwordless resume with case-reference recovery instead of forcing permanent registration.

## 2026-05-15: PASA Death Process Baseline Decision

Produced foundational decision on case-scoped notifier model for death-process example. Confirmed notifier as authenticated workflow actor, deceased member as linked subject, no mandatory registration up front. Hybrid save/resume via verified-session + case-reference. Decision merged to shared registry.

### 2026-05-16T10:59:37.438+01:00 | Workflow editor architecture proposal

- Jonny wants the next workflow editor effort grounded in Prism's existing workflow/forms/runtime model, but intentionally designed for both human editing and AI/agent authoring/testing.
- Recommended architecture is a three-plane split: **authored model** (editor-native graph + component semantics), **runtime projection** (Prism-compatible `WorkflowDefinitionFile`/component tree), and **agent surfaces** (MCP/skills/structured diff APIs) so AI integrations do not become the runtime authority.
- Planning application should be the reference demo because it spans rich citizen input, multi-step service design, check-answers, and cross-surface handoff potential better than the simpler enquiry or payment samples.
- Prism pages should stay content-owned shells while the business app/runtime remains authoritative for state, transitions, validation, nonce-safe field contracts, and render-shell inference.
- Key grounding paths: `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`, `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`, `src/UmbracoPrism.Core/Models/Workflow/WorkflowRenderShellResolver.cs`, `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`, `src/UmbracoPrism.Core/Views/workflowPage.cshtml`, `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`, `src/UmbracoPrism.MockBusinessApp/Program.cs`, `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json`.

## Learnings — 2026-05-16 Workflow Editor V1 spine

**V1 architecture invariants (locked):**
- Three planes — Authoring / Projection / Agent — with stable contracts between them; the Prism runtime contract is untouched.
- `WorkflowDefinitionFile` is the projection *target*, never the editor's primary model. Authors design stages; shells stay inferred via `PrismComponentExtensions.InferStepType()`.
- Projection is a pure, deterministic function: same authored input ⇒ byte-identical seed; unknown fields rejected; total over stages and handoffs.
- Every agent change is a structured proposal bundle (authored diff + projected diff + rationale + validation + preview + provenance). No live-instance writes, ever.
- Natural-language generation and conversational refinement are first-class entry points, but they ride on **general** agents (GitHub Copilot) for NL/drafting and on **workflow-specific** MCP tools for transforms, projection, semantic diffing, simulation.
- Planning application is the single V1 reference demo; "insert external ID&V after declaration" is the canonical agent-loop scenario.
- Authoring lives in the Business App; Umbraco keeps public/member shells; the v17 backoffice extension is a thin link/embed, not a re-implementation.

**Deferred to V2:**
- Versioning / lifecycle / rollback semantics.
- In-flight instance migration when projected definitions change.
- Multi-tenant authoring and collaborative real-time editing.
- Operator backstage UI contract (Blathers open #1).
- Permission expressiveness, routing authoring depth, task-list authoring (Blathers open #2–#4).
- Agent autonomy ceiling (when, if ever, a green proposal auto-applies).
- Cross-workflow refactors (rename actor/policy across definitions).

**Tensions resolved:**
- *Editor vs runtime coupling.* Resolved by making `WorkflowDefinitionFile` a projection target, not the authored source — protects existing Prism contracts while freeing the authored model to grow.
- *AI scope creep.* Resolved by the reuse-don't-reinvent directive: general agents do general work; workflow tools do workflow work. The proposal bundle is the seam.
- *Where does authoring live?* Resolved per Brewster — Business App owns workflow authoring; Umbraco backoffice gets a thin link/embed extension, not a re-implementation.
- *NL generation vs safety.* Resolved by routing all NL changes through the same proposal/validate/preview/approve loop human edits use — same review surface, same guardrails.
- *Conversational refinement.* Resolved as layered proposals, not hidden mutations — each refinement is its own artifact with its own provenance.
