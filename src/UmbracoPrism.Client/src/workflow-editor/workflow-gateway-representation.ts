import type { AuthoredGateway, AuthoredWorkflow } from './types.js';
import { workflowGateways } from './types.js';
import { flattenRoutes } from './workflow-routes.js';
import { normaliseLaneKey, stageLaneKey } from './workflow-stage-assignment.js';

export interface GatewayBinding {
  gateway: AuthoredGateway;
  laneKey: string;
  anchorStageKey: string | null;
  relatedTransitionIndices: number[];
}

function shiftCandidate(
  candidatesByLane: Map<string, string[]>,
  laneKey: string
): string | null {
  const direct = candidatesByLane.get(laneKey);
  if (direct && direct.length > 0) {
    return direct.shift() ?? null;
  }

  for (const candidates of candidatesByLane.values()) {
    if (candidates.length > 0) {
      return candidates.shift() ?? null;
    }
  }

  return null;
}

export function gatewayLaneKey(gateway: AuthoredGateway): string {
  return normaliseLaneKey(gateway.queueKey ?? gateway.laneKey) || normaliseLaneKey(gateway.actor);
}

export function deriveGatewayBindings(workflow: Pick<AuthoredWorkflow, 'states' | 'gateways' | 'metadata'>): GatewayBinding[] {
  const outgoingByStage = new Map<string, number[]>();
  const incomingByStage = new Map<string, number[]>();
  const explicitSplitBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();
  const explicitJoinBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();
  const routes = flattenRoutes(workflow);

  routes.forEach((transition, index) => {
    outgoingByStage.set(transition.fromStage, [...(outgoingByStage.get(transition.fromStage) ?? []), index]);
    incomingByStage.set(transition.toStage, [...(incomingByStage.get(transition.toStage) ?? []), index]);

    if (transition.fromGateway) {
      const existing = explicitSplitBindings.get(transition.fromGateway);
      explicitSplitBindings.set(transition.fromGateway, {
        anchorStageKey: existing?.anchorStageKey ?? routes.find(route => route.toStage === transition.fromGateway && !route.fromGateway)?.fromStage ?? null,
        relatedTransitionIndices: [...(existing?.relatedTransitionIndices ?? []), index],
      });
    }

    if (transition.toGateway) {
      const existing = explicitJoinBindings.get(transition.toGateway);
      explicitJoinBindings.set(transition.toGateway, {
        anchorStageKey: existing?.anchorStageKey ?? routes.find(route => route.toStage === transition.toGateway && !route.fromGateway)?.fromStage ?? null,
        relatedTransitionIndices: [...(existing?.relatedTransitionIndices ?? []), index],
      });
    }
  });

  const splitCandidatesByLane = new Map<string, string[]>();
  const joinCandidatesByLane = new Map<string, string[]>();

  workflow.states.forEach(stage => {
    const stageKey = stage.stateKey;
    const laneKey = stageLaneKey(stage);
    const outgoing = outgoingByStage.get(stageKey) ?? [];
    const incoming = incomingByStage.get(stageKey) ?? [];

    if (outgoing.length > 1) {
      splitCandidatesByLane.set(laneKey, [...(splitCandidatesByLane.get(laneKey) ?? []), stageKey]);
    }

    if (incoming.length > 1) {
      joinCandidatesByLane.set(laneKey, [...(joinCandidatesByLane.get(laneKey) ?? []), stageKey]);
    }
  });

  return workflowGateways(workflow).map(gateway => {
    const laneKey = gatewayLaneKey(gateway);
    const explicitBinding = gateway.gatewayType === 'Split'
      ? explicitSplitBindings.get(gateway.key)
      : explicitJoinBindings.get(gateway.key);
    const anchorStageKey = explicitBinding?.anchorStageKey ?? (
      gateway.gatewayType === 'Split'
        ? shiftCandidate(splitCandidatesByLane, laneKey)
        : shiftCandidate(joinCandidatesByLane, laneKey)
    );

    return {
      gateway,
      laneKey,
      anchorStageKey,
      relatedTransitionIndices:
        explicitBinding?.relatedTransitionIndices
        ?? (anchorStageKey === null
          ? []
          : gateway.gatewayType === 'Split'
            ? (outgoingByStage.get(anchorStageKey) ?? [])
            : (incomingByStage.get(anchorStageKey) ?? [])),
    };
  });
}
