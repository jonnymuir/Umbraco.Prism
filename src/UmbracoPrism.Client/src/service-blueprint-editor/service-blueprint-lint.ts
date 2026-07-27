import type { AuthoredServiceBlueprint } from './types.js';
import { hydrateServiceBlueprintDefinition } from './types.js';

export type DefinitionLint = {
  message: string;
  line?: number;
  pathHint?: string;
};

const ALLOWED_STAGE_KINDS = new Set(['Question', 'CheckAnswers', 'Confirmation', 'TaskList']);
const ALLOWED_GATEWAY_KINDS = new Set(['Split', 'Join']);

function findLine(source: string, needle: string): number | undefined {
  const index = source.indexOf(needle);
  if (index < 0) {
    return undefined;
  }
  return source.slice(0, index).split('\n').length;
}

export function lintAuthoredServiceBlueprintDocument(parsed: unknown, source: string): DefinitionLint[] {
  const issues: DefinitionLint[] = [];

  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    issues.push({ message: 'Definition must be a JSON object.' });
    return issues;
  }

  const root = parsed as Record<string, unknown>;

  for (const required of ['definitionKey', 'displayName', 'initialStage']) {
    if (typeof root[required] !== 'string' || !(root[required] as string).trim()) {
      issues.push({
        message: `Missing or empty "${required}".`,
        pathHint: required,
        line: findLine(source, `"${required}"`),
      });
    }
  }

  if (!Array.isArray(root.queues)) {
    issues.push({ message: '"queues" must be an array.', pathHint: 'queues' });
  }

  if (!Array.isArray(root.stages)) {
    issues.push({ message: '"stages" must be an array.', pathHint: 'stages' });
  } else {
    const seenStateKeys = new Set<string>();
    root.stages.forEach((rawState, index) => {
      if (!rawState || typeof rawState !== 'object' || Array.isArray(rawState)) {
        issues.push({ message: `State at index ${index} must be an object.` });
        return;
      }

      const state = rawState as Record<string, unknown>;
      const stateKey = typeof state.stageKey === 'string'
        ? state.stageKey
        : typeof state.stateKey === 'string'
          ? state.stateKey
          : '';
      if (!stateKey.trim()) {
        issues.push({ message: `State at index ${index} is missing "stageKey".` });
      } else if (seenStateKeys.has(stateKey)) {
        issues.push({
          message: `Duplicate stage key "${stateKey}".`,
          line: findLine(source, `"${stateKey}"`),
        });
      } else {
        seenStateKeys.add(stateKey);
      }

      const kind = typeof state.stageType === 'string' && state.stageType
        ? state.stageType
        : typeof state.stageType === 'string' && state.stageType
          ? state.stageType
          : typeof (state.metadata as Record<string, unknown> | undefined)?.stageType === 'string'
            ? String((state.metadata as Record<string, unknown>).stageType)
            : typeof (state.metadata as Record<string, unknown> | undefined)?.stageType === 'string'
              ? String((state.metadata as Record<string, unknown>).stageType)
              : '';
      if (kind && !ALLOWED_STAGE_KINDS.has(kind)) {
        issues.push({
          message: `State "${stateKey || index}" has unsupported stageType "${kind}". Allowed kinds: ${[...ALLOWED_STAGE_KINDS].join(', ')}.`,
          line: findLine(source, `"${kind}"`),
        });
      }

      if (typeof state.queueKey !== 'string' || !state.queueKey.trim()) {
        issues.push({ message: `State "${stateKey || index}" is missing "queueKey".` });
      }

      if (state.routes !== undefined && !Array.isArray(state.routes)) {
        issues.push({ message: `State "${stateKey || index}" has a non-array "routes" value.` });
      }
    });
  }

  if (!Array.isArray(root.gateways)) {
    issues.push({ message: '"gateways" must be an array.', pathHint: 'gateways' });
  } else {
    const seenGatewayKeys = new Set<string>();
    root.gateways.forEach((rawGateway, index) => {
      if (!rawGateway || typeof rawGateway !== 'object' || Array.isArray(rawGateway)) {
        issues.push({ message: `Gateway at index ${index} must be an object.` });
        return;
      }

      const gateway = rawGateway as Record<string, unknown>;
      const key = typeof gateway.key === 'string' ? gateway.key : '';
      if (!key.trim()) {
        issues.push({ message: `Gateway at index ${index} is missing "key".` });
      } else if (seenGatewayKeys.has(key)) {
        issues.push({
          message: `Duplicate gateway key "${key}".`,
          line: findLine(source, `"${key}"`),
        });
      } else {
        seenGatewayKeys.add(key);
      }

      const kind = typeof gateway.gatewayType === 'string' ? gateway.gatewayType : '';
      if (kind && !ALLOWED_GATEWAY_KINDS.has(kind)) {
        issues.push({
          message: `Gateway "${key || index}" has unsupported gatewayType "${kind}". Allowed kinds: ${[...ALLOWED_GATEWAY_KINDS].join(', ')}.`,
          line: findLine(source, `"${kind}"`),
        });
      }

      if (typeof gateway.queueKey !== 'string' || !gateway.queueKey.trim()) {
        issues.push({ message: `Gateway "${key || index}" is missing "queueKey".` });
      }

      if (!Array.isArray(gateway.routes)) {
        issues.push({ message: `Gateway "${key || index}" must declare a "routes" array.` });
      }
    });
  }

  return issues;
}

export function coerceParsedAuthoredServiceBlueprint(parsed: unknown): AuthoredServiceBlueprint {
  const root = parsed as Record<string, unknown>;
  return hydrateServiceBlueprintDefinition({
    definitionKey: String(root.definitionKey ?? ''),
    displayName: String(root.displayName ?? ''),
    version: typeof root.version === 'number' ? root.version : 1,
    initialStage: String(root.initialStage ?? ''),
    requestPolicy: String(root.requestPolicy ?? 'single'),
    description: typeof root.description === 'string' ? root.description : undefined,
    schemaVersion: typeof root.schemaVersion === 'string' ? root.schemaVersion : undefined,
    queues: Array.isArray(root.queues) ? (root.queues as AuthoredServiceBlueprint['queues']) : [],
    stages: Array.isArray(root.stages) ? (root.stages as AuthoredServiceBlueprint['stages']) : [],
    gateways: Array.isArray(root.gateways) ? (root.gateways as AuthoredServiceBlueprint['gateways']) : [],
    calculations: root.calculations
      ? (root.calculations as AuthoredServiceBlueprint['calculations'])
      : undefined,
    parameterSchemas: Array.isArray(root.parameterSchemas)
      ? (root.parameterSchemas as AuthoredServiceBlueprint['parameterSchemas'])
      : undefined,
    layout: root.layout ? (root.layout as AuthoredServiceBlueprint['layout']) : undefined,
  });
}
