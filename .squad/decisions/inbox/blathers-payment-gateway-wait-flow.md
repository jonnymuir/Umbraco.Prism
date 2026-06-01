---
author: blathers
date: 2026-06-01
status: proposed
area: reference-workflows
issue: 82
---

# Decision: Payment reference flow now waits at the join gateway

## Context

The payment reference example needed to match the product story Jonny signed off:
the web user submits payment details, waits at a real join gateway, the payments
team confirms the payment in the business app, and only then does the user move
to the completion screen.

The gateway projector fix was already in place, but this slice still needed the
payment authored flow updated and the runtime path checked end to end so the web
user saw the waiting state while the back-office confirmation was still pending.

## Decision

- The payment reference workflow now uses:
  - a parallel split from `enter-details`
  - an applicant-side join gateway `await-payment-confirmation`
  - a payments-team confirmation stage `confirm-payment-received`
  - a wait-for-all join release into `payment-complete`
- The waiting message now lives on the join gateway, not on a fake waiting stage.
- The payment entry, payments confirmation, and completion steps now use explicit
  component trees with product-facing fields and copy.
- The business app runtime path now honours gateway targets in this flow so the
  applicant sees the waiting state while the payments lane is outstanding, and
  the join releases once the confirmation arrives.

## Consequence

The payment demo now behaves like a real handoff story instead of a linear
processing placeholder. The example proves the intended split → wait at join →
back-office confirm → release pattern that the other reference workflows can
follow in later slices.
