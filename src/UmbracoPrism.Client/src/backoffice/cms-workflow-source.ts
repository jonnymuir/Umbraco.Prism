// Backoffice WorkflowSource — the editor host implementation for CMS Workflow, Prism's
// Umbraco-only workflow implementation. Unlike MockBusinessAppWorkflowSource (cookie-auth,
// same-origin), the backoffice's Management API is Bearer-token authenticated, and the token
// can rotate between calls — `getToken` is called fresh on every request rather than captured
// once at construction, so a long-open editor session never sends a stale token.

import {
  WorkflowSaveError,
  sanitiseWorkflowSaveErrorLines,
  sanitiseWorkflowSaveErrorText,
  type WorkflowSaveErrorDetail,
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

// A WorkflowDiagnostic's `path` names the offending element with stable keys, e.g.
// "states.licence-details.components[0].items[0].fieldKey" or "calculations.fields.member".
// Only the "states.<key>..." shape names something the canvas can actually jump to.
function stageKeyFromDiagnosticPath(path: unknown): string | undefined {
  if (typeof path !== 'string') {
    return undefined;
  }
  return /^states\.([^.]+)/.exec(path)?.[1];
}

function readStructuredDetails(value: unknown, workflow?: AuthoredWorkflow): WorkflowSaveErrorDetail[] {
  const entries = Array.isArray(value) ? value : typeof value === 'string' ? [value] : [];

  return entries.flatMap((entry): WorkflowSaveErrorDetail[] => {
    const rawMessage =
      entry && typeof entry === 'object' && 'message' in entry && typeof (entry as { message?: unknown }).message === 'string'
        ? (entry as { message: string }).message
        : typeof entry === 'string'
          ? entry
          : null;
    const [message] = sanitiseWorkflowSaveErrorLines([rawMessage]);
    if (!message) {
      return [];
    }

    const rawStageKey =
      entry && typeof entry === 'object' && 'path' in entry
        ? stageKeyFromDiagnosticPath((entry as { path?: unknown }).path)
        : undefined;
    const stage = rawStageKey ? workflow?.states.find(s => s.stateKey === rawStageKey) : undefined;

    // Only offer a jump when the stage actually resolves — a dangling/renamed key isn't
    // navigable, and showing a dead "jump" affordance would be worse than showing none.
    return [{
      message: stage ? `${stage.displayName}: ${message}` : message,
      stageKey: stage ? rawStageKey : undefined,
    }];
  });
}

function readStructuredErrorLines(value: unknown): string[] {
  return readStructuredDetails(value).map(detail => detail.message);
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

// UmbracoPrism.WorkflowRuntime.Services.WorkflowSaveOutcome for a real business-validation
// failure (WorkflowAuthoringService.Validate rejected the workflow) — e.g. a stat-group bound
// to a field that no longer exists, or a showWhen expression referencing an unknown name. This
// is a genuinely different failure than a malformed request: the JSON was well-formed and
// reached the server, but the workflow's own content is invalid. Distinguished from
// ProblemDetails (a framework-level 400, e.g. a JSON deserialization failure) by payload shape,
// not HTTP status — both currently arrive as a plain 400.
function isWorkflowSaveOutcomePayload(payload: unknown): payload is WorkflowSaveOutcomePayload {
  return !!payload && typeof payload === 'object' && Array.isArray((payload as WorkflowSaveOutcomePayload).diagnostics);
}

function parseValidationOutcome(
  payload: WorkflowSaveOutcomePayload,
  workflowKey: string,
  workflow?: AuthoredWorkflow
): WorkflowSaveError {
  const details = readStructuredDetails(payload.diagnostics, workflow);
  const summary = sanitiseWorkflowSaveErrorText(details[0]?.message)
    ?? `“${workflowKey}” has a problem that must be fixed before it can be saved.`;

  return new WorkflowSaveError({
    title: 'This workflow can’t be saved yet',
    summary,
    details: details.filter(detail => detail.message !== summary),
    summaryStageKey: details[0]?.message === summary ? details[0]?.stageKey : undefined,
    statusCode: 400,
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

/**
 * The synchronous core of save-error parsing, factored out from `buildSaveError` so it's
 * testable without mocking `fetch`/`Response` — every branch here is a pure function of the
 * response body text.
 */
export function buildSaveErrorFromPayload(
  payloadText: string,
  status: number,
  statusText: string,
  contentType: string,
  workflowKey: string,
  workflow?: AuthoredWorkflow
): WorkflowSaveError {
  const fallbackSummary = sanitiseWorkflowSaveErrorText(payloadText)
    ?? `Save failed (${status} ${statusText}).`;

  if (contentType.includes('json') || payloadText.trim().startsWith('{')) {
    try {
      const parsed = JSON.parse(payloadText) as WorkflowSaveOutcomePayload | ProblemDetailsPayload;

      if (status === 409) {
        return parseConflictOutcome(parsed as WorkflowSaveOutcomePayload, workflowKey);
      }

      if (isWorkflowSaveOutcomePayload(parsed)) {
        return parseValidationOutcome(parsed, workflowKey, workflow);
      }

      return parseProblemDetails(parsed as ProblemDetailsPayload, status, workflowKey);
    } catch {
      // Fall through to the plain-text fallback.
    }
  }

  return new WorkflowSaveError({
    title: 'We couldn’t save this workflow',
    summary: fallbackSummary,
    statusCode: status,
  });
}

async function buildSaveError(response: Response, workflowKey: string, workflow?: AuthoredWorkflow): Promise<WorkflowSaveError> {
  const payloadText = await response.text().catch(() => '');
  return buildSaveErrorFromPayload(
    payloadText,
    response.status,
    response.statusText,
    response.headers.get('content-type') ?? '',
    workflowKey,
    workflow
  );
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
      throw await buildSaveError(response, workflowKey, workflow);
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
