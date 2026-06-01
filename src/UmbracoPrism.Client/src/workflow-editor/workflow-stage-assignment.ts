import type { AuthoredGateway, AuthoredStage, AuthoredWorkflow } from './types.js';

export type StageSurface = 'front-stage' | 'back-stage';
export interface WorkflowQueueDefinition {
  queueName: string;
  displayName?: string;
  description?: string;
}

type LaneAssignedNode = Pick<AuthoredStage | AuthoredGateway, 'actor' | 'roleGates'> & {
  laneKey?: string;
};

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
  if ((stage.roleGates?.length ?? 0) > 0) {
    return 'back-stage';
  }

  const actor = normaliseLaneKey(stage.actor);
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
  const explicitLane = normaliseLaneKey(stage.laneKey);
  if (explicitLane) {
    return explicitLane;
  }

  const gatedRole = stage.roleGates.find(value => value.trim());
  if (gatedRole) {
    return normaliseLaneKey(gatedRole);
  }

  const actor = normaliseLaneKey(stage.actor);
  if (actor) {
    return actor;
  }

  return stageSurface(stage) === 'back-stage' ? 'reviewer' : 'public';
}

export function stageLaneLabel(
  workflow: Pick<AuthoredWorkflow, 'roles'> | null | undefined,
  laneKey: string,
  availableQueues: ReadonlyArray<WorkflowQueueDefinition> = []
): string {
  const normalised = normaliseLaneKey(laneKey);
  const configuredQueue = availableQueues.find(queue => normaliseLaneKey(queue.queueName) === normalised);
  if (configuredQueue?.displayName?.trim()) {
    return configuredQueue.displayName.trim();
  }

  const workflowRole = workflow?.roles?.find(role =>
    normaliseLaneKey(role.roleKey) === normalised
    || normaliseLaneKey(role.claimMapping) === normalised
  );

  return workflowRole?.displayName?.trim() || humaniseAssignmentLabel(normalised);
}

export function stageLaneDescription(
  workflow: Pick<AuthoredWorkflow, 'roles'> | null | undefined,
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
    return {
      ...stage,
      actor: undefined,
      roleGates: [],
    };
  }

  const usesRoleGate = !FRONT_STAGE_ACTORS.has(normalisedLaneKey);

  return {
    ...stage,
    actor: normalisedLaneKey,
    roleGates: usesRoleGate ? [normalisedLaneKey] : [],
  };
}

export function workflowLaneOptions(
  workflow: Pick<AuthoredWorkflow, 'roles' | 'stages' | 'gateways'> | null | undefined,
  availableQueues: ReadonlyArray<WorkflowQueueDefinition> = []
): string[] {
  const laneKeys = new Set<string>();

  availableQueues.forEach(queue => {
    const key = normaliseLaneKey(queue.queueName);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflow?.roles?.forEach(role => {
    const key = normaliseLaneKey(role.roleKey || role.claimMapping);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflow?.stages?.forEach(stage => {
    const key = stageLaneKey(stage);
    if (key) {
      laneKeys.add(key);
    }
  });

  workflow?.gateways?.forEach(gateway => {
    const key = stageLaneKey(gateway);
    if (key) {
      laneKeys.add(key);
    }
  });

  return [...laneKeys];
}
