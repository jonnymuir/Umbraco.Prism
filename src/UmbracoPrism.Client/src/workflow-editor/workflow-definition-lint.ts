import type { AuthoredWorkflow } from './types.js';

/**
 * Definition-tab schema/shape validator. Mirrors the gateway-only model the
 * server (PROJ005/141/142) enforces — runs against the *raw* parsed JSON so the
 * JSON editor can surface inline diagnostics before the document hits the API.
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

  if ('transitions' in root) {
    issues.push({
      message: 'Top-level "transitions" was retired in Slice C. Move routes onto the owning gateway as `gateway.routes[]` and set `gateway.source`.',
      pathHint: 'transitions',
      line: findLine(source, '"transitions"'),
    });
  }

  if ('gateways' in root && root.gateways !== undefined && !Array.isArray(root.gateways)) {
    issues.push({ message: '"gateways" must be an array when present.', pathHint: 'gateways' });
  } else if (Array.isArray(root.gateways)) {
    const seenGatewayKeys = new Set<string>();
    const sourceStageBySplitGateway = new Map<string, string>();
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
      const kind = typeof gateway.kind === 'string' ? gateway.kind : '';
      if (kind && !ALLOWED_GATEWAY_KINDS.has(kind)) {
        issues.push({
          message: `Gateway "${key || index}" has unsupported kind "${kind}". Allowed kinds: ${[...ALLOWED_GATEWAY_KINDS].join(', ')}.`,
          line: findLine(source, `"${kind}"`),
        });
      }

      const source_ = typeof gateway.source === 'string' ? gateway.source : '';
      if (kind === 'Split') {
        if (!source_.trim()) {
          issues.push({
            message: `Split gateway "${key || index}" must declare a "source" stage (PROJ141).`,
            pathHint: key,
          });
        } else if (sourceStageBySplitGateway.has(source_)) {
          issues.push({
            message: `Split gateway "${key || index}" shares source stage "${source_}" with another split gateway. One split gateway per source stage (PROJ143).`,
            pathHint: key,
          });
        } else {
          sourceStageBySplitGateway.set(source_, key);
        }
      } else if (kind === 'Join' && source_.trim()) {
        issues.push({
          message: `Join gateway "${key || index}" must not declare a "source" (PROJ152).`,
          pathHint: key,
        });
      }

      const routes = Array.isArray(gateway.routes) ? gateway.routes : [];
      if (routes.length === 0) {
        issues.push({
          message: `Gateway "${key || index}" must declare at least one route (PROJ144).`,
          pathHint: key,
        });
      }
      const seenRouteIds = new Set<string>();
      const seenTriggerTargets = new Set<string>();
      routes.forEach((rawRoute, routeIndex) => {
        if (!rawRoute || typeof rawRoute !== 'object' || Array.isArray(rawRoute)) {
          issues.push({ message: `Route at index ${routeIndex} on gateway "${key || index}" must be an object.` });
          return;
        }
        const route = rawRoute as Record<string, unknown>;
        const id = typeof route.id === 'string' ? route.id : '';
        if (!id.trim()) {
          issues.push({
            message: `Route ${routeIndex} on gateway "${key || index}" is missing "id" (PROJ145).`,
          });
        } else if (seenRouteIds.has(id)) {
          issues.push({
            message: `Duplicate route id "${id}" on gateway "${key || index}" (PROJ146).`,
          });
        } else {
          seenRouteIds.add(id);
        }
        const trigger = typeof route.trigger === 'string' ? route.trigger : '';
        if (!trigger.trim()) {
          issues.push({
            message: `Route "${id || routeIndex}" on gateway "${key || index}" is missing "trigger" (PROJ147).`,
          });
        }
        const target = typeof route.target === 'string' ? route.target : '';
        if (!target.trim()) {
          issues.push({
            message: `Route "${id || routeIndex}" on gateway "${key || index}" is missing "target" (PROJ149).`,
          });
        }
        const triggerTargetKey = `${trigger}::${target}`;
        if (trigger && target) {
          if (seenTriggerTargets.has(triggerTargetKey)) {
            issues.push({
              message: `Gateway "${key || index}" has two routes with the same (trigger, target) "(${trigger}, ${target})" (PROJ148).`,
            });
          } else {
            seenTriggerTargets.add(triggerTargetKey);
          }
        }
      });
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
    gateways: Array.isArray(root.gateways) ? (root.gateways as AuthoredWorkflow['gateways']) : [],
    roles: Array.isArray(root.roles) ? (root.roles as AuthoredWorkflow['roles']) : undefined,
    authorNote: typeof root.authorNote === 'string' ? root.authorNote : undefined,
  };
}
