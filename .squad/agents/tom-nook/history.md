## 2026-06-05: Queue-only model implementation completed

- Tom Nook: Contract definition and implementation plan locked
- Tangy: Behavioural test specification and validation gates defined
- Isabelle: Editor refactor completed (build ✅, Playwright suite ✅)
- Blathers: Runtime refactor completed (core test suite ✅)
- Team coordination: All decisions merged to .squad/decisions.md
- Next: Full integration validation and cross-stack testing

---

## 2026-06-01T22:34:47Z — Queue Model: Clean Division of Responsibilities

**Task:** Define queue model boundaries; lock shared/host responsibilities  
**Status:** ✅ Complete

### Directive Captured

User directive (2026-06-01T23:34:47+01:00): Treat each workflow lane as a queue with explicit `queueName`. Host apps, not the shared workflow runtime or editor, decide who can start or act in each queue. TestSite web-user queue starts workflows in its queue; MockBusinessApp business-user queue moves instances on from admin page only. Editor reads available queues from host interface.

### Decision Locked

**Queue Model: Clean Division of Responsibilities**

- **Shared runtime/editor own:** Queue topology recognition, validation that all stages have `queueName`, canvas grouping by queue visually
- **Host apps own:** Queue definition discovery (`availableQueues()` interface method), access control at workflow boundaries, queue-aware UI

Stages and gateways now assigned to named queues (`queueName: string` field). `roleGates` remains access-control mechanism; runtime evaluates it after routing, but does NOT interpret queue-based access.

Payment workflow reshapes cleanly: stages get explicit queue names, routing topology (split/join) unchanged.

### Reference Implementation

- TestSite: Exposes "web-user" queue only; web users can start and work workflows in that queue
- MockBusinessApp: Exposes "admin" queue; admins view and manually transition instances from admin page

### Follow-Up

Isabelle (editor) and Blathers (runtime) now proceed with implementation. Payment demo validation false-positives (Join gateway bug) remain in separate slice.

---


## 2026-06-04: Flattened Workflow Model Session

**Agents:** Tom Nook, Tangy, Isabelle, Blathers  
**Session:** Queue-first architecture consolidation  
**Decision:** Single `WorkflowDefinition` contract approved

**Key outcomes:**
- AuthoredWorkflow + WorkflowDefinitionFile + ProjectedWorkflow → single canonical schema
- Lanes renamed to queues; laneKey + queueName merged to single identifier
- Gateways elevated from metadata to first-class definition elements
- Editor and runtime both use persisted contract directly

**Team coordination:** Decisions merged from 4 agents into `.squad/decisions.md`

**Tom Nook contribution:** Defined flattened single persisted workflow definition strategy; locked architecture decision on queue-first model with single WorkflowDefinition contract.
## 2026-05-25T07:47:41Z — Landing Local Main Commit

**Task:** Land local main commit to origin/main

**Outcome:** ✅ Completed

**Summary:**
- Published the local main commit via PR #77
- Added missing `all` aggregate workflow check in `squad-ci.yml` to satisfy branch protection rules
- Merged PR #77 to main
- Synced local main with origin/main

**Result:** Local main now synced with origin/main; CI pipeline fully configured.

## Learnings

### 2026-05-25T09:32:35.455+01:00 — Concurrent lanes backlog slicing

- Reviewed the open backlog first (#28, #63, #73) to avoid creating overlap.
- Split the concurrent multi-lane redesign into seven ordered issues: cleanup/projection contract first, then lane model, editor UX, join gateways, concurrent engine behaviour, history clarity, and showcase/test evolution.
- Kept each issue framed in product language with acceptance criteria, explicit sequence, and a standing requirement to keep behavioural tests green.

## 2026-05-25 (09:32:35 UTC) — Concurrent Lanes Redesign Sequenced

- Issues #81–#87 created per concurrent multi-lane redesign plan
- Orchestration log recorded
- Tangy executing parallel behavioural track (#78–#80)
- Squad ready for coordinated execution

### 2026-05-25T11:48:05.065+01:00 — Issue #81 landing discipline

- When issue work is sitting uncommitted on `main`, branch it before landing; the repo now treats feature-branch + PR workflow as mandatory for code changes.
- For workflow lane cleanup, ship the shared assignment helper, projection sanitiser, docs, and behavioural proof together so the source-of-truth change is explicit across code and design notes.
