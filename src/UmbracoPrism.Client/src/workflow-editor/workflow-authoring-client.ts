/**
 * HTTP client for the Workflow Authoring API (Blathers' surface).
 * Targets:
 *   1. explicit host shell configuration
 *   2. VITE_AUTHORING_API_BASE
 *   3. current origin when hosted alongside the API
 *   4. https://localhost:7245 as the local-development fallback
 *
 * Endpoints:
 *   GET  /api/workflow-authoring/workflows
 *   GET  /api/workflow-authoring/workflows/{key}
 *   POST /api/workflow-authoring/workflows/{key}/preview
 *   POST /api/workflow-authoring/workflows/{key}/apply
 */

import type { AuthoredWorkflow, ProposalEnvelope } from './types.js';

export type WorkflowAuthoringSummary = {
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
  const candidate = base?.trim() || defaultAuthoringApiBase();
  return candidate.replace(/\/+$/, '');
}

function url(path: string, apiBase?: string): string {
  return `${normaliseAuthoringApiBase(apiBase)}${path}`;
}

export async function listWorkflows(apiBase?: string): Promise<WorkflowAuthoringSummary[]> {
  const res = await fetch(url('/api/workflow-authoring/workflows', apiBase), {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) {
    throw new Error(`Failed to list workflows: ${res.status} ${res.statusText}`);
  }
  return res.json() as Promise<WorkflowAuthoringSummary[]>;
}

export async function fetchWorkflow(key: string, apiBase?: string): Promise<AuthoredWorkflow> {
  const res = await fetch(url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}`, apiBase), {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) throw new Error(`Failed to fetch workflow "${key}": ${res.status} ${res.statusText}`);
  return res.json() as Promise<AuthoredWorkflow>;
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
