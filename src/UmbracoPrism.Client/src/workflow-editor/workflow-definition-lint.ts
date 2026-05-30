import type { AuthoredWorkflow } from './types.js';

/**
 * Definition-tab schema/shape validator. Mirrors the gateway-only model the
 * server (PROJ140/141/142) enforces — but acts on the *raw* parsed JSON before
 * the authoring client's silent `Waiting` → `Question` rewrite would mask
 * problems.
 *
 * Returns a list of issues. Each carries a short author-facing message and an
 * optional 1-based line number derived from the editor source so the JSON
 * editor can render an inline diagnostic.
 */
export type DefinitionLint = {
  message: string;
  /** 1-based line in the source text, when locatable. */
  line?: number;
  /** A token that identifies the issue's location for inline lookup. */
  pathHint?: string;
};

const RETIRED_STAGE_KINDS = new Set(['Waiting', 'StatusTimeline']);
const ALLOWED_STAGE_KINDS = new Set(['Question', 'CheckAnswers', 'Confirmation', 'TaskList']);
const ALLOWED_GATEWAY_KINDS = new Set(['Split', 'Join']);

function findLine(source: string, needle: string): number | undefined {
  const index = source.indexOf(needle);
  if (index < 0) {
    return undefined;
  }
  return source.slice(0, index).split('\n').length;
}

export function lintAuthoredWorkflowDocument(
  parsed: unknown,
  source: string
): DefinitionLint[] {
  const issues: DefinitionLint[] = [];

  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    issues.push({ message: 'Definition must be a JSON object.' });
    return issues;
  }

  const root = parsed as Record<string, unknown>;

  for (const required of ['definitionKey', 'displayName', 'initialStageKey']) {
    if (typeof root[required] !== 'string' || !(root[required] as string).trim()) {
      issues.push({
        message: `Missing or empty "${required}".`,
        pathHint: required,
        line: findLine(source, `"${required}"`),
      });
    }
  }

  if (!Array.isArray(root.stages)) {
    issues.push({ message: '"stages" must be an array.', pathHint: 'stages' });
  } else {
    const seenStageKeys = new Set<string>();
    root.stages.forEach((rawStage, index) => {
      if (!rawStage || typeof rawStage !== 'object' || Array.isArray(rawStage)) {
        issues.push({ message: `Stage at index ${index} must be an object.` });
        return;
      }
      const stage = rawStage as Record<string, unknown>;
      const stageKey = typeof stage.stageKey === 'string' ? stage.stageKey : '';
      if (!stageKey.trim()) {
        issues.push({ message: `Stage at index ${index} is missing "stageKey".` });
      } else if (seenStageKeys.has(stageKey)) {
        issues.push({
          message: `Duplicate stage key "${stageKey}".`,
          line: findLine(source, `"${stageKey}"`),
        });
      } else {
        seenStageKeys.add(stageKey);
      }

      const kind = typeof stage.kind === 'string' ? stage.kind : '';
      if (RETIRED_STAGE_KINDS.has(kind)) {
        issues.push({
          message: `Stage "${stageKey || index}" uses retired kind "${kind}". Move waiting copy onto a join gateway and use "Question" instead.`,
          line: findLine(source, `"${kind}"`),
          pathHint: stageKey,
        });
      } else if (kind && !ALLOWED_STAGE_KINDS.has(kind)) {
        issues.push({
          message: `Stage "${stageKey || index}" has unsupported kind "${kind}". Allowed kinds: ${[...ALLOWED_STAGE_KINDS].join(', ')}.`,
          line: findLine(source, `"${kind}"`),
        });
      }

      if ('statusTimeline' in stage) {
        issues.push({
          message: `Stage "${stageKey || index}" has a "statusTimeline" payload. The retired StatusTimeline stage type was replaced by gateway waiting copy.`,
        });
      }
    });
  }

  if (!Array.isArray(root.transitions)) {
    issues.push({ message: '"transitions" must be an array.', pathHint: 'transitions' });
  }

  if ('gateways' in root && root.gateways !== undefined && !Array.isArray(root.gateways)) {
    issues.push({ message: '"gateways" must be an array when present.', pathHint: 'gateways' });
  } else if (Array.isArray(root.gateways)) {
    const seenGatewayKeys = new Set<string>();
    root.gateways.forEach((rawGateway, index) => {
      if (!rawGateway || typeof rawGateway !== 'object' || Array.isArray(rawGateway)) {
        issues.push({ message: `Gateway at index ${index} must be an object.` });
        return;
      }
      const gateway = rawGateway as Record<string, unknown>;
      const key = typeof gateway.gatewayKey === 'string' ? gateway.gatewayKey : '';
      if (!key.trim()) {
        issues.push({ message: `Gateway at index ${index} is missing "gatewayKey".` });
      } else if (seenGatewayKeys.has(key)) {
        issues.push({
          message: `Duplicate gateway key "${key}".`,
          line: findLine(source, `"${key}"`),
        });
      } else {
        seenGatewayKeys.add(key);
      }
      if (!key.trim()) {
        issues.push({
          message: `Gateway at index ${index} has no name. Named gateways are required.`,
        });
      }
      const kind = typeof gateway.kind === 'string' ? gateway.kind : '';
      if (kind && !ALLOWED_GATEWAY_KINDS.has(kind)) {
        issues.push({
          message: `Gateway "${key || index}" has unsupported kind "${kind}". Allowed kinds: ${[...ALLOWED_GATEWAY_KINDS].join(', ')}.`,
          line: findLine(source, `"${kind}"`),
        });
      }
    });
  }

  return issues;
}

/**
 * Apply minimal coercions so the parsed object slots into AuthoredWorkflow's
 * runtime shape. Callers should only invoke this after `lintAuthoredWorkflowDocument`
 * returned no issues.
 */
export function coerceParsedAuthoredWorkflow(parsed: unknown): AuthoredWorkflow {
  const root = parsed as Record<string, unknown>;
  return {
    definitionKey: String(root.definitionKey ?? ''),
    displayName: String(root.displayName ?? ''),
    version: typeof root.version === 'number' ? root.version : 1,
    schemaVersion: String(root.schemaVersion ?? '1.0'),
    instancePolicy: String(root.instancePolicy ?? 'single'),
    initialStageKey: String(root.initialStageKey ?? ''),
    stages: Array.isArray(root.stages) ? (root.stages as AuthoredWorkflow['stages']) : [],
    transitions: Array.isArray(root.transitions)
      ? (root.transitions as AuthoredWorkflow['transitions'])
      : [],
    gateways: Array.isArray(root.gateways) ? (root.gateways as AuthoredWorkflow['gateways']) : [],
    roles: Array.isArray(root.roles) ? (root.roles as AuthoredWorkflow['roles']) : undefined,
    authorNote: typeof root.authorNote === 'string' ? root.authorNote : undefined,
  };
}
