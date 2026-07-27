// Host integration EXAMPLE — not part of the `@umbraco-prism/client` boundary
// surface. The reference MockBusinessApp uses this implementation to wire its
// `/mockapp/service-blueprints/*` endpoints into the editor's `ServiceBlueprintSource` contract.
// Real downstream apps fork/copy this file into their own bundle.

import {
  ServiceBlueprintSaveError,
  sanitiseServiceBlueprintSaveErrorLines,
  sanitiseServiceBlueprintSaveErrorText,
  type ServiceBlueprintSource,
  type ServiceBlueprintSummary,
} from '../service-blueprint-source.js';
import type { AuthoredServiceBlueprint } from '../types.js';
import { hydrateServiceBlueprintDefinition } from '../types.js';
import { serializeAuthoredServiceBlueprint } from '../service-blueprint-canonical-json.js';

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

// The shape UmbracoPrism.ProcessManager.Services.ServiceBlueprintSaveOutcome serializes to — returned
// by both /mockapp/service-blueprints/{key} and /prism/service-blueprint-authoring/blueprints/{key} on a version
// conflict (409). Not a ProblemDetails payload, so it's parsed separately.
type ServiceBlueprintSaveOutcomePayload = {
  status?: unknown;
  errors?: unknown;
  currentVersion?: unknown;
  newVersion?: unknown;
};

function parseConflictOutcome(payload: ServiceBlueprintSaveOutcomePayload, blueprintKey: string): ServiceBlueprintSaveError {
  const currentVersion = typeof payload.currentVersion === 'number' ? payload.currentVersion : null;
  const detailLines = readStructuredErrorLines(payload.errors);
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

function readStructuredErrorLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return sanitiseServiceBlueprintSaveErrorLines(value.filter((entry): entry is string => typeof entry === 'string'));
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

  return typeof value === 'string' ? sanitiseServiceBlueprintSaveErrorLines([value]) : [];
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
  ) ?? `The host app rejected the save request for “${blueprintKey}”.`;
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

  return new ServiceBlueprintSaveError({
    title,
    summary,
    detailLines,
    traceId,
    statusCode,
  });
}

async function buildSaveError(response: Response, blueprintKey: string): Promise<ServiceBlueprintSaveError> {
  const payloadText = await response.text().catch(() => '');
  const contentType = response.headers.get('content-type') ?? '';
  const fallbackSummary = sanitiseServiceBlueprintSaveErrorText(payloadText)
    ?? `Save failed (${response.status} ${response.statusText}).`;

  if (contentType.includes('json') || payloadText.trim().startsWith('{')) {
    try {
      if (response.status === 409) {
        return parseConflictOutcome(JSON.parse(payloadText) as ServiceBlueprintSaveOutcomePayload, blueprintKey);
      }

      const payload = JSON.parse(payloadText) as ProblemDetailsPayload;
      return parseProblemDetails(payload, response.status, blueprintKey);
    } catch {
      // Fall through to the plain-text fallback.
    }
  }

  return new ServiceBlueprintSaveError({
    title: 'We couldn’t save this service blueprint',
    summary: fallbackSummary,
    statusCode: response.status,
  });
}

export interface MockBusinessAppServiceBlueprintSourceOptions {
  /** Origin override for cross-origin development. Defaults to same-origin. */
  baseUrl?: string;
}

export class MockBusinessAppServiceBlueprintSource implements ServiceBlueprintSource {
  private readonly base: string;

  constructor(options: MockBusinessAppServiceBlueprintSourceOptions = {}) {
    this.base = (options.baseUrl ?? '').replace(/\/$/, '');
  }

  async list(): Promise<ServiceBlueprintSummary[]> {
    const response = await fetch(`${this.base}/mockapp/service-blueprints`, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to list service blueprints (${response.status} ${response.statusText}).`);
    }
    // /mockapp/service-blueprints serializes ServiceBlueprintSourceSummary(DefinitionKey, DisplayName) — there's no
    // separate "host-facing key" concept on this host, so blueprintKey and definitionKey are the
    // same string here. The naive `as ServiceBlueprintSummary[]` this replaced compiled fine (TypeScript
    // doesn't check across a JSON boundary) but left every option's `blueprintKey` undefined at
    // runtime, so the shell's `option.blueprintKey === this._draftBlueprintKey` selected-match never
    // fired and the <select> silently fell back to its first option regardless of which service blueprint
    // was actually loaded.
    const summaries = (await response.json()) as Array<{ definitionKey: string; displayName: string }>;
    return summaries.map(({ definitionKey, displayName }) => ({
      blueprintKey: definitionKey,
      definitionKey,
      displayName,
    }));
  }

  async load(blueprintKey: string): Promise<AuthoredServiceBlueprint> {
    const response = await fetch(`${this.base}/mockapp/service-blueprints/${encodeURIComponent(blueprintKey)}`, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (!response.ok) {
      throw new Error(`Failed to load service blueprint '${blueprintKey}' (${response.status} ${response.statusText}).`);
    }
    const payload = (await response.json()) as Record<string, unknown>;
    return hydrateServiceBlueprintDefinition(payload as unknown as AuthoredServiceBlueprint);
  }

  async save(blueprintKey: string, serviceBlueprint: AuthoredServiceBlueprint): Promise<void> {
    const body = serializeAuthoredServiceBlueprint(serviceBlueprint);
    const response = await fetch(`${this.base}/mockapp/service-blueprints/${encodeURIComponent(blueprintKey)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      credentials: 'same-origin',
      body,
    });
    if (!response.ok) {
      throw await buildSaveError(response, blueprintKey);
    }
  }

  /**
   * Cheap poll target: reads just the version, not the full definition. Uses the
   * definitionKey-keyed toolkit route rather than /mockapp/service-blueprints/* — both read from the
   * same underlying store, so either is correct, but this one exists specifically for this.
   */
  async checkVersion(blueprintKey: string): Promise<number | null> {
    const response = await fetch(
      `${this.base}/prism/service-blueprint-authoring/blueprints/${encodeURIComponent(blueprintKey)}/version`,
      { headers: { Accept: 'application/json' }, credentials: 'same-origin' }
    );
    if (!response.ok) {
      return null;
    }
    const payload = (await response.json()) as { version?: unknown };
    return typeof payload.version === 'number' ? payload.version : null;
  }
}
