// Backoffice ServiceBlueprintSource — the editor host implementation for CMS Service Blueprint, Prism's
// Umbraco-only service blueprint implementation. Unlike MockBusinessAppServiceBlueprintSource (cookie-auth,
// same-origin), the backoffice's Management API is Bearer-token authenticated, and the token
// can rotate between calls — `getToken` is called fresh on every request rather than captured
// once at construction, so a long-open editor session never sends a stale token.

import {
  ServiceBlueprintSaveError,
  sanitiseServiceBlueprintSaveErrorLines,
  sanitiseServiceBlueprintSaveErrorText,
  type ServiceBlueprintSaveErrorDetail,
  type ServiceBlueprintSource,
  type ServiceBlueprintSummary,
} from '../service-blueprint-editor/service-blueprint-source.js';
import type { AuthoredServiceBlueprint } from '../service-blueprint-editor/types.js';
import { hydrateServiceBlueprintDefinition } from '../service-blueprint-editor/types.js';
import { serializeAuthoredServiceBlueprint } from '../service-blueprint-editor/service-blueprint-canonical-json.js';

const API_BASE = '/umbraco/management/api/v1/prism/cms-service-blueprints';

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

// The shape UmbracoPrism.ServiceBlueprintRuntime.Services.ServiceBlueprintSaveOutcome serializes to.
type ServiceBlueprintSaveOutcomePayload = {
  status?: unknown;
  diagnostics?: unknown;
  currentVersion?: unknown;
  newVersion?: unknown;
};

// A ServiceBlueprintDiagnostic's `path` names the offending element with stable keys, e.g.
// "stages.licence-details.components[0].items[0].fieldKey" or "calculations.fields.member".
// Only the "stages.<key>..." shape names something the canvas can actually jump to.
function stageKeyFromDiagnosticPath(path: unknown): string | undefined {
  if (typeof path !== 'string') {
    return undefined;
  }
  return /^stages\.([^.]+)/.exec(path)?.[1];
}

function readStructuredDetails(value: unknown, serviceBlueprint?: AuthoredServiceBlueprint): ServiceBlueprintSaveErrorDetail[] {
  const entries = Array.isArray(value) ? value : typeof value === 'string' ? [value] : [];

  return entries.flatMap((entry): ServiceBlueprintSaveErrorDetail[] => {
    const rawMessage =
      entry && typeof entry === 'object' && 'message' in entry && typeof (entry as { message?: unknown }).message === 'string'
        ? (entry as { message: string }).message
        : typeof entry === 'string'
          ? entry
          : null;
    const [message] = sanitiseServiceBlueprintSaveErrorLines([rawMessage]);
    if (!message) {
      return [];
    }

    const rawStageKey =
      entry && typeof entry === 'object' && 'path' in entry
        ? stageKeyFromDiagnosticPath((entry as { path?: unknown }).path)
        : undefined;
    const stage = rawStageKey ? serviceBlueprint?.stages.find(s => s.stateKey === rawStageKey) : undefined;

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

function parseConflictOutcome(payload: ServiceBlueprintSaveOutcomePayload, blueprintKey: string): ServiceBlueprintSaveError {
  const currentVersion = typeof payload.currentVersion === 'number' ? payload.currentVersion : null;
  const detailLines = readStructuredErrorLines(payload.diagnostics);
  const summary = sanitiseServiceBlueprintSaveErrorText(detailLines[0])
    ?? `“${blueprintKey}” was changed elsewhere since you loaded it${currentVersion != null ? ` (now at version ${currentVersion})` : ''}.`;

  return new ServiceBlueprintSaveError({
    title: 'This service blueprint changed elsewhere',
    summary,
    detailLines: detailLines.filter(line => line !== summary),
    statusCode: 409,
    isConflict: true,
    currentVersion,
  });
}

// UmbracoPrism.ServiceBlueprintRuntime.Services.ServiceBlueprintSaveOutcome for a real business-validation
// failure (ServiceBlueprintAuthoringService.Validate rejected the service blueprint) — e.g. a stat-group bound
// to a field that no longer exists, or a showWhen expression referencing an unknown name. This
// is a genuinely different failure than a malformed request: the JSON was well-formed and
// reached the server, but the service blueprint's own content is invalid. Distinguished from
// ProblemDetails (a framework-level 400, e.g. a JSON deserialization failure) by payload shape,
// not HTTP status — both currently arrive as a plain 400.
function isServiceBlueprintSaveOutcomePayload(payload: unknown): payload is ServiceBlueprintSaveOutcomePayload {
  return !!payload && typeof payload === 'object' && Array.isArray((payload as ServiceBlueprintSaveOutcomePayload).diagnostics);
}

function parseValidationOutcome(
  payload: ServiceBlueprintSaveOutcomePayload,
  blueprintKey: string,
  serviceBlueprint?: AuthoredServiceBlueprint
): ServiceBlueprintSaveError {
  const details = readStructuredDetails(payload.diagnostics, serviceBlueprint);
  const summary = sanitiseServiceBlueprintSaveErrorText(details[0]?.message)
    ?? `“${blueprintKey}” has a problem that must be fixed before it can be saved.`;

  return new ServiceBlueprintSaveError({
    title: 'This service blueprint can’t be saved yet',
    summary,
    details: details.filter(detail => detail.message !== summary),
    summaryStageKey: details[0]?.message === summary ? details[0]?.stageKey : undefined,
    statusCode: 400,
  });
}

function parseProblemDetails(payload: ProblemDetailsPayload, statusCode: number, blueprintKey: string): ServiceBlueprintSaveError {
  const title = sanitiseServiceBlueprintSaveErrorText(typeof payload.title === 'string' ? payload.title : null)
    ?? 'We couldn’t save this service blueprint';
  const summary = sanitiseServiceBlueprintSaveErrorText(
    typeof payload.summary === 'string'
      ? payload.summary
      : typeof payload.detail === 'string'
        ? payload.detail
        : typeof payload.message === 'string'
          ? payload.message
          : null
  ) ?? `The backoffice rejected the save request for “${blueprintKey}”.`;
  const detailLines = sanitiseServiceBlueprintSaveErrorLines([
    ...readStructuredErrorLines(payload.errors),
    ...readStructuredErrorLines(payload.extensions?.errors),
  ]).filter(line => line !== summary);
  const traceId = sanitiseServiceBlueprintSaveErrorText(
    typeof payload.traceId === 'string'
      ? payload.traceId
      : typeof payload.extensions?.traceId === 'string'
        ? payload.extensions.traceId
        : null
  );

  return new ServiceBlueprintSaveError({ title, summary, detailLines, traceId, statusCode });
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
  blueprintKey: string,
  serviceBlueprint?: AuthoredServiceBlueprint
): ServiceBlueprintSaveError {
  const fallbackSummary = sanitiseServiceBlueprintSaveErrorText(payloadText)
    ?? `Save failed (${status} ${statusText}).`;

  if (contentType.includes('json') || payloadText.trim().startsWith('{')) {
    try {
      const parsed = JSON.parse(payloadText) as ServiceBlueprintSaveOutcomePayload | ProblemDetailsPayload;

      if (status === 409) {
        return parseConflictOutcome(parsed as ServiceBlueprintSaveOutcomePayload, blueprintKey);
      }

      if (isServiceBlueprintSaveOutcomePayload(parsed)) {
        return parseValidationOutcome(parsed, blueprintKey, serviceBlueprint);
      }

      return parseProblemDetails(parsed as ProblemDetailsPayload, status, blueprintKey);
    } catch {
      // Fall through to the plain-text fallback.
    }
  }

  return new ServiceBlueprintSaveError({
    title: 'We couldn’t save this service blueprint',
    summary: fallbackSummary,
    statusCode: status,
  });
}

async function buildSaveError(response: Response, blueprintKey: string, serviceBlueprint?: AuthoredServiceBlueprint): Promise<ServiceBlueprintSaveError> {
  const payloadText = await response.text().catch(() => '');
  return buildSaveErrorFromPayload(
    payloadText,
    response.status,
    response.statusText,
    response.headers.get('content-type') ?? '',
    blueprintKey,
    serviceBlueprint
  );
}

export class UmbracoBackofficeServiceBlueprintSource implements ServiceBlueprintSource {
  private readonly getToken: () => Promise<string | undefined>;

  constructor(getToken: () => Promise<string | undefined>) {
    this.getToken = getToken;
  }

  private async authHeaders(extra: Record<string, string> = {}): Promise<Record<string, string>> {
    const token = await this.getToken();
    return {
      ...extra,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    };
  }

  async list(): Promise<ServiceBlueprintSummary[]> {
    const response = await fetch(API_BASE, {
      headers: await this.authHeaders({ Accept: 'application/json' }),
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to list CMS serviceBlueprints (${response.status} ${response.statusText}).`);
    }
    const summaries = (await response.json()) as Array<{ definitionKey: string; displayName: string }>;
    return summaries.map(({ definitionKey, displayName }) => ({
      blueprintKey: definitionKey,
      definitionKey,
      displayName,
    }));
  }

  async load(blueprintKey: string): Promise<AuthoredServiceBlueprint> {
    const response = await fetch(`${API_BASE}/${encodeURIComponent(blueprintKey)}`, {
      headers: await this.authHeaders({ Accept: 'application/json' }),
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to load CMS serviceBlueprint '${blueprintKey}' (${response.status} ${response.statusText}).`);
    }
    const payload = (await response.json()) as Record<string, unknown>;
    return hydrateServiceBlueprintDefinition(payload as unknown as AuthoredServiceBlueprint);
  }

  async save(blueprintKey: string, serviceBlueprint: AuthoredServiceBlueprint): Promise<void> {
    const body = serializeAuthoredServiceBlueprint(serviceBlueprint);
    const response = await fetch(`${API_BASE}/${encodeURIComponent(blueprintKey)}`, {
      method: 'PUT',
      headers: await this.authHeaders({ 'Content-Type': 'application/json', Accept: 'application/json' }),
      credentials: 'same-origin',
      body,
    });
    if (!response.ok) {
      throw await buildSaveError(response, blueprintKey, serviceBlueprint);
    }
  }

  async checkVersion(blueprintKey: string): Promise<number | null> {
    const response = await fetch(`${API_BASE}/${encodeURIComponent(blueprintKey)}/version`, {
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
