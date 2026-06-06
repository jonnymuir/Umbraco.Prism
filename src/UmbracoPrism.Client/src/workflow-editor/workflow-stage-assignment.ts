import type { AuthoredGateway, AuthoredStage, AuthoredWorkflow } from './types.js';
import { gatewayRoleGates, stageActor, stageRoleGates, workflowGateways, workflowQueues, withStageAssignment } from './types.js';

export type StageSurface = 'front-stage' | 'back-stage';
export interface WorkflowQueueDefinition {
  queueName: string;
  displayName?: string;
  description?: string;
}

type LaneAssignedNode = AuthoredStage | AuthoredGateway;

const FRONT_STAGE_ACTORS = new Set(['applicant', 'resident', 'member', 'citizen', 'customer', 'public']);
const BACK_STAGE_ACTORS = new Set(['reviewer', 'caseworker', 'officer', 'administrator', 'admin', 'system']);

export function normaliseLaneKey(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? '';
}

export function humaniseAssignmentLabel(value: string): string {
  return value
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

export function stageSurface(stage: LaneAssignedNode): StageSurface {
  const roleGates = 'metadata' in stage ? stageRoleGates(stage) : gatewayRoleGates(stage);
  if (roleGates.length > 0) {
    return 'back-stage';
  }

  const actor = normaliseLaneKey('metadata' in stage ? stageActor(stage) : stage.actor);
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

export function stageLaneKey(stage: LaneAssignedNode): string {
  const explicitLane = normaliseLaneKey(
    'metadata' in stage
      ? (stage as AuthoredStage).queueKey ?? stage.metadata?.queueKey ?? stage.metadata?.queueName ?? stage.metadata?.laneKey
      : (stage as AuthoredGateway).queueKey ?? (stage as AuthoredGateway).laneKey
  );
  if (explicitLane) {
    return explicitLane;
  }

  const gatedRole = ('metadata' in stage ? stageRoleGates(stage) : gatewayRoleGates(stage)).find(value => value.trim());
  if (gatedRole) {
    return normaliseLaneKey(gatedRole);
  }

  const actor = normaliseLaneKey('metadata' in stage ? stageActor(stage) : stage.actor);
  if (actor) {
    return actor;
  }

  return stageSurface(stage) === 'back-stage' ? 'reviewer' : 'public';
}

export function stageLaneLabel(
  workflow: Pick<AuthoredWorkflow, 'metadata'> | null | undefined,
  laneKey: string,
  availableQueues: ReadonlyArray<WorkflowQueueDefinition> = []
): string {
  const normalised = normaliseLaneKey(laneKey);
  const configuredQueue = availableQueues.find(queue => normaliseLaneKey(queue.queueName) === normalised);
  if (configuredQueue?.displayName?.trim()) {
    return configuredQueue.displayName.trim();
  }

  const workflowQueue = workflowQueues(workflow).find(queue => normaliseLaneKey(queue.key || queue.queueName) === normalised);
  if (workflowQueue?.queueName) {
    const matchingQueue = availableQueues.find(queue => normaliseLaneKey(queue.queueName) === normaliseLaneKey(workflowQueue.queueName));
    if (matchingQueue?.displayName?.trim()) {
      return matchingQueue.displayName.trim();
    }
  }

  return workflowQueue?.displayName?.trim() || humaniseAssignmentLabel(normalised);
}

export function stageLaneDescription(
  workflow: Pick<AuthoredWorkflow, 'metadata'> | null | undefined,
  laneKey: string,
  availableQueues: ReadonlyArray<WorkflowQueueDefinition> = []
): string {
  const normalised = normaliseLaneKey(laneKey);
  const configuredQueue = availableQueues.find(queue => normaliseLaneKey(queue.queueName) === normalised);
  if (configuredQueue?.description?.trim()) {
    return configuredQueue.description.trim();
  }

  return `Stages and gateways in the ${stageLaneLabel(workflow, laneKey, availableQueues)} queue`;
}

export function applyLaneToStage(stage: AuthoredStage, laneKey: string): AuthoredStage {
  const normalisedLaneKey = normaliseLaneKey(laneKey);

  if (!normalisedLaneKey) {
    return withStageAssignment(stage, '', undefined, []);
  }

  const inferredActor = normalisedLaneKey.includes('business')
    ? 'reviewer'
    : normalisedLaneKey.includes('system')
      ? 'system'
      : normalisedLaneKey.includes('review')
        ? 'reviewer'
        : normalisedLaneKey;
  const usesRoleGate = !FRONT_STAGE_ACTORS.has(inferredActor);
  return withStageAssignment(
    stage,
    normalisedLaneKey,
    inferredActor,
    usesRoleGate ? [inferredActor] : []
  );
}

export function workflowLaneOptions(
  workflow: Pick<AuthoredWorkflow, 'metadata' | 'states'> | null | undefined,
  availableQueues: ReadonlyArray<WorkflowQueueDefinition> = []
): string[] {
  const laneKeys = new Set<string>();

  availableQueues.forEach(queue => {
    const key = normaliseLaneKey(queue.queueName);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflowQueues(workflow).forEach(queue => {
    const key = normaliseLaneKey(queue.key || queue.queueName);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflow?.states?.forEach(stage => {
    const key = stageLaneKey(stage);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflowGateways(workflow).forEach(gateway => {
    const key = stageLaneKey(gateway);
    if (key) {
      laneKeys.add(key);
    }
  });

  return [...laneKeys];
}
