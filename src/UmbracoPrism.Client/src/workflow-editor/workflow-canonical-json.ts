import type { AuthoredWorkflow } from './types.js';

/**
 * Stable, deterministic JSON serialization for the AuthoredWorkflow document
 * used by the Definition tab. Top-level keys are emitted in a fixed
 * authoring-friendly order; all other object keys are sorted alphabetically.
 * Output uses 2-space indentation.
 *
 * Stable order matters because the JSON pane and the visual pane round-trip
 * the document — a deterministic shape avoids spurious diffs.
 */
const TOP_LEVEL_KEY_ORDER: readonly string[] = [
  'definitionKey',
  'displayName',
  'version',
  'schemaVersion',
  'instancePolicy',
  'initialStageKey',
  'authorNote',
  'roles',
  'stages',
  'gateways',
  'transitions',
];

function orderTopLevel(value: Record<string, unknown>): Record<string, unknown> {
  const ordered: Record<string, unknown> = {};
  for (const key of TOP_LEVEL_KEY_ORDER) {
    if (key in value) {
      ordered[key] = value[key];
    }
  }
  for (const key of Object.keys(value).sort()) {
    if (!(key in ordered)) {
      ordered[key] = value[key];
    }
  }
  return ordered;
}

function sortKeys(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(sortKeys);
  }
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const sorted: Record<string, unknown> = {};
    for (const key of Object.keys(record).sort()) {
      sorted[key] = sortKeys(record[key]);
    }
    return sorted;
  }
  return value;
}

export function serializeAuthoredWorkflow(workflow: AuthoredWorkflow): string {
  const top = orderTopLevel(workflow as unknown as Record<string, unknown>);
  const canonical: Record<string, unknown> = {};
  for (const key of Object.keys(top)) {
    canonical[key] = sortKeys(top[key]);
  }
  return JSON.stringify(canonical, null, 2);
}

/** Quick semantic equality — both sides through the same canonical form. */
export function authoredWorkflowJsonEquals(
  left: AuthoredWorkflow | null,
  right: AuthoredWorkflow | null
): boolean {
  if (!left && !right) {
    return true;
  }
  if (!left || !right) {
    return false;
  }
  return serializeAuthoredWorkflow(left) === serializeAuthoredWorkflow(right);
}
