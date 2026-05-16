---
name: "third-party-case-step-up-assurance"
description: "Design third-party, one-off case journeys with low-friction access and reviewer-backed step-up assurance"
domain: "security"
confidence: "high"
source: "earned"
---

## Context

Use this when a non-member third party needs to start, save, and resume a sensitive case journey such as bereavement, executor contact, or representative-led updates. The design problem is usually how to avoid over-building registration while still preventing impersonation, privacy leaks, and unsafe disclosure.

## Patterns

### Separate the actor from the subject

- Treat the notifier or representative as the authenticated workflow actor.
- Treat the member or deceased person as the linked subject in server-side case data.
- Never let browser-submitted member identifiers become trusted ownership facts.

### Stage assurance by action risk

- Use lightweight channel verification for save/resume and case communications.
- Use stronger evidence and reviewer judgement before confirming a member match, exposing sensitive status, or progressing financial actions.
- Keep payment or benefit-release activity outside the low-friction entry flow.

### Treat magic links and OTPs as channel proof, not full identity proof

- A verified email or phone proves control of that inbox or device at that moment.
- It does not by itself prove legal authority, entitlement, or the truth of the notifier's claim.
- Pair low-friction access with documentary evidence and reviewer checkpoints for sensitive milestones.

### Prefer case-scoped access over mandatory full registration

- Issue a case reference and establish a case-scoped session once contact verification succeeds.
- Let the user resume through a hub or case list tied to that verified session.
- Add full registration only if there is a genuine long-lived portal need.

### Reveal only generic status before verification thresholds are met

- Safe examples: `received`, `under review`, `more information needed`, `closed`.
- Unsafe examples: pension value, nomination details, benefit eligibility, internal match confidence, or confirmation that the subject is definitely a member.

### Keep workflow state and case state separate

- Use the workflow engine for user-facing progression.
- Keep case ownership, evidence metadata, reviewer notes, match decisions, and anti-fraud signals in separate domain tables.
- Store documents outside workflow field payloads and reference them by metadata only.

## Anti-Patterns

- Forcing bereaved or one-off third parties to create a password account before they can notify you.
- Confirming membership or entitlements as soon as someone presents obituary-level facts.
- Treating a case reference or `instanceId` as authentication.
- Using knowledge-based questions alone as the main online assurance control.
- Returning reviewer notes, fraud flags, or candidate member matches to the browser.
