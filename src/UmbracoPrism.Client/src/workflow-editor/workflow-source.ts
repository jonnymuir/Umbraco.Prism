/**
 * WorkflowSource — the boundary contract between Prism's workflow editor
 * (a service-design tool) and the host business application.
 *
 * Hosts implement this interface to expose their authored workflows to the
 * editor. The editor never speaks HTTP, never reads identity, never knows
 * how the host stores its workflows. Save authorisation is the host's call:
 * resolve `save` to enforce permissions; surface UX hints via
 * `WorkflowAuthorContext` if you want the editor to grey out the Save button.
 *
 * Reference implementation: `InMemoryWorkflowSource` (this package).
 * Integrator examples: `MockBusinessApp/wwwroot/dist/workflow-editor-bootstrap.js`.
 */

import type { AuthoredWorkflow } from './types.js';

export interface WorkflowSaveErrorOptions {
  title: string;
  summary: string;
  detailLines?: string[];
  traceId?: string | null;
  statusCode?: number;
}

type WorkflowSaveErrorLike = Partial<WorkflowSaveErrorOptions> & {
  name?: string;
  message?: string;
  detailLines?: unknown;
  traceId?: unknown;
};

const STACK_TRACE_LINE = /^(at\s+|--- End of stack trace|Stack trace:)/i;
const ERROR_PREFIX = /^[A-Za-z0-9_.]+(?:Exception|Error):\s*/;

function sanitiseWorkflowSaveErrorLine(value: string): string | null {
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

export function sanitiseWorkflowSaveErrorLines(values: Iterable<string | null | undefined>): string[] {
  const lines: string[] = [];
  for (const value of values) {
    if (!value) {
      continue;
    }

    for (const candidate of value.split(/\r?\n/)) {
      const line = sanitiseWorkflowSaveErrorLine(candidate);
      if (line && !lines.includes(line)) {
        lines.push(line);
      }
    }
  }

  return lines.slice(0, 8);
}

export function sanitiseWorkflowSaveErrorText(value: string | null | undefined): string | null {
  const lines = sanitiseWorkflowSaveErrorLines([value]);
  return lines.length > 0 ? lines.join(' ') : null;
}

function buildWorkflowSaveErrorCopyText(error: WorkflowSaveError): string {
  const sections = [
    error.title,
    error.summary,
    ...error.detailLines,
    error.traceId ? `Reference: ${error.traceId}` : null,
  ].filter((section): section is string => typeof section === 'string' && section.trim().length > 0);

  return sections.join('\n');
}

export class WorkflowSaveError extends Error {
  readonly title: string;
  readonly summary: string;
  readonly detailLines: string[];
  readonly traceId: string | null;
  readonly statusCode?: number;

  constructor(options: WorkflowSaveErrorOptions) {
    super(options.summary);
    this.name = 'WorkflowSaveError';
    this.title = options.title;
    this.summary = options.summary;
    this.detailLines = options.detailLines ?? [];
    this.traceId = options.traceId ?? null;
    this.statusCode = options.statusCode;
  }

  get copyText(): string {
    return buildWorkflowSaveErrorCopyText(this);
  }
}

export function normaliseWorkflowSaveError(
  error: unknown,
  fallbackSummary = 'We couldn’t save this workflow.'
): WorkflowSaveError {
  if (error instanceof WorkflowSaveError) {
    return error;
  }

  const candidate = (typeof error === 'object' && error !== null ? error : {}) as WorkflowSaveErrorLike;
  const title = sanitiseWorkflowSaveErrorText(candidate.title) ?? 'We couldn’t save this workflow';
  const summary = sanitiseWorkflowSaveErrorText(candidate.summary)
    ?? sanitiseWorkflowSaveErrorText(candidate.message)
    ?? fallbackSummary;
  const traceId = sanitiseWorkflowSaveErrorText(typeof candidate.traceId === 'string' ? candidate.traceId : null);
  const detailLines = sanitiseWorkflowSaveErrorLines(
    Array.isArray(candidate.detailLines)
      ? candidate.detailLines.filter((line): line is string => typeof line === 'string')
      : []
  )
    .filter(line => line !== summary);

  return new WorkflowSaveError({
    title,
    summary,
    detailLines,
    traceId,
    statusCode: typeof candidate.statusCode === 'number' ? candidate.statusCode : undefined,
  });
}

export interface WorkflowSummary {
  /** Host-facing lookup key. May differ from `definitionKey`. */
  workflowKey: string;
  /** Stable identity of the authored document, when the host tracks one. */
  id?: string;
  /** Definition key embedded in the workflow body. */
  definitionKey: string;
  /** Display name shown in workflow pickers. */
  displayName: string;
}

export interface WorkflowSource {
  /** Returns every workflow the editor should let the author pick. */
  list(): Promise<WorkflowSummary[]>;

  /** Loads one authored workflow by its host-facing key. */
  load(key: string): Promise<AuthoredWorkflow>;

  /**
   * Persists the authored workflow back to the host. The host enforces save permissions.
   * Hosts may throw `WorkflowSaveError` with a user-facing title/summary/detail payload.
   */
  save(key: string, workflow: AuthoredWorkflow): Promise<void>;
}
