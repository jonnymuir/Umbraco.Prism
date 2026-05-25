import type { AuthoredStage, AuthoredWorkflow } from './types.js';

export type StageSurface = 'front-stage' | 'back-stage';

const FRONT_STAGE_ACTORS = new Set(['applicant', 'resident', 'member', 'citizen', 'customer', 'public']);
const BACK_STAGE_ACTORS = new Set(['reviewer', 'caseworker', 'officer', 'administrator', 'admin', 'system']);

export function humaniseAssignmentLabel(value: string): string {
  return value
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

export function stageSurface(stage: Pick<AuthoredStage, 'actor' | 'roleGates'>): StageSurface {
  if ((stage.roleGates?.length ?? 0) > 0) {
    return 'back-stage';
  }

  const actor = stage.actor?.trim().toLowerCase();
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

export function stageLaneKey(stage: Pick<AuthoredStage, 'actor' | 'roleGates'>): string {
  const gatedRole = stage.roleGates.find(value => value.trim());
  if (gatedRole) {
    return gatedRole.trim().toLowerCase();
  }

  const actor = stage.actor?.trim().toLowerCase();
  if (actor) {
    return actor;
  }

  return stageSurface(stage) === 'back-stage' ? 'reviewer' : 'public';
}

export function stageLaneLabel(
  workflow: Pick<AuthoredWorkflow, 'roles'> | null | undefined,
  laneKey: string
): string {
  const normalised = laneKey.trim().toLowerCase();
  const workflowRole = workflow?.roles?.find(role =>
    role.roleKey.trim().toLowerCase() === normalised
    || role.claimMapping?.trim().toLowerCase() === normalised
  );

  return workflowRole?.displayName?.trim() || humaniseAssignmentLabel(normalised);
}

export function stageLaneDescription(
  workflow: Pick<AuthoredWorkflow, 'roles'> | null | undefined,
  laneKey: string
): string {
  return `${stageLaneLabel(workflow, laneKey)} stages and handoffs`;
}
