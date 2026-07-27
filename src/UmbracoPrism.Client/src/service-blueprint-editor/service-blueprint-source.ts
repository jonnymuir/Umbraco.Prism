/**
 * ServiceBlueprintSource — the boundary contract between Prism's serviceBlueprint editor
 * (a service-design tool) and the host business application.
 *
 * Hosts implement this interface to expose their authored serviceBlueprints to the
 * editor. The editor never speaks HTTP, never reads identity, never knows
 * how the host stores its serviceBlueprints. Save authorisation is the host's call:
 * resolve `save` to enforce permissions; surface UX hints via
 * `ServiceBlueprintAuthorContext` if you want the editor to grey out the Save button.
 *
 * Reference implementation: `InMemoryServiceBlueprintSource` (this package).
 * Integrator examples: `MockBusinessApp/wwwroot/dist/service-blueprint-editor-bootstrap.js`.
 */

import type { AuthoredServiceBlueprint } from './types.js';

/**
 * One save-error detail line, optionally locating the stage it came from — a server-side
 * diagnostic's `path` (e.g. `stages.licence-details.components[0].items[0].fieldKey`) names a
 * real stage the editor can jump to, the way the Validation rail's structural issues already
 * do. A diagnostic with no resolvable stage (e.g. a `calculations.fields.X` path) just has no
 * `stageKey` — still shown, just not clickable.
 */
export interface ServiceBlueprintSaveErrorDetail {
  message: string;
  stageKey?: string;
}

export interface ServiceBlueprintSaveErrorOptions {
  title: string;
  summary: string;
  detailLines?: string[];
  /** Rich detail entries for the save-error surface; defaults from detailLines when omitted. */
  details?: ServiceBlueprintSaveErrorDetail[];
  /** Stage the headline `summary` came from, when resolvable — see ServiceBlueprintSaveErrorDetail. */
  summaryStageKey?: string;
  traceId?: string | null;
  statusCode?: number;
  /**
   * True when the save failed because the service blueprint's `version` no longer matched what's
   * currently persisted (HTTP 409) — someone else (a human in the editor, or an AI agent)
   * saved a newer version. Distinct from a validation failure: reload and reapply the
   * change rather than just fixing the payload and retrying.
   */
  isConflict?: boolean;
  /** The version actually persisted now, when `isConflict` is true. */
  currentVersion?: number | null;
}

type ServiceBlueprintSaveErrorLike = Partial<ServiceBlueprintSaveErrorOptions> & {
  name?: string;
  message?: string;
  detailLines?: unknown;
  traceId?: unknown;
};

const STACK_TRACE_LINE = /^(at\s+|--- End of stack trace|Stack trace:)/i;
const ERROR_PREFIX = /^[A-Za-z0-9_.]+(?:Exception|Error):\s*/;

function sanitiseServiceBlueprintSaveErrorLine(value: string): string | null {
  let line = value.trim();
  if (!line || /^</.test(line)) {
    return null;
  }

  if (
    STACK_TRACE_LINE.test(line)
    || /\.cs:\s*line\s*\d+/i.test(line)
    || /\(.+:\d+:\d+\)$/.test(line)
  ) {
    return null;
  }

  line = line.replace(ERROR_PREFIX, '').trim();
  return line.length > 0 ? line : null;
}

export function sanitiseServiceBlueprintSaveErrorLines(values: Iterable<string | null | undefined>): string[] {
  const lines: string[] = [];
  for (const value of values) {
    if (!value) {
      continue;
    }

    for (const candidate of value.split(/\r?\n/)) {
      const line = sanitiseServiceBlueprintSaveErrorLine(candidate);
      if (line && !lines.includes(line)) {
        lines.push(line);
      }
    }
  }

  return lines.slice(0, 8);
}

export function sanitiseServiceBlueprintSaveErrorText(value: string | null | undefined): string | null {
  const lines = sanitiseServiceBlueprintSaveErrorLines([value]);
  return lines.length > 0 ? lines.join(' ') : null;
}

function buildServiceBlueprintSaveErrorCopyText(error: ServiceBlueprintSaveError): string {
  const sections = [
    error.title,
    error.summary,
    ...error.detailLines,
    error.traceId ? `Reference: ${error.traceId}` : null,
  ].filter((section): section is string => typeof section === 'string' && section.trim().length > 0);

  return sections.join('\n');
}

export class ServiceBlueprintSaveError extends Error {
  readonly title: string;
  readonly summary: string;
  readonly detailLines: string[];
  readonly details: ServiceBlueprintSaveErrorDetail[];
  readonly summaryStageKey?: string;
  readonly traceId: string | null;
  readonly statusCode?: number;
  readonly isConflict: boolean;
  readonly currentVersion: number | null;

  constructor(options: ServiceBlueprintSaveErrorOptions) {
    super(options.summary);
    this.name = 'ServiceBlueprintSaveError';
    this.title = options.title;
    this.summary = options.summary;
    this.detailLines = options.detailLines ?? [];
    this.details = options.details ?? this.detailLines.map(message => ({ message }));
    this.summaryStageKey = options.summaryStageKey;
    this.traceId = options.traceId ?? null;
    this.statusCode = options.statusCode;
    this.isConflict = options.isConflict ?? false;
    this.currentVersion = options.currentVersion ?? null;
  }

  get copyText(): string {
    return buildServiceBlueprintSaveErrorCopyText(this);
  }
}

export function normaliseServiceBlueprintSaveError(
  error: unknown,
  fallbackSummary = 'We couldn’t save this service blueprint.'
): ServiceBlueprintSaveError {
  if (error instanceof ServiceBlueprintSaveError) {
    return error;
  }

  const candidate = (typeof error === 'object' && error !== null ? error : {}) as ServiceBlueprintSaveErrorLike;
  const title = sanitiseServiceBlueprintSaveErrorText(candidate.title) ?? 'We couldn’t save this service blueprint';
  const summary = sanitiseServiceBlueprintSaveErrorText(candidate.summary)
    ?? sanitiseServiceBlueprintSaveErrorText(candidate.message)
    ?? fallbackSummary;
  const traceId = sanitiseServiceBlueprintSaveErrorText(typeof candidate.traceId === 'string' ? candidate.traceId : null);
  const detailLines = sanitiseServiceBlueprintSaveErrorLines(
    Array.isArray(candidate.detailLines)
      ? candidate.detailLines.filter((line): line is string => typeof line === 'string')
      : []
  )
    .filter(line => line !== summary);

  return new ServiceBlueprintSaveError({
    title,
    summary,
    detailLines,
    traceId,
    statusCode: typeof candidate.statusCode === 'number' ? candidate.statusCode : undefined,
  });
}

export interface ServiceBlueprintSummary {
  /** Host-facing lookup key. May differ from `definitionKey`. */
  blueprintKey: string;
  /** Stable identity of the authored document, when the host tracks one. */
  id?: string;
  /** Definition key embedded in the service blueprint body. */
  definitionKey: string;
  /** Display name shown in serviceBlueprint pickers. */
  displayName: string;
}

export interface ServiceBlueprintSource {
  /** Returns every serviceBlueprint the editor should let the author pick. */
  list(): Promise<ServiceBlueprintSummary[]>;

  /** Loads one authored serviceBlueprint by its host-facing key. */
  load(key: string): Promise<AuthoredServiceBlueprint>;

  /**
   * Persists the authored serviceBlueprint back to the host. The host enforces save permissions.
   * Hosts may throw `ServiceBlueprintSaveError` with a user-facing title/summary/detail payload
   * (with `isConflict: true` when the service blueprint's `version` no longer matched — see
   * `AuthoredServiceBlueprint.version` and the host's optimistic-concurrency contract).
   */
  save(key: string, serviceBlueprint: AuthoredServiceBlueprint): Promise<void>;

  /**
   * Optional: returns the currently-persisted version of a service blueprint, for a client that wants
   * to proactively detect staleness (e.g. poll while a service blueprint is open) rather than only
   * finding out via a `save` conflict. Hosts that don't support versioning can omit this.
   */
  checkVersion?(key: string): Promise<number | null>;
}
