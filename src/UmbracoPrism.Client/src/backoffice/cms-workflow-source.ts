// Backoffice WorkflowSource — the editor host implementation for CMS Workflow, Prism's
// Umbraco-only workflow implementation. Unlike MockBusinessAppWorkflowSource (cookie-auth,
// same-origin), the backoffice's Management API is Bearer-token authenticated, and the token
// can rotate between calls — `getToken` is called fresh on every request rather than captured
// once at construction, so a long-open editor session never sends a stale token.

import {
  WorkflowSaveError,
  sanitiseWorkflowSaveErrorLines,
  sanitiseWorkflowSaveErrorText,
  type WorkflowSource,
  type WorkflowSummary,
} from '../workflow-editor/workflow-source.js';
import type { AuthoredWorkflow } from '../workflow-editor/types.js';
import { hydrateWorkflowDefinition } from '../workflow-editor/types.js';
import { serializeAuthoredWorkflow } from '../workflow-editor/workflow-canonical-json.js';

const API_BASE = '/umbraco/management/api/v1/prism/cms-workflows';

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

// The shape UmbracoPrism.WorkflowRuntime.Services.WorkflowSaveOutcome serializes to.
type WorkflowSaveOutcomePayload = {
  status?: unknown;
  diagnostics?: unknown;
  currentVersion?: unknown;
  newVersion?: unknown;
};

function readStructuredErrorLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return sanitiseWorkflowSaveErrorLines(
      value.map(entry =>
        entry && typeof entry === 'object' && 'message' in entry && typeof (entry as { message?: unknown }).message === 'string'
          ? (entry as { message: string }).message
          : typeof entry === 'string'
            ? entry
            : null
      )
    );
  }

  return typeof value === 'string' ? sanitiseWorkflowSaveErrorLines([value]) : [];
}

function parseConflictOutcome(payload: WorkflowSaveOutcomePayload, workflowKey: string): WorkflowSaveError {
  const currentVersion = typeof payload.currentVersion === 'number' ? payload.currentVersion : null;
  const detailLines = readStructuredErrorLines(payload.diagnostics);
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
  ) ?? `The backoffice rejected the save request for “${workflowKey}”.`;
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

  return new WorkflowSaveError({ title, summary, detailLines, traceId, statusCode });
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

      return parseProblemDetails(JSON.parse(payloadText) as ProblemDetailsPayload, response.status, workflowKey);
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

export class UmbracoBackofficeWorkflowSource implements WorkflowSource {
  constructor(private readonly getToken: () => Promise<string | undefined>) {}

  private async authHeaders(extra: Record<string, string> = {}): Promise<Record<string, string>> {
    const token = await this.getToken();
    return {
      ...extra,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    };
  }

  async list(): Promise<WorkflowSummary[]> {
    const response = await fetch(API_BASE, {
      headers: await this.authHeaders({ Accept: 'application/json' }),
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to list CMS workflows (${response.status} ${response.statusText}).`);
    }
    const summaries = (await response.json()) as Array<{ definitionKey: string; displayName: string }>;
    return summaries.map(({ definitionKey, displayName }) => ({
      workflowKey: definitionKey,
      definitionKey,
      displayName,
    }));
  }

  async load(workflowKey: string): Promise<AuthoredWorkflow> {
    const response = await fetch(`${API_BASE}/${encodeURIComponent(workflowKey)}`, {
      headers: await this.authHeaders({ Accept: 'application/json' }),
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to load CMS workflow '${workflowKey}' (${response.status} ${response.statusText}).`);
    }
    const payload = (await response.json()) as Record<string, unknown>;
    return hydrateWorkflowDefinition(payload as unknown as AuthoredWorkflow);
  }

  async save(workflowKey: string, workflow: AuthoredWorkflow): Promise<void> {
    const body = serializeAuthoredWorkflow(workflow);
    const response = await fetch(`${API_BASE}/${encodeURIComponent(workflowKey)}`, {
      method: 'PUT',
      headers: await this.authHeaders({ 'Content-Type': 'application/json', Accept: 'application/json' }),
      credentials: 'same-origin',
      body,
    });
    if (!response.ok) {
      throw await buildSaveError(response, workflowKey);
    }
  }

  async checkVersion(workflowKey: string): Promise<number | null> {
    const response = await fetch(`${API_BASE}/${encodeURIComponent(workflowKey)}/version`, {
      headers: await this.authHeaders({ Accept: 'application/json' }),
      credentials: 'same-origin',
    });
    if (!response.ok) {
      return null;
    }
    const payload = (await response.json()) as { version?: unknown };
    return typeof payload.version === 'number' ? payload.version : null;
  }
}
