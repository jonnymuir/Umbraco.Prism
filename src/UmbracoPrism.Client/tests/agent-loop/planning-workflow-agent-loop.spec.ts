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
      // TODO: Replace story ID once Isabelle publishes the conversation-pane story.
      // Expected story: 'workflow-editor-conversation-pane--with-mocked-proposal'
      // The story must:
      //   1. Render <prism-conversation-pane> with data-testid="conversation-pane"
      //   2. Expose data-testid="conversation-input" (textarea/input)
      //   3. Expose data-testid="conversation-send" (button)
      //   4. On submit, fire an 'nl-request' CustomEvent and mount a mocked
      //      <prism-proposal-diff data-proposal-id="mock-proposal-1"> inline.
      //
      // Until the story exists this test will fail at the navigation step.
      // When the story lands, remove this comment block and the skip below.

      // TODO: unskip when Isabelle's conversation-pane story is available
      test.skip(
        true,
        'TODO: Awaiting Isabelle\'s conversation-pane Storybook story (workflow-editor-conversation-pane--with-mocked-proposal)'
      );

      await page.goto(storyUrl('workflow-editor-conversation-pane--with-mocked-proposal'));

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
      // TODO: Replace story ID once Isabelle publishes the failing-proposal story.
      // Expected story: 'workflow-editor-conversation-pane--with-failing-proposal'
      // The story must render a <prism-proposal-diff> with validation.status = "fail"
      // and data-testid="proposal-accept-all" must be rendered but disabled.

      // TODO: unskip when Isabelle's failing-proposal story is available
      test.skip(
        true,
        'TODO: Awaiting Isabelle\'s conversation-pane Storybook story (workflow-editor-conversation-pane--with-failing-proposal)'
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
