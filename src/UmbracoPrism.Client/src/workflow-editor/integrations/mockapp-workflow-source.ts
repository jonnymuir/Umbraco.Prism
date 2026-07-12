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

// The shape UmbracoPrism.WorkflowRuntime.Services.WorkflowSaveOutcome serializes to — returned
// by both /mockapp/workflows/{key} and /prism/workflow-authoring/workflows/{key} on a version
// conflict (409). Not a ProblemDetails payload, so it's parsed separately.
type WorkflowSaveOutcomePayload = {
  status?: unknown;
  errors?: unknown;
  currentVersion?: unknown;
  newVersion?: unknown;
};

function parseConflictOutcome(payload: WorkflowSaveOutcomePayload, workflowKey: string): WorkflowSaveError {
  const currentVersion = typeof payload.currentVersion === 'number' ? payload.currentVersion : null;
  const detailLines = readStructuredErrorLines(payload.errors);
  const summary = sanitiseWorkflowSaveErrorText(detailLines[0])
    ?? `“${workflowKey}” was changed elsewhere since you loaded it${currentVersion != null ? ` (now at version ${currentVersion})` : ''}.`;

  return new WorkflowSaveError({
    title: 'This workflow changed elsewhere',
    summary,
    detailLines: detailLines.filter(line => line !== summary),
    statusCode: 409,
    isConflict: true,
    currentVersion,
  });
}

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
      if (response.status === 409) {
        return parseConflictOutcome(JSON.parse(payloadText) as WorkflowSaveOutcomePayload, workflowKey);
      }

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
    // /mockapp/workflows serializes WorkflowSourceSummary(DefinitionKey, DisplayName) — there's no
    // separate "host-facing key" concept on this host, so workflowKey and definitionKey are the
    // same string here. The naive `as WorkflowSummary[]` this replaced compiled fine (TypeScript
    // doesn't check across a JSON boundary) but left every option's `workflowKey` undefined at
    // runtime, so the shell's `option.workflowKey === this._draftWorkflowKey` selected-match never
    // fired and the <select> silently fell back to its first option regardless of which workflow
    // was actually loaded.
    const summaries = (await response.json()) as Array<{ definitionKey: string; displayName: string }>;
    return summaries.map(({ definitionKey, displayName }) => ({
      workflowKey: definitionKey,
      definitionKey,
      displayName,
    }));
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

  /**
   * Cheap poll target: reads just the version, not the full definition. Uses the
   * definitionKey-keyed toolkit route rather than /mockapp/workflows/* — both read from the
   * same underlying store, so either is correct, but this one exists specifically for this.
   */
  async checkVersion(workflowKey: string): Promise<number | null> {
    const response = await fetch(
      `${this.base}/prism/workflow-authoring/workflows/${encodeURIComponent(workflowKey)}/version`,
      { headers: { Accept: 'application/json' }, credentials: 'same-origin' }
    );
    if (!response.ok) {
      return null;
    }
    const payload = (await response.json()) as { version?: unknown };
    return typeof payload.version === 'number' ? payload.version : null;
  }
}
