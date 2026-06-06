// Host integration EXAMPLE — not part of the `@umbraco-prism/client` boundary
// surface. The reference MockBusinessApp uses this implementation to wire its
// `/mockapp/workflows/*` endpoints into the editor's `WorkflowSource` contract.
// Real downstream apps fork/copy this file into their own bundle.

import {
  WorkflowSaveError,
  sanitiseWorkflowSaveErrorLines,
  sanitiseWorkflowSaveErrorText,
  type WorkflowSource,
  type WorkflowSummary,
} from '../workflow-source.js';
import type { AuthoredWorkflow } from '../types.js';
import { hydrateWorkflowDefinition } from '../types.js';
import { serializeAuthoredWorkflow } from '../workflow-canonical-json.js';

type ProblemDetailsPayload = {
  title?: unknown;
  detail?: unknown;
  status?: unknown;
  traceId?: unknown;
  summary?: unknown;
  message?: unknown;
  errors?: unknown;
  extensions?: {
    traceId?: unknown;
    errors?: unknown;
  };
};

function readStructuredErrorLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return sanitiseWorkflowSaveErrorLines(value.filter((entry): entry is string => typeof entry === 'string'));
  }

  if (value && typeof value === 'object') {
    return Object.entries(value as Record<string, unknown>)
      .flatMap(([field, messages]) => {
        if (Array.isArray(messages)) {
          return messages
            .filter((message): message is string => typeof message === 'string')
            .map(message => field ? `${field}: ${message}` : message);
        }

        return typeof messages === 'string'
          ? [field ? `${field}: ${messages}` : messages]
          : [];
      });
  }

  return typeof value === 'string' ? sanitiseWorkflowSaveErrorLines([value]) : [];
}

function parseProblemDetails(payload: ProblemDetailsPayload, statusCode: number, workflowKey: string): WorkflowSaveError {
  const title = sanitiseWorkflowSaveErrorText(typeof payload.title === 'string' ? payload.title : null)
    ?? 'We couldn’t save this workflow';
  const summary = sanitiseWorkflowSaveErrorText(
    typeof payload.summary === 'string'
      ? payload.summary
      : typeof payload.detail === 'string'
        ? payload.detail
        : typeof payload.message === 'string'
          ? payload.message
          : null
  ) ?? `The host app rejected the save request for “${workflowKey}”.`;
  const detailLines = sanitiseWorkflowSaveErrorLines([
    ...readStructuredErrorLines(payload.errors),
    ...readStructuredErrorLines(payload.extensions?.errors),
  ]).filter(line => line !== summary);
  const traceId = sanitiseWorkflowSaveErrorText(
    typeof payload.traceId === 'string'
      ? payload.traceId
      : typeof payload.extensions?.traceId === 'string'
        ? payload.extensions.traceId
        : null
  );

  return new WorkflowSaveError({
    title,
    summary,
    detailLines,
    traceId,
    statusCode,
  });
}

async function buildSaveError(response: Response, workflowKey: string): Promise<WorkflowSaveError> {
  const payloadText = await response.text().catch(() => '');
  const contentType = response.headers.get('content-type') ?? '';
  const fallbackSummary = sanitiseWorkflowSaveErrorText(payloadText)
    ?? `Save failed (${response.status} ${response.statusText}).`;

  if (contentType.includes('json') || payloadText.trim().startsWith('{')) {
    try {
      const payload = JSON.parse(payloadText) as ProblemDetailsPayload;
      return parseProblemDetails(payload, response.status, workflowKey);
    } catch {
      // Fall through to the plain-text fallback.
    }
  }

  return new WorkflowSaveError({
    title: 'We couldn’t save this workflow',
    summary: fallbackSummary,
    statusCode: response.status,
  });
}

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
    const body = serializeAuthoredWorkflow(workflow);
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
      throw await buildSaveError(response, workflowKey);
    }
  }
}
