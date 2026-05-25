# Workflow editor design (V1)

**Author:** Isabelle (Frontend Dev and Accessibility Lead)  
**Date:** 2026-05-17T22:05:30.472+01:00  
**Status:** Draft  
**Relates to:** `docs/design/workflow-editor-v1/README.md`

---

## 1. What this document is

This is the primary design document for the **workflow editor**. It describes the editor that authors use to define the workflow JSON through a simple product surface, not by hand-editing raw JSON.

V1 focuses on one excellent experience: opening a workflow, editing its structure and behaviour, validating it, previewing it, simulating key paths, and saving it with confidence.

---

## 2. Product goal

The workflow editor should feel like a good modern editor for service workflows:

- simple to learn
- fast for routine changes
- safe for structural changes
- accessible by default
- clear about what will end up in the workflow definition
- friendly to both manual editing and proposal-based AI help

The editor is for building and maintaining the workflow model that later projects into Prism's runtime JSON definition.

---

## 3. V1 principles

1. **Workflow-first, not JSON-first.** Authors edit stages, transitions, actions, actors, and parameters using workflow language.
2. **Simple by default.** The main path stays focused on common tasks. Advanced detail appears only when needed.
3. **One workspace, not many tools.** Structure, details, validation, preview, simulation, and help live in one editor.
4. **Safe editing.** Undo, redo, copy, paste, validation, previews, and explicit save are built in.
5. **Accessible editing is a core feature.** Keyboard and screen reader support are part of the design, not an add-on.
6. **AI is a co-author.** Any AI change appears as a proposal to review, not a hidden edit.

---

## 4. What V1 covers

V1 lets an author define everything needed for the workflow definition, including:

- workflow metadata
- actor roles
- stages
- transitions
- actions within stages and transitions
- action parameters
- forms-backed actions and their field configuration
- validation rules that can be expressed in the authored model
- preview and simulation of the authored flow

V1 does **not** make raw JSON the main authoring surface. JSON can remain an advanced diagnostic view, but the workflow editor is the primary product.

---

## 5. The V1 editor workspace

The V1 editor is a single workspace with persistent structure, editing, and review surfaces.

For the current role-first delivery slice, the main workspace stays on one screen: outline on the left, graph/list canvas in the middle, inspector on the right, and tabbed confidence surfaces underneath. That keeps the role-first framing visible while Canvas, Validation, Preview, Simulation, and Help stay close at hand.

```text
┌─────────────────────────────────────────────────────────────────────┐
│ Toolbar: name • status • save • undo • redo • copy • paste • help │
├───────────────┬───────────────────────────────┬─────────────────────┤
│ Workflow      │ Main canvas or list           │ Inspector           │
│ outline       │ stages, transitions, actions  │ selected item       │
│               │                               │ details             │
├───────────────┴───────────────────────────────┴─────────────────────┤
│ Confidence tabs: Canvas • Validation • Preview • Simulation • Help │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.1 Toolbar

The toolbar gives authors the controls they expect from a good editor:

- save
- undo
- redo
- copy
- paste
- delete
- duplicate
- zoom in and out
- switch between graph view and list view
- open keyboard shortcuts
- open inline help

The toolbar also shows:

- workflow name
- dirty state
- validation status
- last saved state

### 5.2 Workflow outline

The outline is the quick navigation surface. It shows the workflow as a structured tree:

- workflow
- stages
- transitions
- reusable actions or policies if present

Authors use the outline to jump quickly to any part of the workflow. It also helps screen reader users move around the editor without depending on the visual graph.

### 5.3 Main canvas or list

This is the primary editing surface.

Authors can switch between:

- **Graph view** for visual editing of stages and transitions
- **List view** for accessible, compact, detail-friendly editing

Both views edit the same model. In the first role-first slice, graph view uses role-first lanes so authors read the workflow as responsibilities and handoffs rather than a generic node field.

### 5.4 Inspector

The inspector shows the editable properties for the selected item.

If the author selects a stage, the inspector shows stage details. If the author selects a transition, it shows transition details. If the author selects an action, it shows action details and parameters.

The inspector is the only persistent right-side detail surface. AI conversation stays outside the editor so the workspace remains clearly about authoring.

### 5.5 Tabbed confidence surfaces

The bottom confidence strip keeps the review tools in one predictable place. It provides tabs for:

- **Canvas** — returns focus to the main authoring workspace
- **Validation** — errors, warnings, affected items, proposal activity, and save status
- **Preview** — read-only runtime projection for the current stage
- **Simulation** — path-walking through the authored flow
- **Help** — shortcut and editing guidance

Messages are written in workflow language, for example:

- “Review stage has no exit transition.”
- “Submit action is missing a target stage.”
- “ID verification action needs a provider.”

Validation links should take the author back to the Canvas tab before focusing the affected stage, transition, or action so the jump target is never hidden behind another tab.

---

## 6. The V1 editing model

V1 is centred on six editable concepts.

| Concept | What the author edits | Why it matters |
| --- | --- | --- |
| Workflow | name, key, summary, actors, top-level settings | defines the overall workflow |
| Gateway | key, title, description, split/join kind, owning lane, waiting summary | shows where lanes branch or converge without pretending the gateway is a normal stage |
| Stage | key, title, purpose, actor, stage type, actions | defines a unit of work |
| Transition | source, target, trigger, conditions, guards | defines how the workflow moves |
| Action | action type, timing, behaviour | defines what happens in or between stages |
| Parameters | structured values for actions and forms | defines the exact behaviour |

The editor should always answer a simple question: **what will this change do to the workflow?**

---

## 7. Stage editing

A stage is the main building block in V1.

### 7.1 What a stage contains

Each stage can define:

- stage key
- stage title
- short description
- actor or owning role
- optional role gates for reviewer or back-stage access
- stage type such as form, review, decision, confirmation, or system work
- actions that run in the stage
- outbound transitions

### 7.2 Stage creation

In V1, authors can create a stage by:

- clicking “Add stage”
- inserting before or after a selected stage
- pasting a copied stage
- accepting a proposal that adds a stage

### 7.3 Stage editing behaviour

When a stage is selected, the inspector should make common changes easy:

- rename the stage
- change the actor
- change the stage type
- add or remove actions
- reorder actions
- review inbound and outbound transitions
- see where the stage sits in the full flow

### 7.4 Front stage and back stage

The editor must clearly distinguish work based on who is assigned:

- **Front stage** — public or member-facing work (actors like applicant, resident, citizen, member)
- **Back stage** — reviewer, caseworker, or system work (actors like reviewer, officer, caseworker, or stages with role gates)

Lane placement (front vs back stage) is **derived from the stage's actor and role-gate assignment**, not a separate editable or stored field. Authors set the actor and role gates, and the editor displays stages in the appropriate lane visually. Graph view shows these as role-first lanes with visual distinction through lane labels and styling. List view shows the same distinction as a filter.

---

## 8. Transition editing

Transitions define how the workflow moves between stages and gateways.

### 8.1 What a transition contains

A transition can define:

- source stage or gateway
- target stage or gateway
- trigger or action label
- optional condition or guard
- optional role requirement
- optional notes for author understanding

### 8.2 Transition editing behaviour

In V1, authors can:

- create a transition between stages or gateways
- rename the transition label
- change the target
- add simple conditions or guards
- delete a transition
- duplicate a transition when creating similar branches

### 8.3 Transition clarity

The editor should help authors understand branching quickly.

V1 graph view should show:

- direction
- labels
- branching points
- obvious dead ends

V1 list view should show the same information in a compact table.

### 8.4 Gateway representation in the next lane slice

For the next post-#82 behaviour slice, split and join gateways should be represented as **lane-owned diamond gateway nodes**, not as ordinary stages and not as hidden engine metadata.

- A **split gateway** sits in the lane that owns the branch point and visually fans transitions out to other lane paths.
- A **join gateway** sits in the lane that owns the merge point and visually gathers inbound lane paths before the next lane-owned step.
- Gateway cards should show the gateway name, description, split/join kind, and owning lane.
- Join gateways should also surface the waiting summary authors expect the runtime user to see while other lanes are still arriving.
- Authors should be able to connect stages to gateways, gateways to stages, and gateways to other gateways without inventing fake placeholder stages.
- Selecting a gateway should open gateway details in the inspector, but preview, simulation, and publish should keep following the existing stage-to-stage executable path until the later engine slices land.
- Editing rules should stop authors from creating confusing gateway-only routes before join semantics are implemented.

---

## 9. Action editing

Actions describe what happens inside a stage or when a transition is taken.

### 9.1 Common action types

V1 should support action editing for common workflow actions such as:

- assign team or role
- request review
- request more information
- send notification
- wait for an event or deadline
- call an external capability such as ID verification
- record an outcome
- update workflow status

The exact action catalogue can grow over time, but the editing pattern should be consistent from day one.

### 9.2 Action editing behaviour

Authors can:

- add an action
- choose an action type
- reorder actions
- duplicate an action
- remove an action
- edit parameters in the inspector

The action list should show short summaries so authors do not need to open every action to understand the stage.

Example summaries:

- “Assign to Planning Officers”
- “Send confirmation email”
- “Wait up to 10 working days”
- “Run external ID verification”

---

## 10. Parameter editing

Parameter editing is where the editor either feels friendly or falls back into raw configuration. V1 should keep it friendly.

### 10.1 Default parameter editing pattern

For every action, the inspector should show:

- a plain-language summary
- the important parameters first
- sensible defaults where possible
- inline validation
- help text for non-obvious fields

### 10.2 Parameter input types

V1 should support structured parameter inputs such as:

- text
- textarea
- number
- toggle
- select
- radio group
- date or duration
- key-value collections where genuinely needed

### 10.3 Advanced parameters

If an action has advanced parameters, the editor should hide them behind an “Advanced” section instead of showing a long wall of fields by default.

### 10.4 Validation style

Parameter validation should be specific and product-focused.

Good:

- “Notification action needs a template.”
- “Waiting action needs either a date or a duration.”

Not good:

- “parameters.template is required”

---

## 11. Forms-backed actions

Some actions are really mini form builders. V1 should treat them that way.

### 11.1 What this means

If an action collects or configures form data, the editor should present a forms-backed editing experience instead of a generic parameter list.

Examples include:

- request more information
- ask the applicant to upload evidence
- capture a reviewer decision reason
- configure an external check with a response form

### 11.2 V1 forms-backed editing experience

For forms-backed actions, the inspector should support:

- field list
- field label editing
- field type selection
- required toggle
- help text
- basic validation rules
- default value where relevant
- ordering
- simple conditional reveal where supported by the authored model

### 11.3 Forms-backed action summary

The stage should still show a simple summary in the main workspace, for example:

- “Request evidence form: 3 fields”
- “Decision note form: required textarea”

This keeps the main editor scannable.

---

## 12. Preview and simulation

Preview and simulation make the editor trustworthy.

### 12.1 Preview

V1 preview should let authors:

- preview the selected stage
- see the inferred runtime shell
- switch between relevant surfaces such as public, member, and business app views where applicable
- confirm that labels, actions, and forms look right

Preview is read-only. It is there to show what the authored model becomes.

### 12.2 Simulation

V1 simulation should let authors walk a likely path through the workflow:

- start from the beginning
- choose transitions
- follow conditions
- reach waiting, review, and outcome stages
- see validation blockers before saving

The goal is not a perfect runtime clone. The goal is fast design confidence.

### 12.3 What simulation is for

Simulation is especially important for:

- complex branching
- handoffs between front stage and back stage
- inserted actions such as external checks
- making sure a change did not strand the workflow

---

## 13. Editor affordances

V1 should include the editor behaviours people expect.

### 13.1 Undo and redo

Undo and redo are must-have features in V1.

They should cover:

- stage changes
- transition changes
- action changes
- parameter changes
- copy and paste operations
- accepted proposals

### 13.2 Copy and paste

Copy and paste are must-have features in V1.

Authors should be able to copy and paste:

- a stage
- one or more actions
- transition settings where compatible

When pasting a stage, the editor should create a safe new key and clearly show what still needs review.

### 13.3 Duplicate and insert

V1 should support duplicate, insert before, and insert after for fast workflow building.

### 13.4 Keyboard shortcuts

V1 should include a shortcut set for core actions:

- save
- undo
- redo
- copy
- paste
- delete
- duplicate
- switch view
- open help

### 13.5 Help

Help is a must-have feature in V1.

The editor should provide:

- a shortcut reference
- simple guidance for stages, transitions, and actions
- inline explanations for advanced fields
- empty-state guidance when starting a workflow

Help should be available without leaving the editor.

### 13.6 Explicit save

V1 should use **explicit save**, not autosave.

Authors need a clear point where they know what is being saved, especially when structural edits and proposals are involved.

---

## 14. Accessibility model

Accessibility is a first-class part of the workflow editor design.

### 14.1 Core approach

V1 uses a **dual-surface model**:

- **Graph view** for visual editing
- **List view** as the fully accessible structural view

Both views are first-class. List view is not a fallback afterthought.

### 14.2 Keyboard support

Every core workflow task must be possible by keyboard, including:

- moving through the workspace
- selecting stages, transitions, and actions
- editing in the inspector
- creating and deleting transitions
- reordering supported items
- switching between graph and list views
- opening preview and help
- using undo, redo, copy, and paste

### 14.3 Screen reader model

Screen reader users should be able to understand the workflow through:

- the outline
- the list view
- clear section labels
- descriptive action summaries
- status messages for structural changes

When a change happens, the editor should announce it in plain language, for example:

- “Stage ‘ID verification’ added after ‘Declaration’.”
- “Transition from ‘Review’ to ‘Approved’ deleted.”

### 14.4 Focus management

V1 must follow simple, predictable focus rules:

- focus moves into the inspector when it opens from the keyboard
- focus returns to the triggering item when the inspector closes
- modal dialogs trap focus and restore it on close
- proposal arrival never steals focus
- preview and help panels open and close predictably

### 14.5 Visible focus and contrast

All interactive controls need:

- clear visible focus indicators
- compliant colour contrast
- non-colour cues for meaning

Front-stage and back-stage differences must not rely on colour alone.

### 14.6 Validation accessibility

Validation must be:

- visible in context
- available from the validation rail
- linked to the affected item
- announced when relevant

Authors should be able to jump from a validation message directly to the stage, transition, or action that needs attention.

### 14.7 Accessibility validation in Storybook

The workflow editor component set should keep axe-core checks in Storybook as a routine quality gate. V1 should treat serious and critical accessibility issues as release blockers.

---

## 15. Proposal-based AI help

AI help is valuable, but the editor remains human-led.

In V1:

- the author can ask for help in natural language
- the editor shows the result as a proposal diff
- the author can review, accept, reject, or partly accept it
- the proposal uses the same validation and preview model as manual edits

This keeps AI useful without making it opaque.

---

## 16. Must-have V1 capabilities vs later enhancements

### 16.1 Must-have in V1

| Area | Must-have V1 capability |
| --- | --- |
| Workspace | single editor workspace with outline, graph/list, inspector, validation rail, preview/simulation |
| Stage editing | create, rename, move, duplicate, delete, assign actor, set stage type |
| Transition editing | create, relabel, retarget, delete, basic guard editing |
| Actions | add, remove, reorder, duplicate, edit common action types |
| Parameters | structured forms, defaults, validation, advanced section |
| Forms-backed actions | basic field editing, ordering, required flags, help text |
| Editor basics | save, undo, redo, copy, paste, duplicate, delete |
| Help | shortcuts and inline guidance |
| Accessibility | dual graph/list model, keyboard support, focus rules, live announcements, visible focus |
| Validation | persistent workflow-friendly errors and warnings |
| Confidence tools | stage preview and path simulation |
| AI help | proposal diff review, never hidden apply |

### 16.2 Later enhancements

These are useful, but not required for V1:

- real-time collaborative editing
- comments and annotations on stages
- richer bulk editing tools
- branch comparison across workflow versions
- reusable stage templates and action packs
- advanced simulation with richer sample data
- automatic layout tuning for very large workflows
- deep analytics and operational reporting
- macro recording or user-defined editor commands
- limited raw JSON round-tripping inside the main workspace
- autosave once proposal and conflict rules are mature

---

## 17. V1 success statement

V1 succeeds if an author can open the workflow editor and confidently do the following without touching raw JSON:

- add or change stages
- wire transitions
- configure actions
- edit parameters through clear forms
- configure forms-backed actions
- preview the result
- simulate the main path
- undo mistakes
- copy and reuse parts of the workflow
- use the editor accessibly
- save with confidence

That is the bar for the first release.
