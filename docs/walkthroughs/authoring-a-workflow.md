# Walkthrough — Authoring a Workflow in the Editor

Build a workflow from an empty editor, keep the canvas readable, and verify each step against the proving tests.

> **Prerequisites:** Run the client Storybook/test host or the local app host. This walkthrough uses the shared **Leave Request** starter journey that appears in stories, behavioural tests, and the editor help.

---

## What this walkthrough proves

The editor now treats lanes as **vertical service columns**:

- each column belongs to one service owner
- the workflow reads **top to bottom**
- stages are the work cards
- gateways are the only routing points between stages
- same-level siblings in one lane expand **to the right**

The proving tests for this walkthrough are:

| What is proved | Test |
|---|---|
| Empty workflow guidance and first-step help | `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-help.spec.ts` |
| Vertical lane columns, same-lane sibling slots, and no overlap | `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-layout-proof.spec.ts` |
| Gateway-first routing behaviour and route readability | `src/UmbracoPrism.Client/tests/workflow-editor/workflow-transition-editor.spec.ts` |
| General graph/list behaviour and accessible reorder contract | `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-visual.spec.ts` |

---

## The shared starter workflow

The walkthrough and stories use a single small workflow:

1. **Start request** in the applicant lane
2. **Review split** gateway
3. **Applicant amendments** and **Upload evidence** side by side in the applicant lane, plus **Reviewer assessment** in the reviewer lane
4. **Decision join** gateway that owns the waiting copy
5. **Decision confirmed** as the released next step

This keeps the model honest to the editor contract: **stage -> gateway -> stage**, with gateway joins handling waiting semantics.

---

## Step 1: Start from an empty workflow

Open the empty workflow story or editor host and stay on the **Canvas** tab.

You should see:

- a simple empty-state message
- a short checklist for getting started
- one primary action: **Add first stage**

Proof: `workflow-editor-help.spec.ts`

---

## Step 2: Add the first stage

Create the first stage in the lane that owns the opening work.

For the starter journey:

- **Name:** `Start request`
- **Key:** `start-request`
- **Lane owner:** `applicant`
- **Type:** `form`

Expected result:

- the new stage appears in the applicant column
- the outline shows the applicant lane group
- the inspector opens on the new stage

Proof: `workflow-editor-help.spec.ts`

---

## Step 3: Add the next work stages

Add the next pieces of work before you add routing:

- `Applicant amendments`
- `Upload evidence`
- `Reviewer assessment`
- `Decision confirmed`

Keep the columns simple:

- applicant work stays in the applicant column
- reviewer work goes in the reviewer column
- the canvas should still read top to bottom

Proof: `workflow-graph-layout-proof.spec.ts`

---

## Step 4: Add the routing gateways

Add the gateways that make the branching explicit:

1. **Review split** in the applicant lane
2. **Decision join** in the applicant lane

Use the join gateway to carry the waiting explanation instead of adding a waiting stage.

Expected result:

- gateways render as diamonds, not stage cards
- the canvas still reads as lane columns
- the join owns the waiting copy

Proof: `workflow-transition-editor.spec.ts`

---

## Step 5: Add the routes

Create the routes so the canvas shows the intended service shape:

- `Start request -> Review split`
- `Review split -> Applicant amendments`
- `Review split -> Upload evidence`
- `Review split -> Reviewer assessment`
- each branch returns through `Decision join`
- `Decision join -> Decision confirmed`

Expected result:

- same-lane sibling branches sit beside each other
- cross-lane work stays readable
- the next released step sits below the join

Proof: `workflow-transition-editor.spec.ts` and `workflow-graph-layout-proof.spec.ts`

---

## Step 6: Use the confidence surfaces

Before saving:

1. Use **Validation** for structural issues
2. Use **Preview** to inspect the selected stage shape
3. Use **Simulation** to confirm the happy path and waiting/release behaviour

The canvas should stay quiet while these confidence surfaces carry the detail.

Proof: `workflow-editor-help.spec.ts` and `workflow-graph-visual.spec.ts`

---

## Keeping the walkthrough current

When the editor changes:

1. update the shared starter workflow fixture first
2. update the related story if the journey changes
3. update the proving tests
4. then update this walkthrough so each step still points at a live proof

That keeps the documentation, editor behaviour, and visual contract locked together.
