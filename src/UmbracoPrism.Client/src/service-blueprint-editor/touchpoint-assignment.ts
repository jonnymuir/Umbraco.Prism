import type { AuthoredGateway, AuthoredTouchpoint, AuthoredServiceBlueprint } from './types.js';
import { gatewayRoleGates, touchpointActor, touchpointRoleGates, serviceBlueprintGateways, serviceBlueprintQueues, withTouchpointAssignment } from './types.js';

export type TouchpointSurface = 'front-stage' | 'back-stage';
export interface QueueDefinition {
  queueName: string;
  displayName?: string;
  description?: string;
}

type QueueAssignedNode = AuthoredTouchpoint | AuthoredGateway;

const FRONT_STAGE_ACTORS = new Set(['applicant', 'resident', 'member', 'citizen', 'customer', 'public']);
const BACK_STAGE_ACTORS = new Set(['reviewer', 'caseworker', 'officer', 'administrator', 'admin', 'system']);

export function normaliseQueueKey(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? '';
}

export function humaniseAssignmentLabel(value: string): string {
  return value
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

export function touchpointSurface(stage: QueueAssignedNode): TouchpointSurface {
  const roleGates = 'metadata' in stage ? touchpointRoleGates(stage) : gatewayRoleGates(stage);
  if (roleGates.length > 0) {
    return 'back-stage';
  }

  const actor = normaliseQueueKey('metadata' in stage ? touchpointActor(stage) : stage.actor);
  if (!actor) {
    return 'front-stage';
  }

  if (BACK_STAGE_ACTORS.has(actor)) {
    return 'back-stage';
  }

  if (FRONT_STAGE_ACTORS.has(actor)) {
    return 'front-stage';
  }

  return actor.includes('review') || actor.includes('case') || actor.includes('system')
    ? 'back-stage'
    : 'front-stage';
}

export function touchpointQueueKey(stage: QueueAssignedNode): string {
  const explicitQueue = normaliseQueueKey(
    'metadata' in stage
      ? (stage as AuthoredTouchpoint).queueKey ?? stage.metadata?.queueKey ?? stage.metadata?.queueName
      : (stage as AuthoredGateway).queueKey
  );
  if (explicitQueue) {
    return explicitQueue;
  }

  const gatedRole = ('metadata' in stage ? touchpointRoleGates(stage) : gatewayRoleGates(stage)).find(value => value.trim());
  if (gatedRole) {
    return normaliseQueueKey(gatedRole);
  }

  const actor = normaliseQueueKey('metadata' in stage ? touchpointActor(stage) : stage.actor);
  if (actor) {
    return actor;
  }

  return touchpointSurface(stage) === 'back-stage' ? 'reviewer' : 'public';
}

export function touchpointQueueLabel(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'queues'> | null | undefined,
  queueKey: string,
  availableQueues: ReadonlyArray<QueueDefinition> = []
): string {
  const normalised = normaliseQueueKey(queueKey);
  const configuredQueue = availableQueues.find(queue => normaliseQueueKey(queue.queueName) === normalised);
  if (configuredQueue?.displayName?.trim()) {
    return configuredQueue.displayName.trim();
  }

  const serviceBlueprintQueue = serviceBlueprintQueues(serviceBlueprint).find(queue => normaliseQueueKey(queue.key || queue.queueName) === normalised);
  if (serviceBlueprintQueue?.queueName) {
    const matchingQueue = availableQueues.find(queue => normaliseQueueKey(queue.queueName) === normaliseQueueKey(serviceBlueprintQueue.queueName));
    if (matchingQueue?.displayName?.trim()) {
      return matchingQueue.displayName.trim();
    }
  }

  return serviceBlueprintQueue?.displayName?.trim() || humaniseAssignmentLabel(normalised);
}

export function touchpointQueueDescription(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'queues'> | null | undefined,
  queueKey: string,
  availableQueues: ReadonlyArray<QueueDefinition> = []
): string {
  const normalised = normaliseQueueKey(queueKey);
  const configuredQueue = availableQueues.find(queue => normaliseQueueKey(queue.queueName) === normalised);
  if (configuredQueue?.description?.trim()) {
    return configuredQueue.description.trim();
  }

  return `Stages and gateways in the ${touchpointQueueLabel(serviceBlueprint, queueKey, availableQueues)} queue`;
}

export function applyQueueToTouchpoint(stage: AuthoredTouchpoint, queueKey: string): AuthoredTouchpoint {
  const normalisedQueueKey = normaliseQueueKey(queueKey);

  if (!normalisedQueueKey) {
    return withTouchpointAssignment(stage, '', undefined, []);
  }

  const inferredActor = normalisedQueueKey.includes('business')
    ? 'reviewer'
    : normalisedQueueKey.includes('system')
      ? 'system'
      : normalisedQueueKey.includes('review')
        ? 'reviewer'
        : normalisedQueueKey;
  const usesRoleGate = !FRONT_STAGE_ACTORS.has(inferredActor);
  return withTouchpointAssignment(
    stage,
    normalisedQueueKey,
    inferredActor,
    usesRoleGate ? [inferredActor] : []
  );
}

export function serviceBlueprintQueueOptions(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'queues' | 'touchpoints' | 'gateways' | 'metadata'> | null | undefined,
  availableQueues: ReadonlyArray<QueueDefinition> = []
): string[] {
  const queueKeys = new Set<string>();

  availableQueues.forEach(queue => {
    const key = normaliseQueueKey(queue.queueName);
    if (key) {
      queueKeys.add(key);
    }
  });

  serviceBlueprintQueues(serviceBlueprint).forEach(queue => {
    const key = normaliseQueueKey(queue.key || queue.queueName);
    if (key) {
      queueKeys.add(key);
    }
  });

  serviceBlueprint?.touchpoints?.forEach(stage => {
    const key = touchpointQueueKey(stage);
    if (key) {
      queueKeys.add(key);
    }
  });

  serviceBlueprintGateways(serviceBlueprint).forEach(gateway => {
    const key = touchpointQueueKey(gateway);
    if (key) {
      queueKeys.add(key);
    }
  });

  return [...queueKeys];
}
