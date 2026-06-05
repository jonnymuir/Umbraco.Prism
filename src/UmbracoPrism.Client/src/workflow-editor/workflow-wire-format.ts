/**
 * The editor now works directly against the persisted WorkflowDefinition
 * contract, so load/save is a straight JSON pass-through.
 */

import type { AuthoredWorkflow } from './types.js';

export function serialiseWorkflow(workflow: AuthoredWorkflow): Record<string, unknown> {
  return workflow as unknown as Record<string, unknown>;
}

export function normaliseWorkflow(raw: Record<string, unknown>): AuthoredWorkflow {
  return raw as unknown as AuthoredWorkflow;
}
