import { MarkerType, type Edge, type Node } from '@xyflow/react';
import type { AuthoredWorkflow, RouteView } from '../types.js';
import type { GraphProps } from './graph-callbacks.js';
import {
  computeWorkflowGraphLayout,
  parseGraphNodeId,
  type GatewayTopologyNode,
  type GraphTopology,
  type GraphTopologyEdge,
  type LaneGeometry,
  type StageTopologyNode,
  type WorkflowGraphLayout,
} from './workflow-graph-layout.js';

const EDGE_ARROW_COLOR = '#6b7280';

export type StageNodeData = {
  node: StageTopologyNode;
  rowRank: number;
  selected: boolean;
  simulationPath: boolean;
  simulationCurrent: boolean;
  readOnly: boolean;
  [key: string]: unknown;
};

export type GatewayNodeData = {
  node: GatewayTopologyNode;
  rowRank: number;
  selected: boolean;
  readOnly: boolean;
  routeCount: number;
  triggerLabel: string;
  conditionLabel: string | null;
  [key: string]: unknown;
};

export type TransitionChip = {
  index: number;
  label: string;
  ariaLabel: string;
  fromKey: string;
  toKey: string;
  selected: boolean;
  simulationPath: boolean;
  branch: boolean;
  merge: boolean;
};

export type RouteEdgeData = {
  edge: GraphTopologyEdge;
  fromKey: string;
  toKey: string;
  simulationPath: boolean;
  chips: TransitionChip[];
  readOnly: boolean;
  [key: string]: unknown;
};

export type StageFlowNode = Node<StageNodeData, 'stage'>;
export type GatewayFlowNode = Node<GatewayNodeData, 'gateway'>;
export type GraphFlowNode = StageFlowNode | GatewayFlowNode;
export type RouteFlowEdge = Edge<RouteEdgeData, 'route'>;

export type GraphModel = {
  nodes: GraphFlowNode[];
  edges: RouteFlowEdge[];
  lanes: LaneGeometry[];
  bounds: { width: number; height: number };
  topology: GraphTopology;
  layout: WorkflowGraphLayout;
};

function labelForNodeKey(workflow: AuthoredWorkflow | null, key: string): string {
  return workflow?.states.find(stage => stage.stateKey === key)?.displayName
    ?? workflow?.metadata?.gateways?.find(gateway => gateway.key === key)?.displayName
    ?? key;
}

function transitionDescriptor(workflow: AuthoredWorkflow | null, transition: RouteView): string {
  return `${labelForNodeKey(workflow, transition.fromStage)} to ${labelForNodeKey(workflow, transition.toStage)}`;
}

export function buildGraphModel(props: GraphProps): GraphModel {
  const { topology, layout } = computeWorkflowGraphLayout(props.workflow, props.availableQueues);
  const simulationTransitionIndices = new Set(props.simulationPathTransitionIndices);
  const simulationStageKeys = new Set(props.simulationPathStageKeys);

  const nodes: GraphFlowNode[] = topology.nodes.map(topologyNode => {
    const placement = layout.placements.get(topologyNode.id);
    const position = placement ? { x: placement.x, y: placement.y } : { x: 0, y: 0 };
    const rowRank = placement?.rowRank ?? 0;
    const common = {
      id: topologyNode.id,
      position,
      width: topologyNode.width,
      height: topologyNode.height,
      draggable: false,
      selectable: false,
      focusable: false,
      connectable: false,
    } as const;

    if (topologyNode.kind === 'stage') {
      return {
        ...common,
        type: 'stage',
        data: {
          node: topologyNode,
          rowRank,
          selected: props.selectedStageKey === topologyNode.stage.stateKey,
          simulationPath: simulationStageKeys.has(topologyNode.stage.stateKey),
          simulationCurrent: props.simulationCurrentStageKey === topologyNode.stage.stateKey,
          readOnly: props.readOnly,
        },
      } satisfies StageFlowNode;
    }

    const routes = topologyNode.gateway.routes ?? [];
    const pillRoute = topologyNode.pill ? routes[0] : undefined;
    const condition = pillRoute?.condition?.trim();
    return {
      ...common,
      type: 'gateway',
      data: {
        node: topologyNode,
        rowRank,
        selected: props.selectedGatewayKey === topologyNode.gateway.key,
        readOnly: props.readOnly,
        routeCount: routes.length,
        triggerLabel: pillRoute?.trigger ?? '',
        conditionLabel: condition && condition.length > 0 ? condition : null,
      },
    } satisfies GatewayFlowNode;
  });

  const chipsByEdgeKey = new Map<string, TransitionChip[]>();
  topology.transitionBindings.forEach(binding => {
    if (!binding.edgeKey) {
      return;
    }
    const chip: TransitionChip = {
      index: binding.index,
      label: binding.transition.action,
      ariaLabel: `Transition ${binding.transition.action}, ${transitionDescriptor(props.workflow, binding.transition)}`,
      fromKey: parseGraphNodeId(binding.visualFromId).key,
      toKey: parseGraphNodeId(binding.visualToId).key,
      selected: props.selectedTransitionIndex === binding.index,
      simulationPath: simulationTransitionIndices.has(binding.index),
      branch: binding.branch,
      merge: binding.merge,
    };
    chipsByEdgeKey.set(binding.edgeKey, [...(chipsByEdgeKey.get(binding.edgeKey) ?? []), chip]);
  });

  const edges: RouteFlowEdge[] = topology.edges.map(topologyEdge => {
    const simulationPath = topologyEdge.transitionIndices.some(index => simulationTransitionIndices.has(index));
    return {
      id: topologyEdge.key,
      source: topologyEdge.fromId,
      target: topologyEdge.toId,
      sourceHandle: 'out',
      targetHandle: 'in',
      type: 'route',
      focusable: false,
      selectable: false,
      markerEnd: { type: MarkerType.ArrowClosed, color: EDGE_ARROW_COLOR },
      data: {
        edge: topologyEdge,
        fromKey: parseGraphNodeId(topologyEdge.fromId).key,
        toKey: parseGraphNodeId(topologyEdge.toId).key,
        simulationPath,
        chips: chipsByEdgeKey.get(topologyEdge.key) ?? [],
        readOnly: props.readOnly,
      },
    } satisfies RouteFlowEdge;
  });

  return { nodes, edges, lanes: layout.lanes, bounds: layout.bounds, topology, layout };
}
