import { LitElement, css, html, nothing, svg } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import type {
  AuthoredGateway,
  AuthoredStage,
  AuthoredTransition,
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
  workflowLaneOptions,
} from './workflow-stage-assignment.js';
import {
  describeTransitionCondition,
} from './gateway-route-conditions.js';
import { workflowDeadEndStages, workflowUnreachableStages } from './workflow-validation.js';
import {
  deriveGatewayBindings,
  gatewayLaneKey,
  type GatewayBinding,
} from './workflow-gateway-representation.js';

export type GraphMode = 'graph' | 'linear';
type LinearFilter = '__all__' | string;
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

type StageLayout = {
  stage: AuthoredStage;
  stageIndex: number;
  surface: StageSurface;
  laneKey: string;
  laneLabel: string;
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
  x: number;
  y: number;
  width: number;
  height: number;
};

type TransitionLayout = {
  transition: AuthoredTransition;
  index: number;
  path: string;
  labelX: number;
  labelY: number;
  visualFromKey: string;
  visualToKey: string;
  branch: boolean;
  merge: boolean;
};

type WorkspaceLayout = {
  bounds: { width: number; height: number };
  roleLanes: RoleLane[];
  stageLayouts: StageLayout[];
  gatewayLayouts: GatewayLayout[];
  transitionLayouts: TransitionLayout[];
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
  affectedTransitions: AuthoredTransition[];
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
const VERTICAL_GAP = 96;
const TOP_PADDING = 64;
const SIDE_PADDING = 56;
const LANE_WIDTH = 280;
const LANE_GAP = 36;
const EDGE_LABEL_WIDTH = 132;
const EDGE_LABEL_HEIGHT = 32;
const GATEWAY_SIZE = 104;
const GATEWAY_OFFSET = 28;
const ZOOM_MIN = 0.65;
const ZOOM_MAX = 1.5;
const LANE_HEADER_OFFSET = 80;
const ALL_LANES_FILTER: LinearFilter = '__all__';

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

  @property({ type: String })
  mode: GraphMode = 'graph';

  @property({ type: Boolean, attribute: 'allow-linear-mode' })
  allowLinearMode = true;

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
  private _linearFilter: LinearFilter = ALL_LANES_FILTER;

  @state()
  private _draggedLinearStageKey: string | null = null;

  @state()
  private _dragOverLinearStageKey: string | null = null;

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
    if (changed.has('selectedStageKey')) {
      this._selectedStageKey = this.selectedStageKey ?? null;
    }

    if (changed.has('selectedTransitionIndex')) {
      this._selectedTransitionIndex = this.selectedTransitionIndex ?? null;
    }

    if (changed.has('selectedGatewayKey')) {
      this._selectedGatewayKey = this.selectedGatewayKey ?? null;
    }

    const stages = this.workflow?.stages ?? [];
    const transitions = this.workflow?.transitions ?? [];
    const gateways = this.workflow?.gateways ?? [];
    const focusableStages = this.mode === 'linear'
      ? this._visibleLinearStages(stages)
      : stages;

    if (this._selectedStageKey && !stages.some(stage => stage.stageKey === this._selectedStageKey)) {
      this._selectedStageKey = null;
    }

    if (
      this._selectedTransitionIndex !== null
      && (this._selectedTransitionIndex < 0 || this._selectedTransitionIndex >= transitions.length)
    ) {
      this._selectedTransitionIndex = null;
    }

    if (this._selectedGatewayKey && !gateways.some(gateway => gateway.gatewayKey === this._selectedGatewayKey)) {
      this._selectedGatewayKey = null;
    }

    if (focusableStages.length === 0) {
      this._focusedIndex = 0;
    } else if (this._focusedIndex >= focusableStages.length) {
      this._focusedIndex = focusableStages.length - 1;
    }
  }

  private get _layout(): WorkspaceLayout {
    const stages = this.workflow?.stages ?? [];
    const transitions = this.workflow?.transitions ?? [];
    const gateways = this.workflow?.gateways ?? [];
    const roleLanes: RoleLane[] = [];
    const laneByKey = new Map<string, RoleLane>();
    const stageLayouts: StageLayout[] = [];
    const gatewayLayouts: GatewayLayout[] = [];

    // Group stages by lane and track stage indices per lane
    const stagesPerLane = new Map<string, Array<{ stage: AuthoredStage; globalIndex: number }>>();

    const ensureLane = (laneKey: string, surface: StageSurface) => {
      let lane = laneByKey.get(laneKey);
      if (!lane) {
        lane = {
          key: laneKey,
          label: this._roleLabelForLane(laneKey),
          description: this._roleDescriptionForLane(laneKey),
          surface,
          columnIndex: roleLanes.length,
          x: SIDE_PADDING + roleLanes.length * (LANE_WIDTH + LANE_GAP),
          width: LANE_WIDTH,
          stageCount: 0,
        };
        laneByKey.set(laneKey, lane);
        roleLanes.push(lane);
      }

      if (!stagesPerLane.has(laneKey)) {
        stagesPerLane.set(laneKey, []);
      }

      return lane;
    };

    stages.forEach((stage, stageIndex) => {
      const surface = this._surfaceForStage(stage);
      const laneKey = this._roleKeyForStage(stage, surface);
      const lane = ensureLane(laneKey, surface);
      lane.stageCount += 1;
      stagesPerLane.get(laneKey)!.push({ stage, globalIndex: stageIndex });
    });

    gateways.forEach(gateway => {
      const surface = this._surfaceForGateway(gateway);
      ensureLane(this._laneKeyForGateway(gateway), surface);
    });

    // Position stages vertically within each lane
    stagesPerLane.forEach((stageList, laneKey) => {
      const lane = laneByKey.get(laneKey)!;
      stageList.forEach((item, indexInLane) => {
        const x = lane.x + (lane.width - NODE_WIDTH) / 2;
        const y = TOP_PADDING + LANE_HEADER_OFFSET + indexInLane * (NODE_HEIGHT + VERTICAL_GAP);

        const layout: StageLayout = {
          stage: item.stage,
          stageIndex: item.globalIndex,
          surface: lane.surface,
          laneKey,
          laneLabel: lane.label,
          x,
          y,
          width: NODE_WIDTH,
          height: NODE_HEIGHT,
        };

        stageLayouts.push(layout);
      });
    });

    const stageMap = new Map(stageLayouts.map(layout => [layout.stage.stageKey, layout]));
    const gatewayBindings = this.workflow ? deriveGatewayBindings(this.workflow) : [];
    const usedFallbackSlotsByLane = new Map<string, number>();

    gatewayBindings.forEach(binding => {
      const lane = laneByKey.get(binding.laneKey);
      if (!lane) {
        return;
      }

      const anchorStage = binding.anchorStageKey ? stageMap.get(binding.anchorStageKey) ?? null : null;
      const fallbackIndex = usedFallbackSlotsByLane.get(binding.laneKey) ?? 0;
      const x = lane.x + (lane.width - GATEWAY_SIZE) / 2;
      let y = TOP_PADDING + LANE_HEADER_OFFSET + fallbackIndex * (GATEWAY_SIZE + GATEWAY_OFFSET);

      if (anchorStage) {
        y = binding.gateway.kind === 'Split'
          ? anchorStage.y + anchorStage.height + GATEWAY_OFFSET
          : Math.max(TOP_PADDING + LANE_HEADER_OFFSET, anchorStage.y - GATEWAY_SIZE - GATEWAY_OFFSET);
      } else {
        usedFallbackSlotsByLane.set(binding.laneKey, fallbackIndex + 1);
      }

      gatewayLayouts.push({
        gateway: binding.gateway,
        binding,
        surface: this._surfaceForGateway(binding.gateway),
        laneKey: binding.laneKey,
        laneLabel: this._roleLabelForLane(binding.laneKey),
        x,
        y,
        width: GATEWAY_SIZE,
        height: GATEWAY_SIZE,
      });
    });

    const width = roleLanes.length === 0
      ? SIDE_PADDING * 2 + LANE_WIDTH
      : SIDE_PADDING * 2 + roleLanes.length * LANE_WIDTH + Math.max(0, roleLanes.length - 1) * LANE_GAP;
    const contentBottom = Math.max(
      TOP_PADDING + LANE_HEADER_OFFSET + 200,
      ...stageLayouts.map(layout => layout.y + layout.height),
      ...gatewayLayouts.map(layout => layout.y + layout.height)
    );
    const height = contentBottom + TOP_PADDING;

    const splitGatewayByAnchorStage = new Map<string, GatewayLayout>();
    const joinGatewayByAnchorStage = new Map<string, GatewayLayout>();
    gatewayLayouts.forEach(layout => {
      if (layout.binding.anchorStageKey) {
        if (layout.gateway.kind === 'Split') {
          splitGatewayByAnchorStage.set(layout.binding.anchorStageKey, layout);
        } else {
          joinGatewayByAnchorStage.set(layout.binding.anchorStageKey, layout);
        }
      }
    });

    const gatewayLayoutByKey = new Map(gatewayLayouts.map(gl => [gl.gateway.gatewayKey, gl]));

    const transitionLayouts: TransitionLayout[] = transitions.map((transition, index) => {
      const source = stageMap.get(transition.fromStage);
      const target = stageMap.get(transition.toStage);

      if (!source || !target) {
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
        };
      }

      // Explicit gateway routing: author has set fromGateway/toGateway directly.
      const explicitFromGateway = transition.fromGateway ? gatewayLayoutByKey.get(transition.fromGateway) ?? null : null;
      const explicitToGateway = transition.toGateway ? gatewayLayoutByKey.get(transition.toGateway) ?? null : null;

      // Heuristic routing: derive from anchor-stage topology when no explicit gateway is set.
      const sourceGateway = explicitFromGateway ?? (splitGatewayByAnchorStage.get(transition.fromStage) ?? null);
      const targetGateway = explicitToGateway ?? (joinGatewayByAnchorStage.get(transition.toStage) ?? null);
      const viaPoints = [
        ...(sourceGateway && sourceGateway.gateway.kind === 'Split' ? [this._gatewayPoint(sourceGateway)] : []),
        ...(targetGateway && targetGateway.gateway.kind === 'Join' ? [this._gatewayPoint(targetGateway)] : []),
      ];
      const { path, labelX, labelY } = this._buildTransitionPath(source, target, viaPoints);
      return {
        transition,
        index,
        path,
        labelX,
        labelY,
        visualFromKey: sourceGateway?.gateway.gatewayKey ?? transition.fromStage,
        visualToKey: targetGateway?.gateway.gatewayKey ?? transition.toStage,
        branch: Boolean(sourceGateway?.gateway.kind === 'Split'),
        merge: Boolean(targetGateway?.gateway.kind === 'Join'),
      };
    });

    return {
      bounds: { width, height },
      roleLanes,
      stageLayouts,
      gatewayLayouts,
      transitionLayouts,
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
    return stageLaneLabel(this.workflow, laneKey);
  }

  private _roleDescriptionForLane(laneKey: string) {
    return stageLaneDescription(this.workflow, laneKey);
  }

  private _availableLaneKeys() {
    return workflowLaneOptions(this.workflow);
  }

  private _gatewayPoint(gatewayLayout: GatewayLayout) {
    return {
      x: gatewayLayout.x + gatewayLayout.width / 2,
      y: gatewayLayout.y + gatewayLayout.height / 2,
    };
  }

  private _buildTransitionPath(
    source: StageLayout,
    target: StageLayout,
    viaPoints: Array<{ x: number; y: number }> = []
  ) {
    const sameLane = source.laneKey === target.laneKey;
    const startX = source.x + source.width / 2;
    const startY = source.y + source.height;
    const endX = target.x + target.width / 2;
    const endY = target.y;
    const points = [{ x: startX, y: startY }, ...viaPoints, { x: endX, y: endY }];
    let path = `M ${points[0].x} ${points[0].y}`;

    for (let index = 1; index < points.length; index += 1) {
      const previous = points[index - 1];
      const current = points[index];
      const verticalDirection = current.y >= previous.y ? 1 : -1;
      const distance = Math.max(Math.abs(current.y - previous.y), Math.abs(current.x - previous.x), 64);
      const curve = Math.min(Math.max(distance / 2, 56), 180);
      path += ` C ${previous.x} ${previous.y + curve * verticalDirection}, ${current.x} ${current.y - curve * verticalDirection}, ${current.x} ${current.y}`;
    }

    const labelBias = sameLane && viaPoints.length === 0 ? 22 : 0;

    return {
      path,
      labelX: startX + (endX - startX) / 2 + labelBias,
      labelY: startY + (endY - startY) / 2,
    };
  }

  private _toggleMode() {
    if (!this.allowLinearMode) {
      return;
    }
    this.mode = this.mode === 'graph' ? 'linear' : 'graph';
    this._focusedIndex = 0;
    this._dismissContextMenu();
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
    const gateway = this.workflow?.gateways?.find(candidate => candidate.gatewayKey === gatewayKey);
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
    this._announce(`Gateway “${gateway.displayName}” selected. ${gateway.kind} gateway in ${this._roleLabelForLane(this._laneKeyForGateway(gateway))} lane.`);

    if (options?.openInspector) {
      this._requestInspector({ kind: 'gateway', gatewayKey });
    }
  }

  private _selectTransition(index: number, options?: { openInspector?: boolean }) {
    const transition = this.workflow?.transitions[index];
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
    return this.workflow?.stages.find(stage => stage.stageKey === stageKey)?.displayName ?? stageKey;
  }

  private _transitionDescriptor(transition: AuthoredTransition) {
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

  private _visibleLinearStages(stages: AuthoredStage[] = this.workflow?.stages ?? []) {
    return stages.filter(stage =>
      this._linearFilter === ALL_LANES_FILTER || stageLaneKey(stage) === this._linearFilter
    );
  }

  private _visibleLinearGateways(gateways: AuthoredGateway[] = this.workflow?.gateways ?? []) {
    return gateways.filter(gateway =>
      this._linearFilter === ALL_LANES_FILTER || this._laneKeyForGateway(gateway) === this._linearFilter
    );
  }

  private _outgoingTransitionsForStage(stageKey: string) {
    return (this.workflow?.transitions ?? [])
      .map((transition, index) => ({ transition, index }))
      .filter(entry => entry.transition.fromStage === stageKey);
  }

  private _actionCountForStage(stage: AuthoredStage) {
    return (stage.actions ?? []).length;
  }

  private _actionSummariesForStage(stage: AuthoredStage) {
    return (stage.actions ?? [])
      .map(action => action.summary?.trim() || action.type)
      .filter(Boolean)
      .slice(0, 2);
  }

  private _focusLinearRow(index: number) {
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelectorAll<HTMLElement>('[data-prism-list-row-trigger]')
        ?.[index]
        ?.focus();
    });
  }

  private _setLinearFilter(filter: LinearFilter) {
    this._linearFilter = filter;
    this._focusedIndex = 0;
    this._announce(
      filter === ALL_LANES_FILTER
        ? 'Showing all stages in the list workspace.'
        : `Showing ${this._roleLabelForLane(filter)} lane only.`
    );
    this._focusLinearRow(0);
  }

  private _commitStageField(
    stageKey: string,
    field: 'stageKey' | 'displayName' | 'lane' | 'kind',
    value: string
  ) {
    if (!this.workflow) {
      return;
    }

    const stageIndex = this.workflow.stages.findIndex(stage => stage.stageKey === stageKey);
    if (stageIndex < 0) {
      return;
    }

    const currentStage = this.workflow.stages[stageIndex];
    const currentValue = field === 'lane'
      ? stageLaneKey(currentStage)
      : String(currentStage[field] ?? '');
    if (value === currentValue) {
      return;
    }

    const nextStages = [...this.workflow.stages];
    let nextTransitions = [...this.workflow.transitions];
    let nextInitialStageKey = this.workflow.initialStageKey;
    let nextSelectedStageKey = this._selectedStageKey;
    let nextDraggedStageKey = this._draggedLinearStageKey;
    let nextDragOverStageKey = this._dragOverLinearStageKey;
    let announcement = '';

    if (field === 'stageKey') {
      const trimmed = value.trim();
      if (!trimmed) {
        this._announce('Stage key cannot be empty.');
        this.requestUpdate();
        return;
      }

      if (
        trimmed !== currentStage.stageKey
        && this.workflow.stages.some(stage => stage.stageKey === trimmed)
      ) {
        this._announce(`Stage key ${trimmed} is already in use.`);
        this.requestUpdate();
        return;
      }

      nextStages[stageIndex] = { ...currentStage, stageKey: trimmed };
      nextTransitions = nextTransitions.map(transition => ({
        ...transition,
        fromStage: transition.fromStage === currentStage.stageKey ? trimmed : transition.fromStage,
        toStage: transition.toStage === currentStage.stageKey ? trimmed : transition.toStage,
      }));
      nextInitialStageKey = this.workflow.initialStageKey === currentStage.stageKey
        ? trimmed
        : this.workflow.initialStageKey;
      nextSelectedStageKey = this._selectedStageKey === currentStage.stageKey
        ? trimmed
        : this._selectedStageKey;
      nextDraggedStageKey = this._draggedLinearStageKey === currentStage.stageKey
        ? trimmed
        : this._draggedLinearStageKey;
      nextDragOverStageKey = this._dragOverLinearStageKey === currentStage.stageKey
        ? trimmed
        : this._dragOverLinearStageKey;
      announcement = `Stage key updated to ${trimmed}.`;
    } else if (field === 'lane') {
      nextStages[stageIndex] = applyLaneToStage(currentStage, value);
      announcement = `${currentStage.displayName} lane updated.`;
    } else if (field === 'kind') {
      nextStages[stageIndex] = {
        ...currentStage,
        kind: value as AuthoredStage['kind'],
      };
      announcement = `${currentStage.displayName} type updated to ${value}.`;
    } else {
      nextStages[stageIndex] = {
        ...currentStage,
        displayName: value.trim() || currentStage.displayName,
      };
      announcement = `${nextStages[stageIndex].displayName} title updated.`;
    }

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      initialStageKey: nextInitialStageKey,
      stages: nextStages,
      transitions: nextTransitions,
    };

    const selectedStageKey = nextSelectedStageKey ?? nextStages[stageIndex].stageKey;
    this._selectedStageKey = selectedStageKey;
    this._selectedTransitionIndex = null;
    this._draggedLinearStageKey = nextDraggedStageKey;
    this._dragOverLinearStageKey = nextDragOverStageKey;
    this._emitSelectionChange({ kind: 'stage', stageKey: selectedStageKey });
    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: selectedStageKey });
    this._announce(announcement);
  }

  private _makeUniqueStageKey(base: string) {
    const usedKeys = new Set(this.workflow?.stages.map(stage => stage.stageKey) ?? []);
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
      ? this.workflow?.stages.find(stage => stage.stageKey === referenceStageKey) ?? null
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
      stageKey: '',
      displayName: '',
      kind: 'Question',
      roleGates: [],
      actions: [],
      fields: [],
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

    if (this.workflow.stages.some(stage => stage.stageKey === stageKey)) {
      this._createStageDialog = { ...this._createStageDialog, error: 'Stage key must be unique.' };
      return;
    }

    const newStage = applyLaneToStage({
      stageKey,
      displayName: title,
      description: undefined,
      kind: editorStageTypeToStageKind(dialog.stageType),
      actions: [],
      fields: [],
      roleGates: [],
      editorComment: 'Created from the graph workspace.',
    }, dialog.laneKey);

    const stages = [...this.workflow.stages];
    let insertIndex = stages.length;
    if (dialog.referenceStageKey) {
      const referenceIndex = stages.findIndex(stage => stage.stageKey === dialog.referenceStageKey);
      if (referenceIndex >= 0) {
        insertIndex = dialog.position === 'before' ? referenceIndex : referenceIndex + 1;
      }
    }
    stages.splice(insertIndex, 0, newStage);

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      initialStageKey: this.workflow.initialStageKey || newStage.stageKey,
      stages,
    };

    this._selectedStageKey = newStage.stageKey;
    this._selectedTransitionIndex = null;
    this._emitSelectionChange({ kind: 'stage', stageKey: newStage.stageKey });
    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: newStage.stageKey });
    this._requestInspector({ kind: 'stage', stageKey: newStage.stageKey });
    this._announce(`${newStage.displayName} added to the workspace.`);
    this._closeCreateStageDialog();
  }

  private _openCreateGatewayDialog(returnTarget?: HTMLElement | null) {
    if (!this.workflow) {
      return;
    }
    this._dialogReturnTarget = returnTarget ?? null;
    const defaultLane = workflowLaneOptions(this.workflow)[0] ?? 'public';
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
      ...this.workflow.stages.map(s => s.stageKey),
      ...(this.workflow.gateways ?? []).map(g => g.gatewayKey),
    ];
    if (usedKeys.includes(key)) {
      this._createGatewayDialog = { ...dialog, error: 'Gateway key must be unique across all stages and gateways.' };
      return;
    }

    const newGateway: AuthoredGateway = {
      gatewayKey: key,
      displayName: title,
      kind: dialog.kind,
      laneKey: dialog.laneKey,
      actor: dialog.laneKey,
      roleGates: [],
    };

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      gateways: [...(this.workflow.gateways ?? []), newGateway],
    };

    this._emitWorkflowUpdated(workflow, { kind: 'gateway', gatewayKey: newGateway.gatewayKey });
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
      affectedTransitions: this.workflow.transitions.filter(
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
    const stages = this.workflow.stages.filter(stage => stage.stageKey !== stageKey);
    const transitions = this.workflow.transitions.filter(
      transition => transition.fromStage !== stageKey && transition.toStage !== stageKey
    );

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      stages,
      transitions,
      initialStageKey:
        this.workflow.initialStageKey === stageKey
          ? stages[0]?.stageKey ?? ''
          : this.workflow.initialStageKey,
    };

    this._selectedStageKey = null;
    this._selectedTransitionIndex = null;
    this._draggedLinearStageKey = null;
    this._dragOverLinearStageKey = null;
    this._emitWorkflowUpdated(workflow, null);
    this._announce(
      `${deletedLabel} deleted.${transitionCount > 0 ? ` ${transitionCount} affected transition${transitionCount === 1 ? '' : 's'} removed.` : ''}`
    );
    this._closeDeleteStageDialog();
  }

  private async _copyStage(stageKey: string) {
    const stage = this.workflow?.stages.find(candidate => candidate.stageKey === stageKey);
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
    const transition = this.workflow?.transitions[index];
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

  private _moveStage(stageKey: string, delta: -1 | 1) {
    if (!this.workflow) {
      return;
    }

    const stages = [...this.workflow.stages];
    const currentIndex = stages.findIndex(stage => stage.stageKey === stageKey);
    if (currentIndex < 0) {
      return;
    }

    const nextIndex = Math.min(stages.length - 1, Math.max(0, currentIndex + delta));
    if (nextIndex === currentIndex) {
      this._announce(delta < 0 ? 'Stage is already first.' : 'Stage is already last.');
      return;
    }

    const [movedStage] = stages.splice(currentIndex, 1);
    stages.splice(nextIndex, 0, movedStage);

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      stages,
    };

    this._selectedStageKey = movedStage.stageKey;
    this._selectedTransitionIndex = null;
    this._emitSelectionChange({ kind: 'stage', stageKey: movedStage.stageKey });
    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: movedStage.stageKey });

    const visibleIndex = this._visibleLinearStages(stages).findIndex(
      stage => stage.stageKey === movedStage.stageKey
    );
    this._focusedIndex = Math.max(visibleIndex, 0);
    this._announce(`${movedStage.displayName} moved to position ${nextIndex + 1}.`);
    this._focusLinearRow(this._focusedIndex);
  }

  private _reorderStageBefore(stageKey: string, beforeStageKey: string) {
    if (!this.workflow || stageKey === beforeStageKey) {
      this._draggedLinearStageKey = null;
      this._dragOverLinearStageKey = null;
      return;
    }

    const stages = [...this.workflow.stages];
    const fromIndex = stages.findIndex(stage => stage.stageKey === stageKey);
    const targetIndex = stages.findIndex(stage => stage.stageKey === beforeStageKey);
    if (fromIndex < 0 || targetIndex < 0) {
      this._draggedLinearStageKey = null;
      this._dragOverLinearStageKey = null;
      return;
    }

    const [movedStage] = stages.splice(fromIndex, 1);
    const insertIndex = fromIndex < targetIndex ? targetIndex - 1 : targetIndex;
    stages.splice(insertIndex, 0, movedStage);

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      stages,
    };

    this._selectedStageKey = movedStage.stageKey;
    this._selectedTransitionIndex = null;
    this._draggedLinearStageKey = null;
    this._dragOverLinearStageKey = null;
    this._emitSelectionChange({ kind: 'stage', stageKey: movedStage.stageKey });
    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: movedStage.stageKey });

    const visibleIndex = this._visibleLinearStages(stages).findIndex(
      stage => stage.stageKey === movedStage.stageKey
    );
    this._focusedIndex = Math.max(visibleIndex, 0);
    this._announce(`${movedStage.displayName} reordered before ${this._labelForStage(beforeStageKey)}.`);
    this._focusLinearRow(this._focusedIndex);
  }

  private _deleteTransition(index: number) {
    if (!this.workflow) {
      return;
    }

    const transition = this.workflow.transitions[index];
    if (!transition) {
      return;
    }

    const transitions = this.workflow.transitions.filter((_, transitionIndex) => transitionIndex !== index);
    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      transitions,
    };

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
        ? this.workflow?.stages.find(stage => stage.stageKey === referenceStageKey) ?? null
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
        this._selectStage(node.stage.stageKey, { focusIndex: node.index });
      } else {
        this._selectGateway(node.gateway.gatewayKey);
      }
      return;
    }

    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      if (node.kind === 'stage') {
        this._selectStage(node.stage.stageKey, { openInspector: true, focusIndex: node.index });
      } else {
        this._selectGateway(node.gateway.gatewayKey, { openInspector: true });
      }
      return;
    }

    if (node.kind === 'stage' && (event.key === 'Delete' || event.key === 'Backspace')) {
      event.preventDefault();
      this._openDeleteStageDialog(node.stage.stageKey, event.currentTarget as HTMLElement);
      return;
    }

    if (node.kind === 'stage' && (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10'))) {
      event.preventDefault();
      this._openContextMenuFromKeyboard({ kind: 'stage', stageKey: node.stage.stageKey }, event.currentTarget as HTMLElement);
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

  private _handleListKeydown(event: KeyboardEvent, stageKey: string, index: number) {
    const stages = this._visibleLinearStages();
    if (stages.length === 0) {
      return;
    }

    let nextIndex = index;
    if (event.altKey && event.key === 'ArrowUp') {
      event.preventDefault();
      this._moveStage(stageKey, -1);
      return;
    } else if (event.altKey && event.key === 'ArrowDown') {
      event.preventDefault();
      this._moveStage(stageKey, 1);
      return;
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      nextIndex = Math.min(index + 1, stages.length - 1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      nextIndex = Math.max(index - 1, 0);
    } else if (event.key === 'Home') {
      event.preventDefault();
      nextIndex = 0;
    } else if (event.key === 'End') {
      event.preventDefault();
      nextIndex = stages.length - 1;
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this._selectStage(stageKey, { openInspector: true, focusIndex: index });
      return;
    } else if (event.key === ' ') {
      event.preventDefault();
      this._selectStage(stageKey, { focusIndex: index });
      return;
    } else if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      this._selectStage(stageKey, { openInspector: true, focusIndex: index });
      return;
    } else if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this._openDeleteStageDialog(stageKey, event.currentTarget as HTMLElement);
      return;
    } else if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
      event.preventDefault();
      this._openContextMenuFromKeyboard(
        { kind: 'stage', stageKey },
        event.currentTarget as HTMLElement
      );
      return;
    } else {
      return;
    }

    this._focusedIndex = nextIndex;
    this._focusLinearRow(nextIndex);
  }

  private _handleLinearRowClick(event: MouseEvent, stageKey: string, index: number) {
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, input, select, textarea')) {
      return;
    }

    this._selectStage(stageKey, { openInspector: true, focusIndex: index });
  }

  private _handleInlineEditorCommit(
    event: Event,
    stageKey: string,
    field: 'stageKey' | 'displayName' | 'lane' | 'kind'
  ) {
    const value = (event.currentTarget as HTMLInputElement | HTMLSelectElement).value;
    this._commitStageField(stageKey, field, value);
  }

  private _handleInlineEditorKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
      (event.currentTarget as HTMLElement).blur();
    }
  }

  private _handleLinearDragStart(event: DragEvent, stageKey: string) {
    this._draggedLinearStageKey = stageKey;
    this._dragOverLinearStageKey = null;
    event.dataTransfer?.setData('text/plain', stageKey);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
    this._announce(`Dragging ${this._labelForStage(stageKey)}.`);
  }

  private _handleLinearDragOver(event: DragEvent, stageKey: string) {
    if (!this._draggedLinearStageKey || this._draggedLinearStageKey === stageKey) {
      return;
    }

    event.preventDefault();
    this._dragOverLinearStageKey = stageKey;
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  private _handleLinearDrop(event: DragEvent, stageKey: string) {
    event.preventDefault();
    const draggedStageKey = this._draggedLinearStageKey
      ?? event.dataTransfer?.getData('text/plain')
      ?? null;
    if (!draggedStageKey) {
      return;
    }

    this._reorderStageBefore(draggedStageKey, stageKey);
  }

  private _handleLinearDragEnd() {
    this._draggedLinearStageKey = null;
    this._dragOverLinearStageKey = null;
  }

  private _jumpToValidationStage(stageKey: string) {
    const stage = this.workflow?.stages.find(candidate => candidate.stageKey === stageKey);
    if (!stage) {
      return;
    }

    if (this.mode === 'linear' && this._linearFilter !== ALL_LANES_FILTER && stageLaneKey(stage) !== this._linearFilter) {
      this._linearFilter = ALL_LANES_FILTER;
    }

    const visibleStages = this.mode === 'linear'
      ? this._visibleLinearStages(this.workflow?.stages ?? [])
      : (this.workflow?.stages ?? []);
    const focusIndex = Math.max(visibleStages.findIndex(candidate => candidate.stageKey === stageKey), 0);
    this._selectStage(stageKey, { openInspector: true, focusIndex });
  }

  private _renderValidationSummary() {
    if (!this.workflow) {
      return nothing;
    }

    const unreachableStages = workflowUnreachableStages(this.workflow);
    const deadEndStages = workflowDeadEndStages(this.workflow);
    if (unreachableStages.length === 0 && deadEndStages.length === 0) {
      return nothing;
    }

    return html`
      <section class="validation-banner" aria-labelledby="workspace-validation-heading">
        <div class="validation-banner-header">
          <h2 id="workspace-validation-heading" class="validation-banner-title">Routing warnings</h2>
          <span class="validation-banner-meta">${unreachableStages.length + deadEndStages.length}</span>
        </div>
        <ul class="validation-banner-list">
          ${unreachableStages.map(stage => html`
            <li>
              <button
                type="button"
                class="validation-link"
                data-prism-validation-unreachable=${stage.stageKey}
                @click=${() => this._jumpToValidationStage(stage.stageKey)}
              >
                ${stage.displayName} is unreachable from the workflow start.
              </button>
            </li>
          `)}
          ${deadEndStages.map(stage => html`
            <li>
              <button
                type="button"
                class="validation-link"
                data-prism-validation-dead-end=${stage.stageKey}
                @click=${() => this._jumpToValidationStage(stage.stageKey)}
              >
                ${stage.displayName} has no outbound transition.
              </button>
            </li>
          `)}
        </ul>
      </section>
    `;
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
            Name the stage, choose its key, lane owner, and type, then continue editing in the inspector.
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
              <span class="dialog-label">Lane owner</span>
              <input
                class="dialog-control"
                data-prism-create-stage-lane
                .value=${dialog.laneKey}
                list="create-stage-lane-options"
                placeholder="planning-officer"
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
              <span class="dialog-label">Lane owner</span>
              <input
                class="dialog-control"
                data-prism-create-gateway-lane
                .value=${dialog.laneKey}
                list="create-gateway-lane-options"
                placeholder="public"
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
    const { bounds, roleLanes, stageLayouts, gatewayLayouts, transitionLayouts } = this._layout;
    const isEmpty = stageLayouts.length === 0 && gatewayLayouts.length === 0;
    const dragPath: string | null = null;

    return html`
      <div class="graph-hud" aria-label="Workspace controls and hints">
        <div class="hud-group">
          <button
            type="button"
            class="hud-button"
            data-prism-add-stage
            @click=${(event: Event) => {
              const selectedStage = this.workflow?.stages.find(stage => stage.stageKey === this._selectedStageKey) ?? null;
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
        Tab through role bands, stage cards, transition chips, and transition handles. Enter selects, T opens transition creation, E opens the inspector, and Shift+F10 opens the context menu.
      </p>

      ${isEmpty
        ? this._renderWorkspaceEmptyState('graph')
        : html`<div
            class="graph-canvas"
            role="application"
            tabindex="0"
            aria-label=${`Workflow graph canvas — ${this.workflow?.displayName ?? 'workflow'}`}
            aria-roledescription="Role-first workflow editor workspace"
            @click=${() => this._dismissContextMenu(false)}
            @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'canvas' })}
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
                    @focus=${() => this._announce(`${lane.label} lane. ${lane.stageCount} stage${lane.stageCount === 1 ? '' : 's'}. ${lane.description}.`)}
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
                ${transitionLayouts.map(layout => layout.path ? svg`
                  <path
                    class=${`edge-path ${layout.branch ? 'branch-path' : ''} ${layout.merge ? 'merge-path' : ''} ${this._selectedTransitionIndex === layout.index ? 'selected' : ''} ${this._transitionIsInSimulationPath(layout.index) ? 'simulation-path' : ''}`}
                    d=${layout.path}
                    marker-end="url(#graph-arrow)"
                    data-prism-transition-path=${String(layout.index)}
                    data-prism-transition-from=${layout.visualFromKey}
                    data-prism-transition-to=${layout.visualToKey}
                    data-prism-transition-simulation-path=${String(this._transitionIsInSimulationPath(layout.index))}
                  ></path>
                ` : nothing)}
                ${dragPath ? svg`<path class="edge-path draft" d=${dragPath}></path>` : nothing}
              </svg>

              ${transitionLayouts.map(layout => layout.path ? html`
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
                  @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'transition', transitionIndex: layout.index }, event.currentTarget as HTMLElement)}
                >
                  ${layout.transition.action}
                </button>
              ` : nothing)}

              ${gatewayLayouts.map(layout => html`
                <div
                  class="gateway-node-shell"
                  style=${`left:${layout.x}px;top:${layout.y}px;width:${layout.width}px;height:${layout.height}px;`}
                >
                  <button
                    type="button"
                    class=${`gateway-node ${layout.surface} kind-${layout.gateway.kind.toLowerCase()} ${this._selectedGatewayKey === layout.gateway.gatewayKey ? 'selected' : ''}`}
                    aria-pressed=${String(this._selectedGatewayKey === layout.gateway.gatewayKey)}
                    aria-label=${`${layout.gateway.displayName}, ${layout.gateway.kind} gateway, ${layout.laneLabel} lane`}
                    data-prism-gateway=${layout.gateway.gatewayKey}
                    data-prism-gateway-kind=${layout.gateway.kind}
                    data-prism-lane=${layout.laneKey}
                    @click=${() => this._selectGateway(layout.gateway.gatewayKey)}
                    @dblclick=${() => this._selectGateway(layout.gateway.gatewayKey, { openInspector: true })}
                    @keydown=${(event: KeyboardEvent) => this._handleGraphNodeKeydown(event, { kind: 'gateway', gateway: layout.gateway })}
                  >
                    <span class="surface-tag">${layout.laneLabel}</span>
                    <span class="gateway-kind-badge">${layout.gateway.kind} gateway</span>
                    <span class="node-label">${layout.gateway.displayName}</span>
                    <span class="node-meta">${layout.binding.relatedTransitionIndices.length} related route${layout.binding.relatedTransitionIndices.length === 1 ? '' : 's'}</span>
                  </button>
                </div>
              `)}

              ${stageLayouts.map((layout, visualIndex) => html`
                <div
                  class="stage-node-shell"
                  style=${`left:${layout.x}px;top:${layout.y}px;width:${layout.width}px;height:${layout.height}px;`}
                >
                  <button
                    type="button"
                    class=${`stage-node ${layout.surface} ${this._selectedStageKey === layout.stage.stageKey ? 'selected' : ''} ${this._stageIsInSimulationPath(layout.stage.stageKey) ? 'simulation-path' : ''} ${this.simulationCurrentStageKey === layout.stage.stageKey ? 'simulation-current' : ''}`}
                    aria-pressed=${String(this._selectedStageKey === layout.stage.stageKey)}
                    aria-label=${`${layout.stage.displayName}, ${layout.laneLabel} lane`}
                    data-prism-stage="${layout.stage.stageKey}"
                    data-prism-stage-simulation-path=${String(this._stageIsInSimulationPath(layout.stage.stageKey))}
                    data-prism-stage-simulation-current=${String(this.simulationCurrentStageKey === layout.stage.stageKey)}
                    @click=${() => this._selectStage(layout.stage.stageKey, { focusIndex: visualIndex })}
                    @dblclick=${() => this._selectStage(layout.stage.stageKey, { openInspector: true, focusIndex: visualIndex })}
                    @keydown=${(event: KeyboardEvent) => this._handleGraphNodeKeydown(event, { kind: 'stage', stage: layout.stage, index: visualIndex })}
                    @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'stage', stageKey: layout.stage.stageKey }, event.currentTarget as HTMLElement)}
                  >
                    <span class="surface-tag">${layout.laneLabel}</span>
                    <span class="node-label">${layout.stage.displayName}</span>
                    <span class="node-meta">${layout.stage.kind} · ${layout.laneLabel} lane</span>
                  </button>
                </div>
              `)}
            </div>
          </div>
        </div>
      </div>`}
    `;
  }

  private _renderWorkspaceEmptyState(mode: 'graph' | 'linear') {
    return html`
      <section class="workspace-empty-state" role="status" data-prism-empty-state=${mode}>
        <h2 class="workspace-empty-title">Start building your workflow</h2>
        <p class="workspace-empty-copy">
          This workflow does not have any stages yet. Add the first stage, then connect routes as you model the author journey.
        </p>
        <ul class="workspace-empty-tips">
          <li>Use <strong>Add stage</strong>, then name the lane owner that should own the work.</li>
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
      </section>
    `;
  }

  private _renderLinear(stages: AuthoredStage[]) {
    const visibleStages = this._visibleLinearStages(stages);
    const visibleGateways = this._visibleLinearGateways();
    const laneFilters = this._availableLaneKeys();
    const gatewayBindingsByKey = new Map(this._layout.gatewayLayouts.map(layout => [layout.gateway.gatewayKey, layout]));

    return html`
      <section class="linear-workspace" aria-label="Workflow stages — list workspace">
        <div class="linear-toolbar" aria-label="List workspace controls">
          <div class="hud-group">
            <button
              type="button"
              class=${`hud-button ${this._linearFilter === ALL_LANES_FILTER ? 'filter-active' : ''}`}
              aria-pressed=${String(this._linearFilter === ALL_LANES_FILTER)}
              data-prism-linear-filter=${ALL_LANES_FILTER}
              @click=${() => this._setLinearFilter(ALL_LANES_FILTER)}
            >
              All stages
            </button>
            ${laneFilters.map(laneKey => html`
              <button
                type="button"
                class=${`hud-button ${this._linearFilter === laneKey ? 'filter-active' : ''}`}
                aria-pressed=${String(this._linearFilter === laneKey)}
                data-prism-linear-filter=${laneKey}
                @click=${() => this._setLinearFilter(laneKey)}
              >
                ${this._roleLabelForLane(laneKey)} lane
              </button>
            `)}
          </div>
          <div class="hud-group">
            <button
              type="button"
              class="hud-button"
              data-prism-list-add-stage
              @click=${(event: Event) => this._openCreateStageDialog('front-stage', this._selectedStageKey ? 'after' : 'append', this._selectedStageKey, event.currentTarget as HTMLElement)}
            >
              Add stage
            </button>
          </div>
        </div>

        <p class="graph-hint">
          Tab into the row controls. Arrow keys move between rows, Enter opens the inspector, and Alt plus Arrow Up or Arrow Down reorders stages. Route creation now lives on each gateway inspector.
        </p>

        ${visibleStages.length === 0 && visibleGateways.length === 0
          ? this._renderWorkspaceEmptyState('linear')
          : html`
              <div class="linear-table-scroll" tabindex="0">
                <table class="stage-table" data-prism-linear-table>
                  <thead>
                    <tr>
                      <th scope="col">Row</th>
                      <th scope="col">Stage key</th>
                      <th scope="col">Title</th>
                      <th scope="col">Lane owner</th>
                      <th scope="col">Type</th>
                      <th scope="col">Action count</th>
                      <th scope="col">Outbound transitions</th>
                      <th scope="col">Lane</th>
                      <th scope="col">Row actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    ${visibleStages.map((stage, index) => {
                      const outgoing = this._outgoingTransitionsForStage(stage.stageKey);
                      const actionCount = this._actionCountForStage(stage);
                      const actionSummaries = this._actionSummariesForStage(stage);
                      const isSelected = this._selectedStageKey === stage.stageKey;
                      const isDragOver = this._dragOverLinearStageKey === stage.stageKey;
                      const isDragging = this._draggedLinearStageKey === stage.stageKey;
                      const inputIdBase = `stage-${stage.stageKey.replace(/[^a-z0-9-]/gi, '-')}`;
                      const moveUpDisabled = (this.workflow?.stages.findIndex(candidate => candidate.stageKey === stage.stageKey) ?? 0) === 0;
                      const moveDownDisabled = (this.workflow?.stages.findIndex(candidate => candidate.stageKey === stage.stageKey) ?? 0) === (this.workflow?.stages.length ?? 1) - 1;
                      const surface = this._surfaceForStage(stage);
                      return html`
                        <tr
                          class=${`stage-table-row ${surface} ${isSelected ? 'selected' : ''} ${isDragOver ? 'drag-over' : ''} ${isDragging ? 'dragging' : ''}`}
                          data-prism-list-row="${stage.stageKey}"
                          @click=${(event: MouseEvent) => this._handleLinearRowClick(event, stage.stageKey, index)}
                          @dragover=${(event: DragEvent) => this._handleLinearDragOver(event, stage.stageKey)}
                          @drop=${(event: DragEvent) => this._handleLinearDrop(event, stage.stageKey)}
                        >
                          <td class="row-trigger-cell">
                            <button
                              type="button"
                              class="row-trigger"
                              data-prism-list-row-trigger
                              data-prism-stage="${stage.stageKey}"
                              tabindex=${String(index === this._focusedIndex ? '0' : '-1')}
                              aria-current=${isSelected ? 'true' : 'false'}
                              aria-label=${`Open ${stage.displayName} in the inspector`}
                              @click=${() => this._selectStage(stage.stageKey, { openInspector: true, focusIndex: index })}
                              @keydown=${(event: KeyboardEvent) => this._handleListKeydown(event, stage.stageKey, index)}
                              @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'stage', stageKey: stage.stageKey }, event.currentTarget as HTMLElement)}
                            >
                              Row ${index + 1}
                            </button>
                            <button
                              type="button"
                              class="drag-handle"
                              draggable="true"
                              aria-label=${`Drag ${stage.displayName} to reorder`}
                              data-prism-stage-drag="${stage.stageKey}"
                              @dragstart=${(event: DragEvent) => this._handleLinearDragStart(event, stage.stageKey)}
                              @dragend=${this._handleLinearDragEnd}
                            >
                              ↕
                            </button>
                          </td>
                          <td>
                            <label class="sr-only" for=${`${inputIdBase}-key`}>Stage key</label>
                            <input
                              id=${`${inputIdBase}-key`}
                              class="table-input"
                              data-prism-inline-field="stageKey"
                              .value=${stage.stageKey}
                              @change=${(event: Event) => this._handleInlineEditorCommit(event, stage.stageKey, 'stageKey')}
                              @keydown=${this._handleInlineEditorKeydown}
                            />
                          </td>
                          <td>
                            <label class="sr-only" for=${`${inputIdBase}-title`}>Stage title</label>
                            <input
                              id=${`${inputIdBase}-title`}
                              class="table-input"
                              data-prism-inline-field="displayName"
                              .value=${stage.displayName}
                              @change=${(event: Event) => this._handleInlineEditorCommit(event, stage.stageKey, 'displayName')}
                              @keydown=${this._handleInlineEditorKeydown}
                            />
                          </td>
                          <td>
                            <label class="sr-only" for=${`${inputIdBase}-lane`}>Stage lane owner</label>
                            <input
                              id=${`${inputIdBase}-lane`}
                              class="table-input"
                              data-prism-inline-field="lane"
                              .value=${stageLaneKey(stage)}
                              placeholder="applicant"
                              @change=${(event: Event) => this._handleInlineEditorCommit(event, stage.stageKey, 'lane')}
                              @keydown=${this._handleInlineEditorKeydown}
                            />
                          </td>
                          <td>
                            <label class="sr-only" for=${`${inputIdBase}-kind`}>Stage type</label>
                            <select
                              id=${`${inputIdBase}-kind`}
                              class="table-select"
                              data-prism-inline-field="kind"
                              @change=${(event: Event) => this._handleInlineEditorCommit(event, stage.stageKey, 'kind')}
                            >
                              ${(['Question', 'CheckAnswers', 'Confirmation', 'TaskList'] as const).map(kind => html`
                                <option value=${kind} ?selected=${stage.kind === kind}>${kind}</option>
                              `)}
                            </select>
                          </td>
                          <td>
                            <div class="stage-action-summary-cell">
                              <span class="metric-pill" data-prism-action-count>${actionCount}</span>
                              ${actionSummaries.length > 0
                                ? html`
                                    <ul class="stage-action-summary-list" data-prism-list-action-summary="${stage.stageKey}">
                                      ${actionSummaries.map(summary => html`<li>${summary}</li>`)}
                                    </ul>
                                  `
                                : html`<span class="transition-empty">No action summaries</span>`}
                            </div>
                          </td>
                          <td>
                            <div class="transition-summary" data-prism-outbound-count=${String(outgoing.length)}>
                              <span class="metric-pill">${outgoing.length}</span>
                              ${outgoing.length === 0
                                ? html`<span class="transition-empty">No outbound transitions</span>`
                                : html`
                                    <ul class="transition-list">
                                      ${outgoing.map(({ transition, index: transitionIndex }) => html`
                                        <li>
                                          <button
                                            type="button"
                                            class="transition-link"
                                            data-prism-list-transition=${String(transitionIndex)}
                                            @click=${() => this._selectTransition(transitionIndex, { openInspector: true })}
                                          >
                                            ${transition.action} → ${this._labelForStage(transition.toStage)}
                                            ${transition.condition ? ` (${describeTransitionCondition(transition.condition)})` : ''}
                                          </button>
                                        </li>
                                      `)}
                                    </ul>
                                  `}
                            </div>
                          </td>
                          <td>
                            <span class="badge">${this._roleLabelForLane(stageLaneKey(stage))}</span>
                          </td>
                          <td>
                            <div class="row-actions">
                              <button
                                type="button"
                                class="row-action-button"
                                data-prism-insert-before="${stage.stageKey}"
                                @click=${(event: Event) => this._openCreateStageDialog(this._surfaceForStage(stage), 'before', stage.stageKey, event.currentTarget as HTMLElement)}
                              >
                                Insert before
                              </button>
                              <button
                                type="button"
                                class="row-action-button"
                                data-prism-insert-after="${stage.stageKey}"
                                @click=${(event: Event) => this._openCreateStageDialog(this._surfaceForStage(stage), 'after', stage.stageKey, event.currentTarget as HTMLElement)}
                              >
                                Insert after
                              </button>
                              <button
                                type="button"
                                class="row-action-button"
                                data-prism-move-up="${stage.stageKey}"
                                ?disabled=${moveUpDisabled}
                                @click=${() => this._moveStage(stage.stageKey, -1)}
                              >
                                Move up
                              </button>
                              <button
                                type="button"
                                class="row-action-button"
                                data-prism-move-down="${stage.stageKey}"
                                ?disabled=${moveDownDisabled}
                                @click=${() => this._moveStage(stage.stageKey, 1)}
                              >
                                Move down
                              </button>
                              <button
                                type="button"
                                class="row-action-button danger"
                                data-prism-delete-stage="${stage.stageKey}"
                                @click=${(event: Event) => this._openDeleteStageDialog(stage.stageKey, event.currentTarget as HTMLElement)}
                              >
                                Delete
                              </button>
                            </div>
                          </td>
                        </tr>
                      `;
                    })}
                    ${visibleGateways.map((gateway, index) => {
                      const binding = gatewayBindingsByKey.get(gateway.gatewayKey);
                      const laneKey = this._laneKeyForGateway(gateway);
                      const laneLabel = this._roleLabelForLane(laneKey);
                      const isSelected = this._selectedGatewayKey === gateway.gatewayKey;
                      const rowIndex = visibleStages.length + index + 1;
                      return html`
                        <tr
                          class=${`stage-table-row gateway-table-row ${isSelected ? 'selected' : ''}`}
                          data-prism-list-row=${gateway.gatewayKey}
                          data-prism-row-type="gateway"
                        >
                          <td class="row-trigger-cell">
                            <button
                              type="button"
                              class="row-trigger"
                              aria-current=${isSelected ? 'true' : 'false'}
                              aria-label=${`Open ${gateway.displayName} in the inspector`}
                              @click=${() => this._selectGateway(gateway.gatewayKey, { openInspector: true })}
                            >
                              Row ${rowIndex}
                            </button>
                          </td>
                          <td><span class="gateway-inline-key">${gateway.gatewayKey}</span></td>
                          <td><strong>${gateway.displayName}</strong></td>
                          <td>${laneKey}</td>
                          <td><span class="badge gateway-badge-inline">${gateway.kind}</span></td>
                          <td><span class="transition-empty">Editor only</span></td>
                          <td>
                            <div class="transition-summary">
                              <span class="metric-pill">${binding?.binding.relatedTransitionIndices.length ?? 0}</span>
                              <span class="transition-empty">
                                ${gateway.kind === 'Split' ? 'Branch point' : 'Merge point'}
                                ${binding?.binding.anchorStageKey ? ` near ${this._labelForStage(binding.binding.anchorStageKey)}` : ''}
                              </span>
                            </div>
                          </td>
                          <td><span class="badge gateway-badge-inline">${laneLabel}</span></td>
                          <td><span class="transition-empty">Select to inspect</span></td>
                        </tr>
                      `;
                    })}
                  </tbody>
                </table>
              </div>
            `}
      </section>
    `;
  }

  render() {
    const stages = this.workflow?.stages ?? [];
    const isLinear = this.allowLinearMode && this.mode === 'linear';

    return html`
      <div class="workflow-graph-root" data-prism-component="workflow-graph" data-prism-mode=${this.mode}>
        <div class="toolbar">
          <div class="toolbar-title-block">
            <span class="workflow-title">${this.workflow?.displayName ?? 'No workflow loaded'}</span>
            <span class="workflow-subtitle">Graph workspace for lane-owned stages, gateways, and transitions</span>
          </div>
          ${this.allowLinearMode
            ? html`
                <button
                  class="mode-toggle"
                  aria-pressed=${String(isLinear)}
                  @click=${this._toggleMode}
                  title=${isLinear ? 'Switch to graph view' : 'Switch to linear list view'}
                >
                  ${isLinear ? 'Graph view' : 'List view'}
                </button>
              `
            : nothing}
        </div>

        <div id="graph-announcer" role="status" aria-live="polite" aria-atomic="true" class="sr-only"></div>

        ${this._renderValidationSummary()}
        ${isLinear ? this._renderLinear(stages) : this._renderGraph()}
        ${this._renderContextMenu()}
        ${this._renderCreateStageDialog()}
        ${this._renderDeleteStageDialog()}
        ${this._renderCreateGatewayDialog()}
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

    .gateway-kind-badge {
      align-self: center;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      background: rgba(124, 58, 237, 0.12);
      color: #6d28d9;
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }

    .gateway-node.kind-join .gateway-kind-badge {
      background: rgba(15, 118, 110, 0.12);
      color: #0f766e;
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
