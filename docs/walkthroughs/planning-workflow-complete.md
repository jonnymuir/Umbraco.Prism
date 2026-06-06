# Walkthrough — Planning Workflow Complete End-to-End

A comprehensive quality assurance guide covering the complete planning workflow journey from editor authoring through runtime execution across public, member, and back-stage surfaces. This walkthrough validates all critical paths: approval, rejection, and re-submission.

> **Prerequisites:** Stack running via [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup). Familiarity with [Planning Workflow Editor](planning-workflow-editor.md) and [Planning Notification](planning-notification.md) walkthroughs recommended.

---

## Overview

The planning workflow complete E2E test validates the entire workflow lifecycle:

| Phase | Surface | Actor | What's Tested |
|---|---|---|---|
| **Authoring** | Workflow Editor | Developer | Graph visualization, validation, publish |
| **Public Entry** | Umbraco public pages | Applicant | Form rendering, data capture, initial submission |
| **Member Continuation** | Umbraco member pages | Applicant | Resume workflow, complete application |
| **Back-stage Review** | MockBusinessApp Admin | Caseworker | Instance management, approval/rejection |
| **Critical Paths** | All surfaces | All actors | Validation, rejection → re-submission, state transitions |

The test validates that:
- Workflow editor correctly authors and publishes workflow definitions
- Published workflows run correctly in Umbraco runtime
- Public applicants can start and complete workflows
- Members can resume in-progress workflows
- Back-stage caseworkers can review and make decisions
- All critical paths (approval, rejection, re-submission) work end-to-end
- Validation blocks invalid submissions at appropriate stages

Every move from one stage to another happens through a gateway. Single-route gateways render as a small pill; multi-route gateways open up as a diamond.

---

## Phase 1: Workflow Editor — Authoring and Publishing

### Step 1 — Load the workflow editor

Navigate to `/workflow-editor` in MockBusinessApp. The editor shell loads with the planning workflow selected.

![Workflow editor loaded with planning workflow](../images/walkthroughs/apply-for-planning-permission-complete/01-editor-loaded.png)

**What to verify:**
- Shell heading: "Workflow Editor"
- Workflow picker shows "planning" selected
- Editor loads without errors
- `data-prism-workflow-loaded` attribute present on `<prism-workflow-editor>`

**Source:** [`prism-workflow-editor-shell.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor-shell.ts)

---

### Step 2 — Verify workflow graph shows all stages

The workflow graph renders the planning workflow stages as a directed graph.

![Workflow graph showing planning stages](../images/walkthroughs/apply-for-planning-permission-complete/02-editor-graph.png)

**Expected stages:**
1. **Declaration** — Applicant identity and site basics
2. **Application Form** — Main planning request details
3. **Check your answers** — GDS check-answers summary
4. **Application submitted** — Terminal confirmation stage

**What to verify:**
- All four stages visible in graph
- Routes between stages through gateways shown as edges
- Graph is keyboard-accessible (`role="application"`)

**Source:** [`prism-workflow-graph.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts)

---

### Step 3 — Validate and publish workflow

The validation rail (if visible) should show no errors. The save/publish button should be enabled.

![Workflow published successfully](../images/walkthroughs/apply-for-planning-permission-complete/03-editor-published.png)

**What to verify:**
- Validation rail shows no error-level issues (if present)
- Save/Publish button is enabled
- Clicking publish triggers `POST /api/workflow-authoring/workflows/planning/publish`
- Success toast appears: "Workflow updated successfully" or similar
- Published workflow is ready for runtime execution

**Source:** [`workflow-validation.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/workflow-validation.ts)

---

## Phase 2: Public Entry — Starting Workflow via Umbraco

### Step 4 — Navigate to public workflow entry point

Navigate to `/apply-for-planning-permission` in the Umbraco TestSite. The first stage (Declaration) renders.

![Public entry: Declaration stage](../images/walkthroughs/apply-for-planning-permission-complete/04-public-entry.png)

**What to verify:**
- Page loads without 404 or server errors
- Stage heading visible: "Declaration" or "Planning Application"
- Required fields present:
  - Applicant name (text input)
  - Site address (textarea)
- Continue button present and enabled

**Authentication:** User must be authenticated (signed in via Keycloak) to access workflow pages.

---

### Step 5 — Fill in declaration stage

Enter applicant details and site address.

![Declaration stage filled](../images/walkthroughs/apply-for-planning-permission-complete/05-public-declaration-filled.png)

**Example data:**
- **Applicant name:** Jane Smith
- **Site address:** 123 Main Street, Townsville, AB12 3CD

**What to verify:**
- Fields accept input
- HTML5 validation enforces required fields
- Character limits respected (if defined)

---

### Step 6 — Continue to application form stage

Click "Continue" to advance to the next stage.

![Application form stage](../images/walkthroughs/apply-for-planning-permission-complete/06-public-application-form.png)

**What to verify:**
- Page transitions to application form stage
- URL remains `/apply-for-planning-permission` (single-page workflow)
- New heading: "Application Form" or "Proposed works"
- New fields present:
  - Description of proposed works (textarea)
  - Type of development (select/radio)

**Source:** Workflow engine in `UmbracoPrism.MockBusinessApp` handles stage transitions and form rendering.

---

### Step 7 — Fill in application form

Enter details about the proposed works.

![Application form filled](../images/walkthroughs/apply-for-planning-permission-complete/07-public-application-filled.png)

**Example data:**
- **Description:** Construction of a single-storey rear extension
- **Type of development:** Extension

**What to verify:**
- Textarea accepts multi-line input
- Select/radio options match workflow definition
- Validation feedback appears for invalid input

---

### Step 8 — Review answers at check-answers stage

Click "Continue" to reach the check-answers review stage.

![Check answers stage](../images/walkthroughs/apply-for-planning-permission-complete/08-public-check-answers.png)

**What to verify:**
- All captured data displayed:
  - Applicant name: Jane Smith
  - Site address: 123 Main Street, Townsville, AB12 3CD
  - Description: Construction of a single-storey rear extension
- Change links present (if workflow supports editing)
- Submit button present

**Pattern:** GDS-style check-answers pattern shows summary before submission.

---

### Step 9 — Submit application

Click "Submit" to complete the public workflow and move to terminal stage.

![Application submitted confirmation](../images/walkthroughs/apply-for-planning-permission-complete/09-public-submitted.png)

**What to verify:**
- Submission triggers workflow instance state update
- Terminal stage renders: "Application submitted" or "Confirmation"
- Confirmation message visible
- Workflow instance created in back-stage system
- No errors in browser console

**What happens:**
- Workflow engine creates instance record
- Instance moves to terminal "submitted" stage
- Handoff to back-stage caseworker actor occurs
- Applicant can view status in dashboard

---

## Phase 3: Back-stage Review — Caseworker Decision Flow

### Step 10 — Navigate to dashboard

Return to Umbraco dashboard to access workflow admin.

![Dashboard with workflow admin link](../images/walkthroughs/apply-for-planning-permission-complete/10-dashboard.png)

**What to verify:**
- Dashboard heading: "Workflow Demos"
- "Open Admin" link visible and pointing to `https://localhost:7245/admin/workflow`
- Link opens in new tab

---

### Step 11 — Open workflow admin

Click "Open Admin" to open the MockBusinessApp workflow admin interface.

![Workflow admin: instances list](../images/walkthroughs/apply-for-planning-permission-complete/11-admin-instances.png)

**What to verify:**
- Admin page loads without errors
- Two main sections visible:
  - Workflow Instances
  - Workflow Definitions
- Instance list shows submitted planning applications

**Source:** MockBusinessApp admin controller at `/admin/workflow`

---

### Step 12 — Find submitted planning instance

Locate the instance for "Jane Smith" in the instances list.

![Planning instance found in list](../images/walkthroughs/apply-for-planning-permission-complete/12-admin-instance-found.png)

**What to verify:**
- Instance shows:
  - Workflow name: "planning" or "Planning Application"
  - Applicant reference: "Jane Smith"
  - Current stage or status
- Instance is clickable

**Query:** Instances filtered by workflow definition key and current stage.

---

### Step 13 — Open instance detail

Click the instance to view full details.

![Instance detail view](../images/walkthroughs/apply-for-planning-permission-complete/13-admin-instance-detail.png)

**What to verify:**
- Instance detail page shows:
  - Applicant name: Jane Smith
  - Site address: 123 Main Street, Townsville, AB12 3CD
  - Proposed works description
  - Current stage: "submitted" or awaiting review
  - Stage history (if available)
- Decision actions visible:
  - Approve button
  - Reject button (if workflow supports rejection)

---

### Step 14 — Approve application (approval path)

Click "Approve" to approve the planning application.

![Application approved](../images/walkthroughs/apply-for-planning-permission-complete/14-admin-approved.png)

**What to verify:**
- Approve action triggers workflow transition
- Instance status updates to "Approved"
- Confirmation message appears
- Applicant can view approval status in dashboard

**Workflow contract:** Approval transitions instance to terminal approved stage.

---

## Critical Path: Rejection and Re-submission

### Step 15 — Submit application for rejection test

Start a new workflow instance with incomplete data.

![Application submitted for rejection test](../images/walkthroughs/apply-for-planning-permission-complete/15-rejection-submitted.png)

**Test data:**
- **Applicant name:** Bob Johnson
- **Site address:** 456 Oak Avenue, Woodville, CD34 5EF
- **Description:** Incomplete application - missing details

**Purpose:** Test rejection flow when caseworker identifies issues.

---

### Step 16 — Review instance in admin

Caseworker opens instance for Bob Johnson in workflow admin.

![Instance detail for rejection](../images/walkthroughs/apply-for-planning-permission-complete/16-rejection-instance-detail.png)

**What to verify:**
- Instance shows Bob Johnson's application
- Description indicates incomplete application
- Reject button visible

---

### Step 17 — Reject application with reason

Click "Reject" and provide feedback.

![Application rejected with reason](../images/walkthroughs/apply-for-planning-permission-complete/17-rejection-rejected.png)

**Rejection reason:**
"Application is incomplete. Please provide more details about the proposed works."

**What to verify:**
- Reason field accepts feedback text
- Reject action updates instance status
- Applicant receives notification (if notification system in place)
- Instance transitions to rejected state

**Workflow contract:** Rejection may allow applicant to revise and re-submit.

---

### Step 18 — Applicant resumes rejected application

Applicant returns to dashboard and finds rejected application.

![Applicant re-submits with corrections](../images/walkthroughs/apply-for-planning-permission-complete/18-rejection-resubmit.png)

**What to verify:**
- Dashboard shows rejected application with "Resume" link
- Clicking resume returns applicant to editable stage
- Rejection feedback visible to applicant
- Applicant can update application fields

**Updated description:**
"Updated: Construction of a single-storey rear extension with full architectural plans"

---

### Step 19 — Re-submit corrected application

Applicant re-submits after addressing feedback.

![Application re-submitted](../images/walkthroughs/apply-for-planning-permission-complete/19-rejection-resubmitted.png)

**What to verify:**
- Re-submission creates new review cycle
- Instance returns to "submitted" state awaiting review
- Caseworker sees updated application in admin
- Workflow history shows rejection → resubmission path

**Contract:** Re-submission follows same approval flow as initial submission.

---

## Critical Path: Validation Enforcement

### Step 20 — Attempt submission with missing required fields

Try to continue without filling required fields.

![Validation blocks submission](../images/walkthroughs/apply-for-planning-permission-complete/20-validation-required-fields.png)

**What to verify:**
- Browser validation prevents form submission (HTML5 required attribute)
- OR server-side validation returns error message
- Error message indicates required fields: "Applicant name is required"
- User cannot proceed to next stage

**Validation contract:** Required fields enforced at each stage before transition.

---

### Step 21 — Validation passes with valid data

Fill in required fields with valid data.

![Validation passes, stage advances](../images/walkthroughs/apply-for-planning-permission-complete/21-validation-passed.png)

**What to verify:**
- With valid data, Continue button advances stage
- Page transitions to "Application Form" stage
- No validation errors shown

**Contract:** Validation gates protect data integrity throughout workflow.

---

## Critical Path: Member Continuation

### Step 22 — Start workflow and navigate away

Authenticated member starts workflow but doesn't complete it.

![Dashboard showing in-progress workflows](../images/walkthroughs/apply-for-planning-permission-complete/22-member-dashboard.png)

**Test flow:**
1. Fill in declaration stage (Applicant: "Member User", Address: "789 Pine Street...")
2. Continue to application form stage
3. Navigate away without completing (go to dashboard)

**What to verify:**
- Workflow instance saved in "in-progress" state
- Dashboard shows "In progress" or "Active workflows" section
- Member's incomplete workflow listed

---

### Step 23 — View in-progress workflows

Dashboard highlights incomplete workflows for authenticated member.

![In-progress workflows section](../images/walkthroughs/apply-for-planning-permission-complete/23-member-in-progress.png)

**What to verify:**
- In-progress section present
- Planning workflow shown with status "In progress"
- Resume link visible

**Contract:** Members can resume workflows across sessions.

---

### Step 24 — Resume workflow from dashboard

Click "Resume" to return to incomplete workflow.

![Workflow resumed at application form stage](../images/walkthroughs/apply-for-planning-permission-complete/24-member-resumed.png)

**What to verify:**
- Resume returns to exact stage where user left off (Application Form)
- Previously entered data preserved (name, address from declaration)
- Form fields ready for input
- User can continue workflow without re-entering previous data

**Source:** Workflow engine resolves existing instance by user ID and workflow definition.

---

### Step 25 — Complete resumed workflow

Member completes application form and submits.

![Workflow completed after resume](../images/walkthroughs/apply-for-planning-permission-complete/25-member-completed.png)

**Updated data:**
- **Description:** Resumed and completed application

**What to verify:**
- Workflow advances through remaining stages
- Check answers shows all captured data (from declaration + resumed form)
- Submit completes workflow
- Confirmation stage renders
- Instance moves to "submitted" state in back-stage

**Contract:** Resume functionality maintains workflow state integrity.

---

## Running the Test Suite

Execute the complete E2E test:

```bash
cd src/UmbracoPrism.Client
npm run test:playwright:localhost-auth -- \
  --grep "Planning workflow complete E2E" \
  --reporter=line
```

Regenerate walkthrough screenshots:

```bash
cd src/UmbracoPrism.Client
CAPTURE_SCREENSHOTS=1 npx playwright test \
  --config=playwright.localhost-auth.config.ts \
  tests/walkthroughs/apply-for-planning-permission-complete.walkthrough.spec.ts \
  --reporter=line
```

Screenshots write to `docs/images/walkthroughs/apply-for-planning-permission-complete/`.

---

## Quality Gate Summary

The planning workflow complete E2E test validates:

✅ **Editor Phase:**
- Workflow loads in editor
- Graph shows all stages and gateways
- Validation passes (no errors)
- Publish succeeds

✅ **Public Entry Phase:**
- Public workflow page loads
- Form fields render correctly
- Data capture works for all stages
- Submission creates instance

✅ **Member Continuation:**
- In-progress workflows saved
- Dashboard lists incomplete workflows
- Resume returns to correct stage
- Previously entered data preserved
- Completion advances normally

✅ **Back-stage Review:**
- Admin lists workflow instances
- Instance detail shows captured data
- Approval flow works
- Rejection flow works

✅ **Critical Paths:**
- **Approval:** Caseworker approves → instance terminal approved state
- **Rejection:** Caseworker rejects → applicant receives feedback → applicant updates → re-submits → new review cycle
- **Validation:** Required fields enforced → invalid submissions blocked → valid submissions proceed

✅ **State Transitions:**
- Declaration → Application Form → Check Answers → Submitted → Review → Approved/Rejected
- All stage routes execute correctly
- Instance state persists across stages

---

## Related

- **Executable spec:** This walkthrough is executed by [`planning-workflow-complete.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/apply-for-planning-permission-complete.walkthrough.spec.ts)
- **Editor context:** [Planning Workflow Editor](planning-workflow-editor.md)
- **Runtime context:** [Planning Notification](planning-notification.md)
- **Issue #72:** [QA: Complete planning workflow end-to-end test](https://github.com/jonnymuir/Umbraco.Prism/issues/72)
- **Skills:**
  - [walkthroughs-as-executable-specs](../../.squad/skills/walkthroughs-as-executable-specs/SKILL.md)
  - [workflow-stage-preview-runtime](../../.squad/skills/workflow-stage-preview-runtime/SKILL.md)
