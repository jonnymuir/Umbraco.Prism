import { LitElement, css, html, nothing, svg } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import type { AuthoredStage, AuthoredTransition, AuthoredWorkflow, EditorStageType } from './types.js';
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
  defaultTransitionAction,
  defaultTransitionTarget,
  describeTransitionCondition,
  serialiseTransitionCondition,
  TRANSITION_ACTION_OPTIONS,
  transitionQuickAction,
  type TransitionConditionMode,
} from './workflow-transition-editing.js';
import { workflowDeadEndStages, workflowUnreachableStages } from './workflow-validation.js';

export type GraphMode = 'graph' | 'linear';
type LinearFilter = '__all__' | string;
type SelectionKind = 'stage' | 'transition';

type GraphSelectionDetail = {
  kind: SelectionKind;
  stageKey?: string;
  transitionIndex?: number;
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

type TransitionLayout = {
  transition: AuthoredTransition;
  index: number;
  path: string;
  labelX: number;
  labelY: number;
};

type WorkspaceLayout = {
  bounds: { width: number; height: number };
  roleLanes: RoleLane[];
  stageLayouts: StageLayout[];
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

type CreateTransitionDialogState = {
  sourceStageKey: string;
  targetStageKey: string;
  action: string;
  conditionMode: TransitionConditionMode;
  conditionValue: string;
  requiresRole: string;
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
  private _dragTransition:
    | { sourceStageKey: string; x: number; y: number; targetStageKey: string | null }
    | null = null;

  @state()
  private _createStageDialog: CreateStageDialogState | null = null;

  @state()
  private _deleteStageDialog: DeleteStageDialogState | null = null;

  @state()
  private _createTransitionDialog: CreateTransitionDialogState | null = null;

  @query('.graph-canvas')
  private _graphCanvas?: HTMLDivElement;

  private _contextReturnTarget: HTMLElement | null = null;
  private _statusTimer: number | null = null;
  private _dialogReturnTarget: HTMLElement | null = null;

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener('pointermove', this._handleWindowPointerMove);
    window.addEventListener('pointerup', this._handleWindowPointerUp);
  }

  disconnectedCallback() {
    window.removeEventListener('pointermove', this._handleWindowPointerMove);
    window.removeEventListener('pointerup', this._handleWindowPointerUp);
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

    const stages = this.workflow?.stages ?? [];
    const transitions = this.workflow?.transitions ?? [];
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

    if (focusableStages.length === 0) {
      this._focusedIndex = 0;
    } else if (this._focusedIndex >= focusableStages.length) {
      this._focusedIndex = focusableStages.length - 1;
    }
  }

  private get _layout(): WorkspaceLayout {
    const stages = this.workflow?.stages ?? [];
    const transitions = this.workflow?.transitions ?? [];
    const roleLanes: RoleLane[] = [];
    const laneByKey = new Map<string, RoleLane>();
    const stageLayouts: StageLayout[] = [];

    // Group stages by lane and track stage indices per lane
    const stagesPerLane = new Map<string, Array<{ stage: AuthoredStage; globalIndex: number }>>();

    stages.forEach((stage, stageIndex) => {
      const surface = this._surfaceForStage(stage);
      const laneKey = this._roleKeyForStage(stage, surface);
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
        stagesPerLane.set(laneKey, []);
      }

      lane.stageCount += 1;
      stagesPerLane.get(laneKey)!.push({ stage, globalIndex: stageIndex });
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

    const width = roleLanes.length === 0
      ? SIDE_PADDING * 2 + LANE_WIDTH
      : SIDE_PADDING * 2 + roleLanes.length * LANE_WIDTH + Math.max(0, roleLanes.length - 1) * LANE_GAP;
    const maxStagesInAnyLane = Math.max(0, ...Array.from(stagesPerLane.values()).map(list => list.length));
    const height = maxStagesInAnyLane === 0
      ? TOP_PADDING * 2 + LANE_HEADER_OFFSET + 200
      : TOP_PADDING * 2 + LANE_HEADER_OFFSET + maxStagesInAnyLane * NODE_HEIGHT + Math.max(0, maxStagesInAnyLane - 1) * VERTICAL_GAP;

    const stageMap = new Map(stageLayouts.map(layout => [layout.stage.stageKey, layout]));
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
        };
      }

      const { path, labelX, labelY } = this._buildTransitionPath(source, target);
      return { transition, index, path, labelX, labelY };
    });

    return {
      bounds: { width, height },
      roleLanes,
      stageLayouts,
      transitionLayouts,
    };
  }

  private _surfaceForStage(stage: AuthoredStage): StageSurface {
    return stageSurface(stage);
  }

  private _roleKeyForStage(stage: AuthoredStage, surface = this._surfaceForStage(stage)) {
    return stageLaneKey(stage) || (surface === 'back-stage' ? 'reviewer' : 'public');
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

  private _buildTransitionPath(source: StageLayout, target: StageLayout) {
    const sameLane = source.laneKey === target.laneKey;
    const startX = source.x + source.width / 2;
    const startY = source.y + source.height;
    const endX = target.x + target.width / 2;
    const endY = target.y;
    const distance = Math.max(Math.abs(endY - startY), 64);
    const curve = Math.min(Math.max(distance / 2, 56), 180);

    if (sameLane) {
      // Vertical transition within same lane
      const path = `M ${startX} ${startY} C ${startX} ${startY + curve}, ${endX} ${endY - curve}, ${endX} ${endY}`;

      return {
        path,
        labelX: startX + (endX - startX) / 2 + 22,
        labelY: startY + (endY - startY) / 2,
      };
    }

    // Cross-lane transition
    const path = `M ${startX} ${startY} C ${startX} ${startY + curve}, ${endX} ${endY - curve}, ${endX} ${endY}`;

    return {
      path,
      labelX: startX + (endX - startX) / 2,
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

  private _selectTransition(index: number, options?: { openInspector?: boolean }) {
    const transition = this.workflow?.transitions[index];
    if (!transition) {
      return;
    }

    this._selectedTransitionIndex = index;
    this._selectedStageKey = null;

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

  private _openCreateTransitionDialog(
    sourceStageKey: string,
    targetStageKey: string,
    returnTarget?: HTMLElement | null
  ) {
    if (!this.workflow) {
      return;
    }

    this._dialogReturnTarget = returnTarget ?? this._contextReturnTarget ?? null;
    this._createTransitionDialog = {
      sourceStageKey,
      targetStageKey,
      action: defaultTransitionAction(this.workflow, targetStageKey),
      conditionMode: 'always',
      conditionValue: '',
      requiresRole: '',
      error: null,
    };
    this._dismissContextMenu(false);
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLInputElement>('[data-prism-create-transition-label]')
        ?.focus();
    });
  }

  private _openCreateTransitionFromStage(stageKey: string, returnTarget?: HTMLElement | null) {
    if (!this.workflow) {
      return;
    }

    const targetStageKey = defaultTransitionTarget(this.workflow, stageKey);
    if (!targetStageKey) {
      this._announce('Add another stage before creating a transition.');
      return;
    }

    this._openCreateTransitionDialog(stageKey, targetStageKey, returnTarget);
  }

  private _closeCreateTransitionDialog() {
    this._createTransitionDialog = null;
    const returnTarget = this._dialogReturnTarget;
    this._dialogReturnTarget = null;
    requestAnimationFrame(() => returnTarget?.focus());
  }

  private _submitCreateTransition() {
    if (!this.workflow || !this._createTransitionDialog) {
      return;
    }

    const dialog = this._createTransitionDialog;
    const action = dialog.action.trim();
    if (!action) {
      this._createTransitionDialog = { ...dialog, error: 'Transition label is required.' };
      return;
    }

    if (!dialog.targetStageKey || dialog.targetStageKey === dialog.sourceStageKey) {
      this._createTransitionDialog = { ...dialog, error: 'Choose a different target stage.' };
      return;
    }

    if (dialog.conditionMode !== 'always' && !dialog.conditionValue.trim()) {
      this._createTransitionDialog = {
        ...dialog,
        error: dialog.conditionMode === 'event' ? 'Event name is required.' : 'Guard expression is required.',
      };
      return;
    }

    const existingIndex = this.workflow.transitions.findIndex(transition =>
      transition.fromStage === dialog.sourceStageKey
      && transition.toStage === dialog.targetStageKey
    );
    if (existingIndex >= 0) {
      this._createTransitionDialog = { ...dialog, error: 'That route already exists. Edit the existing transition instead.' };
      return;
    }

    const transition: AuthoredTransition = {
      fromStage: dialog.sourceStageKey,
      toStage: dialog.targetStageKey,
      action,
      condition: serialiseTransitionCondition(dialog.conditionMode, dialog.conditionValue),
      requiresRole: dialog.requiresRole.trim() || undefined,
      actions: [],
    };

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      transitions: [...this.workflow.transitions, transition],
    };

    const transitionIndex = workflow.transitions.length - 1;
    this._emitWorkflowUpdated(workflow, { kind: 'transition', transitionIndex });
    this._selectTransition(transitionIndex, { openInspector: true });
    this._announce(
      `Transition ${action} created from ${this._labelForStage(dialog.sourceStageKey)} to ${this._labelForStage(dialog.targetStageKey)}.`
    );
    this._closeCreateTransitionDialog();
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
      waiting: dialog.stageType === 'waiting' ? { allowDefer: false } : undefined,
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
      if (action === 'add-transition') {
        this._openCreateTransitionFromStage(target.stageKey);
      } else if (action === 'copy-stage') {
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

  private _handleGraphNodeKeydown(event: KeyboardEvent, stage: AuthoredStage, index: number) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this._selectStage(stage.stageKey, { focusIndex: index });
      return;
    }

    if (event.key.toLowerCase() === 'e') {
      event.preventDefault();
      this._selectStage(stage.stageKey, { openInspector: true, focusIndex: index });
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this._openDeleteStageDialog(stage.stageKey, event.currentTarget as HTMLElement);
      return;
    }

    if (event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10')) {
      event.preventDefault();
      this._openContextMenuFromKeyboard({ kind: 'stage', stageKey: stage.stageKey }, event.currentTarget as HTMLElement);
      return;
    }

    if (event.key.toLowerCase() === 't') {
      event.preventDefault();
      this._openCreateTransitionFromStage(stage.stageKey, event.currentTarget as HTMLElement);
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

  private _handleWindowPointerMove = (event: PointerEvent) => {
    if (!this._dragTransition) {
      return;
    }

    const point = this._scenePointFromClient(event.clientX, event.clientY);
    this._dragTransition = {
      ...this._dragTransition,
      x: point.x,
      y: point.y,
      targetStageKey: this._stageKeyAtClientPoint(event.clientX, event.clientY),
    };
  };

  private _handleWindowPointerUp = () => {
    if (!this._dragTransition) {
      return;
    }

    const { sourceStageKey, targetStageKey } = this._dragTransition;
    this._dragTransition = null;

    if (targetStageKey && targetStageKey !== sourceStageKey) {
      this._openCreateTransitionDialog(sourceStageKey, targetStageKey);
    } else {
      this._announce('Transition creation cancelled.');
    }
  };

  private _scenePointFromClient(clientX: number, clientY: number) {
    const frame = this.shadowRoot?.querySelector<HTMLElement>('.graph-scene-frame');
    if (!frame) {
      return { x: clientX, y: clientY };
    }

    const rect = frame.getBoundingClientRect();
    return {
      x: (clientX - rect.left) / this._zoom,
      y: (clientY - rect.top) / this._zoom,
    };
  }

  private _stageKeyAtClientPoint(clientX: number, clientY: number) {
    const node = this.shadowRoot?.elementFromPoint(clientX, clientY)?.closest<HTMLElement>('[data-prism-stage]');
    return node?.getAttribute('data-prism-stage') ?? null;
  }

  private _startTransitionDrag(event: PointerEvent, stage: AuthoredStage) {
    event.preventDefault();
    event.stopPropagation();
    const point = this._scenePointFromClient(event.clientX, event.clientY);
    this._dragTransition = {
      sourceStageKey: stage.stageKey,
      x: point.x,
      y: point.y,
      targetStageKey: null,
    };
    this._announce(`Creating transition from ${stage.displayName}. Drop on another stage to connect it.`);
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
              <button type="button" role="menuitem" @click=${() => this._handleContextMenuAction('add-transition')}>
                Create transition
              </button>
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
                <option value="waiting" ?selected=${dialog.stageType === 'waiting'}>Waiting</option>
                <option value="confirmation" ?selected=${dialog.stageType === 'confirmation'}>Confirmation</option>
                <option value="system-work" ?selected=${dialog.stageType === 'system-work'}>System work</option>
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

  private _renderCreateTransitionDialog() {
    const dialog = this._createTransitionDialog;
    if (!dialog || !this.workflow) {
      return nothing;
    }

    const availableTargets = this.workflow.stages.filter(stage => stage.stageKey !== dialog.sourceStageKey);
    const quickAction = transitionQuickAction(dialog.action);

    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-transition-dialog-title"
          aria-describedby="create-transition-dialog-copy"
          data-prism-create-transition-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeCreateTransitionDialog())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow">Transition creation</p>
              <h2 id="create-transition-dialog-title" class="dialog-title">Create transition</h2>
            </div>
          </div>
          <p id="create-transition-dialog-copy" class="dialog-copy">
            Confirm the route, choose a transition label, and add a simple condition or role guard if needed.
          </p>
          ${dialog.error ? html`<p class="dialog-error" data-prism-create-transition-error>${dialog.error}</p>` : nothing}
          <div class="dialog-grid">
            <label class="dialog-field">
              <span class="dialog-label">From</span>
              <input class="dialog-control" .value=${this._labelForStage(dialog.sourceStageKey)} disabled />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">To</span>
              <select
                class="dialog-control"
                data-prism-create-transition-target
                @change=${(event: Event) => {
                  const targetStageKey = (event.currentTarget as HTMLSelectElement).value;
                  this._createTransitionDialog = this._createTransitionDialog
                    ? {
                        ...this._createTransitionDialog,
                        targetStageKey,
                        action:
                          quickAction === 'custom'
                            ? this._createTransitionDialog.action
                            : defaultTransitionAction(this.workflow!, targetStageKey),
                        error: null,
                      }
                    : null;
                }}
              >
                ${availableTargets.map(stage => html`
                  <option value=${stage.stageKey} ?selected=${stage.stageKey === dialog.targetStageKey}>${stage.displayName}</option>
                `)}
              </select>
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Label</span>
              <input
                class="dialog-control"
                data-prism-create-transition-label
                .value=${dialog.action}
                @input=${(event: Event) => {
                  const action = (event.currentTarget as HTMLInputElement).value;
                  this._createTransitionDialog = this._createTransitionDialog
                    ? { ...this._createTransitionDialog, action, error: null }
                    : null;
                }}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Action shortcut</span>
              <select
                class="dialog-control"
                data-prism-create-transition-action
                @change=${(event: Event) => {
                  const nextAction = (event.currentTarget as HTMLSelectElement).value;
                  if (nextAction === 'custom') {
                    return;
                  }
                  this._createTransitionDialog = this._createTransitionDialog
                    ? { ...this._createTransitionDialog, action: nextAction, error: null }
                    : null;
                }}
              >
                ${TRANSITION_ACTION_OPTIONS.map(option => html`
                  <option value=${option.value} ?selected=${quickAction === option.value}>${option.label}</option>
                `)}
                <option value="custom" ?selected=${quickAction === 'custom'}>Custom label</option>
              </select>
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Condition type</span>
              <select
                class="dialog-control"
                data-prism-create-transition-condition-mode
                @change=${(event: Event) => {
                  const conditionMode = (event.currentTarget as HTMLSelectElement).value as TransitionConditionMode;
                  this._createTransitionDialog = this._createTransitionDialog
                    ? {
                        ...this._createTransitionDialog,
                        conditionMode,
                        conditionValue:
                          conditionMode === this._createTransitionDialog.conditionMode
                            ? this._createTransitionDialog.conditionValue
                            : '',
                      }
                    : null;
                }}
              >
                <option value="always" ?selected=${dialog.conditionMode === 'always'}>Always available</option>
                <option value="event" ?selected=${dialog.conditionMode === 'event'}>Event</option>
                <option value="guard" ?selected=${dialog.conditionMode === 'guard'}>Guard expression</option>
              </select>
            </label>
            <label class="dialog-field ${dialog.conditionMode === 'always' ? 'dialog-field-disabled' : ''}">
              <span class="dialog-label">${dialog.conditionMode === 'event' ? 'Event name' : 'Condition value'}</span>
              <input
                class="dialog-control"
                data-prism-create-transition-condition-value
                .value=${dialog.conditionValue}
                ?disabled=${dialog.conditionMode === 'always'}
                placeholder=${dialog.conditionMode === 'event' ? 'application-submitted' : 'application.isComplete == true'}
                @input=${(event: Event) => {
                  const conditionValue = (event.currentTarget as HTMLInputElement).value;
                  this._createTransitionDialog = this._createTransitionDialog
                    ? { ...this._createTransitionDialog, conditionValue }
                    : null;
                }}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Role guard</span>
              <input
                class="dialog-control"
                data-prism-create-transition-role
                .value=${dialog.requiresRole}
                placeholder="reviewer"
                @input=${(event: Event) => {
                  const requiresRole = (event.currentTarget as HTMLInputElement).value;
                  this._createTransitionDialog = this._createTransitionDialog
                    ? { ...this._createTransitionDialog, requiresRole }
                    : null;
                }}
              />
            </label>
          </div>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" @click=${this._closeCreateTransitionDialog}>Cancel</button>
            <button type="button" class="dialog-button primary" data-prism-create-transition-submit @click=${this._submitCreateTransition}>Create transition</button>
          </div>
        </div>
      </div>
    `;
  }

  private _renderGraph() {
    const { bounds, roleLanes, stageLayouts, transitionLayouts } = this._layout;
    const isEmpty = stageLayouts.length === 0;
    const dragSource = this._dragTransition
      ? stageLayouts.find(layout => layout.stage.stageKey === this._dragTransition?.sourceStageKey)
      : null;
    const dragPath = dragSource && this._dragTransition
      ? `M ${dragSource.x + dragSource.width} ${dragSource.y + dragSource.height / 2} C ${dragSource.x + dragSource.width + 80} ${dragSource.y + dragSource.height / 2}, ${this._dragTransition.x - 80} ${this._dragTransition.y}, ${this._dragTransition.x} ${this._dragTransition.y}`
      : null;

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
                    class=${`edge-path ${this._selectedTransitionIndex === layout.index ? 'selected' : ''} ${this._transitionIsInSimulationPath(layout.index) ? 'simulation-path' : ''}`}
                    d=${layout.path}
                    marker-end="url(#graph-arrow)"
                    data-prism-transition-path=${String(layout.index)}
                    data-prism-transition-simulation-path=${String(this._transitionIsInSimulationPath(layout.index))}
                  ></path>
                ` : nothing)}
                ${dragPath ? svg`<path class="edge-path draft" d=${dragPath}></path>` : nothing}
              </svg>

              ${transitionLayouts.map(layout => layout.path ? html`
                <button
                  type="button"
                  class=${`edge-chip ${this._selectedTransitionIndex === layout.index ? 'selected' : ''} ${this._transitionIsInSimulationPath(layout.index) ? 'simulation-path' : ''}`}
                  style=${`left:${layout.labelX - EDGE_LABEL_WIDTH / 2}px;top:${layout.labelY - EDGE_LABEL_HEIGHT / 2}px;`}
                  aria-label=${`Transition ${layout.transition.action}, ${this._transitionDescriptor(layout.transition)}`}
                  data-prism-transition="${layout.index}"
                  data-prism-transition-simulation-path=${String(this._transitionIsInSimulationPath(layout.index))}
                  @click=${() => this._selectTransition(layout.index)}
                  @dblclick=${() => this._selectTransition(layout.index, { openInspector: true })}
                  @keydown=${(event: KeyboardEvent) => this._handleTransitionKeydown(event, layout.index)}
                  @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'transition', transitionIndex: layout.index }, event.currentTarget as HTMLElement)}
                >
                  ${layout.transition.action}
                </button>
              ` : nothing)}

              ${stageLayouts.map((layout, visualIndex) => html`
                <div
                  class="stage-node-shell"
                  style=${`left:${layout.x}px;top:${layout.y}px;width:${layout.width}px;height:${layout.height}px;`}
                >
                  <button
                    type="button"
                    class=${`stage-node ${layout.surface} ${this._selectedStageKey === layout.stage.stageKey ? 'selected' : ''} ${this._dragTransition?.targetStageKey === layout.stage.stageKey ? 'drag-target' : ''} ${this._stageIsInSimulationPath(layout.stage.stageKey) ? 'simulation-path' : ''} ${this.simulationCurrentStageKey === layout.stage.stageKey ? 'simulation-current' : ''}`}
                    aria-pressed=${String(this._selectedStageKey === layout.stage.stageKey)}
                    aria-label=${`${layout.stage.displayName}, ${layout.laneLabel} lane`}
                    data-prism-stage="${layout.stage.stageKey}"
                    data-prism-stage-simulation-path=${String(this._stageIsInSimulationPath(layout.stage.stageKey))}
                    data-prism-stage-simulation-current=${String(this.simulationCurrentStageKey === layout.stage.stageKey)}
                    @click=${() => this._selectStage(layout.stage.stageKey, { focusIndex: visualIndex })}
                    @dblclick=${() => this._selectStage(layout.stage.stageKey, { openInspector: true, focusIndex: visualIndex })}
                    @keydown=${(event: KeyboardEvent) => this._handleGraphNodeKeydown(event, layout.stage, visualIndex)}
                    @contextmenu=${(event: MouseEvent) => this._openContextMenu(event, { kind: 'stage', stageKey: layout.stage.stageKey }, event.currentTarget as HTMLElement)}
                  >
                    <span class="surface-tag">${layout.laneLabel}</span>
                    <span class="node-label">${layout.stage.displayName}</span>
                    <span class="node-meta">${layout.stage.kind} · ${layout.laneLabel} lane</span>
                  </button>
                  <button
                    type="button"
                    class="transition-handle"
                    aria-label=${`Create transition from ${layout.stage.displayName}`}
                    data-prism-transition-handle="${layout.stage.stageKey}"
                    @click=${(event: MouseEvent) => {
                      if (event.detail === 0) {
                        this._openCreateTransitionFromStage(layout.stage.stageKey, event.currentTarget as HTMLElement);
                      }
                    }}
                    @pointerdown=${(event: PointerEvent) => this._startTransitionDrag(event, layout.stage)}
                  >
                    +
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
    const laneFilters = this._availableLaneKeys();

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
          Tab into the row controls. Arrow keys move between rows, Enter opens the inspector, Add transition opens routing creation, and Alt plus Arrow Up or Arrow Down reorders stages.
        </p>

        ${visibleStages.length === 0
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
                              ${(['Question', 'CheckAnswers', 'Confirmation', 'TaskList', 'Waiting', 'StatusTimeline'] as const).map(kind => html`
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
                                data-prism-create-transition="${stage.stageKey}"
                                @click=${(event: Event) => this._openCreateTransitionFromStage(stage.stageKey, event.currentTarget as HTMLElement)}
                              >
                                Add transition
                              </button>
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
            <span class="workflow-subtitle">Graph workspace for stages and transitions</span>
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
        ${this._renderCreateTransitionDialog()}
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

    .stage-node-shell {
      position: absolute;
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
