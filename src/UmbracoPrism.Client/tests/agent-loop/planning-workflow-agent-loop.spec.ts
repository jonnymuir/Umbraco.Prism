/**
 * Agent-loop journey stubs — Planning Workflow
 *
 * These tests form the seam between the NL → proposal → apply pipeline and the
 * editor UI components. They run against Storybook stories (baseURL: http://127.0.0.1:6006).
 *
 * Selector contract (from docs/design/workflow-editor-v1/01-authoring-ux.md §10 Test Hooks):
 *   data-testid="conversation-pane"    → <prism-conversation-pane> root
 *   data-testid="conversation-input"   → NL text input
 *   data-testid="conversation-send"    → Send button
 *   data-proposal-id="{id}"            → <prism-proposal-diff> root
 *   data-testid="proposal-accept-all"  → Accept all button (disabled when validation=fail)
 *   data-testid="proposal-reject"      → Reject button
 *
 * Hooks not yet implemented by Isabelle are clearly marked TODO.
 * Tests that depend on real projection infrastructure are marked test.fixme().
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// Story URL helpers
// ---------------------------------------------------------------------------

/** Storybook iframe URL for a given story. */
function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

// ---------------------------------------------------------------------------
// 1. NL → proposal → diff preview
// ---------------------------------------------------------------------------

test.describe('Agent-loop: NL request → proposal diff', () => {
  test(
    'Author can request a workflow change in natural language and see a proposal diff before applying',
    async ({ page }) => {
      // The closest shipped story is 'workflow-editor-conversation-pane--with-proposal'
      // (src/UmbracoPrism.Client/src/workflow-editor/prism-conversation-pane.stories.ts).
      // However, that story pre-populates the proposal in its play() function — it does not
      // test the NL input → agent call → proposal render pipeline end-to-end.
      //
      // What is needed before this test can be un-skipped:
      //   1. A Storybook story (or a MockBusinessApp page) that starts with an empty
      //      conversation and responds to a submitted nl-request event with a mocked
      //      proposal envelope, simulating Blathers' /api/workflow-authoring endpoint.
      //   2. Stable data-prism-conversation-input and data-prism-component="proposal-diff"
      //      hooks (or role-based equivalents) for the NL send and diff display surfaces.
      //
      // This is a Wave 1 integration concern. The component-level contract is already
      // exercised by the planning-workflow-editor.walkthrough.spec.ts via LiveAppHost.
      test.skip(
        true,
        "Awaiting an NL-input→proposal Storybook story: the shipped '--with-proposal' story pre-populates the proposal " +
        'and does not exercise the NL submission pipeline (Wave 1 foundation)'
      );

      await page.goto(storyUrl('workflow-editor-conversation-pane--with-proposal'));

      const conversationPane = page.locator('[data-testid="conversation-pane"]');
      await expect(conversationPane).toBeVisible();

      // Type a natural language change request
      const nlInput = page.locator('[data-testid="conversation-input"]');
      await expect(nlInput).toBeVisible();
      await nlInput.fill('Add an identity verification step before the reviewer assessment');

      // Submit — should fire nl-request event; story intercepts and mounts a mocked proposal
      const nlRequestFired = page.waitForEvent('console', msg =>
        msg.text().includes('nl-request') || msg.type() === 'log'
      );
      await page.locator('[data-testid="conversation-send"]').click();

      // Proposal diff must appear in the conversation thread
      const proposalDiff = page.locator('[data-proposal-id]').first();
      await expect(proposalDiff).toBeVisible({ timeout: 5_000 });

      // The diff must contain at least one hunk describing the proposed change
      await expect(proposalDiff.locator('[data-hunk-id]').first()).toBeVisible();

      // Accept-all button must be enabled (mocked proposal has validation status = pass)
      const acceptAll = page.locator('[data-testid="proposal-accept-all"]');
      await expect(acceptAll).toBeEnabled();
    }
  );

  // ---------------------------------------------------------------------------
  // 2. Validation-fail → accept disabled
  // ---------------------------------------------------------------------------

  test(
    'Author cannot apply a proposal whose validation status is fail',
    async ({ page }) => {
      // The 'workflow-editor-conversation-pane--with-failing-proposal' story does not
      // exist in the current Storybook (prism-conversation-pane.stories.ts ships
      // 'empty' and 'with-proposal' only). The 'with-proposal' story uses STUB_PROPOSAL
      // which has validation.status = 'pass', not 'fail'.
      //
      // What is needed before this test can be un-skipped:
      //   A new Storybook story that renders a <prism-proposal-diff> with a proposal
      //   whose validationResult.status is "fail", causing the Accept all button to
      //   be rendered in a disabled state. Isabelle owns this story.
      test.skip(
        true,
        "Awaiting a 'with-failing-proposal' Storybook story: the shipped stories only cover 'empty' and 'with-proposal' " +
        "(validation status 'pass'); a story with status 'fail' is required to assert the disabled Accept all state (Wave 1 foundation)"
      );

      await page.goto(storyUrl('workflow-editor-conversation-pane--with-failing-proposal'));

      const proposalDiff = page.locator('[data-proposal-id]').first();
      await expect(proposalDiff).toBeVisible();

      // Validation status must be communicated accessibly
      await expect(proposalDiff.getByRole('status')).toContainText(/fail/i);

      // Accept-all must be present but disabled — authors cannot apply an invalid proposal
      const acceptAll = page.locator('[data-testid="proposal-accept-all"]');
      await expect(acceptAll).toBeDisabled();

      // Reject is always available regardless of validation status
      await expect(page.locator('[data-testid="proposal-reject"]')).toBeEnabled();
    }
  );

  // ---------------------------------------------------------------------------
  // 3. ID&V waiting state — full journey stub (depends on later waves)
  // ---------------------------------------------------------------------------

  test.fixme(
    'Applicant cannot submit planning application without identity verification when ID&V step is enabled',
    async ({ page }) => {
      // RATIONALE: This test requires the full agent-apply pipeline to be operational:
      //   1. The planning.workflow.json fixture must be projected to a live seed.
      //   2. The workflow.draft-proposal MCP tool must exist and be callable.
      //   3. The proposal envelope apply step must write the updated seed to disk.
      //   4. The TestSite planning application journey must load the updated workflow.
      //
      // None of these are available in the V1 foundation slice. This stub is marked
      // test.fixme() so it appears in the test report as a known gap, not a failure.
      //
      // When implementing: navigate the planning journey to submission, assert that
      // the identity-verification waiting state is rendered before the applicant can
      // reach the reviewer-assessment stage. Use the GDS journey pattern from
      // tests/workflow-gds-journey.spec.ts as the structural template.
      //
      // Selector to assert: getByRole('heading', { name: /identity verification/i })
      // or a data-node-type="waiting" locator once the ID&V stage is projected.
    }
  );
});
