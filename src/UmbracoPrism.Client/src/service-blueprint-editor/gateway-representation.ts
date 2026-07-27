import type { AuthoredGateway, AuthoredServiceBlueprint } from './types.js';
import { serviceBlueprintGateways } from './types.js';
import { flattenRoutes } from './route-model.js';
import { normaliseQueueKey, stageQueueKey } from './stage-assignment.js';

export interface GatewayBinding {
  gateway: AuthoredGateway;
  queueKey: string;
  anchorStageKey: string | null;
  relatedTransitionIndices: number[];
}

function shiftCandidate(
  candidatesByQueue: Map<string, string[]>,
  queueKey: string
): string | null {
  const direct = candidatesByQueue.get(queueKey);
  if (direct && direct.length > 0) {
    return direct.shift() ?? null;
  }

  for (const candidates of candidatesByQueue.values()) {
    if (candidates.length > 0) {
      return candidates.shift() ?? null;
    }
  }

  return null;
}

export function gatewayQueueKey(gateway: AuthoredGateway): string {
  return normaliseQueueKey(gateway.queueKey) || normaliseQueueKey(gateway.actor);
}

export function deriveGatewayBindings(serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'>): GatewayBinding[] {
  const outgoingByStage = new Map<string, number[]>();
  const incomingByStage = new Map<string, number[]>();
  const explicitSplitBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();
  const explicitJoinBindings = new Map<string, { anchorStageKey: string | null; relatedTransitionIndices: number[] }>();
  const routes = flattenRoutes(serviceBlueprint);

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

  const splitCandidatesByQueue = new Map<string, string[]>();
  const joinCandidatesByQueue = new Map<string, string[]>();

  serviceBlueprint.stages.forEach(stage => {
    const stageKey = stage.stateKey;
    const queueKey = stageQueueKey(stage);
    const outgoing = outgoingByStage.get(stageKey) ?? [];
    const incoming = incomingByStage.get(stageKey) ?? [];

    if (outgoing.length > 1) {
      splitCandidatesByQueue.set(queueKey, [...(splitCandidatesByQueue.get(queueKey) ?? []), stageKey]);
    }

    if (incoming.length > 1) {
      joinCandidatesByQueue.set(queueKey, [...(joinCandidatesByQueue.get(queueKey) ?? []), stageKey]);
    }
  });

  return serviceBlueprintGateways(serviceBlueprint).map(gateway => {
    const queueKey = gatewayQueueKey(gateway);
    const explicitBinding = gateway.gatewayType === 'Split'
      ? explicitSplitBindings.get(gateway.key)
      : explicitJoinBindings.get(gateway.key);
    const anchorStageKey = explicitBinding?.anchorStageKey ?? (
      gateway.gatewayType === 'Split'
        ? shiftCandidate(splitCandidatesByQueue, queueKey)
        : shiftCandidate(joinCandidatesByQueue, queueKey)
    );

    return {
      gateway,
      queueKey,
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
