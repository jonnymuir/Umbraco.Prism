import { LitElement, css, html, nothing, svg } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import type {
  AuthoredGateway,
  AuthoredStage,
  RouteView,
  AuthoredWorkflow,
  EditorStageType,
} from './types.js';
import { editorStageTypeToStageKind } from './types.js';
import {
  applyLaneToStage,
  stageLaneDescription,
  stageLaneKey,
  stageLaneLabel,
  stageSurface,
  type StageSurface,
  type WorkflowQueueDefinition,
  workflowLaneOptions,
} from './workflow-stage-assignment.js';
import { workflowGateways } from './types.js';
import {
  deriveGatewayBindings,
  gatewayLaneKey,
  type GatewayBinding,
} from './workflow-gateway-representation.js';
import { deleteRoute, flattenRoutes } from './workflow-routes.js';

type SelectionKind = 'stage' | 'transition' | 'gateway';

type GraphSelectionDetail = {
  kind: SelectionKind;
  stageKey?: string;
  transitionIndex?: number;
  gatewayKey?: string;
};

type WorkflowUpdatedDetail = {
  workflow: AuthoredWorkflow;
  selection?: GraphSelectionDetail | null;
};

type ContextMenuTarget =
  | { kind: 'canvas' }
  | { kind: 'stage'; stageKey: string }
  | { kind: 'transition'; transitionIndex: number };

type ContextMenuState = ContextMenuTarget & {
  x: number;
  y: number;
};

type VisualNodeKind = 'stage' | 'gateway';

type StageLayout = {
  stage: AuthoredStage;
  stageIndex: number;
  surface: StageSurface;
  laneKey: string;
  laneLabel: string;
  // Row band the stage sits in. Bands flow top-to-bottom; stages live on
  // even-rank bands and gateways on odd-rank bands so the canvas reads as
  // stage → gateway → stage without crossing wires.
  rowRank: number;
  x: number;
  y: number;
  width: number;
  height: number;
};

type GatewayLayout = {
  gateway: AuthoredGateway;
  binding: GatewayBinding;
  surface: StageSurface;
  laneKey: string;
  laneLabel: string;
  rowRank: number;
  x: number;
  y: number;
  width: number;
  height: number;
};

type TransitionLayout = {
  transition: RouteView;
  index: number;
  path: string;
  labelX: number;
  labelY: number;
  visualFromKey: string;
  visualToKey: string;
  branch: boolean;
  merge: boolean;
  showLabel: boolean;
};

/**
 * Visual rail between two graph nodes (stage→gateway, gateway→stage, or a
 * direct stage→stage hop when authors have not yet connected a gateway). Built
 * from the adjacency graph rather than from raw transitions so each pair of
 * nodes contributes exactly one rail even when multiple authored routes go via
 * the same gateway. Provides selectors Slice 7 visual-regression can target:
 *   data-prism-route-path / -from / -to / -simulation-path.
 */
type VisualRouteLayout = {
  key: string;
  path: string;
  fromKey: string;
  toKey: string;
  branch: boolean;
  merge: boolean;
  simulationPath: boolean;
};

type WorkspaceLayout = {
  bounds: { width: number; height: number };
  roleLanes: RoleLane[];
  stageLayouts: StageLayout[];
  gatewayLayouts: GatewayLayout[];
  transitionLayouts: TransitionLayout[];
  visualRouteLayouts: VisualRouteLayout[];
};

type RoleLane = {
  key: string;
  label: string;
  description: string;
  surface: StageSurface;
  columnIndex: number;
  x: number;
  width: number;
  stageCount: number;
};

type CreateStageDialogState = {
  surfaceHint: StageSurface;
  position: 'append' | 'before' | 'after';
  referenceStageKey: string | null;
  title: string;
  stageKey: string;
  laneKey: string;
  stageType: EditorStageType;
  keyTouched: boolean;
  error: string | null;
};

type DeleteStageDialogState = {
  stageKey: string;
  affectedTransitions: RouteView[];
};

type CreateGatewayDialogState = {
  title: string;
  gatewayKey: string;
  kind: 'Split' | 'Join';
  laneKey: string;
  keyTouched: boolean;
  error: string | null;
};

const NODE_WIDTH = 224;
const NODE_HEIGHT = 128;
// Vertical pitch between successive row bands. Each band centres a node
// (stage or gateway) and the pitch must clear NODE_HEIGHT so adjacent rows do
// not collide. Drives the trunk that gateways and routes follow.
const ROW_BAND_PITCH = 152;
const TOP_PADDING = 64;
const SIDE_PADDING = 56;
// Floor lane column width — lanes widen automatically when a row band needs
// more horizontal space for sibling slots.
const LANE_WIDTH = 280;
const LANE_GAP = 36;
// Horizontal padding inside a lane before slot columns start, so cards never
// sit flush against the lane chrome.
const LANE_INSET = 28;
// Horizontal gap between sibling slot columns inside the same lane row band.
// Used when a stage fans out to multiple same-lane gateways or vice versa.
const SLOT_GAP = 56;
const EDGE_LABEL_WIDTH = 132;
const EDGE_LABEL_HEIGHT = 32;
const GATEWAY_SIZE = 132;
const GATEWAY_PILL_HEIGHT = 40;
const GATEWAY_PILL_MIN_WIDTH = 104;
const GATEWAY_PILL_MAX_WIDTH = 208;
// Trunk length below/above a gateway diamond before a rail bends sideways.
// Keeps incoming branch rails terminating at the join's edge instead of
// running through the join body.
const GATEWAY_TRUNK = 36;
const ZOOM_MIN = 0.65;
const ZOOM_MAX = 1.5;
const LANE_HEADER_OFFSET = 80;

/**
 * Workflow graph workspace for stage/transition authoring.
 *
 * Emits:
 *  - stage-selected CustomEvent<{ stageKey: string }>
 *  - transition-selected CustomEvent<{ transitionIndex: number }>
 *  - selection-change CustomEvent<GraphSelectionDetail>
 *  - inspector-requested CustomEvent<GraphSelectionDetail>
 *  - workflow-updated CustomEvent<WorkflowUpdatedDetail>
 */
@customElement('prism-workflow-graph')
export class PrismWorkflowGraphElement extends LitElement {
  @property({ attribute: false })
  workflow: AuthoredWorkflow | null = null;

  @property({ attribute: false })
  availableQueues: WorkflowQueueDefinition[] = [];

  /**
   * Render the graph as a pure viewer — no toolbar create buttons, no creation
   * dialogs, no context menus. Selection and zoom remain available so the viewer
   * is keyboard-navigable. Defaults to false (full authoring surface).
   */
  @property({ type: Boolean, attribute: 'read-only', reflect: true })
  readOnly = false;

  /**
   * Declarative JSON form of {@link workflow}. Lets the element be initialised
   * from HTML/Razor markup without JS wiring — Razor authors can write
   * `<prism-workflow-graph read-only workflow-json='...'>` and skip the prop
   * assignment. When set, this attribute is parsed and assigned to `workflow`.
   */
  @property({ type: String, attribute: 'workflow-json' })
  workflowJson: string | null = null;

  @property({ attribute: false })
  selectedStageKey: string | null = null;

  @property({ attribute: false })
  selectedTransitionIndex: number | null = null;

  @property({ attribute: false })
  selectedGatewayKey: string | null = null;

  @property({ attribute: false })
  simulationCurrentStageKey: string | null = null;

  @property({ attribute: false })
  simulationPathStageKeys: string[] = [];

  @property({ attribute: false })
  simulationPathTransitionIndices: number[] = [];

  @state()
  private _selectedStageKey: string | null = null;

  @state()
  private _selectedTransitionIndex: number | null = null;

  @state()
  private _selectedGatewayKey: string | null = null;

  @state()
  private _focusedIndex = 0;

  @state()
  private _zoom = 1;

  @state()
  private _contextMenu: ContextMenuState | null = null;

  @state()
  private _createStageDialog: CreateStageDialogState | null = null;

  @state()
  private _deleteStageDialog: DeleteStageDialogState | null = null;

  @state()
  private _createGatewayDialog: CreateGatewayDialogState | null = null;

  @query('.graph-canvas')
  private _graphCanvas?: HTMLDivElement;

  private _contextReturnTarget: HTMLElement | null = null;
  private _statusTimer: number | null = null;
  private _dialogReturnTarget: HTMLElement | null = null;

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    if (this._statusTimer !== null) {
      window.clearTimeout(this._statusTimer);
      this._statusTimer = null;
    }
    super.disconnectedCallback();
  }

  protected updated(changed: Map<string, unknown>) {
    if (changed.has('workflowJson') && this.workflowJson) {
      try {
        const parsed = JSON.parse(this.workflowJson) as AuthoredWorkflow;
        this.workflow = parsed;
      } catch (error) {
        console.error('prism-workflow-graph: workflow-json could not be parsed.', error);
      }
    }

    if (changed.has('selectedStageKey')) {
      this._selectedStageKey = this.selectedStageKey ?? null;
    }

    if (changed.has('selectedTransitionIndex')) {
      this._selectedTransitionIndex = this.selectedTransitionIndex ?? null;
    }

    if (changed.has('selectedGatewayKey')) {
      this._selectedGatewayKey = this.selectedGatewayKey ?? null;
    }

    const stages = this.workflow?.states ?? [];
    const transitions = flattenRoutes(this.workflow);
    const gateways = this.workflow?.metadata?.gateways ?? [];
    const focusableStages = stages;

    if (this._selectedStageKey && !stages.some(stage => stage.stateKey === this._selectedStageKey)) {
      this._selectedStageKey = null;
    }

    if (
      this._selectedTransitionIndex !== null
      && (this._selectedTransitionIndex < 0 || this._selectedTransitionIndex >= transitions.length)
    ) {
      this._selectedTransitionIndex = null;
    }

    if (this._selectedGatewayKey && !gateways.some(gateway => gateway.key === this._selectedGatewayKey)) {
      this._selectedGatewayKey = null;
    }

    if (focusableStages.length === 0) {
      this._focusedIndex = 0;
    } else if (this._focusedIndex >= focusableStages.length) {
      this._focusedIndex = focusableStages.length - 1;
    }
  }

  private get _layout(): WorkspaceLayout {
    const stages = this.workflow?.states ?? [];
    const transitions = flattenRoutes(this.workflow);
    const gatewayBindings = this.workflow ? deriveGatewayBindings(this.workflow) : [];

    // 1. Lane entries: keep first-appearance order so the canvas reads left to
    //    right in the order the author introduced lanes.
    const stageEntries = stages.map((stage, stageIndex) => {
      const surface = this._surfaceForStage(stage);
      const laneKey = this._roleKeyForStage(stage, surface);
      return {
        id: `stage:${stage.stateKey}`,
        stage,
        stageIndex,
        surface,
        laneKey,
        laneLabel: this._roleLabelForLane(laneKey),
      };
    });
    const gatewayEntries = gatewayBindings.map(binding => {
      const surface = this._surfaceForGateway(binding.gateway);
      return {
        id: `gateway:${binding.gateway.key}`,
        gateway: binding.gateway,
        binding,
        surface,
        laneKey: binding.laneKey || this._laneKeyForGateway(binding.gateway),
        laneLabel: this._roleLabelForLane(binding.laneKey || this._laneKeyForGateway(binding.gateway)),
      };
    });

    const laneStateByKey = new Map<string, { surface: StageSurface; stageCount: number }>();
    const laneOrder: string[] = [];
    const ensureLane = (laneKey: string, surface: StageSurface, isStage: boolean) => {
      const existing = laneStateByKey.get(laneKey);
      if (existing) {
        if (isStage) {
          existing.stageCount += 1;
        }
        return;
      }
      laneStateByKey.set(laneKey, { surface, stageCount: isStage ? 1 : 0 });
      laneOrder.push(laneKey);
    };
    stageEntries.forEach(entry => ensureLane(entry.laneKey, entry.surface, true));
    gatewayEntries.forEach(entry => ensureLane(entry.laneKey, entry.surface, false));

    // 2. Adjacency graph spanning stages and gateways. Each gateway is wired
    //    to its anchor stage (split: stage→gateway, join: gateway→stage) so
    //    the topological sort produces a stage → gateway → stage reading.
    const nodeIds = [...stageEntries.map(entry => entry.id), ...gatewayEntries.map(entry => entry.id)];
    const nodeKind = new Map<string, VisualNodeKind>();
    const nodeOrder = new Map<string, number>();
    const adjacency = new Map<string, Set<string>>();
    const inDegree = new Map<string, number>();
    const edgeTransitionIndices = new Map<string, Set<number>>();

    nodeIds.forEach((id, index) => {
      nodeKind.set(id, id.startsWith('gateway:') ? 'gateway' : 'stage');
      nodeOrder.set(id, index);
      inDegree.set(id, 0);
    });

    const addEdge = (fromId: string, toId: string, transitionIndex?: number | number[]) => {
      if (fromId === toId || !nodeKind.has(fromId) || !nodeKind.has(toId)) {
        return;
      }
      let outgoing = adjacency.get(fromId);
      if (!outgoing) {
        outgoing = new Set<string>();
        adjacency.set(fromId, outgoing);
      }
      if (!outgoing.has(toId)) {
        outgoing.add(toId);
        inDegree.set(toId, (inDegree.get(toId) ?? 0) + 1);
      }
      const indices = transitionIndex === undefined
        ? []
        : Array.isArray(transitionIndex)
          ? transitionIndex
          : [transitionIndex];
      if (indices.length > 0) {
        const key = `${fromId}->${toId}`;
        const existing = edgeTransitionIndices.get(key) ?? new Set<number>();
        indices.forEach(index => existing.add(index));
        edgeTransitionIndices.set(key, existing);
      }
    };

    const splitGatewayKeyByAnchorStage = new Map<string, string>();
    const joinGatewayKeyByAnchorStage = new Map<string, string>();

    gatewayEntries.forEach(entry => {
      const anchorStageKey = entry.binding.anchorStageKey;
      if (!anchorStageKey) {
        return;
      }
      const anchorStageId = `stage:${anchorStageKey}`;
      if (entry.gateway.gatewayType === 'Split') {
        if (!splitGatewayKeyByAnchorStage.has(anchorStageKey)) {
          splitGatewayKeyByAnchorStage.set(anchorStageKey, entry.gateway.key);
        }
        addEdge(anchorStageId, entry.id, entry.binding.relatedTransitionIndices);
      } else {
        // Join gateways: record the mapping for reference but do NOT add an edge
        // from the join back to its anchor. In the new routes model the anchor is
        // an upstream stage, not the downstream merge target, so adding that edge
        // would create a cycle and leave all downstream nodes at rank 0.
        // The correct downstream edge (join → next stage) is built in the
        // transitions loop from the gateway's own routes.
        if (!joinGatewayKeyByAnchorStage.has(anchorStageKey)) {
          joinGatewayKeyByAnchorStage.set(anchorStageKey, entry.gateway.key);
        }
      }
    });

    transitions.forEach((transition, index) => {
      const sourceStageId = `stage:${transition.fromStage}`;
      const targetStageId = `stage:${transition.toStage}`;
      const sourceGatewayKey = transition.fromGateway ?? splitGatewayKeyByAnchorStage.get(transition.fromStage) ?? null;
      // Do NOT fall back to joinGatewayKeyByAnchorStage here: in the new routes
      // model the anchor is an upstream stage, so the lookup would incorrectly
      // intercept direct routes to regular stages and add backward edges. All
      // routes that genuinely target a join gateway already carry an explicit
      // toGateway value set by flattenRoutes.
      const targetGatewayKey = transition.toGateway ?? null;
      const sourceGatewayId = sourceGatewayKey ? `gateway:${sourceGatewayKey}` : null;
      const targetGatewayId = targetGatewayKey ? `gateway:${targetGatewayKey}` : null;

      if (sourceGatewayId) {
        addEdge(sourceStageId, sourceGatewayId, index);
      }
      const routedSourceId = sourceGatewayId ?? sourceStageId;
      if (targetGatewayId) {
        addEdge(routedSourceId, targetGatewayId, index);
        addEdge(targetGatewayId, targetStageId, index);
        return;
      }
      addEdge(routedSourceId, targetStageId, index);
    });

    // 2b. Remove backward edges from Join gateways.
    //     A Join gateway that routes back to an earlier (upstream) stage creates a
    //     cycle: Kahn's algorithm cannot rank any node in the cycle, so everything
    //     collapses to rank 0 and the canvas sprawls horizontally.
    //     We detect these by BFS: for each outgoing edge of a Join gateway,
    //     check whether the target stage can reach the gateway itself through the
    //     rest of the graph. If it can, the edge is backward — remove it from the
    //     adjacency map and decrement inDegree so the target remains a DAG root.
    //     The edge is still present in the transitions list and rendered as an
    //     upward-curving rail in the canvas.
    const joinGatewayIdSet = new Set(
      gatewayEntries
        .filter(entry => entry.gateway.gatewayType === 'Join')
        .map(entry => entry.id)
    );
    for (const fromId of joinGatewayIdSet) {
      const neighbors = adjacency.get(fromId);
      if (!neighbors) {
        continue;
      }
      for (const toId of [...neighbors]) {
        // BFS from toId: if we can reach fromId, this edge closes a backward cycle.
        const visited = new Set<string>();
        const searchQueue = [toId];
        let createsCycle = false;
        while (searchQueue.length > 0 && !createsCycle) {
          const current = searchQueue.shift()!;
          if (current === fromId) {
            createsCycle = true;
            break;
          }
          if (visited.has(current)) {
            continue;
          }
          visited.add(current);
          adjacency.get(current)?.forEach(next => {
            if (!visited.has(next)) {
              searchQueue.push(next);
            }
          });
        }
        if (createsCycle) {
          neighbors.delete(toId);
          inDegree.set(toId, (inDegree.get(toId) ?? 1) - 1);
        }
      }
    }

    // 3. Row-rank via longest-path (Kahn's algorithm). Each node's rank is the
    //    length of the longest path from any root to that node, guaranteeing
    //    that if there is an edge A→B then rank(B) > rank(A) regardless of lane.
    const ranks = new Map<string, number>();
    nodeIds.forEach(id => ranks.set(id, 0));

    // Work on a mutable copy of inDegree so adjacency walk is non-destructive.
    const inDegreeCopy = new Map(inDegree);

    const queue = nodeIds
      .filter(id => (inDegreeCopy.get(id) ?? 0) === 0)
      .sort((left, right) => (nodeOrder.get(left) ?? 0) - (nodeOrder.get(right) ?? 0));

    while (queue.length > 0) {
      const currentId = queue.shift()!;
      const currentRank = ranks.get(currentId) ?? 0;
      const neighbours = adjacency.get(currentId);
      if (!neighbours) {
        continue;
      }
      [...neighbours]
        .sort((left, right) => (nodeOrder.get(left) ?? 0) - (nodeOrder.get(right) ?? 0))
        .forEach(nextId => {
          ranks.set(nextId, Math.max(ranks.get(nextId) ?? 0, currentRank + 1));

          const nextInDegree = (inDegreeCopy.get(nextId) ?? 0) - 1;
          inDegreeCopy.set(nextId, nextInDegree);
          if (nextInDegree === 0) {
            queue.push(nextId);
            queue.sort((left, right) => (nodeOrder.get(left) ?? 0) - (nodeOrder.get(right) ?? 0));
          }
        });
    }

    // 4. Bucket nodes by (lane, rowRank) so each band can size and centre
    //    its slot columns. Same-lane fan-out widens the lane horizontally;
    //    cross-lane fan-out keeps a single readable branch row.
    type LaneRowItem =
      | { id: string; kind: 'stage'; stageEntry: typeof stageEntries[number] }
      | { id: string; kind: 'gateway'; gatewayEntry: typeof gatewayEntries[number] };

    const nodesByLaneRow = new Map<string, Map<number, LaneRowItem[]>>();
    const pushLaneRowItem = (laneKey: string, rowRank: number, item: LaneRowItem) => {
      let rows = nodesByLaneRow.get(laneKey);
      if (!rows) {
        rows = new Map<number, LaneRowItem[]>();
        nodesByLaneRow.set(laneKey, rows);
      }
      const rowItems = rows.get(rowRank) ?? [];
      rowItems.push(item);
      rows.set(rowRank, rowItems);
    };
    stageEntries.forEach(entry => {
      pushLaneRowItem(entry.laneKey, ranks.get(entry.id) ?? 0, { id: entry.id, kind: 'stage', stageEntry: entry });
    });
    gatewayEntries.forEach(entry => {
      pushLaneRowItem(entry.laneKey, ranks.get(entry.id) ?? 1, { id: entry.id, kind: 'gateway', gatewayEntry: entry });
    });

    // 5. Lane width = widest row band in that lane (LANE_INSET on either side
    //    + slot content + SLOT_GAP between siblings). Lanes with only one slot
    //    keep the floor width.
    const laneWidthByKey = new Map<string, number>();
    laneOrder.forEach(laneKey => {
      const rows = nodesByLaneRow.get(laneKey);
      let widestRow = LANE_WIDTH;
      rows?.forEach(items => {
        const contentWidth = items.reduce((sum, item) => {
          if (item.kind === 'stage') {
            return sum + NODE_WIDTH;
          }
          return sum + this._gatewaySize(item.gatewayEntry.gateway).width;
        }, 0);
        widestRow = Math.max(
          widestRow,
          LANE_INSET * 2 + contentWidth + Math.max(items.length - 1, 0) * SLOT_GAP
        );
      });
      laneWidthByKey.set(laneKey, widestRow);
    });

    const roleLanes: RoleLane[] = [];
    const laneByKey = new Map<string, RoleLane>();
    let currentLaneX = SIDE_PADDING;
    laneOrder.forEach((laneKey, columnIndex) => {
      const laneState = laneStateByKey.get(laneKey)!;
      const lane: RoleLane = {
        key: laneKey,
        label: this._roleLabelForLane(laneKey),
        description: this._roleDescriptionForLane(laneKey),
        surface: laneState.surface,
        columnIndex,
        x: currentLaneX,
        width: laneWidthByKey.get(laneKey) ?? LANE_WIDTH,
        stageCount: laneState.stageCount,
      };
      laneByKey.set(laneKey, lane);
      roleLanes.push(lane);
      currentLaneX += lane.width + LANE_GAP;
    });

    // 6. Place nodes inside their lane × row band. Slots within a band are
    //    centred and laid left-to-right by node introduction order.
    const stageLayouts: StageLayout[] = [];
    const gatewayLayouts: GatewayLayout[] = [];

    laneOrder.forEach(laneKey => {
      const lane = laneByKey.get(laneKey);
      const rows = nodesByLaneRow.get(laneKey);
      if (!lane || !rows) {
        return;
      }
      [...rows.entries()]
        .sort((left, right) => left[0] - right[0])
        .forEach(([rowRank, items]) => {
          const orderedItems = [...items].sort(
            (left, right) => (nodeOrder.get(left.id) ?? 0) - (nodeOrder.get(right.id) ?? 0)
          );
          const contentWidth = orderedItems.reduce((sum, item) => {
            if (item.kind === 'stage') {
              return sum + NODE_WIDTH;
            }
            return sum + this._gatewaySize(item.gatewayEntry.gateway).width;
          }, 0);
          const totalWidth = contentWidth + Math.max(orderedItems.length - 1, 0) * SLOT_GAP;
          let cursorX = lane.x + (lane.width - totalWidth) / 2;
          const bandCenter = this._rowBandCenter(rowRank);

          orderedItems.forEach(item => {
            const gatewaySize = item.kind === 'gateway' ? this._gatewaySize(item.gatewayEntry.gateway) : null;
            const width = item.kind === 'stage' ? NODE_WIDTH : gatewaySize!.width;
            const height = item.kind === 'stage' ? NODE_HEIGHT : gatewaySize!.height;
            const y = bandCenter - height / 2;
            if (item.kind === 'stage') {
              stageLayouts.push({
                stage: item.stageEntry.stage,
                stageIndex: item.stageEntry.stageIndex,
                surface: item.stageEntry.surface,
                laneKey,
                laneLabel: item.stageEntry.laneLabel,
                rowRank,
                x: cursorX,
                y,
                width,
                height,
              });
            } else {
              gatewayLayouts.push({
                gateway: item.gatewayEntry.gateway,
                binding: item.gatewayEntry.binding,
                surface: item.gatewayEntry.surface,
                laneKey,
                laneLabel: item.gatewayEntry.laneLabel,
                rowRank,
                x: cursorX,
                y,
                width,
                height,
              });
            }
            cursorX += width + SLOT_GAP;
          });
        });
    });

    const stageMap = new Map(stageLayouts.map(layout => [layout.stage.stateKey, layout]));
    const gatewayLayoutByKey = new Map(gatewayLayouts.map(layout => [layout.gateway.key, layout]));
    const layoutByNodeId = new Map<string, StageLayout | GatewayLayout>([
      ...stageLayouts.map(layout => [`stage:${layout.stage.stateKey}`, layout] as const),
      ...gatewayLayouts.map(layout => [`gateway:${layout.gateway.key}`, layout] as const),
    ]);
    const splitLayoutByAnchorStage = new Map<string, GatewayLayout>();
    const joinLayoutByAnchorStage = new Map<string, GatewayLayout>();
    gatewayLayouts.forEach(layout => {
      const anchorStageKey = layout.binding.anchorStageKey;
      if (!anchorStageKey) {
        return;
      }
      if (layout.gateway.gatewayType === 'Split') {
        if (!splitLayoutByAnchorStage.has(anchorStageKey)) {
          splitLayoutByAnchorStage.set(anchorStageKey, layout);
        }
      } else if (!joinLayoutByAnchorStage.has(anchorStageKey)) {
        joinLayoutByAnchorStage.set(anchorStageKey, layout);
      }
    });

    // 7. Visual rails: one path per adjacency edge so each pair of nodes
    //    contributes a single rail even when many transitions go via the same
    //    gateway. Sibling outgoing/incoming edges use slot offsets so rails
    //    leave/arrive on distinct corridors instead of stacking on one stem.
    const outgoingByNode = new Map<string, string[]>();
    const incomingByNode = new Map<string, string[]>();
    const orderByLayout = (left: string, right: string) => {
      const leftLayout = layoutByNodeId.get(left);
      const rightLayout = layoutByNodeId.get(right);
      if (!leftLayout || !rightLayout) {
        return (nodeOrder.get(left) ?? 0) - (nodeOrder.get(right) ?? 0);
      }
      if (leftLayout.rowRank !== rightLayout.rowRank) {
        return leftLayout.rowRank - rightLayout.rowRank;
      }
      return this._layoutCenter(leftLayout).x - this._layoutCenter(rightLayout).x;
    };
    adjacency.forEach((targets, fromId) => {
      const ordered = [...targets].sort(orderByLayout);
      outgoingByNode.set(fromId, ordered);
      ordered.forEach(targetId => {
        incomingByNode.set(targetId, [...(incomingByNode.get(targetId) ?? []), fromId]);
      });
    });
    incomingByNode.forEach((sources, targetId) => {
      incomingByNode.set(targetId, [...sources].sort(orderByLayout));
    });

    const visualRouteLayouts: VisualRouteLayout[] = [];
    outgoingByNode.forEach((targets, fromId) => {
      const fromLayout = layoutByNodeId.get(fromId);
      if (!fromLayout) {
        return;
      }
      targets.forEach((toId, outgoingIndex) => {
        const toLayout = layoutByNodeId.get(toId);
        if (!toLayout) {
          return;
        }
        const incomingSources = incomingByNode.get(toId) ?? [];
        const incomingIndex = Math.max(0, incomingSources.indexOf(fromId));
        const indices = edgeTransitionIndices.get(`${fromId}->${toId}`) ?? new Set<number>();
        const path = this._buildVisualRoutePath(fromLayout, toLayout, {
          outgoingIndex,
          outgoingCount: targets.length,
          incomingIndex,
          incomingCount: incomingSources.length,
        });
        visualRouteLayouts.push({
          key: `${fromId}->${toId}`,
          path,
          fromKey: fromId.replace(/^(stage|gateway):/, ''),
          toKey: toId.replace(/^(stage|gateway):/, ''),
          branch: 'gateway' in fromLayout && fromLayout.gateway.gatewayType === 'Split',
          merge: 'gateway' in toLayout && toLayout.gateway.gatewayType === 'Join',
          simulationPath: [...indices].some(index => this._transitionIsInSimulationPath(index)),
        });
      });
    });

    // 8. Transition chips: keep one path per authored transition so the chip
    //    label hovers along its route. Geometry follows the same slot rails.
    const transitionLayouts: TransitionLayout[] = transitions.map((transition, index) => {
      const sourceStage = stageMap.get(transition.fromStage);
      const targetStage = stageMap.get(transition.toStage);
      const sourceGateway = transition.fromGateway
        ? gatewayLayoutByKey.get(transition.fromGateway) ?? null
        : splitLayoutByAnchorStage.get(transition.fromStage) ?? null;
      // Do NOT fall back to joinLayoutByAnchorStage: the anchor is an upstream
      // stage, so the lookup would draw chip paths through the wrong gateway for
      // routes that target ordinary stages upstream of a join.
      const targetGateway = transition.toGateway
        ? gatewayLayoutByKey.get(transition.toGateway) ?? null
        : null;

      // Slice C: a route may target a gateway directly (e.g. a feeder split
      // pointing into a Join). When the toStage is itself a gateway key, the
      // terminal node of the path is the gateway, not a stage.
      const effectiveSource = sourceStage ?? sourceGateway ?? null;
      const effectiveTarget = targetStage ?? targetGateway ?? null;
      if (!effectiveSource || !effectiveTarget) {
        return {
          transition,
          index,
          path: '',
          labelX: 0,
          labelY: 0,
          visualFromKey: transition.fromGateway ?? transition.fromStage,
          visualToKey: transition.toGateway ?? transition.toStage,
          branch: false,
          merge: false,
          showLabel: false,
        };
      }

      const routedNodes: Array<StageLayout | GatewayLayout> = [effectiveSource];
      if (sourceGateway && routedNodes[routedNodes.length - 1] !== sourceGateway) {
        routedNodes.push(sourceGateway);
      }
      if (targetGateway && routedNodes[routedNodes.length - 1] !== targetGateway) {
        routedNodes.push(targetGateway);
      }
      if (routedNodes[routedNodes.length - 1] !== effectiveTarget) {
        routedNodes.push(effectiveTarget);
      }

      const { path, labelX, labelY } = this._buildTransitionPath(routedNodes);
      return {
        transition,
        index,
        path,
        labelX,
        labelY,
        visualFromKey: sourceGateway?.gateway.key ?? transition.fromStage,
        visualToKey: targetGateway?.gateway.key ?? transition.toStage,
        branch: Boolean(sourceGateway?.gateway.gatewayType === 'Split'),
        merge: Boolean(targetGateway?.gateway.gatewayType === 'Join'),
        showLabel: true,
      };
    });

    const occupiedRects = [
      ...stageLayouts.map(layout => ({
        left: layout.x - 8,
        right: layout.x + layout.width + 8,
        top: layout.y - 8,
        bottom: layout.y + layout.height + 8,
      })),
      ...gatewayLayouts.map(layout => ({
        left: layout.x - 8,
        right: layout.x + layout.width + 8,
        top: layout.y - 8,
        bottom: layout.y + layout.height + 8,
      })),
    ];
    const placedLabelRects: Array<{ left: number; right: number; top: number; bottom: number }> = [];
    const resolvedTransitionLayouts = transitionLayouts.map((layout, index) => {
      if (!layout.showLabel || !layout.path) {
        return layout;
      }

      const offsets = [0, -44, 44, -88, 88, -132, 132];
      for (const offset of offsets) {
        const candidate = {
          left: layout.labelX - EDGE_LABEL_WIDTH / 2,
          right: layout.labelX + EDGE_LABEL_WIDTH / 2,
          top: layout.labelY - EDGE_LABEL_HEIGHT / 2 + offset,
          bottom: layout.labelY + EDGE_LABEL_HEIGHT / 2 + offset,
        };
        const overlapsNode = occupiedRects.some(rect =>
          rect.left < candidate.right
          && candidate.left < rect.right
          && rect.top < candidate.bottom
          && candidate.top < rect.bottom
        );
        const overlapsLabel = placedLabelRects.some(rect =>
          rect.left < candidate.right
          && candidate.left < rect.right
          && rect.top < candidate.bottom
          && candidate.top < rect.bottom
        );
        if (overlapsNode || overlapsLabel) {
          continue;
        }

        placedLabelRects.push(candidate);
        return {
          ...layout,
          labelY: layout.labelY + offset,
        };
      }

      const fallbackOffset = index % 2 === 0 ? -44 : 44;
      placedLabelRects.push({
        left: layout.labelX - EDGE_LABEL_WIDTH / 2,
        right: layout.labelX + EDGE_LABEL_WIDTH / 2,
        top: layout.labelY - EDGE_LABEL_HEIGHT / 2 + fallbackOffset,
        bottom: layout.labelY + EDGE_LABEL_HEIGHT / 2 + fallbackOffset,
      });
      return {
        ...layout,
        labelY: layout.labelY + fallbackOffset,
      };
    });

    const width = roleLanes.length === 0
      ? SIDE_PADDING * 2 + LANE_WIDTH
      : currentLaneX - LANE_GAP + SIDE_PADDING;
    const contentBottom = Math.max(
      TOP_PADDING + LANE_HEADER_OFFSET + NODE_HEIGHT,
      ...stageLayouts.map(layout => layout.y + layout.height),
      ...gatewayLayouts.map(layout => layout.y + layout.height)
    );
    const height = contentBottom + TOP_PADDING;

    return {
      bounds: { width, height },
      roleLanes,
      stageLayouts,
      gatewayLayouts,
      transitionLayouts: resolvedTransitionLayouts,
      visualRouteLayouts,
    };
  }

  private _surfaceForStage(stage: AuthoredStage): StageSurface {
    return stageSurface(stage);
  }

  private _surfaceForGateway(gateway: AuthoredGateway): StageSurface {
    return stageSurface(gateway);
  }

  private _roleKeyForStage(stage: AuthoredStage, surface = this._surfaceForStage(stage)) {
    return stageLaneKey(stage) || (surface === 'back-stage' ? 'reviewer' : 'public');
  }

  private _laneKeyForGateway(gateway: AuthoredGateway) {
    return gatewayLaneKey(gateway) || 'public';
  }

  private _roleLabelForLane(laneKey: string) {
    return stageLaneLabel(this.workflow, laneKey, this.availableQueues);
  }

  private _roleDescriptionForLane(laneKey: string) {
    return stageLaneDescription(this.workflow, laneKey, this.availableQueues);
  }

  private _availableLaneKeys() {
    return workflowLaneOptions(this.workflow, this.availableQueues);
  }

  private _layoutCenter(layout: StageLayout | GatewayLayout) {
    return { x: layout.x + layout.width / 2, y: layout.y + layout.height / 2 };
  }

  private _rowBandCenter(rowRank: number) {
    return TOP_PADDING + LANE_HEADER_OFFSET + NODE_HEIGHT / 2 + rowRank * ROW_BAND_PITCH;
  }

  private _isPillGateway(gateway: AuthoredGateway) {
    return gateway.gatewayType === 'Split' && (gateway.routes ?? []).length === 1;
  }

  private _gatewaySize(gateway: AuthoredGateway) {
    if (!this._isPillGateway(gateway)) {
      return { width: GATEWAY_SIZE, height: GATEWAY_SIZE };
    }

    const pillLabel = (gateway.routes ?? [])[0]?.trigger?.trim() || gateway.displayName;
    const estimatedWidth = 44 + pillLabel.length * 8;
    return {
      width: Math.max(GATEWAY_PILL_MIN_WIDTH, Math.min(GATEWAY_PILL_MAX_WIDTH, estimatedWidth)),
      height: GATEWAY_PILL_HEIGHT,
    };
  }

  /**
   * Distribute sibling rails across a node's face so multiple choices leave
   * (or arrive) on distinct corridors. `maxSpread` keeps offsets within the
   * node's painted width.
   */
  private _slotOffset(slotIndex: number, slotCount: number, maxSpread: number) {
    if (slotCount <= 1) {
      return 0;
    }
    const spread = Math.max(0, Math.min(maxSpread, (slotCount - 1) * 40));
    if (spread === 0) {
      return 0;
    }
    const start = -spread / 2;
    const step = spread / (slotCount - 1);
    return start + slotIndex * step;
  }

  // Gateway attachments hug the diamond's vertical tips so the rail can bend
  // inside GATEWAY_TRUNK rather than crossing the diamond outline.
  private _gatewayAttachmentInset(layout: GatewayLayout) {
    return Math.max(14, layout.height * 0.18);
  }

  private _routeEntryPoint(layout: StageLayout | GatewayLayout, slotIndex: number, slotCount: number) {
    if ('stage' in layout) {
      const centre = this._layoutCenter(layout);
      return {
        x: centre.x + this._slotOffset(slotIndex, slotCount, Math.max(0, layout.width - 72)),
        y: layout.y,
      };
    }
    const centre = this._layoutCenter(layout);
    return {
      x: centre.x + this._slotOffset(slotIndex, slotCount, layout.width * 0.34),
      y: layout.y + this._gatewayAttachmentInset(layout),
    };
  }

  private _routeExitPoint(layout: StageLayout | GatewayLayout, slotIndex: number, slotCount: number) {
    if ('stage' in layout) {
      const centre = this._layoutCenter(layout);
      return {
        x: centre.x + this._slotOffset(slotIndex, slotCount, Math.max(0, layout.width - 72)),
        y: layout.y + layout.height,
      };
    }
    const centre = this._layoutCenter(layout);
    return {
      x: centre.x + this._slotOffset(slotIndex, slotCount, layout.width * 0.34),
      y: layout.y + layout.height - this._gatewayAttachmentInset(layout),
    };
  }

  private _railY(start: { x: number; y: number }, end: { x: number; y: number }) {
    if (Math.abs(start.x - end.x) < 4) {
      return start.y + (end.y - start.y) / 2;
    }
    const verticalGap = Math.max(24, end.y - start.y);
    return start.y + Math.min(72, Math.max(GATEWAY_TRUNK, verticalGap * 0.32));
  }

  private _pushRoutePoint(points: Array<{ x: number; y: number }>, point: { x: number; y: number }) {
    const previous = points[points.length - 1];
    if (previous && Math.abs(previous.x - point.x) < 0.5 && Math.abs(previous.y - point.y) < 0.5) {
      return;
    }
    points.push(point);
  }

  private _normaliseRoutePoints(points: Array<{ x: number; y: number }>) {
    const deduped = points.filter((point, index) => {
      if (index === 0) {
        return true;
      }
      const previous = points[index - 1];
      return Math.abs(previous.x - point.x) >= 0.5 || Math.abs(previous.y - point.y) >= 0.5;
    });
    return deduped.filter((point, index, list) => {
      if (index === 0 || index === list.length - 1) {
        return true;
      }
      const previous = list[index - 1];
      const next = list[index + 1];
      const collinearX = Math.abs(previous.x - point.x) < 0.5 && Math.abs(point.x - next.x) < 0.5;
      const collinearY = Math.abs(previous.y - point.y) < 0.5 && Math.abs(point.y - next.y) < 0.5;
      return !(collinearX || collinearY);
    });
  }

  private _pathFromPoints(points: Array<{ x: number; y: number }>) {
    if (points.length === 0) {
      return '';
    }
    return `M ${points[0].x} ${points[0].y}${points
      .slice(1)
      .map(point => ` L ${point.x} ${point.y}`)
      .join('')}`;
  }

  private _labelPositionFromRoute(points: Array<{ x: number; y: number }>) {
    if (points.length === 0) {
      return { x: 0, y: 0 };
    }
    let bestHorizontal = { length: 0, x: points[0].x, y: points[0].y };
    let bestAny = { length: 0, x: points[0].x, y: points[0].y };
    for (let index = 1; index < points.length; index += 1) {
      const previous = points[index - 1];
      const current = points[index];
      const deltaX = Math.abs(current.x - previous.x);
      const deltaY = Math.abs(current.y - previous.y);
      const length = deltaX + deltaY;
      const midpoint = {
        x: previous.x + (current.x - previous.x) / 2,
        y: previous.y + (current.y - previous.y) / 2,
      };

      if (length >= bestAny.length) {
        bestAny = { length, ...midpoint };
      }

      if (deltaY < 0.5 && deltaX >= EDGE_LABEL_WIDTH + 16 && length >= bestHorizontal.length) {
        bestHorizontal = { length, ...midpoint };
      }
    }
    const best = bestHorizontal.length > 0 ? bestHorizontal : bestAny;
    return { x: best.x, y: best.y };
  }

  /**
   * Build a stage→gateway or gateway→stage rail using orthogonal segments and
   * slot offsets so siblings leave the source on distinct corridors. The rail
   * always exits the source vertically, takes one horizontal jog, and arrives
   * vertically into the target — the lane-spine reading the slot-matrix design
   * relies on.
   */
  private _buildVisualRoutePath(
    from: StageLayout | GatewayLayout,
    to: StageLayout | GatewayLayout,
    options: {
      outgoingIndex: number;
      outgoingCount: number;
      incomingIndex: number;
      incomingCount: number;
    }
  ): string {
    const start = this._routeExitPoint(from, options.outgoingIndex, options.outgoingCount);
    const end = this._routeEntryPoint(to, options.incomingIndex, options.incomingCount);
    const points: Array<{ x: number; y: number }> = [start];
    const railY = this._railY(start, end);
    this._pushRoutePoint(points, { x: start.x, y: railY });
    this._pushRoutePoint(points, { x: end.x, y: railY });
    this._pushRoutePoint(points, end);
    return this._pathFromPoints(this._normaliseRoutePoints(points));
  }

  /**
   * Build the transition-chip path through the routed nodes. Each segment uses
   * the same orthogonal rail so the chip label can hover over a long, mostly-
   * horizontal segment and never sit on top of a stage card.
   */
  private _buildTransitionPath(routeNodes: Array<StageLayout | GatewayLayout>) {
    if (routeNodes.length < 2) {
      return { path: '', labelX: 0, labelY: 0 };
    }
    const points: Array<{ x: number; y: number }> = [this._routeExitPoint(routeNodes[0], 0, 1)];
    for (let index = 1; index < routeNodes.length; index += 1) {
      const from = routeNodes[index - 1];
      const to = routeNodes[index];
      const currentPoint = points[points.length - 1] ?? this._routeExitPoint(from, 0, 1);
      const targetPoint = this._routeEntryPoint(to, 0, 1);
      const railY = this._railY(currentPoint, targetPoint);
      this._pushRoutePoint(points, { x: currentPoint.x, y: railY });
      this._pushRoutePoint(points, { x: targetPoint.x, y: railY });
      this._pushRoutePoint(points, targetPoint);
      // After arriving, queue the exit point of the just-visited node so the
      // next segment leaves the node's bottom rather than its entry.
      if (index < routeNodes.length - 1) {
        this._pushRoutePoint(points, this._routeExitPoint(to, 0, 1));
      }
    }
    const normalisedPoints = this._normaliseRoutePoints(points);
    const label = this._labelPositionFromRoute(normalisedPoints);
    return {
      path: this._pathFromPoints(normalisedPoints),
      labelX: label.x,
      labelY: label.y,
    };
  }

  private _announce(message: string) {
    const announcer = this.shadowRoot?.getElementById('graph-announcer');
    if (!announcer) {
      return;
    }

    announcer.textContent = '';
    requestAnimationFrame(() => {
      announcer.textContent = message;
    });
  }

  private _selectStage(stageKey: string, options?: { openInspector?: boolean; focusIndex?: number }) {
    this._selectedStageKey = stageKey;
    this._selectedTransitionIndex = null;
    this._selectedGatewayKey = null;

    if (typeof options?.focusIndex === 'number') {
      this._focusedIndex = options.focusIndex;
    }

    this.dispatchEvent(
      new CustomEvent<{ stageKey: string }>('stage-selected', {
        detail: { stageKey },
        bubbles: true,
        composed: true,
      })
    );
    this._emitSelectionChange({ kind: 'stage', stageKey });
    this._announce(`Stage “${this._labelForStage(stageKey)}” selected.`);

    if (options?.openInspector) {
      this._requestInspector({ kind: 'stage', stageKey });
    }
  }

  private _selectGateway(gatewayKey: string, options?: { openInspector?: boolean }) {
    const gateway = this.workflow?.metadata?.gateways?.find(candidate => candidate.key === gatewayKey);
    if (!gateway) {
      return;
    }

    this._selectedGatewayKey = gatewayKey;
    this._selectedStageKey = null;
    this._selectedTransitionIndex = null;

    this.dispatchEvent(
      new CustomEvent<{ gatewayKey: string }>('gateway-selected', {
        detail: { gatewayKey },
        bubbles: true,
        composed: true,
      })
    );
    this._emitSelectionChange({ kind: 'gateway', gatewayKey });
    this._announce(`Gateway “${gateway.displayName}” selected. ${gateway.gatewayType} gateway in the ${this._roleLabelForLane(this._laneKeyForGateway(gateway))} queue.`);

    if (options?.openInspector) {
      this._requestInspector({ kind: 'gateway', gatewayKey });
    }
  }

  private _selectTransition(index: number, options?: { openInspector?: boolean }) {
    const transition = (flattenRoutes(this.workflow))[index];
    if (!transition) {
      return;
    }

    this._selectedTransitionIndex = index;
    this._selectedStageKey = null;
    this._selectedGatewayKey = null;

    this.dispatchEvent(
      new CustomEvent<{ transitionIndex: number }>('transition-selected', {
        detail: { transitionIndex: index },
        bubbles: true,
        composed: true,
      })
    );
    this._emitSelectionChange({ kind: 'transition', transitionIndex: index });
    this._announce(
      `Transition “${transition.action}” selected, from ${this._labelForStage(transition.fromStage)} to ${this._labelForStage(transition.toStage)}.`
    );

    if (options?.openInspector) {
      this._requestInspector({ kind: 'transition', transitionIndex: index });
    }
  }

  private _emitSelectionChange(detail: GraphSelectionDetail) {
    this.dispatchEvent(
      new CustomEvent<GraphSelectionDetail>('selection-change', {
        detail,
        bubbles: true,
        composed: true,
      })
    );
  }

  private _requestInspector(detail: GraphSelectionDetail) {
    this.dispatchEvent(
      new CustomEvent<GraphSelectionDetail>('inspector-requested', {
        detail,
        bubbles: true,
        composed: true,
      })
    );
  }

  private _emitWorkflowUpdated(workflow: AuthoredWorkflow, selection?: GraphSelectionDetail | null) {
    this.workflow = workflow;
    this.dispatchEvent(
      new CustomEvent<WorkflowUpdatedDetail>('workflow-updated', {
        detail: { workflow, selection },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _labelForStage(stageKey: string): string {
    return this.workflow?.states.find(stage => stage.stateKey === stageKey)?.displayName
      ?? this.workflow?.metadata?.gateways?.find(gateway => gateway.key === stageKey)?.displayName
      ?? stageKey;
  }

  private _transitionDescriptor(transition: RouteView) {
    return `${this._labelForStage(transition.fromStage)} to ${this._labelForStage(transition.toStage)}`;
  }

  private _stageIsInSimulationPath(stageKey: string) {
    return this.simulationPathStageKeys.includes(stageKey);
  }

  private _transitionIsInSimulationPath(transitionIndex: number) {
    return this.simulationPathTransitionIndices.includes(transitionIndex);
  }

  private _zoomBy(delta: number) {
    this._zoom = Number(Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, this._zoom + delta)).toFixed(2));
    this._announce(`Zoom ${Math.round(this._zoom * 100)} percent.`);
  }

  private _fitToScreen() {
    const canvas = this._graphCanvas;
    if (!canvas) {
      return;
    }

    const { width, height } = this._layout.bounds;
    const availableWidth = Math.max(canvas.clientWidth - 32, 1);
    const availableHeight = Math.max(canvas.clientHeight - 32, 1);
    const nextZoom = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.min(availableWidth / width, availableHeight / height)));
    this._zoom = Number(nextZoom.toFixed(2));
    requestAnimationFrame(() => {
      canvas.scrollTo({ left: 0, top: 0, behavior: 'smooth' });
    });
    this._announce('Canvas fit to screen.');
  }

  private _makeUniqueStageKey(base: string) {
    const usedKeys = new Set(this.workflow?.states.map(stage => stage.stateKey) ?? []);
    let candidate = base;
    let suffix = 2;
    while (usedKeys.has(candidate)) {
      candidate = `${base}-${suffix}`;
      suffix += 1;
    }
    return candidate;
  }

  private _slugifyStageKey(value: string, fallback: string) {
    const slug = value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      || fallback;
    return this._makeUniqueStageKey(slug);
  }

  private _defaultLaneForSurface(surface: StageSurface) {
    return surface === 'back-stage' ? 'reviewer' : 'public';
  }

  private _openCreateStageDialog(
    surfaceHint: StageSurface,
    position: 'append' | 'before' | 'after',
    referenceStageKey: string | null,
    returnTarget?: HTMLElement | null
  ) {
    const referenceStage = referenceStageKey
      ? this.workflow?.states.find(stage => stage.stateKey === referenceStageKey) ?? null
      : null;
    const defaultLaneKey = referenceStage ? stageLaneKey(referenceStage) : this._defaultLaneForSurface(surfaceHint);
    const baseTitle = 'New stage';
    this._dialogReturnTarget = returnTarget ?? this._contextReturnTarget ?? null;
    this._createStageDialog = {
      surfaceHint,
      position,
      referenceStageKey,
      title: baseTitle,
      stageKey: this._slugifyStageKey(baseTitle, 'new-stage'),
      laneKey: defaultLaneKey,
      stageType: 'form',
      keyTouched: false,
      error: null,
    };
    this._dismissContextMenu(false);
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLInputElement>('[data-prism-create-stage-title]')
        ?.focus();
    });
  }

  private _updateCreateStageTitle(value: string) {
    if (!this._createStageDialog) {
      return;
    }

    this._createStageDialog = {
      ...this._createStageDialog,
      title: value,
      stageKey: this._createStageDialog.keyTouched
        ? this._createStageDialog.stageKey
        : this._slugifyStageKey(value, 'new-stage'),
      error: null,
    };
  }

  private _updateCreateStageKey(value: string) {
    if (!this._createStageDialog) {
      return;
    }

    this._createStageDialog = {
      ...this._createStageDialog,
      stageKey: value,
      keyTouched: true,
      error: null,
    };
  }

  private _updateCreateStageLane(value: string) {
    if (!this._createStageDialog) {
      return;
    }

    const previewStage = applyLaneToStage({
      stateKey: '',
      displayName: '',
      metadata: { stageType: 'Question', actions: [], roleGates: [] },
      roleGates: [],
      actions: [],
      components: [],
    }, value);

    this._createStageDialog = {
      ...this._createStageDialog,
      laneKey: value,
      surfaceHint: stageSurface(previewStage),
      error: null,
    };
  }

  private _closeCreateStageDialog() {
    this._createStageDialog = null;
    const returnTarget = this._dialogReturnTarget;
    this._dialogReturnTarget = null;
    requestAnimationFrame(() => returnTarget?.focus());
  }

  private _submitCreateStage() {
    if (!this.workflow || !this._createStageDialog) {
      return;
    }

    const dialog = this._createStageDialog;
    const title = dialog.title.trim();
    const stageKey = dialog.stageKey.trim().toLowerCase();
    if (!title) {
      this._createStageDialog = { ...this._createStageDialog, error: 'Stage name is required.' };
      return;
    }

    if (!stageKey) {
      this._createStageDialog = { ...this._createStageDialog, error: 'Stage key is required.' };
      return;
    }

    if (this.workflow.states.some(stage => stage.stateKey === stageKey)) {
      this._createStageDialog = { ...this._createStageDialog, error: 'Stage key must be unique.' };
      return;
    }

    const newStage = applyLaneToStage({
      stageKey,
      displayName: title,
            components: [],
      metadata: {
        stageType: editorStageTypeToStageKind(dialog.stageType),
        actions: [],
        roleGates: [],
        editorComment: 'Created from the graph workspace.',
      },
    } as unknown as AuthoredStage, dialog.laneKey);

    const stages = [...this.workflow.states];
    let insertIndex = stages.length;
    if (dialog.referenceStageKey) {
      const referenceIndex = stages.findIndex(stage => stage.stateKey === dialog.referenceStageKey);
      if (referenceIndex >= 0) {
        insertIndex = dialog.position === 'before' ? referenceIndex : referenceIndex + 1;
      }
    }
    stages.splice(insertIndex, 0, newStage);

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      initialState: this.workflow.initialState || newStage.stateKey,
      states: stages,
    };

    this._selectedStageKey = newStage.stateKey;
    this._selectedTransitionIndex = null;
    this._emitSelectionChange({ kind: 'stage', stageKey: newStage.stateKey });
    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: newStage.stateKey });
    this._requestInspector({ kind: 'stage', stageKey: newStage.stateKey });
    this._announce(`${newStage.displayName} added to the workspace.`);
    this._closeCreateStageDialog();
  }

  private _openCreateGatewayDialog(returnTarget?: HTMLElement | null) {
    if (!this.workflow) {
      return;
    }
    this._dialogReturnTarget = returnTarget ?? null;
    const defaultLane = workflowLaneOptions(this.workflow, this.availableQueues)[0] ?? 'public';
    this._createGatewayDialog = {
      title: '',
      gatewayKey: '',
      kind: 'Split',
      laneKey: defaultLane,
      keyTouched: false,
      error: null,
    };
    requestAnimationFrame(() => {
      this.shadowRoot?.querySelector<HTMLInputElement>('[data-prism-create-gateway-title]')?.focus();
    });
  }

  private _closeCreateGatewayDialog() {
    this._createGatewayDialog = null;
    this._dialogReturnTarget?.focus();
    this._dialogReturnTarget = null;
  }

  private _submitCreateGateway() {
    if (!this.workflow || !this._createGatewayDialog) {
      return;
    }

    const dialog = this._createGatewayDialog;
    const title = dialog.title.trim();
    const key = dialog.gatewayKey.trim();

    if (!title) {
      this._createGatewayDialog = { ...dialog, error: 'Gateway name is required.' };
      return;
    }

    if (!key) {
      this._createGatewayDialog = { ...dialog, error: 'Gateway key is required.' };
      return;
    }

    const usedKeys = [
      ...this.workflow.states.map(s => s.stateKey),
      ...(this.workflow.metadata?.gateways ?? []).map(g => g.key),
    ];
    if (usedKeys.includes(key)) {
      this._createGatewayDialog = { ...dialog, error: 'Gateway key must be unique across all stages and gateways.' };
      return;
    }

    const newGateway: AuthoredGateway = {
      key,
      displayName: title,
      gatewayType: dialog.kind,
      laneKey: dialog.laneKey,
      actor: dialog.laneKey,
      roleGates: [],
    };

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      metadata: {
        ...(this.workflow.metadata ?? {}),
        gateways: [...(this.workflow.metadata?.gateways ?? []), newGateway],
      },
    };

    this._emitWorkflowUpdated(workflow, { kind: 'gateway', gatewayKey: newGateway.key });
    this._announce(`${title} ${dialog.kind} gateway created.`);
    this._closeCreateGatewayDialog();
  }

  private _openDeleteStageDialog(stageKey: string, returnTarget?: HTMLElement | null) {
    if (!this.workflow) {
      return;
    }

    this._dialogReturnTarget = returnTarget ?? this._contextReturnTarget ?? null;
    this._deleteStageDialog = {
      stageKey,
      affectedTransitions: (flattenRoutes(this.workflow)).filter(
        transition => transition.fromStage === stageKey || transition.toStage === stageKey
      ),
    };
    this._dismissContextMenu(false);
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLButtonElement>('[data-prism-delete-stage-cancel]')
        ?.focus();
    });
  }

  private _closeDeleteStageDialog() {
    this._deleteStageDialog = null;
    const returnTarget = this._dialogReturnTarget;
    this._dialogReturnTarget = null;
    requestAnimationFrame(() => returnTarget?.focus());
  }

  private _confirmDeleteStage() {
    if (!this.workflow || !this._deleteStageDialog) {
      return;
    }

    const stageKey = this._deleteStageDialog.stageKey;
    const deletedLabel = this._labelForStage(stageKey);
    const transitionCount = this._deleteStageDialog.affectedTransitions.length;
    const stages = this.workflow.states.filter(stage => stage.stateKey !== stageKey);

    // Drop any gateway whose source was this stage, and remove any route
    // that targeted this stage. The derived `transitions` view is rebuilt
    // by `withDerivedTransitions` before we hand the workflow downstream.
    const gateways = workflowGateways(this.workflow)
      .filter(gateway => gateway.key !== stageKey)
      .map(gateway => ({
        ...gateway,
        routes: (gateway.routes ?? []).filter(route => route.target !== stageKey),
      }));
    const stagesWithRoutes = stages.map(stage => ({
      ...stage,
      routes: (stage.routes ?? []).filter(route => route.target !== stageKey),
    }));

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      states: stagesWithRoutes,
      gateways,
      initialState:
        this.workflow.initialState === stageKey
          ? stages[0]?.stateKey ?? ''
          : this.workflow.initialState,
    };

    this._selectedStageKey = null;
    this._selectedTransitionIndex = null;
    this._emitWorkflowUpdated(workflow, null);
    this._announce(
      `${deletedLabel} deleted.${transitionCount > 0 ? ` ${transitionCount} affected transition${transitionCount === 1 ? '' : 's'} removed.` : ''}`
    );
    this._closeDeleteStageDialog();
  }

  private async _copyStage(stageKey: string) {
    const stage = this.workflow?.states.find(candidate => candidate.stateKey === stageKey);
    if (!stage) {
      return;
    }

    const payload = JSON.stringify(stage, null, 2);
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(payload);
      }
      this._announce(`${stage.displayName} copied.`);
    } catch {
      this._announce(`${stage.displayName} copy prepared, but clipboard access was unavailable.`);
    }
    this._dismissContextMenu(false);
  }

  private async _copyTransition(index: number) {
    const transition = (flattenRoutes(this.workflow))[index];
    if (!transition) {
      return;
    }

    const payload = JSON.stringify(transition, null, 2);
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(payload);
      }
      this._announce(`Transition “${transition.action}” copied.`);
    } catch {
      this._announce(`Transition “${transition.action}” copy prepared, but clipboard access was unavailable.`);
    }
    this._dismissContextMenu(false);
  }

  private _deleteTransition(index: number) {
    if (!this.workflow) {
      return;
    }

    const transition = (flattenRoutes(this.workflow))[index];
    if (!transition) {
      return;
    }

    const gatewayKey = transition.key;
    const routeId = transition.routeId;
    if (!gatewayKey || !routeId) {
      return;
    }
    const workflow: AuthoredWorkflow = deleteRoute(this.workflow, { gatewayKey, routeId });

    this._selectedTransitionIndex = null;
    this._emitWorkflowUpdated(workflow, null);
    this._dismissContextMenu(false);
    this._announce(`Transition “${transition.action}” deleted.`);
  }

  private _openContextMenu(event: MouseEvent, target: ContextMenuTarget, returnTarget?: HTMLElement) {
    event.preventDefault();
    event.stopPropagation();
    const hostRect = this.getBoundingClientRect();
    this._contextMenu = {
      ...target,
      x: Math.max(12, event.clientX - hostRect.left),
      y: Math.max(12, event.clientY - hostRect.top),
    };
    this._contextReturnTarget = returnTarget ?? null;

    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLButtonElement>('[data-prism-context-menu] button')
        ?.focus();
    });
  }

  private _openContextMenuFromKeyboard(target: ContextMenuTarget, anchor: HTMLElement) {
    const rect = anchor.getBoundingClientRect();
    const event = new MouseEvent('contextmenu', {
      bubbles: true,
      cancelable: true,
      composed: true,
      clientX: rect.left + rect.width / 2,
      clientY: rect.bottom,
    });
    this._openContextMenu(event, target, anchor);
  }

  private _dismissContextMenu(restoreFocus = true) {
    this._contextMenu = null;
    if (restoreFocus && this._contextReturnTarget) {
      requestAnimationFrame(() => this._contextReturnTarget?.focus());
    }
    this._contextReturnTarget = null;
  }

  private _handleContextMenuAction(action: string) {
    const target = this._contextMenu;
    if (!target) {
      return;
    }

    if (action === 'fit-screen') {
      this._fitToScreen();
      this._dismissContextMenu(false);
      return;
    }

    if (action === 'add-stage') {
      const referenceStageKey = target.kind === 'stage' ? target.stageKey : this._selectedStageKey;
      const referenceStage = referenceStageKey
        ? this.workflow?.states.find(stage => stage.stateKey === referenceStageKey) ?? null
        : null;
      this._openCreateStageDialog(
        referenceStage ? this._surfaceForStage(referenceStage) : 'front-stage',
        target.kind === 'stage' ? 'after' : 'append',
        target.kind === 'stage' ? target.stageKey : null
      );
      return;
    }

    if (target.kind === 'stage') {
      if (action === 'copy-stage') {
        void this._copyStage(target.stageKey);
      } else if (action === 'delete-stage') {
        this._openDeleteStageDialog(target.stageKey);
      } else if (action === 'edit-stage') {
        this._selectStage(target.stageKey, { openInspector: true });
        this._dismissContextMenu(false);
      }
      return;
    }

    if (target.kind === 'transition') {
      if (action === 'copy-transition') {
        void this._copyTransition(target.transitionIndex);
      } else if (action === 'delete-transition') {
        this._deleteTransition(target.transitionIndex);
      } else if (action === 'edit-transition') {
        this._selectTransition(target.transitionIndex, { openInspector: true });
        this._dismissContextMenu(false);
      }
    }
  }

  private _handleGraphNodeKeydown(
    event: KeyboardEvent,
    node: { kind: 'stage'; stage: AuthoredStage; index: number } | { kind: 'gateway'; gateway: AuthoredGateway }
  ) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (node.kind === 'stage') {
        this._selectStage(node.stage.stateKey, { focusIndex: node.index });
      } else {
        this._selectGateway(node.gateway.key);
      }
      return;
    }

    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      if (node.kind === 'stage') {
        this._selectStage(node.stage.stateKey, { openInspector: true, focusIndex: node.index });
      } else {
        this._selectGateway(node.gateway.key, { openInspector: true });
      }
      return;
    }

    if (node.kind === 'stage' && (event.key === 'Delete' || event.key === 'Backspace')) {
      event.preventDefault();
      this._openDeleteStageDialog(node.stage.stateKey, event.currentTarget as HTMLElement);
      return;
    }

    if (node.kind === 'stage' && (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10'))) {
      event.preventDefault();
      this._openContextMenuFromKeyboard({ kind: 'stage', stageKey: node.stage.stateKey }, event.currentTarget as HTMLElement);
      return;
    }
  }

  private _handleTransitionKeydown(event: KeyboardEvent, transitionIndex: number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this._selectTransition(transitionIndex);
      return;
    }

    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      this._selectTransition(transitionIndex, { openInspector: true });
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this._deleteTransition(transitionIndex);
      return;
    }

    if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
      event.preventDefault();
      this._openContextMenuFromKeyboard({ kind: 'transition', transitionIndex }, event.currentTarget as HTMLElement);
    }
  }

  private _renderContextMenu() {
    const target = this._contextMenu;
    if (!target) {
      return nothing;
    }

    return html`
      <div
        class="context-menu"
        style=${`left:${target.x}px;top:${target.y}px;`}
        role="menu"
        aria-label="Graph workspace actions"
        data-prism-context-menu
        @keydown=${(event: KeyboardEvent) => {
          if (event.key === 'Escape') {
            event.preventDefault();
            this._dismissContextMenu();
          }
        }}
        @click=${(event: Event) => event.stopPropagation()}
      >
        ${target.kind !== 'transition'
          ? html`
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('add-stage')}>
                Add stage
              </button>
            `
          : nothing}
        ${target.kind === 'canvas'
          ? html`
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('fit-screen')}>
                Fit to screen
              </button>
            `
          : nothing}
        ${target.kind === 'stage'
          ? html`
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('edit-stage')}>
                Open stage inspector
              </button>
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('copy-stage')}>
                Copy stage JSON
              </button>
              <button type="button" role="menuitem" class="danger" @click=${() => this._handleContextMenuAction('delete-stage')}>
                Delete stage
              </button>
            `
          : nothing}
        ${target.kind === 'transition'
          ? html`
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('edit-transition')}>
                Open transition inspector
              </button>
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('copy-transition')}>
                Copy transition JSON
              </button>
              <button type="button" role="menuitem" class="danger" @click=${() => this._handleContextMenuAction('delete-transition')}>
                Delete transition
              </button>
            `
          : nothing}
      </div>
    `;
  }

  private _handleDialogKeydown(event: KeyboardEvent, onClose: () => void) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const root = event.currentTarget as HTMLElement;
    const focusable = Array.from(
      root.querySelectorAll<HTMLElement>('button, input, select, textarea, [href], [tabindex]:not([tabindex="-1"])')
    ).filter(element => !element.hasAttribute('disabled') && element.tabIndex >= 0);
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const activeElement = this.shadowRoot?.activeElement as HTMLElement | null;
    if (event.shiftKey && activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private _renderCreateStageDialog() {
    const dialog = this._createStageDialog;
    if (!dialog) {
      return nothing;
    }

    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-stage-dialog-title"
          aria-describedby="create-stage-dialog-copy"
          data-prism-create-stage-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeCreateStageDialog())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow">Stage creation</p>
              <h2 id="create-stage-dialog-title" class="dialog-title">Create stage</h2>
            </div>
          </div>
          <p id="create-stage-dialog-copy" class="dialog-copy">
            Name the stage, choose its key, queue, and type, then continue editing in the inspector.
          </p>
          ${dialog.error ? html`<p class="dialog-error" data-prism-create-stage-error>${dialog.error}</p>` : nothing}
          <div class="dialog-grid">
            <label class="dialog-field">
              <span class="dialog-label">Name</span>
              <input
                class="dialog-control"
                data-prism-create-stage-title
                .value=${dialog.title}
                @input=${(event: Event) => this._updateCreateStageTitle((event.currentTarget as HTMLInputElement).value)}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Key</span>
              <input
                class="dialog-control"
                data-prism-create-stage-key
                .value=${dialog.stageKey}
                @input=${(event: Event) => this._updateCreateStageKey((event.currentTarget as HTMLInputElement).value)}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Queue</span>
              <input
                class="dialog-control"
                data-prism-create-stage-lane
                .value=${dialog.laneKey}
                list="create-stage-lane-options"
                placeholder="planning"
                @input=${(event: Event) => this._updateCreateStageLane((event.currentTarget as HTMLInputElement).value)}
              />
              <datalist id="create-stage-lane-options">
                ${this._availableLaneKeys().map(option => html`
                  <option value=${option}>${this._roleLabelForLane(option)}</option>
                `)}
              </datalist>
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Type</span>
              <select
                class="dialog-control"
                data-prism-create-stage-type
                @change=${(event: Event) => {
                  const stageType = (event.currentTarget as HTMLSelectElement).value as EditorStageType;
                  this._createStageDialog = this._createStageDialog
                    ? { ...this._createStageDialog, stageType }
                    : null;
                }}
              >
                <option value="form" ?selected=${dialog.stageType === 'form'}>Form</option>
                <option value="review" ?selected=${dialog.stageType === 'review'}>Review</option>
                <option value="decision" ?selected=${dialog.stageType === 'decision'}>Decision</option>
                <option value="confirmation" ?selected=${dialog.stageType === 'confirmation'}>Confirmation</option>
              </select>
            </label>
          </div>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" @click=${this._closeCreateStageDialog}>Cancel</button>
            <button type="button" class="dialog-button primary" data-prism-create-stage-submit @click=${this._submitCreateStage}>Create stage</button>
          </div>
        </div>
      </div>
    `;
  }

  private _renderDeleteStageDialog() {
    const dialog = this._deleteStageDialog;
    if (!dialog) {
      return nothing;
    }

    const stageLabel = this._labelForStage(dialog.stageKey);
    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel dialog-panel-danger"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-stage-dialog-title"
          aria-describedby="delete-stage-dialog-copy"
          data-prism-delete-stage-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeDeleteStageDialog())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow danger">Delete stage</p>
              <h2 id="delete-stage-dialog-title" class="dialog-title">Delete ${stageLabel}?</h2>
            </div>
          </div>
          <p id="delete-stage-dialog-copy" class="dialog-copy">
            This removes the stage and every transition connected to it.
          </p>
          <div class="delete-impact" data-prism-delete-stage-transitions>
            ${dialog.affectedTransitions.length === 0
              ? html`<p>No transitions will be removed.</p>`
              : html`
                  <p>${dialog.affectedTransitions.length} affected transition${dialog.affectedTransitions.length === 1 ? '' : 's'}:</p>
                  <ul>
                    ${dialog.affectedTransitions.map(transition => html`
                      <li>${this._labelForStage(transition.fromStage)} → ${this._labelForStage(transition.toStage)} (${transition.action})</li>
                    `)}
                  </ul>
                `}
          </div>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" data-prism-delete-stage-cancel @click=${this._closeDeleteStageDialog}>Cancel</button>
            <button type="button" class="dialog-button danger" data-prism-delete-stage-confirm @click=${this._confirmDeleteStage}>Delete stage</button>
          </div>
        </div>
      </div>
    `;
  }


  private _renderCreateGatewayDialog() {
    const dialog = this._createGatewayDialog;
    if (!dialog) {
      return nothing;
    }

    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-gateway-dialog-title"
          aria-describedby="create-gateway-dialog-copy"
          data-prism-create-gateway-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeCreateGatewayDialog())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow">Gateway creation</p>
              <h2 id="create-gateway-dialog-title" class="dialog-title">Add gateway</h2>
            </div>
          </div>
          <p id="create-gateway-dialog-copy" class="dialog-copy">
            Add a Split or Join gateway to the workspace. Continue editing in the inspector after creation.
          </p>
          ${dialog.error ? html`<p class="dialog-error" data-prism-create-gateway-error>${dialog.error}</p>` : nothing}
          <div class="dialog-grid">
            <label class="dialog-field">
              <span class="dialog-label">Name</span>
              <input
                class="dialog-control"
                data-prism-create-gateway-title
                .value=${dialog.title}
                @input=${(event: Event) => {
                  const title = (event.currentTarget as HTMLInputElement).value;
                  const gatewayKey = dialog.keyTouched
                    ? dialog.gatewayKey
                    : title.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '');
                  this._createGatewayDialog = this._createGatewayDialog
                    ? { ...this._createGatewayDialog, title, gatewayKey, error: null }
                    : null;
                }}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Key</span>
              <input
                class="dialog-control"
                data-prism-create-gateway-key
                .value=${dialog.gatewayKey}
                @input=${(event: Event) => {
                  const gatewayKey = (event.currentTarget as HTMLInputElement).value;
                  this._createGatewayDialog = this._createGatewayDialog
                    ? { ...this._createGatewayDialog, gatewayKey, keyTouched: true, error: null }
                    : null;
                }}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Kind</span>
              <select
                class="dialog-control"
                data-prism-create-gateway-kind
                @change=${(event: Event) => {
                  const kind = (event.currentTarget as HTMLSelectElement).value as 'Split' | 'Join';
                  this._createGatewayDialog = this._createGatewayDialog
                    ? { ...this._createGatewayDialog, kind }
                    : null;
                }}
              >
                <option value="Split" ?selected=${dialog.kind === 'Split'}>Split — branches into multiple paths</option>
                <option value="Join" ?selected=${dialog.kind === 'Join'}>Join — converges multiple paths</option>
              </select>
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Queue</span>
              <input
                class="dialog-control"
                data-prism-create-gateway-lane
                .value=${dialog.laneKey}
                list="create-gateway-lane-options"
                placeholder="applicant"
                @input=${(event: Event) => {
                  const laneKey = (event.currentTarget as HTMLInputElement).value;
                  this._createGatewayDialog = this._createGatewayDialog
                    ? { ...this._createGatewayDialog, laneKey }
                    : null;
                }}
              />
              <datalist id="create-gateway-lane-options">
                ${this._availableLaneKeys().map(option => html`
                  <option value=${option}>${this._roleLabelForLane(option)}</option>
                `)}
              </datalist>
            </label>
          </div>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" @click=${this._closeCreateGatewayDialog}>Cancel</button>
            <button type="button" class="dialog-button primary" data-prism-create-gateway-submit @click=${this._submitCreateGateway}>Create gateway</button>
          </div>
        </div>
      </div>
    `;
  }

  private _renderGraph() {
    const { bounds, roleLanes, stageLayouts, gatewayLayouts, transitionLayouts, visualRouteLayouts } = this._layout;
    const isEmpty = stageLayouts.length === 0 && gatewayLayouts.length === 0;
    const dragPath: string | null = null;

    return html`
      <div class="graph-hud" aria-label="Workspace controls and hints">
        ${this.readOnly
          ? nothing
          : html`
              <div class="hud-group">
                <button
                  type="button"
                  class="hud-button"
                  data-prism-add-stage
                  @click=${(event: Event) => {
                    const selectedStage = this.workflow?.states.find(stage => stage.stateKey === this._selectedStageKey) ?? null;
                    this._openCreateStageDialog(
                      selectedStage ? this._surfaceForStage(selectedStage) : 'front-stage',
                      this._selectedStageKey ? 'after' : 'append',
                      this._selectedStageKey,
                      event.currentTarget as HTMLElement
                    );
                  }}
                >
                  Add stage
                </button>
                <button
                  type="button"
                  class="hud-button"
                  data-prism-add-gateway
                  @click=${(event: Event) => this._openCreateGatewayDialog(event.currentTarget as HTMLElement)}
                >
                  Add gateway
                </button>
              </div>
            `}
        <div class="hud-group">
          <button type="button" class="hud-button" aria-label="Zoom out" @click=${() => this._zoomBy(-0.1)}>
            −
          </button>
          <span class="zoom-indicator" data-prism-zoom>${Math.round(this._zoom * 100)}%</span>
          <button type="button" class="hud-button" aria-label="Zoom in" @click=${() => this._zoomBy(0.1)}>
            +
          </button>
          <button type="button" class="hud-button" data-prism-fit-screen @click=${this._fitToScreen}>
            Fit
          </button>
        </div>
      </div>

      <p class="graph-hint">
        ${this.readOnly
          ? 'Tab through queues and stages. Enter selects, and arrow keys move between stages.'
          : 'Tab through queues, stages, routes, and gateways. Enter selects, E opens details, and Shift+F10 opens the menu.'}
      </p>

      ${isEmpty
        ? this._renderWorkspaceEmptyState()
        : html`<div
            class="graph-canvas"
            role="application"
            tabindex="0"
            aria-label=${`Workflow graph canvas — ${this.workflow?.displayName ?? 'workflow'}`}
            aria-roledescription=${this.readOnly ? 'Workflow graph viewer' : 'Workflow graph editor'}
            @click=${() => this._dismissContextMenu(false)}
            @contextmenu=${this.readOnly ? nothing : (event: MouseEvent) => this._openContextMenu(event, { kind: 'canvas' })}
          >
        <div class="graph-viewport" tabindex="0">
          <div
            class="graph-scene-frame"
            style=${`width:${bounds.width * this._zoom}px;height:${bounds.height * this._zoom}px;`}
          >
            <div
              class="graph-scene"
              style=${`width:${bounds.width}px;height:${bounds.height}px;transform:scale(${this._zoom});`}
              data-prism-component="workflow-graph"
              data-prism-mode="graph"
            >
              ${roleLanes.map(lane => {
                const headingId = `lane-heading-${lane.key}`;
                const copyId = `lane-copy-${lane.key}`;
                return html`
                  <section
                    class=${`lane ${lane.surface === 'back-stage' ? 'lane-supporting' : 'lane-primary'}`}
                    style=${`left:${lane.x}px;width:${lane.width}px;`}
                    tabindex="0"
                    aria-labelledby=${headingId}
                    aria-describedby=${copyId}
                    data-prism-role-lane=${lane.key}
                    data-prism-lane-container=${lane.key}
                    @focus=${() => this._announce(`${lane.label} queue. ${lane.stageCount} stage${lane.stageCount === 1 ? '' : 's'}. ${lane.description}.`)}
                  >
                    <div class="lane-header" data-prism-lane-header=${lane.key}>
                      <div id=${headingId} class="lane-heading">${lane.label}</div>
                      <div class="lane-meta">${lane.stageCount} stage${lane.stageCount === 1 ? '' : 's'}</div>
                    </div>
                    <div id=${copyId} class="lane-copy">${lane.description}</div>
                  </section>
                `;
              })}

              <svg class="graph-edges" viewBox=${`0 0 ${bounds.width} ${bounds.height}`} aria-hidden="true">
                <defs>
                  <marker id="graph-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto" markerUnits="strokeWidth">
                    <path d="M0,0 L0,6 L9,3 z" fill="#6b7280"></path>
                  </marker>
                </defs>
                ${visualRouteLayouts.map(layout => layout.path ? svg`
                  <path
                    class=${`edge-path route-rail ${layout.simulationPath ? 'simulation-path' : ''}`}
                    d=${layout.path}
                    marker-end="url(#graph-arrow)"
                    data-prism-route-path=${layout.key}
                    data-prism-route-from=${layout.fromKey}
                    data-prism-route-to=${layout.toKey}
                    data-prism-route-simulation-path=${String(layout.simulationPath)}
                  ></path>
                ` : nothing)}
                ${transitionLayouts.map(layout => layout.path ? svg`
                  <path
                    class=${`edge-path ${layout.branch ? 'branch-path' : ''} ${layout.merge ? 'merge-path' : ''} ${this._selectedTransitionIndex === layout.index ? 'selected' : ''} ${this._transitionIsInSimulationPath(layout.index) ? 'simulation-path' : ''}`}
                    d=${layout.path}
                    data-prism-transition-path=${String(layout.index)}
                    data-prism-transition-from=${layout.visualFromKey}
                    data-prism-transition-to=${layout.visualToKey}
                    data-prism-transition-simulation-path=${String(this._transitionIsInSimulationPath(layout.index))}
                  ></path>
                ` : nothing)}
                ${dragPath ? svg`<path class="edge-path draft" d=${dragPath}></path>` : nothing}
              </svg>

              ${transitionLayouts.map(layout => layout.path && layout.showLabel ? html`
                <button
                  type="button"
                  class=${`edge-chip ${layout.branch ? 'branch-path' : ''} ${layout.merge ? 'merge-path' : ''} ${this._selectedTransitionIndex === layout.index ? 'selected' : ''} ${this._transitionIsInSimulationPath(layout.index) ? 'simulation-path' : ''}`}
                  style=${`left:${layout.labelX - EDGE_LABEL_WIDTH / 2}px;top:${layout.labelY - EDGE_LABEL_HEIGHT / 2}px;`}
                  aria-label=${`Transition ${layout.transition.action}, ${this._transitionDescriptor(layout.transition)}`}
                  data-prism-transition="${layout.index}"
                  data-prism-transition-from=${layout.visualFromKey}
                  data-prism-transition-to=${layout.visualToKey}
                  data-prism-transition-simulation-path=${String(this._transitionIsInSimulationPath(layout.index))}
                  @click=${() => this._selectTransition(layout.index)}
                  @dblclick=${() => this._selectTransition(layout.index, { openInspector: true })}
                  @keydown=${(event: KeyboardEvent) => this._handleTransitionKeydown(event, layout.index)}
                  @contextmenu=${this.readOnly ? nothing : (event: MouseEvent) => this._openContextMenu(event, { kind: 'transition', transitionIndex: layout.index }, event.currentTarget as HTMLElement)}
                >
                  ${layout.transition.action}
                </button>
              ` : nothing)}

              ${gatewayLayouts.map(layout => {
                const routeCount = (layout.gateway.routes ?? []).length;
                const isPill = this._isPillGateway(layout.gateway);
                const shapeClass = isPill ? 'shape-pill' : 'shape-diamond';
                const route = isPill ? (layout.gateway.routes ?? [])[0] : null;
                const triggerLabel = route?.trigger ?? '';
                const hasCondition = !!(route?.condition && route.condition.trim().length > 0);
                return html`
                <div
                  class="gateway-node-shell ${shapeClass}"
                  data-prism-gateway-node=${layout.gateway.key}
                  data-prism-gateway-shape=${isPill ? 'pill' : 'diamond'}
                  data-prism-row-rank=${String(layout.rowRank)}
                  style=${`left:${layout.x}px;top:${layout.y}px;width:${layout.width}px;height:${layout.height}px;`}
                >
                  <button
                    type="button"
                    class=${`gateway-node ${layout.surface} kind-${layout.gateway.gatewayType.toLowerCase()} ${shapeClass} ${this._selectedGatewayKey === layout.gateway.key ? 'selected' : ''}`}
                    aria-pressed=${String(this._selectedGatewayKey === layout.gateway.key)}
                    aria-label=${isPill
                      ? `${layout.gateway.displayName}, single-route gateway via “${triggerLabel}”, ${layout.laneLabel} queue`
                      : `${layout.gateway.displayName}, ${layout.gateway.gatewayType} gateway, ${layout.laneLabel} queue`}
                    data-prism-gateway=${layout.gateway.key}
                    data-prism-gateway-kind=${layout.gateway.gatewayType}
                    data-prism-gateway-route-count=${String(routeCount)}
                    data-prism-lane=${layout.laneKey}
                    @click=${() => this._selectGateway(layout.gateway.key)}
                    @dblclick=${() => this._selectGateway(layout.gateway.key, { openInspector: true })}
                    @keydown=${(event: KeyboardEvent) => this._handleGraphNodeKeydown(event, { kind: 'gateway', gateway: layout.gateway })}
                  >
                    ${isPill
                      ? html`
                          <span class="pill-trigger">${triggerLabel || layout.gateway.displayName}</span>
                          ${hasCondition ? html`<span class="pill-condition" aria-label="conditional route" title="${route?.condition ?? ''}">•</span>` : nothing}
                        `
                      : html`
                          <span class="node-label">${layout.gateway.displayName}</span>
                        `}
                  </button>
                </div>
              `;
              })}

              ${stageLayouts.map((layout, visualIndex) => html`
                <div
                  class="stage-node-shell"
                  data-prism-stage-card=${layout.stage.stateKey}
                  data-prism-row-rank=${String(layout.rowRank)}
                  style=${`left:${layout.x}px;top:${layout.y}px;width:${layout.width}px;height:${layout.height}px;`}
                >
                  <button
                    type="button"
                    class=${`stage-node ${layout.surface} ${this._selectedStageKey === layout.stage.stateKey ? 'selected' : ''} ${this._stageIsInSimulationPath(layout.stage.stateKey) ? 'simulation-path' : ''} ${this.simulationCurrentStageKey === layout.stage.stateKey ? 'simulation-current' : ''}`}
                    aria-pressed=${String(this._selectedStageKey === layout.stage.stateKey)}
                    aria-label=${`${layout.stage.displayName}, ${layout.laneLabel} queue`}
                    data-prism-stage="${layout.stage.stateKey}"
                    data-prism-lane=${layout.laneKey}
                    data-prism-stage-simulation-path=${String(this._stageIsInSimulationPath(layout.stage.stateKey))}
                    data-prism-stage-simulation-current=${String(this.simulationCurrentStageKey === layout.stage.stateKey)}
                    @click=${() => this._selectStage(layout.stage.stateKey, { focusIndex: visualIndex })}
                    @dblclick=${() => this._selectStage(layout.stage.stateKey, { openInspector: true, focusIndex: visualIndex })}
                    @keydown=${(event: KeyboardEvent) => this._handleGraphNodeKeydown(event, { kind: 'stage', stage: layout.stage, index: visualIndex })}
                    @contextmenu=${this.readOnly ? nothing : (event: MouseEvent) => this._openContextMenu(event, { kind: 'stage', stageKey: layout.stage.stateKey }, event.currentTarget as HTMLElement)}
                  >
                    <span class="node-label">${layout.stage.displayName}</span>
                    <span class="node-meta">${layout.stage.kind}</span>
                  </button>
                </div>
              `)}
            </div>
          </div>
        </div>
      </div>`}
    `;
  }

  private _renderWorkspaceEmptyState() {
    return html`
      <section class="workspace-empty-state" role="status" data-prism-empty-state="graph">
        <h2 class="workspace-empty-title">${this.readOnly ? 'No stages to display' : 'Start building your workflow'}</h2>
        <p class="workspace-empty-copy">
          ${this.readOnly
            ? 'This workflow has no stages.'
            : 'This workflow does not have any stages yet. Add the first stage, then connect routes as you model the author journey.'}
        </p>
        ${this.readOnly
          ? nothing
          : html`
              <ul class="workspace-empty-tips">
                <li>Use <strong>Add stage</strong>, then choose the queue that should own the work.</li>
                <li><strong>Add the next stage before you branch</strong> — gateways always connect existing stages, never empty space.</li>
                <li>Use the editor Help button or press <strong>F1</strong> to review shortcuts while you work.</li>
              </ul>
              <div class="workspace-empty-actions">
                <button
                  type="button"
                  class="hud-button"
                  data-prism-empty-add-stage
                  @click=${(event: Event) => this._openCreateStageDialog('front-stage', 'append', null, event.currentTarget as HTMLElement)}
                >
                  Add first stage
                </button>
              </div>
            `}
      </section>
    `;
  }


  render() {
    return html`
      <div class="workflow-graph-root" data-prism-component="workflow-graph" data-prism-mode="graph" data-prism-read-only=${String(this.readOnly)}>
        <div class="toolbar">
          <div class="toolbar-title-block">
            <span class="workflow-title">${this.workflow?.displayName ?? 'No workflow loaded'}</span>
            <span class="workflow-subtitle">${this.readOnly ? 'Published workflow — read-only viewer' : 'Visual workflow map'}</span>
          </div>
        </div>

        <div id="graph-announcer" role="status" aria-live="polite" aria-atomic="true" class="sr-only"></div>

        ${this._renderGraph()}
        ${this.readOnly ? nothing : this._renderContextMenu()}
        ${this.readOnly ? nothing : this._renderCreateStageDialog()}
        ${this.readOnly ? nothing : this._renderDeleteStageDialog()}
        ${this.readOnly ? nothing : this._renderCreateGatewayDialog()}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      height: 100%;
      min-height: 0;
      overflow: hidden;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
      color: #111827;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .workflow-graph-root {
      position: relative;
      display: flex;
      flex-direction: column;
      flex: 1;
      height: 100%;
      min-height: 0;
      background: #f8fafc;
      border: 1px solid #d1d5db;
      border-radius: 12px;
      overflow: hidden;
    }

    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.875rem 1rem;
      background: #ffffff;
      border-bottom: 1px solid #dbe2ea;
    }

    .toolbar-title-block {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      min-width: 0;
    }

    .workflow-title {
      font-size: 1rem;
      font-weight: 700;
      color: #0f172a;
    }

    .workflow-subtitle {
      font-size: 0.8125rem;
      color: #475569;
    }

    .mode-toggle,
    .hud-button,
    .context-menu button,
    .edge-chip,
    .exit-tag,
    .transition-handle {
      font: inherit;
    }

    .mode-toggle,
    .hud-button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.25rem;
      min-height: 2.25rem;
      padding: 0.375rem 0.875rem;
      border: 1px solid #475569;
      border-radius: 999px;
      background: #ffffff;
      color: #0f172a;
      cursor: pointer;
    }

    .mode-toggle[aria-pressed='true'] {
      background: #1d4ed8;
      border-color: #1d4ed8;
      color: #ffffff;
    }

    .mode-toggle:focus-visible,
    .hud-button:focus-visible,
    .context-menu button:focus-visible,
    .validation-link:focus-visible,
    .transition-link:focus-visible,
    .edge-chip:focus-visible,
    .gateway-node:focus-visible,
    .stage-node:focus-visible,
    .row-trigger:focus-visible,
    .drag-handle:focus-visible,
    .row-action-button:focus-visible,
    .table-input:focus-visible,
    .table-select:focus-visible,
    .exit-tag:focus-visible,
    .transition-handle:focus-visible {
      outline: 3px solid #0b0c0c;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px #ffdd00;
    }

    .validation-banner {
      margin: 0 1rem;
      padding: 0.875rem 1rem;
      border-bottom: 1px solid #fdba74;
      background: #fff7ed;
    }

    .validation-banner-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
    }

    .validation-banner-title {
      margin: 0;
      font-size: 0.875rem;
      font-weight: 700;
      color: #9a3412;
    }

    .validation-banner-meta {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #9a3412;
    }

    .validation-banner-list {
      margin: 0.625rem 0 0;
      padding-left: 1rem;
      display: grid;
      gap: 0.375rem;
    }

    .validation-link {
      border: none;
      padding: 0;
      background: transparent;
      color: #9a3412;
      font: inherit;
      text-align: left;
      text-decoration: underline;
      cursor: pointer;
    }

    .graph-hud {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.875rem 1rem 0.5rem;
      background: linear-gradient(180deg, #f8fafc 0%, rgba(248, 250, 252, 0.92) 100%);
    }

    .hud-group {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }

    .zoom-indicator {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 3rem;
      font-size: 0.875rem;
      font-weight: 600;
      color: #334155;
    }

    .graph-hint {
      margin: 0;
      padding: 0 1rem 0.75rem;
      font-size: 0.8125rem;
      color: #475569;
    }

    .graph-canvas {
      flex: 1;
      min-height: 0;
      padding: 0 1rem 1rem;
      overflow: auto;
      min-width: 800px;
      min-height: 400px;
    }

    .graph-viewport {
      position: relative;
      min-width: 100%;
      min-height: 100%;
      width: fit-content;
      overflow: visible;
      border: 1px solid #dbe2ea;
      border-radius: 12px;
      background:
        radial-gradient(circle at top left, rgba(59, 130, 246, 0.08), transparent 28%),
        linear-gradient(180deg, #ffffff 0%, #f8fafc 100%);
    }

    .graph-scene-frame {
      position: relative;
    }

    .graph-scene {
      position: relative;
      transform-origin: top left;
    }

    .lane {
      position: absolute;
      box-sizing: border-box;
      top: ${TOP_PADDING}px;
      height: calc(100% - ${TOP_PADDING * 2}px);
      border-radius: 18px;
      border: 1px solid #dbe2ea;
      padding: 18px 20px;
      background: rgba(255, 255, 255, 0.88);
    }

    .lane:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 3px;
    }

    .lane-primary {
      box-shadow: inset 0 0 0 1px rgba(29, 78, 216, 0.08);
    }

    .lane-supporting {
      box-shadow: inset 0 0 0 1px rgba(71, 85, 105, 0.14);
      background: rgba(248, 250, 252, 0.96);
    }

    .lane-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .lane-heading {
      font-size: 0.875rem;
      font-weight: 700;
      color: #0f172a;
    }

    .lane-meta {
      font-size: 0.75rem;
      font-weight: 700;
      color: #334155;
    }

    .lane-copy {
      margin-top: 0.125rem;
      font-size: 0.75rem;
      color: #475569;
    }

    .graph-edges {
      position: absolute;
      inset: 0;
      overflow: visible;
      pointer-events: none;
    }

    .edge-path {
      fill: none;
      stroke: #6b7280;
      stroke-width: 2.25;
      stroke-linecap: round;
      stroke-linejoin: round;
      opacity: 0.82;
    }

    .edge-path.selected {
      stroke: #1d4ed8;
      stroke-width: 3;
      opacity: 1;
    }

    .edge-path.simulation-path {
      stroke: #00703c;
      stroke-width: 3.5;
      opacity: 1;
    }

    .edge-path.draft {
      stroke-dasharray: 10 8;
      stroke: #1d4ed8;
      opacity: 0.9;
    }

    .edge-path.branch-path {
      stroke: #7c3aed;
      stroke-dasharray: 8 8;
    }

    .edge-path.merge-path {
      stroke: #0f766e;
    }

    .edge-chip {
      position: absolute;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: ${EDGE_LABEL_WIDTH}px;
      min-height: ${EDGE_LABEL_HEIGHT}px;
      padding: 0.25rem 0.625rem;
      border: 1px solid #cbd5e1;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.96);
      color: #0f172a;
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.08);
      cursor: pointer;
    }

    .edge-chip.selected {
      border-color: #1d4ed8;
      background: #dbeafe;
      color: #1d4ed8;
    }

    .edge-chip.simulation-path {
      border-color: #00703c;
      background: #e8f5e9;
      color: #005a30;
    }

    .edge-chip.branch-path {
      border-color: #c4b5fd;
      background: #f5f3ff;
      color: #6d28d9;
    }

    .edge-chip.merge-path {
      border-color: #99f6e4;
      background: #f0fdfa;
      color: #0f766e;
    }

    .edge-chip.branch-path.selected,
    .edge-chip.merge-path.selected {
      border-color: #1d4ed8;
      color: #1d4ed8;
    }

    .stage-node-shell {
      position: absolute;
    }

    .gateway-node-shell {
      position: absolute;
    }

    .gateway-node {
      position: relative;
      display: flex;
      width: 100%;
      height: 100%;
      flex-direction: column;
      justify-content: center;
      gap: 0.35rem;
      padding: 0.75rem;
      appearance: none;
      text-align: center;
      border: 2px dashed #8b5cf6;
      border-radius: 28px;
      background: linear-gradient(180deg, #ffffff 0%, #f5f3ff 100%);
      box-shadow: 0 10px 26px rgba(124, 58, 237, 0.12);
      cursor: pointer;
    }

    .gateway-node.kind-join {
      border-color: #0f766e;
      background: linear-gradient(180deg, #ffffff 0%, #ecfeff 100%);
      box-shadow: 0 10px 26px rgba(15, 118, 110, 0.12);
    }

    .gateway-node.selected {
      border-style: solid;
      border-color: #1d4ed8;
      box-shadow: 0 0 0 3px rgba(29, 78, 216, 0.18), 0 12px 28px rgba(29, 78, 216, 0.16);
    }

    .gateway-node .surface-tag {
      align-self: center;
    }

    /* Single-route Split gateways render as a thin pill — low visual weight
       so straight-through routing reads as "stage → small pill → next stage"
       instead of a heavy diamond. Multi-route Splits and all Joins keep the
       full diamond shape rendered above. */
    .gateway-node.shape-pill {
      flex-direction: row;
      gap: 0.35rem;
      padding: 0.2rem 0.65rem;
      align-items: center;
      justify-content: center;
      border-style: solid;
      border-width: 1px;
      border-radius: 999px;
      background: #f5f3ff;
      box-shadow: 0 1px 3px rgba(124, 58, 237, 0.18);
      font-size: 0.75rem;
      font-weight: 600;
      color: #5b21b6;
    }
    .gateway-node.shape-pill .pill-trigger {
      white-space: nowrap;
    }
    .gateway-node.shape-pill .pill-condition {
      color: #6d28d9;
      font-weight: 700;
    }
    .gateway-node.shape-pill.selected {
      box-shadow: 0 0 0 3px rgba(29, 78, 216, 0.25), 0 1px 3px rgba(29, 78, 216, 0.2);
    }

    .stage-node {
      position: relative;
      display: flex;
      width: 100%;
      height: 100%;
      flex-direction: column;
      gap: 0.4rem;
      padding: 0.875rem 1rem 1rem;
      appearance: none;
      text-align: left;
      border: 2px solid #bfdbfe;
      border-radius: 18px;
      background: linear-gradient(180deg, #ffffff 0%, #eff6ff 100%);
      box-shadow: 0 10px 30px rgba(37, 99, 235, 0.08);
      cursor: pointer;
    }

    .stage-node.back-stage {
      border-color: #cbd5e1;
      background: linear-gradient(180deg, #ffffff 0%, #f8fafc 100%);
      box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08);
    }

    .stage-node.selected {
      border-color: #1d4ed8;
      box-shadow: 0 0 0 3px rgba(29, 78, 216, 0.18), 0 14px 32px rgba(29, 78, 216, 0.16);
    }

    .stage-node.simulation-path {
      border-color: #00703c;
      box-shadow: 0 0 0 3px rgba(0, 112, 60, 0.14), 0 14px 32px rgba(0, 112, 60, 0.12);
    }

    .stage-node.simulation-current {
      border-color: #0b0c0c;
      box-shadow: 0 0 0 4px rgba(255, 221, 0, 0.9), 0 0 0 7px rgba(11, 12, 12, 0.18);
    }

    .stage-node.drag-target {
      border-color: #0f766e;
      box-shadow: 0 0 0 4px rgba(15, 118, 110, 0.16);
    }

    .surface-tag {
      align-self: flex-start;
      padding: 0.125rem 0.5rem;
      border-radius: 999px;
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      background: rgba(29, 78, 216, 0.12);
      color: #1d4ed8;
    }

    .back-stage .surface-tag {
      background: rgba(71, 85, 105, 0.14);
      color: #334155;
    }

    .node-label {
      font-size: 1rem;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.3;
      overflow-wrap: anywhere;
    }

    .node-meta {
      font-size: 0.8125rem;
      color: #475569;
    }

    .node-action-summary {
      font-size: 0.75rem;
      color: #1e293b;
      line-height: 1.35;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .transition-handle {
      position: absolute;
      top: 50%;
      right: -14px;
      transform: translateY(-50%);
      width: 2rem;
      height: 2rem;
      border: 2px solid #1d4ed8;
      border-radius: 999px;
      background: #ffffff;
      color: #1d4ed8;
      font-size: 1rem;
      font-weight: 700;
      cursor: grab;
    }

    section[aria-label] {
      flex: 1;
      min-height: 0;
      padding: 1rem;
      overflow: auto;
    }

    .linear-workspace {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .linear-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
    }

    .hud-button.filter-active,
    .hud-button[aria-pressed='true'] {
      background: #dbeafe;
      border-color: #1d4ed8;
      color: #1d4ed8;
    }

    .linear-table-scroll {
      overflow: auto;
      border: 1px solid #dbe2ea;
      border-radius: 12px;
      background: #ffffff;
    }

    .stage-table {
      width: 100%;
      border-collapse: collapse;
      min-width: 980px;
    }

    .stage-table th,
    .stage-table td {
      padding: 0.75rem;
      vertical-align: top;
      border-bottom: 1px solid #e5e7eb;
      text-align: left;
    }

    .stage-table thead th {
      position: sticky;
      top: 0;
      z-index: 1;
      background: #f8fafc;
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      color: #475569;
    }

    .stage-table-row {
      background: #ffffff;
    }

    .stage-table-row.back-stage {
      background: #f8fafc;
    }

    .stage-table-row.selected {
      box-shadow: inset 4px 0 0 #1d4ed8;
      background: #eff6ff;
    }

    .stage-table-row.drag-over {
      background: #ecfeff;
    }

    .stage-table-row.dragging {
      opacity: 0.72;
    }

    .gateway-table-row {
      background: #faf5ff;
    }

    .gateway-inline-key {
      font-family: ui-monospace, SFMono-Regular, SFMono-Regular, Menlo, monospace;
      font-size: 0.8125rem;
      color: #5b21b6;
    }

    .gateway-badge-inline {
      background: rgba(124, 58, 237, 0.12);
      color: #6d28d9;
    }

    .row-trigger-cell {
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
      min-width: 8.5rem;
    }

    .row-trigger,
    .drag-handle,
    .row-action-button,
    .table-input,
    .table-select {
      font: inherit;
    }

    .row-trigger,
    .drag-handle,
    .row-action-button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 2.25rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #0f172a;
      cursor: pointer;
    }

    .row-trigger {
      flex: 1;
      justify-content: flex-start;
      padding: 0.375rem 0.625rem;
      font-weight: 600;
    }

    .drag-handle {
      width: 2.25rem;
      flex-shrink: 0;
      cursor: grab;
    }

    .row-action-button {
      padding: 0.375rem 0.625rem;
      white-space: nowrap;
    }

    .row-action-button:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }

    .row-action-button.danger {
      color: #b91c1c;
      border-color: #fecaca;
    }

    .table-input,
    .table-select {
      width: 100%;
      min-height: 2.5rem;
      padding: 0.5rem 0.625rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #0f172a;
    }

    .metric-pill {
      display: inline-flex;
      min-width: 2rem;
      align-items: center;
      justify-content: center;
      padding: 0.1875rem 0.5rem;
      border-radius: 999px;
      background: #e2e8f0;
      color: #334155;
      font-size: 0.75rem;
      font-weight: 700;
    }

    .stage-action-summary-cell {
      display: grid;
      gap: 0.375rem;
      align-content: start;
    }

    .stage-action-summary-list {
      margin: 0;
      padding-left: 1rem;
      color: #1e293b;
      font-size: 0.75rem;
      line-height: 1.4;
      display: grid;
      gap: 0.25rem;
    }

    .transition-summary {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .transition-link {
      border: none;
      padding: 0;
      background: transparent;
      color: #1d4ed8;
      font: inherit;
      text-align: left;
      text-decoration: underline;
      cursor: pointer;
      min-width: 12rem;
    }

    .transition-list {
      margin: 0;
      padding-left: 1rem;
      color: #334155;
      font-size: 0.8125rem;
    }

    .transition-empty {
      font-size: 0.8125rem;
      color: #334155;
    }

    .row-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.375rem;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      padding: 0.125rem 0.5rem;
      border-radius: 999px;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      background: rgba(29, 78, 216, 0.12);
      color: #1d4ed8;
    }

    .back-stage .badge {
      background: rgba(71, 85, 105, 0.14);
      color: #334155;
    }

    .exit-tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      min-height: 2rem;
      padding: 0.25rem 0.625rem;
      border: 1px solid #cbd5e1;
      border-radius: 999px;
      background: #ffffff;
      color: #334155;
      cursor: pointer;
    }

    .exit-tag.selected {
      border-color: #1d4ed8;
      background: #dbeafe;
      color: #1d4ed8;
    }

    .context-menu {
      position: absolute;
      z-index: 20;
      min-width: 14rem;
      padding: 0.375rem;
      border: 1px solid #cbd5e1;
      border-radius: 12px;
      background: #ffffff;
      box-shadow: 0 18px 40px rgba(15, 23, 42, 0.18);
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
    }

    .context-menu button {
      display: flex;
      align-items: center;
      width: 100%;
      min-height: 2.5rem;
      padding: 0.5rem 0.75rem;
      border: none;
      border-radius: 8px;
      background: transparent;
      color: #0f172a;
      text-align: left;
      cursor: pointer;
    }

    .context-menu button:hover {
      background: #eff6ff;
    }

    .context-menu button.danger {
      color: #b91c1c;
    }

    .context-menu button.danger:hover {
      background: #fee2e2;
    }

    .workspace-empty-state {
      margin: 0.5rem 0 0;
      padding: 1.25rem;
      border: 1px solid #dbe2ea;
      border-radius: 16px;
      background: #ffffff;
      display: grid;
      gap: 0.875rem;
    }

    .workspace-empty-title {
      margin: 0;
      color: #0f172a;
      font-size: 1rem;
      line-height: 1.3;
    }

    .workspace-empty-copy {
      margin: 0;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .workspace-empty-tips {
      margin: 0;
      padding-left: 1.125rem;
      color: #334155;
      display: grid;
      gap: 0.375rem;
      font-size: 0.875rem;
    }

    .workspace-empty-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }

    .dialog-backdrop {
      position: fixed;
      inset: 0;
      z-index: 30;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1.5rem;
      background: rgba(15, 23, 42, 0.48);
    }

    .dialog-panel {
      width: min(32rem, 100%);
      max-height: calc(100% - 3rem);
      overflow: auto;
      padding: 1.25rem;
      border-radius: 16px;
      background: #ffffff;
      box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
      display: grid;
      gap: 1rem;
    }

    .dialog-panel-danger {
      width: min(34rem, 100%);
    }

    .dialog-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 0.75rem;
    }

    .dialog-eyebrow {
      margin: 0 0 0.25rem;
      color: #1d4ed8;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .dialog-eyebrow.danger {
      color: #b91c1c;
    }

    .dialog-title {
      margin: 0;
      color: #0f172a;
      font-size: 1.25rem;
      line-height: 1.3;
    }

    .dialog-copy {
      margin: 0;
      color: #475569;
      font-size: 0.9375rem;
      line-height: 1.5;
    }

    .dialog-error {
      margin: 0;
      padding: 0.75rem 0.875rem;
      border-radius: 12px;
      background: #fff1f2;
      color: #b91c1c;
      font-weight: 600;
    }

    .dialog-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.875rem;
    }

    .dialog-field {
      display: grid;
      gap: 0.375rem;
      min-width: 0;
    }

    .dialog-field-disabled {
      opacity: 0.72;
    }

    .dialog-label {
      color: #334155;
      font-size: 0.8125rem;
      font-weight: 700;
    }

    .dialog-control {
      width: 100%;
      min-height: 2.625rem;
      padding: 0.625rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #0f172a;
      font: inherit;
      box-sizing: border-box;
    }

    .dialog-control:focus-visible,
    .dialog-button:focus-visible {
      outline: 3px solid #1d4ed8;
      outline-offset: 2px;
    }

    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .dialog-button {
      min-height: 2.5rem;
      padding: 0.625rem 0.875rem;
      border-radius: 10px;
      border: 1px solid #cbd5e1;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }

    .dialog-button.secondary {
      background: #ffffff;
      color: #0f172a;
    }

    .dialog-button.primary {
      background: #1d4ed8;
      border-color: #1d4ed8;
      color: #ffffff;
    }

    .dialog-button.danger {
      background: #b91c1c;
      border-color: #b91c1c;
      color: #ffffff;
    }

    .delete-impact {
      padding: 0.875rem;
      border-radius: 12px;
      background: #fff7ed;
      color: #9a3412;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .delete-impact p,
    .delete-impact ul {
      margin: 0;
    }

    .delete-impact ul {
      margin-top: 0.625rem;
      padding-left: 1rem;
      display: grid;
      gap: 0.375rem;
    }

    @media (max-width: 900px) {
      .graph-hud,
      .toolbar,
      .linear-toolbar,
      .dialog-actions {
        flex-direction: column;
        align-items: stretch;
      }

      .hud-group {
        justify-content: space-between;
      }

      .dialog-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .mode-toggle,
      .hud-button,
      .stage-node,
      .row-trigger,
      .row-action-button,
      .edge-chip,
      .exit-tag {
        transition: none;
      }

      .graph-canvas {
        scroll-behavior: auto;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-graph': PrismWorkflowGraphElement;
  }
}
