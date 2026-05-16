---
name: "third-party-notifier-case-access"
description: "Design third-party initiated workflows that need proofed access, member linking, and resumable case tracking without mandatory full registration"
domain: "workflow-backend"
confidence: "medium"
source: "observed"
---

## Context

Use this skill when a workflow is started by someone acting about another person or subject, such as bereavement reporting, representative claims, or delegated notifications.

## Patterns

- Separate the **authenticated actor** from the **domain subject**:
  - actor = notifier or representative
  - subject = member, claimant, deceased person, policy, or account being linked
- Do not trust browser-submitted subject identifiers as proof of ownership; perform matching server-side.
- Prefer lightweight verified case access first:
  - email magic link
  - SMS OTP
  - short-lived case-scoped session
- Require full registration only when there is a real long-lived portal need, not just to make save/resume work.
- Default to a **hybrid** recovery model:
  - verified-session resume as the primary digital path
  - case reference plus claimant checks as fallback
- Create the case and workflow instance once the contact channel is verified, then use hub or prompt patterns for resume.
- Keep domain case state outside workflow field payloads:
  - workflow instance = journey position
  - case aggregate = operational truth, evidence, notes, linkage, outcomes
- Store uploaded evidence as document references plus metadata, not raw workflow answers.
- Use reviewer-gated transitions for match confirmation, request-more-info loops, and final completion.
- Step up verification only when the case crosses into sensitive disclosure, contested authority, or payment-affecting actions.

## Examples

- `docs/design/pasa-death-process.md`
- `docs/design/workflow-forms-engine-backend.md`
- `docs/design/workflow-forms-engine-security.md`
- `docs/design/workflow-hub-and-conditional-fields.md`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/information-request.json`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/payment-demo.json`

## Anti-Patterns

- Forcing bereaved or delegated users into permanent account registration for a one-off notification.
- Treating the linked member record as if it were the authenticated workflow user.
- Storing reviewer notes, case matching decisions, or document blobs inside generic workflow field values.
