// Host integration EXAMPLE — not part of the `@umbraco-prism/client` boundary
// surface. The reference MockBusinessApp uses this implementation to wire its
// `/mockapp/workflows/*` endpoints into the editor's `WorkflowSource` contract.
// Real downstream apps fork/copy this file into their own bundle.

import type { WorkflowSource, WorkflowSummary } from '../workflow-source.js';
import type { AuthoredWorkflow } from '../types.js';
import { hydrateWorkflowDefinition } from '../types.js';

export interface MockBusinessAppWorkflowSourceOptions {
  /** Origin override for cross-origin development. Defaults to same-origin. */
  baseUrl?: string;
}

export class MockBusinessAppWorkflowSource implements WorkflowSource {
  private readonly base: string;

  constructor(options: MockBusinessAppWorkflowSourceOptions = {}) {
    this.base = (options.baseUrl ?? '').replace(/\/$/, '');
  }

  async list(): Promise<WorkflowSummary[]> {
    const response = await fetch(`${this.base}/mockapp/workflows`, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to list workflows (${response.status} ${response.statusText}).`);
    }
    return (await response.json()) as WorkflowSummary[];
  }

  async load(workflowKey: string): Promise<AuthoredWorkflow> {
    const response = await fetch(`${this.base}/mockapp/workflows/${encodeURIComponent(workflowKey)}`, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to load workflow '${workflowKey}' (${response.status} ${response.statusText}).`);
    }
    const payload = (await response.json()) as Record<string, unknown>;
    return hydrateWorkflowDefinition(payload as unknown as AuthoredWorkflow);
  }

  async save(workflowKey: string, workflow: AuthoredWorkflow): Promise<void> {
    const body = JSON.stringify(workflow);
    const response = await fetch(`${this.base}/mockapp/workflows/${encodeURIComponent(workflowKey)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      credentials: 'same-origin',
      body,
    });
    if (!response.ok) {
      const detail = await response.text().catch(() => '');
      throw new Error(`Failed to save workflow '${workflowKey}' (${response.status} ${response.statusText}). ${detail}`.trim());
    }
  }
}
