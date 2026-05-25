/**
 * HTTP client for the Workflow Authoring API (Blathers' surface).
 * Targets:
 *   1. explicit host shell configuration
 *   2. VITE_AUTHORING_API_BASE
 *   3. current origin when hosted alongside the API
 *   4. https://localhost:7245 as the local-development fallback
 */

import type {
  ActionCatalogEntry,
  AuthoredAction,
  AuthoredField,
  AuthoredGateway,
  AuthoredParameterDefinition,
  AuthoredStage,
  AuthoredTransition,
  AuthoredWorkflow,
  FieldKind,
  GatewayKind,
  ProposalEnvelope,
  StageKind,
} from './types.js';
import { STUB_ACTION_CATALOG } from './types.js';
import { projectWorkflowLocally, type ProjectWorkflowResult } from './workflow-runtime-projection.js';

function stripLegacyStageSurface<T extends AuthoredStage>(stage: T): T {
  const { editorSurface: _editorSurface, ...rest } = stage as T & { editorSurface?: 'front-stage' | 'back-stage' };
  return rest as T;
}

function serialiseWorkflow(workflow: AuthoredWorkflow): AuthoredWorkflow {
  return {
    ...workflow,
    stages: workflow.stages.map(stage => stripLegacyStageSurface(stage)),
  };
}

export type WorkflowAuthoringSummary = {
  workflowKey: string;
  id: string;
  definitionKey: string;
  displayName: string;
};

function fallbackOrigin(): string {
  if (typeof window !== 'undefined' && window.location.origin) {
    return window.location.origin;
  }
  return 'https://localhost:7245';
}

export function defaultAuthoringApiBase(): string {
  return normaliseAuthoringApiBase(
    (import.meta.env?.VITE_AUTHORING_API_BASE as string | undefined) ?? fallbackOrigin()
  );
}

export function normaliseAuthoringApiBase(base?: string): string {
  const candidate = base?.trim() || fallbackOrigin();
  return candidate.replace(/\/+$/, '');
}

function url(path: string, apiBase?: string): string {
  return `${normaliseAuthoringApiBase(apiBase)}${path}`;
}

function mapStageKind(raw: string | undefined): StageKind {
  switch (raw) {
    case 'CheckAnswers':
    case 'Confirmation':
    case 'TaskList':
    case 'Waiting':
    case 'StatusTimeline':
      return raw;
    default:
      return 'Question';
  }
}

function mapFieldKind(raw: string | undefined): FieldKind {
  switch (raw) {
    case 'Number':
    case 'Decimal':
      return 'NumberInput';
    case 'Email':
      return 'EmailInput';
    case 'Textarea':
      return 'Textarea';
    case 'Radios':
      return 'Radios';
    case 'Checkboxes':
      return 'Checkboxes';
    case 'Select':
      return 'Select';
    case 'Date':
    case 'DateInput':
      return 'DateInput';
    case 'Boolean':
      return 'Toggle';
    case 'FileUpload':
      return 'FileUpload';
    case 'Hidden':
      return 'Hidden';
    default:
      return 'TextInput';
  }
}

function mapGatewayKind(raw: string | undefined): GatewayKind {
  switch (raw) {
    case 'Join':
      return 'Join';
    case 'Split':
    default:
      return 'Split';
  }
}

function normaliseField(raw: Record<string, unknown>): AuthoredField {
  return {
    fieldKey: String(raw.fieldKey ?? raw.key ?? ''),
    label: String(raw.label ?? ''),
    kind: mapFieldKind(typeof raw.kind === 'string' ? raw.kind : typeof raw.type === 'string' ? raw.type : undefined),
    required: Boolean(raw.required),
    hintText: typeof raw.hintText === 'string' ? raw.hintText : typeof raw.hint === 'string' ? raw.hint : undefined,
    validationPattern:
      typeof raw.validationPattern === 'string'
        ? raw.validationPattern
        : undefined,
    defaultValue: raw.defaultValue,
    options: Array.isArray(raw.options) ? raw.options.map(value => String(value)) : [],
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseAction(raw: Record<string, unknown>): AuthoredAction {
  return {
    type: String(raw.type ?? ''),
    timing: (typeof raw.timing === 'string' ? raw.timing : 'OnEntry') as AuthoredAction['timing'],
    params: typeof raw.params === 'object' && raw.params !== null ? raw.params as Record<string, unknown> : {},
    parameterSchemaKey: typeof raw.parameterSchemaKey === 'string' ? raw.parameterSchemaKey : undefined,
    summary: typeof raw.summary === 'string' ? raw.summary : undefined,
  };
}

function normaliseStage(raw: Record<string, unknown>): AuthoredStage {
  return {
    stageKey: String(raw.stageKey ?? raw.key ?? ''),
    displayName: String(raw.displayName ?? raw.title ?? ''),
    description: typeof raw.description === 'string' ? raw.description : undefined,
    kind: mapStageKind(typeof raw.kind === 'string' ? raw.kind : typeof raw.type === 'string' ? raw.type : undefined),
    actor: typeof raw.actor === 'string' ? raw.actor : undefined,
    actions: Array.isArray(raw.actions)
      ? raw.actions.map(action => normaliseAction(action as Record<string, unknown>))
      : [],
    fields: Array.isArray(raw.fields) ? raw.fields.map(field => normaliseField(field as Record<string, unknown>)) : [],
    roleGates: Array.isArray(raw.roleGates) ? raw.roleGates.map(value => String(value)) : [],
    waiting: typeof raw.waiting === 'object' && raw.waiting !== null ? (raw.waiting as AuthoredStage['waiting']) : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseGateway(raw: Record<string, unknown>): AuthoredGateway {
  return {
    gatewayKey: String(raw.gatewayKey ?? raw.key ?? ''),
    displayName: String(raw.displayName ?? raw.title ?? ''),
    description: typeof raw.description === 'string' ? raw.description : undefined,
    kind: mapGatewayKind(typeof raw.kind === 'string' ? raw.kind : typeof raw.type === 'string' ? raw.type : undefined),
    actor: typeof raw.actor === 'string' ? raw.actor : undefined,
    roleGates: Array.isArray(raw.roleGates) ? raw.roleGates.map(value => String(value)) : [],
    waiting: typeof raw.waiting === 'object' && raw.waiting !== null ? (raw.waiting as AuthoredGateway['waiting']) : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseTransition(raw: Record<string, unknown>): AuthoredTransition {
  const firstCondition =
    Array.isArray(raw.conditions) && raw.conditions.length > 0 && typeof raw.conditions[0] === 'object' && raw.conditions[0] !== null
      ? (raw.conditions[0] as Record<string, unknown>)
      : null;

  return {
    fromStage: String(raw.fromStage ?? raw.source ?? ''),
    toStage: String(raw.toStage ?? raw.target ?? ''),
    action: String(raw.action ?? raw.trigger ?? ''),
    actions: Array.isArray(raw.actions)
      ? raw.actions.map(action => normaliseAction(action as Record<string, unknown>))
      : [],
    requiresRole: typeof raw.requiresRole === 'string' ? raw.requiresRole : undefined,
    condition:
      typeof raw.condition === 'string'
        ? raw.condition
        : typeof firstCondition?.expression === 'string'
          ? firstCondition.expression
          : undefined,
    editorComment: typeof raw.editorComment === 'string' ? raw.editorComment : undefined,
  };
}

function normaliseWorkflow(raw: Record<string, unknown>): AuthoredWorkflow {
  return {
    definitionKey: String(raw.definitionKey ?? ''),
    displayName: String(raw.displayName ?? ''),
    version: typeof raw.version === 'number' ? raw.version : 1,
    schemaVersion: String(raw.schemaVersion ?? '1.0'),
    instancePolicy: String(raw.instancePolicy ?? 'single'),
    initialStageKey: String(raw.initialStageKey ?? ''),
    stages: Array.isArray(raw.stages) ? raw.stages.map(stage => normaliseStage(stage as Record<string, unknown>)) : [],
    transitions: Array.isArray(raw.transitions)
      ? raw.transitions.map(transition => normaliseTransition(transition as Record<string, unknown>))
      : [],
    gateways: Array.isArray(raw.gateways)
      ? raw.gateways.map(gateway => normaliseGateway(gateway as Record<string, unknown>))
      : [],
    authorNote: typeof raw.authorNote === 'string' ? raw.authorNote : undefined,
  };
}

function normaliseParameterDefinition(raw: Record<string, unknown>): AuthoredParameterDefinition {
  return {
    key: String(raw.key ?? ''),
    title: typeof raw.title === 'string' ? raw.title : String(raw.key ?? ''),
    description: typeof raw.description === 'string' ? raw.description : undefined,
    valueKind: (typeof raw.valueKind === 'string' ? raw.valueKind : 'String') as AuthoredParameterDefinition['valueKind'],
    format: typeof raw.format === 'string' ? raw.format : undefined,
    editor: typeof raw.editor === 'string' ? raw.editor : undefined,
    allowedValues: Array.isArray(raw.allowedValues) ? raw.allowedValues.map(value => String(value)) : undefined,
    defaultValue: raw.defaultValue,
    properties: Array.isArray(raw.properties)
      ? raw.properties.map(property => normaliseParameterDefinition(property as Record<string, unknown>))
      : undefined,
    items:
      typeof raw.items === 'object' && raw.items !== null
        ? normaliseParameterDefinition(raw.items as Record<string, unknown>)
        : null,
  };
}

function normaliseActionCatalogEntry(raw: Record<string, unknown>): ActionCatalogEntry {
  return {
    type: String(raw.type ?? ''),
    label: String(raw.label ?? raw.type ?? ''),
    summary: String(raw.summary ?? ''),
    appliesTo: Array.isArray(raw.appliesTo) ? raw.appliesTo.map(value => String(value)) : [],
    paramsSchema:
      typeof raw.paramsSchema === 'object' && raw.paramsSchema !== null
        ? {
            key: String((raw.paramsSchema as Record<string, unknown>).key ?? ''),
            title: String((raw.paramsSchema as Record<string, unknown>).title ?? ''),
            description:
              typeof (raw.paramsSchema as Record<string, unknown>).description === 'string'
                ? String((raw.paramsSchema as Record<string, unknown>).description)
                : undefined,
            appliesTo: Array.isArray((raw.paramsSchema as Record<string, unknown>).appliesTo)
              ? ((raw.paramsSchema as Record<string, unknown>).appliesTo as unknown[]).map(value => String(value))
              : [],
            valueKind:
              typeof (raw.paramsSchema as Record<string, unknown>).valueKind === 'string'
                ? ((raw.paramsSchema as Record<string, unknown>).valueKind as ActionCatalogEntry['paramsSchema']['valueKind'])
                : 'Object',
            allowAdditionalProperties: Boolean((raw.paramsSchema as Record<string, unknown>).allowAdditionalProperties),
            properties: Array.isArray((raw.paramsSchema as Record<string, unknown>).properties)
              ? ((raw.paramsSchema as Record<string, unknown>).properties as Record<string, unknown>[]).map(normaliseParameterDefinition)
              : [],
            required: Array.isArray((raw.paramsSchema as Record<string, unknown>).required)
              ? ((raw.paramsSchema as Record<string, unknown>).required as unknown[]).map(value => String(value))
              : [],
          }
        : { key: '', title: '' },
    parameterWidgets:
      typeof raw.parameterWidgets === 'object' && raw.parameterWidgets !== null
        ? Object.fromEntries(
            Object.entries(raw.parameterWidgets as Record<string, unknown>).map(([key, value]) => [key, String(value)])
          )
        : undefined,
    defaultParams:
      typeof raw.defaultParams === 'object' && raw.defaultParams !== null
        ? raw.defaultParams as Record<string, unknown>
        : {},
    status: typeof raw.status === 'string' ? raw.status : undefined,
    runtimeImplementation: typeof raw.runtimeImplementation === 'string' ? raw.runtimeImplementation : undefined,
  };
}

function mergeActionCatalog(entries: ActionCatalogEntry[], fallbackEntries: ActionCatalogEntry[]): ActionCatalogEntry[] {
  const merged = new Map<string, ActionCatalogEntry>();
  fallbackEntries.forEach(entry => {
    merged.set(entry.type, entry);
  });
  entries.forEach(entry => {
    merged.set(entry.type, { ...(merged.get(entry.type) ?? {}), ...entry });
  });
  return Array.from(merged.values());
}

function normaliseWorkflowSummary(raw: Record<string, unknown>): WorkflowAuthoringSummary {
  const definitionKey = String(raw.definitionKey ?? '');
  const workflowKey = String(raw.workflowKey ?? definitionKey);

  return {
    workflowKey,
    id: String(raw.id ?? ''),
    definitionKey,
    displayName: String(raw.displayName ?? workflowKey ?? definitionKey),
  };
}

export async function listWorkflows(apiBase?: string): Promise<WorkflowAuthoringSummary[]> {
  const res = await fetch(url('/api/workflow-authoring/workflows', apiBase), {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) {
    throw new Error(`Failed to list workflows: ${res.status} ${res.statusText}`);
  }
  const payload = await res.json() as unknown;
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload.map(item => normaliseWorkflowSummary(item as Record<string, unknown>));
}

export async function fetchActionCatalog(apiBase?: string): Promise<ActionCatalogEntry[]> {
  try {
    const res = await fetch(url('/api/workflow-authoring/action-catalog', apiBase), {
      headers: { Accept: 'application/json' },
    });
    if (!res.ok) {
      throw new Error(`Failed to fetch action catalog: ${res.status} ${res.statusText}`);
    }

    const payload = await res.json() as unknown;
    if (!Array.isArray(payload)) {
      return STUB_ACTION_CATALOG;
    }

    return mergeActionCatalog(payload.map(item => normaliseActionCatalogEntry(item as Record<string, unknown>)), STUB_ACTION_CATALOG);
  } catch {
    return STUB_ACTION_CATALOG;
  }
}

export async function fetchWorkflow(key: string, apiBase?: string): Promise<AuthoredWorkflow> {
  const res = await fetch(url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}`, apiBase), {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) throw new Error(`Failed to fetch workflow "${key}": ${res.status} ${res.statusText}`);
  return normaliseWorkflow(await res.json() as Record<string, unknown>);
}

export async function previewProposal(
  key: string,
  proposal: ProposalEnvelope,
  apiBase?: string
): Promise<ProposalEnvelope> {
  const res = await fetch(
    url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/preview`, apiBase),
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(proposal),
    }
  );
  if (!res.ok)
    throw new Error(`Preview failed for "${key}": ${res.status} ${res.statusText}`);
  return res.json() as Promise<ProposalEnvelope>;
}

export async function applyProposal(
  key: string,
  proposal: ProposalEnvelope,
  apiBase?: string,
  approver = 'reference-shell'
): Promise<void> {
  const res = await fetch(
    url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/apply`, apiBase),
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ envelope: proposal, approver }),
    }
  );
  if (!res.ok)
    throw new Error(`Apply failed for "${key}": ${res.status} ${res.statusText}`);
}

export async function publishWorkflow(
  key: string,
  workflow: AuthoredWorkflow,
  apiBase?: string
): Promise<void> {
  const payload = serialiseWorkflow(workflow);
  const res = await fetch(
    url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/publish`, apiBase),
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }
  );
  if (!res.ok)
    throw new Error(`Save failed for "${key}": ${res.status} ${res.statusText}`);
}

export async function projectWorkflow(
  key: string,
  workflow: AuthoredWorkflow,
  apiBase?: string,
  options?: { signal?: AbortSignal }
): Promise<ProjectWorkflowResult> {
  const requestBody = serialiseWorkflow(workflow);
  try {
    const res = await fetch(
      url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/project`, apiBase),
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify(requestBody),
        signal: options?.signal,
      }
    );

    if (!res.ok) {
      throw new Error(`Project failed for "${key}": ${res.status} ${res.statusText}`);
    }

    const payload = await res.json() as unknown;
    if (isProjectWorkflowResult(payload)) {
      return payload;
    }
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
  }

  return projectWorkflowLocally(requestBody);
}

function isProjectWorkflowResult(value: unknown): value is ProjectWorkflowResult {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<ProjectWorkflowResult>;
  return typeof candidate.checksum === 'string'
    && Array.isArray(candidate.diagnostics)
    && typeof candidate.hasErrors === 'boolean'
    && Boolean(candidate.file)
    && Array.isArray(candidate.file?.states)
    && Array.isArray(candidate.file?.transitions);
}
