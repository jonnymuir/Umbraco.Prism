/**
 * V1 SCAFFOLDING — Mock agent drafter for the planning workflow walkthrough.
 *
 * Real drafting routes through GitHub Copilot via Tangy's MCP surface in a
 * later slice.  This file exists ONLY to make the end-to-end planning walkthrough
 * work offline while Blathers' `workflow.draft-proposal` endpoint is not yet
 * shipped.  Delete or replace this entire file once the real drafting pipeline
 * is wired.
 *
 * Canned prompts:
 *   • Match "id&v" | "identity" → propose inserting an id-verification stage
 *     BEFORE the "submitted" stage.
 *   • Anything else → null (caller should show the friendly message below).
 */

import type { AuthoredWorkflow, AuthoredStage, ProposalEnvelope } from './types.js';

export const V1_UNRECOGNISED_MESSAGE =
  "V1 only recognises one canned change — try 'insert ID&V before submission'.";

/**
 * Returns a ProposalEnvelope if the text matches a V1 canned prompt, or null
 * if the text is not recognised.  The caller is responsible for displaying
 * `V1_UNRECOGNISED_MESSAGE` when null is returned.
 */
export function draftProposal(
  nlText: string,
  workflow: AuthoredWorkflow
): ProposalEnvelope | null {
  const lower = nlText.toLowerCase();
  if (!lower.includes('id&v') && !lower.includes('identity')) return null;

  const submittedIndex = workflow.stages.findIndex(s => s.stageKey === 'submitted');
  const insertPath =
    submittedIndex >= 0 ? `/stages/${submittedIndex}` : `/stages/${workflow.stages.length}`;

  const insertBeforeKey = submittedIndex >= 0 ? 'submitted' : null;
  const insertAfterKey =
    submittedIndex > 0 ? workflow.stages[submittedIndex - 1].stageKey : null;

  const idvStage: AuthoredStage = {
    stageKey: 'id-verification',
    displayName: 'Identity Verification',
    kind: 'Question',
    fields: [
      {
        fieldKey: 'id-document-uploaded',
        label: 'Identity document',
        kind: 'FileUpload',
        required: true,
        options: [],
      },
    ],
    roleGates: [],
    editorComment: 'V1 canned ID&V stage — verify applicant identity before submission.',
  };

  const now = new Date().toISOString();

  const proposal: ProposalEnvelope = {
    id: `v1-canned-idv-${Date.now()}`,
    createdAt: now,
    agent: {
      kind: 'human-assisted',
      identity: 'v1-mock-drafter',
      sessionRef: 'v1-walkthrough',
    },
    targetWorkflowId: workflow.definitionKey,
    rationale:
      'Insert a mandatory Identity Verification (ID&V) stage before the application submission ' +
      'stage so that applicant identity is confirmed before the case enters the system.',
    ops: [
      {
        op: 'insert-stage',
        path: insertPath,
        before: insertBeforeKey ?? undefined,
        value: idvStage,
      },
      {
        // Upsert the transition from the preceding stage into id-verification.
        op: 'update-transition',
        path: `/transitions/${workflow.transitions.length}`,
        value: {
          fromStage: insertAfterKey ?? 'check-answers',
          toStage: 'id-verification',
          action: 'continue',
        },
      },
      {
        // Upsert the transition from id-verification into the next stage.
        op: 'update-transition',
        path: `/transitions/${workflow.transitions.length + 1}`,
        value: {
          fromStage: 'id-verification',
          toStage: insertBeforeKey ?? 'submitted',
          action: 'continue',
        },
      },
    ],
    placement: {
      insertBeforeStageKey: insertBeforeKey,
      insertAfterStageKey: insertAfterKey,
      handoffId: null,
      transitionId: null,
    },
    validationResult: {
      status: 'pass',
      checkedAt: now,
      errors: [],
    },
    previewArtifactRef: null,
  };

  return proposal;
}
