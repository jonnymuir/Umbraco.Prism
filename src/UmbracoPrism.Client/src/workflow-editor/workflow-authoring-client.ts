/**
 * HTTP client for the Workflow Authoring API (Blathers' surface).
 * Targets VITE_AUTHORING_API_BASE (default: https://localhost:7245).
 *
 * Endpoints:
 *   GET  /api/workflow-authoring/workflows/{key}
 *   POST /api/workflow-authoring/workflows/{key}/preview
 *   POST /api/workflow-authoring/workflows/{key}/apply
 */

import type { AuthoredWorkflow, ProposalEnvelope } from './types.js';

const BASE: string =
  (import.meta.env?.VITE_AUTHORING_API_BASE as string | undefined) ?? 'https://localhost:7245';

function url(path: string): string {
  return `${BASE}${path}`;
}

export async function fetchWorkflow(key: string): Promise<AuthoredWorkflow> {
  const res = await fetch(url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}`), {
    headers: { Accept: 'application/json' },
  });
  if (!res.ok) throw new Error(`Failed to fetch workflow "${key}": ${res.status} ${res.statusText}`);
  return res.json() as Promise<AuthoredWorkflow>;
}

export async function previewProposal(
  key: string,
  proposal: ProposalEnvelope
): Promise<ProposalEnvelope> {
  const res = await fetch(
    url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/preview`),
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
  proposal: ProposalEnvelope
): Promise<void> {
  const res = await fetch(
    url(`/api/workflow-authoring/workflows/${encodeURIComponent(key)}/apply`),
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(proposal),
    }
  );
  if (!res.ok)
    throw new Error(`Apply failed for "${key}": ${res.status} ${res.statusText}`);
}
