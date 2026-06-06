# Decision: Fix Community Enquiry Heading Mismatches (Playwright CI Failures)

**Date:** 2026-06-06  
**Author:** Isabelle (Frontend Dev)  
**Status:** Applied

---

## What Was Wrong

Three categories of Playwright test failure, all traced to heading lookup failures in the community-enquiry workflow:

1. `localhost-auth-session.spec.ts` — tests at lines 56 and 78 expected heading `'Your details'` on `/get-in-touch` (initial state of community enquiry). The rendered heading was `'Tell us about your enquiry'`.

2. `community-enquiry.walkthrough.spec.ts` — expected `'Your details'` initially and `'Thank you'` after clicking Submit. The seed routed submit to an `under-review` state (displayName `'Your enquiry is with us'`) that was gated to the reviewer queue.

3. `workflow-administration.walkthrough.spec.ts` and `home-entry.walkthrough.spec.ts` — both expected `'Your details'` for the `/get-in-touch` entry point.

The `workflow-gds-journey.spec.ts` tests check planning workflow headings (`Declaration`, `Application Form`, `Check your answers`, `Application submitted`) — all of which correctly match `planning.json`. No fix was needed there.

---

## Why It Happened

In commit `84ba5eb` ("Foundation: define workflow schema and authoring data model"), the test expectations were updated to use cleaner GDS-style headings matching the C# test fixture (`community-enquiry.workflow.json`):
- Initial state: `'Your details'`
- Post-submission: `'Thank you'`

However, the runtime seed file (`community-enquiry.json` in `workflow-seeds/`) was **not updated** at that time. The seed retained `'Tell us about your enquiry'` as the initial state displayName, and after submission routed to `under-review` (a multi-actor reviewer queue with displayName `'Your enquiry is with us'`).

During the v1.10.0 workflow migrations (commit `66b374f`), the seed was migrated to the new queues/routes format, but the heading mismatches carried forward unchanged. The migration also added a full reviewer flow (`under-review` → `complete`) that deepened the routing divergence from the simple 2-state flow the tests expected.

---

## What Was Fixed

Updated `src/UmbracoPrism.MockBusinessApp/workflow-seeds/community-enquiry.json` to align with the C# fixture and test expectations:

1. **Renamed `collecting-details.displayName`** from `'Tell us about your enquiry'` to `'Your details'`.

2. **Replaced the multi-actor reviewer flow** (`under-review` + `complete` states, reviewer gateways, reviewer queue) with a clean 2-state applicant flow:
   - `collecting-details` (Question) → gateway `route-submit-enquiry` → `submitted` (Confirmation)
   - `submitted.displayName = 'Thank you'`

3. **Removed** `under-review`, `complete`, `route-from-collecting-details`, `route-save-draft`, `route-from-under-review`, `join-return-to-form` gateways, and the `business-user` reviewer queue.

The form components on `collecting-details` (inset-text, fieldsets, details, warning-text) are fully preserved.

---

## Scope of Impact

- **Fixes:** All `'Your details'` heading assertions on `/get-in-touch` and all `'Thank you'` post-submission assertions in the walkthrough and auth-session test suites.
- **Backend tests unaffected:** `CommunityEnquirySeed_ModelsExplanatoryCopyAsComponents` and `DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata` still pass — the `collecting-details` component structure is unchanged.
- **Storybook/editor tests unaffected:** The `COMMUNITY_ENQUIRY_WORKFLOW` TypeScript fixture in `fixtures/index.ts` already matched the corrected 2-state design; 15/15 migrated-workflow Playwright specs pass.
- **Reviewer flow:** Removed from the applicant-facing seed. The community-enquiry demo is intended as a simple contact form (submit → thank you), not a multi-actor review workflow. Reviewer functionality is available via the MockBusinessApp admin TUI for those scenarios.
