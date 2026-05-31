import type { AuthoredGateway, AuthoredWorkflow } from './types.js';
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
  return normaliseLaneKey(gateway.laneKey) || stageLaneKey(gateway);
}

export function deriveGatewayBindings(workflow: Pick<AuthoredWorkflow, 'stages' | 'gateways'>): GatewayBinding[] {
  const stageByKey = new Map(workflow.stages.map(stage => [stage.stageKey, stage]));
  const outgoingByStage = new Map<string, number[]>();
  const incomingByStage = new Map<string, number[]>();
  const explicitSplitBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();
  const explicitJoinBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();

  (flattenRoutes(workflow)).forEach((transition, index) => {
    outgoingByStage.set(transition.fromStage, [...(outgoingByStage.get(transition.fromStage) ?? []), index]);
    incomingByStage.set(transition.toStage, [...(incomingByStage.get(transition.toStage) ?? []), index]);

    if (transition.fromGateway) {
      const existing = explicitSplitBindings.get(transition.fromGateway);
      explicitSplitBindings.set(transition.fromGateway, {
        anchorStageKey: existing?.anchorStageKey ?? transition.fromStage,
        relatedTransitionIndices: [...(existing?.relatedTransitionIndices ?? []), index],
      });
    }

    if (transition.toGateway) {
      const existing = explicitJoinBindings.get(transition.toGateway);
      explicitJoinBindings.set(transition.toGateway, {
        anchorStageKey: existing?.anchorStageKey ?? transition.toStage,
        relatedTransitionIndices: [...(existing?.relatedTransitionIndices ?? []), index],
      });
    }
  });

  const splitCandidatesByLane = new Map<string, string[]>();
  const joinCandidatesByLane = new Map<string, string[]>();

  workflow.stages.forEach(stage => {
    const stageKey = stage.stageKey;
    const laneKey = stageLaneKey(stage);
    const outgoing = outgoingByStage.get(stageKey) ?? [];
    const incoming = incomingByStage.get(stageKey) ?? [];

    if (outgoing.length > 1) {
      const targetLanes = new Set(
        outgoing
          .map(index => stageByKey.get((flattenRoutes(workflow))[index].toStage))
          .filter(Boolean)
          .map(target => stageLaneKey(target!))
      );
      if (targetLanes.size > 1 || outgoing.length > 1) {
        splitCandidatesByLane.set(laneKey, [...(splitCandidatesByLane.get(laneKey) ?? []), stageKey]);
      }
    }

    if (incoming.length > 1) {
      const sourceLanes = new Set(
        incoming
          .map(index => stageByKey.get((flattenRoutes(workflow))[index].fromStage))
          .filter(Boolean)
          .map(source => stageLaneKey(source!))
      );
      if (sourceLanes.size > 1 || incoming.length > 1) {
        joinCandidatesByLane.set(laneKey, [...(joinCandidatesByLane.get(laneKey) ?? []), stageKey]);
      }
    }
  });

  return (workflow.gateways ?? []).map(gateway => {
    const laneKey = gatewayLaneKey(gateway);
    const explicitBinding = gateway.kind === 'Split'
      ? explicitSplitBindings.get(gateway.gatewayKey)
      : explicitJoinBindings.get(gateway.gatewayKey);
    const anchorStageKey = explicitBinding?.anchorStageKey ?? (
      gateway.kind === 'Split'
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
          : gateway.kind === 'Split'
            ? (outgoingByStage.get(anchorStageKey) ?? [])
            : (incomingByStage.get(anchorStageKey) ?? [])),
    };
  });
}
