import { LitElement, css, html, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type {
  AuthoredGateway,
  AuthoredStage,
  RouteView,
  AuthoredServiceBlueprint,
  EditorStageType,
} from './types.js';
import { editorStageTypeToStageKind } from './types.js';
import {
  applyQueueToStage,
  stageQueueKey,
  stageQueueLabel,
  stageSurface,
  type StageSurface,
  type QueueDefinition,
  serviceBlueprintQueueOptions,
} from './stage-assignment.js';
import { serviceBlueprintGateways } from './types.js';
import { gatewayQueueKey } from './gateway-representation.js';
import {
  addRoute,
  buildRoute,
  deleteRoute,
  findOrCreateSplitGateway,
  flattenRoutes,
} from './route-model.js';
import type { GraphBridge } from './graph/graph-bridge.js';
import type { GraphCallbacks, GraphNodeMove, GraphProps } from './graph/graph-callbacks.js';
import { gatewayNodeId, parseGraphNodeId, stageNodeId } from './graph/service-blueprint-graph-layout.js';
import { applyAutoArrange, pruneLayout, setNodePositions } from './graph/service-blueprint-graph-layout-block.js';

type SelectionKind = 'stage' | 'transition' | 'gateway';

type GraphSelectionDetail = {
  kind: SelectionKind;
  stageKey?: string;
  transitionIndex?: number;
  gatewayKey?: string;
};

type ServiceBlueprintUpdatedDetail = {
  serviceBlueprint: AuthoredServiceBlueprint;
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

type CreateStageDialogState = {
  surfaceHint: StageSurface;
  position: 'append' | 'before' | 'after';
  referenceStageKey: string | null;
  title: string;
  stageKey: string;
  queueKey: string;
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
  queueKey: string;
  keyTouched: boolean;
  error: string | null;
};

const TOP_PADDING = 64;
const EDGE_LABEL_WIDTH = 92;
const EDGE_LABEL_HEIGHT = 22;

/**
 * ServiceBlueprint graph workspace for stage/transition authoring.
 *
 * Emits:
 *  - stage-selected CustomEvent<{ stageKey: string }>
 *  - transition-selected CustomEvent<{ transitionIndex: number }>
 *  - selection-change CustomEvent<GraphSelectionDetail>
 *  - inspector-requested CustomEvent<GraphSelectionDetail>
 *  - service-blueprint-updated CustomEvent<ServiceBlueprintUpdatedDetail>
 */
@customElement('prism-service-blueprint-graph')
export class PrismServiceBlueprintGraphElement extends LitElement {
  @property({ attribute: false })
  serviceBlueprint: AuthoredServiceBlueprint | null = null;

  @property({ attribute: false })
  availableQueues: QueueDefinition[] = [];

  /**
   * Render the graph as a pure viewer — no toolbar create buttons, no creation
   * dialogs, no context menus. Selection and zoom remain available so the viewer
   * is keyboard-navigable. Defaults to false (full authoring surface).
   */
  @property({ type: Boolean, attribute: 'read-only', reflect: true })
  readOnly = false;

  /**
   * Declarative JSON form of {@link serviceBlueprint}. Lets the element be initialised
   * from HTML/Razor markup without JS wiring — Razor authors can write
   * `<prism-service-blueprint-graph read-only service-blueprint-json='...'>` and skip the prop
   * assignment. When set, this attribute is parsed and assigned to `serviceBlueprint`.
   */
  @property({ type: String, attribute: 'service-blueprint-json' })
  serviceBlueprintJson: string | null = null;

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
  private _zoom = 1;

  @state()
  private _contextMenu: ContextMenuState | null = null;

  @state()
  private _createStageDialog: CreateStageDialogState | null = null;

  @state()
  private _deleteStageDialog: DeleteStageDialogState | null = null;

  @state()
  private _createGatewayDialog: CreateGatewayDialogState | null = null;

  private _contextReturnTarget: HTMLElement | null = null;
  private _statusTimer: number | null = null;
  private _dialogReturnTarget: HTMLElement | null = null;
  private _bridge: GraphBridge | null = null;
  private _bridgeHost: HTMLElement | null = null;
  private _bridgeLoading = false;
  private _lastMultiSelection: string[] = [];

  connectedCallback() {
    super.connectedCallback();
  }

  disconnectedCallback() {
    if (this._statusTimer !== null) {
      window.clearTimeout(this._statusTimer);
      this._statusTimer = null;
    }
    this._teardownGraphCanvas();
    super.disconnectedCallback();
  }

  protected updated(changed: Map<string, unknown>) {
    if (changed.has('serviceBlueprintJson') && this.serviceBlueprintJson) {
      try {
        const parsed = JSON.parse(this.serviceBlueprintJson) as AuthoredServiceBlueprint;
        this.serviceBlueprint = parsed;
      } catch (error) {
        console.error('prism-service-blueprint-graph: service-blueprint-json could not be parsed.', error);
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

    const stages = this.serviceBlueprint?.stages ?? [];
    const transitions = flattenRoutes(this.serviceBlueprint);
    const gateways = this.serviceBlueprint?.metadata?.gateways ?? [];

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

    this._syncGraphCanvas();
  }

  private _lastSnapshot: GraphProps | null = null;

  private _graphSnapshot(): GraphProps {
    // Hosts may recreate array props on every render (e.g. mapping the
    // simulation history inline). Reuse the previous reference when the
    // contents are unchanged so the React canvas only re-renders — and
    // re-seeds its local node state — on genuine changes.
    const previous = this._lastSnapshot;
    const stable = <T>(next: T[], prior: T[] | undefined): T[] =>
      prior && prior.length === next.length && next.every((value, index) => value === prior[index])
        ? prior
        : next;
    const snapshot: GraphProps = {
      serviceBlueprint: this.serviceBlueprint,
      availableQueues: stable(this.availableQueues, previous?.availableQueues),
      readOnly: this.readOnly,
      selectedStageKey: this._selectedStageKey,
      selectedGatewayKey: this._selectedGatewayKey,
      selectedTransitionIndex: this._selectedTransitionIndex,
      simulationCurrentStageKey: this.simulationCurrentStageKey,
      simulationPathStageKeys: stable(this.simulationPathStageKeys, previous?.simulationPathStageKeys),
      simulationPathTransitionIndices: stable(
        this.simulationPathTransitionIndices,
        previous?.simulationPathTransitionIndices
      ),
    };
    this._lastSnapshot = snapshot;
    return snapshot;
  }

  private _graphCallbacks(): GraphCallbacks {
    return {
      selectStage: (stageKey, options) => this._selectStage(stageKey, options),
      selectGateway: (gatewayKey, options) => this._selectGateway(gatewayKey, options),
      selectTransition: (transitionIndex, options) => this._selectTransition(transitionIndex, options),
      requestDeleteStage: (stageKey, returnTarget) => {
        if (!this.readOnly) {
          this._openDeleteStageDialog(stageKey, returnTarget ?? null);
        }
      },
      requestDeleteTransition: transitionIndex => {
        if (!this.readOnly) {
          this._deleteTransition(transitionIndex);
        }
      },
      openContextMenu: (position, target, returnTarget) => {
        if (!this.readOnly) {
          this._openContextMenu(position, target, returnTarget);
        }
      },
      paneClicked: () => this._dismissContextMenu(false),
      nodesMoved: moves => this._handleNodesMoved(moves),
      connectRequested: connection => this._handleConnectRequested(connection),
      multiSelectionChanged: nodeIds => {
        // React Flow reports selection with a fresh array identity on every
        // render — only forward genuine changes or the host re-render loops.
        const unchanged = nodeIds.length === this._lastMultiSelection.length
          && nodeIds.every((id, index) => id === this._lastMultiSelection[index]);
        if (unchanged) {
          return;
        }
        this._lastMultiSelection = nodeIds;
        this.dispatchEvent(
          new CustomEvent<{ nodeIds: string[] }>('graph-multi-selection', {
            detail: { nodeIds },
            bubbles: true,
            composed: true,
          })
        );
      },
      laneFocused: lane => this._announce(
        `${lane.label} queue. ${lane.stageCount} stage${lane.stageCount === 1 ? '' : 's'}. ${lane.description}.`
      ),
      zoomChanged: zoom => {
        this._zoom = Number(zoom.toFixed(2));
      },
      ready: () => this.setAttribute('data-prism-graph-ready', 'true'),
    };
  }

  /**
   * Mount/refresh/unmount the React Flow canvas against the .graph-react-host
   * element the Lit template renders. The React bundle (react, react-dom,
   * @xyflow/react) loads lazily on first mount so definition-only usage never
   * downloads it — mirroring how CodeMirror is deferred.
   */
  private _syncGraphCanvas() {
    const host = this.shadowRoot?.querySelector<HTMLElement>('.graph-react-host') ?? null;
    if (!host) {
      this._teardownGraphCanvas();
      return;
    }

    if (this._bridge && this._bridgeHost === host) {
      this._bridge.update(this._graphSnapshot());
      return;
    }

    if (this._bridge) {
      this._teardownGraphCanvas();
    }

    if (this._bridgeLoading) {
      return;
    }
    this._bridgeLoading = true;
    void (async () => {
      try {
        const [{ GraphBridge: GraphBridgeCtor }, { graphStyleSheets }] = await Promise.all([
          import('./graph/graph-bridge.js'),
          import('./graph/graph-styles.js'),
        ]);
        const root = this.shadowRoot;
        if (!this.isConnected || !root) {
          return;
        }
        for (const sheet of graphStyleSheets()) {
          if (!root.adoptedStyleSheets.includes(sheet)) {
            root.adoptedStyleSheets = [...root.adoptedStyleSheets, sheet];
          }
        }
        const currentHost = root.querySelector<HTMLElement>('.graph-react-host');
        if (!currentHost) {
          return;
        }
        this._bridgeHost = currentHost;
        this._bridge = new GraphBridgeCtor(currentHost, this._graphSnapshot(), this._graphCallbacks());
      } finally {
        this._bridgeLoading = false;
      }
    })();
  }

  private _teardownGraphCanvas() {
    this._bridge?.unmount();
    this._bridge = null;
    this._bridgeHost = null;
    this.removeAttribute('data-prism-graph-ready');
  }

  private _currentSelectionDetail(): GraphSelectionDetail | null {
    if (this._selectedStageKey) {
      return { kind: 'stage', stageKey: this._selectedStageKey };
    }
    if (this._selectedGatewayKey) {
      return { kind: 'gateway', gatewayKey: this._selectedGatewayKey };
    }
    if (this._selectedTransitionIndex !== null) {
      return { kind: 'transition', transitionIndex: this._selectedTransitionIndex };
    }
    return null;
  }

  private _handleNodesMoved(moves: GraphNodeMove[]) {
    if (this.readOnly || !this.serviceBlueprint || moves.length === 0) {
      return;
    }

    let next = setNodePositions(
      this.serviceBlueprint,
      Object.fromEntries(moves.map(move => [move.nodeId, { x: move.x, y: move.y }]))
    );

    const queueMoves = moves.filter(move => move.queueKey);
    for (const move of queueMoves) {
      const parsed = parseGraphNodeId(move.nodeId);
      if (parsed.kind === 'stage') {
        next = {
          ...next,
          stages: next.stages.map(stage =>
            stage.stateKey === parsed.key ? applyQueueToStage(stage, move.queueKey!) : stage
          ),
        };
      } else {
        next = {
          ...next,
          gateways: serviceBlueprintGateways(next).map(gateway =>
            gateway.key === parsed.key
              ? { ...gateway, queueKey: move.queueKey!, actor: move.queueKey! }
              : gateway
          ),
        };
      }
    }

    this._emitServiceBlueprintUpdated(next, this._currentSelectionDetail());

    if (queueMoves.length > 0) {
      const first = queueMoves[0];
      this._announce(
        `${this._labelForStage(parseGraphNodeId(first.nodeId).key)} moved to the ${this._roleLabelForQueue(first.queueKey!)} queue.`
      );
    } else if (moves.length === 1) {
      this._announce(`${this._labelForStage(parseGraphNodeId(moves[0].nodeId).key)} moved.`);
    } else {
      this._announce(`${moves.length} nodes moved.`);
    }
  }

  /**
   * Drag-to-connect. The gateway-routing invariant is preserved by
   * construction: state→state connections are routed through the source's
   * Split gateway (created on demand); state routes may target gateways
   * directly; gateway routes may target anything.
   */
  private _handleConnectRequested(connection: { sourceId: string; targetId: string }) {
    if (this.readOnly || !this.serviceBlueprint) {
      return;
    }
    const source = parseGraphNodeId(connection.sourceId);
    const target = parseGraphNodeId(connection.targetId);
    if (source.key === target.key) {
      return;
    }

    let serviceBlueprint = this.serviceBlueprint;
    let ownerKey = source.key;

    if (source.kind === 'stage' && target.kind === 'stage') {
      const ensured = findOrCreateSplitGateway(serviceBlueprint, source.key);
      serviceBlueprint = ensured.serviceBlueprint;
      ownerKey = ensured.gatewayKey;
    }

    const route = buildRoute({ source: ownerKey, target: target.key, trigger: 'continue' });
    if (flattenRoutes(serviceBlueprint).some(view => view.routeId === route.id)) {
      this._announce(
        `A “continue” route from ${this._labelForStage(source.key)} to ${this._labelForStage(target.key)} already exists.`
      );
      return;
    }

    if (ownerKey === source.key && source.kind === 'stage') {
      serviceBlueprint = {
        ...serviceBlueprint,
        stages: serviceBlueprint.stages.map(stage =>
          stage.stateKey === source.key
            ? { ...stage, routes: [...(stage.routes ?? []), route] }
            : stage
        ),
      };
    } else {
      serviceBlueprint = addRoute(serviceBlueprint, ownerKey, route);
    }

    const transitionIndex = flattenRoutes(serviceBlueprint).findIndex(view => view.routeId === route.id);
    const selection: GraphSelectionDetail | null = transitionIndex >= 0
      ? { kind: 'transition', transitionIndex }
      : null;
    if (selection) {
      this._selectedTransitionIndex = transitionIndex;
      this._selectedStageKey = null;
      this._selectedGatewayKey = null;
    }
    this._emitServiceBlueprintUpdated(serviceBlueprint, selection);
    if (selection) {
      this._emitSelectionChange(selection);
      this._requestInspector(selection);
    }
    this._announce(
      `Route added from ${this._labelForStage(source.key)} to ${this._labelForStage(target.key)}.`
    );
  }

  private _tidyLayout() {
    if (!this.serviceBlueprint) {
      return;
    }
    const next = applyAutoArrange(this.serviceBlueprint, this.availableQueues);
    this._emitServiceBlueprintUpdated(next, this._currentSelectionDetail());
    this._announce('Canvas tidied — nodes returned to the automatic layout.');
    requestAnimationFrame(() => this._bridge?.fitView());
  }

  private _surfaceForStage(stage: AuthoredStage): StageSurface {
    return stageSurface(stage);
  }

  private _queueKeyForGateway(gateway: AuthoredGateway) {
    return gatewayQueueKey(gateway) || 'public';
  }

  private _roleLabelForQueue(queueKey: string) {
    return stageQueueLabel(this.serviceBlueprint, queueKey, this.availableQueues);
  }

  private _availableQueueKeys() {
    return serviceBlueprintQueueOptions(this.serviceBlueprint, this.availableQueues);
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

  private _selectStage(stageKey: string, options?: { openInspector?: boolean }) {
    this._selectedStageKey = stageKey;
    this._selectedTransitionIndex = null;
    this._selectedGatewayKey = null;

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
    const gateway = this.serviceBlueprint?.metadata?.gateways?.find(candidate => candidate.key === gatewayKey);
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
    this._announce(`Gateway “${gateway.displayName}” selected. ${gateway.gatewayType} gateway in the ${this._roleLabelForQueue(this._queueKeyForGateway(gateway))} queue.`);

    if (options?.openInspector) {
      this._requestInspector({ kind: 'gateway', gatewayKey });
    }
  }

  private _selectTransition(index: number, options?: { openInspector?: boolean }) {
    const transition = (flattenRoutes(this.serviceBlueprint))[index];
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

  private _emitServiceBlueprintUpdated(serviceBlueprint: AuthoredServiceBlueprint, selection?: GraphSelectionDetail | null) {
    this.serviceBlueprint = serviceBlueprint;
    this.dispatchEvent(
      new CustomEvent<ServiceBlueprintUpdatedDetail>('service-blueprint-updated', {
        detail: { serviceBlueprint, selection },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _labelForStage(stageKey: string): string {
    return this.serviceBlueprint?.stages.find(stage => stage.stateKey === stageKey)?.displayName
      ?? this.serviceBlueprint?.metadata?.gateways?.find(gateway => gateway.key === stageKey)?.displayName
      ?? stageKey;
  }

  private _makeUniqueStageKey(base: string) {
    const usedKeys = new Set(this.serviceBlueprint?.stages.map(stage => stage.stateKey) ?? []);
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

  private _defaultQueueForSurface(surface: StageSurface) {
    return surface === 'back-stage' ? 'reviewer' : 'public';
  }

  private _openCreateStageDialog(
    surfaceHint: StageSurface,
    position: 'append' | 'before' | 'after',
    referenceStageKey: string | null,
    returnTarget?: HTMLElement | null
  ) {
    const referenceStage = referenceStageKey
      ? this.serviceBlueprint?.stages.find(stage => stage.stateKey === referenceStageKey) ?? null
      : null;
    const defaultQueueKey = referenceStage ? stageQueueKey(referenceStage) : this._defaultQueueForSurface(surfaceHint);
    const baseTitle = 'New stage';
    this._dialogReturnTarget = returnTarget ?? this._contextReturnTarget ?? null;
    this._createStageDialog = {
      surfaceHint,
      position,
      referenceStageKey,
      title: baseTitle,
      stageKey: this._slugifyStageKey(baseTitle, 'new-stage'),
      queueKey: defaultQueueKey,
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

  private _updateCreateStageQueue(value: string) {
    if (!this._createStageDialog) {
      return;
    }

    const previewStage = applyQueueToStage({
      stateKey: '',
      displayName: '',
      metadata: { stageType: 'Question', actions: [], roleGates: [] },
      roleGates: [],
      actions: [],
      components: [],
    }, value);

    this._createStageDialog = {
      ...this._createStageDialog,
      queueKey: value,
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
    if (!this.serviceBlueprint || !this._createStageDialog) {
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

    if (this.serviceBlueprint.stages.some(stage => stage.stateKey === stageKey)) {
      this._createStageDialog = { ...this._createStageDialog, error: 'Stage key must be unique.' };
      return;
    }

    const newStage = applyQueueToStage({
      stateKey: stageKey,
      displayName: title,
      components: [],
      metadata: {
        stageType: editorStageTypeToStageKind(dialog.stageType),
        actions: [],
        roleGates: [],
        editorComment: 'Created from the graph workspace.',
      },
    } as unknown as AuthoredStage, dialog.queueKey);

    const stages = [...this.serviceBlueprint.stages];
    let insertIndex = stages.length;
    if (dialog.referenceStageKey) {
      const referenceIndex = stages.findIndex(stage => stage.stateKey === dialog.referenceStageKey);
      if (referenceIndex >= 0) {
        insertIndex = dialog.position === 'before' ? referenceIndex : referenceIndex + 1;
      }
    }
    stages.splice(insertIndex, 0, newStage);

    const serviceBlueprint: AuthoredServiceBlueprint = {
      ...this.serviceBlueprint,
      initialStage: this.serviceBlueprint.initialStage || newStage.stateKey,
      stages: stages,
    };

    this._selectedStageKey = newStage.stateKey;
    this._selectedTransitionIndex = null;
    this._emitSelectionChange({ kind: 'stage', stageKey: newStage.stateKey });
    this._emitServiceBlueprintUpdated(serviceBlueprint, { kind: 'stage', stageKey: newStage.stateKey });
    this._requestInspector({ kind: 'stage', stageKey: newStage.stateKey });
    this._announce(`${newStage.displayName} added to the workspace.`);
    this._closeCreateStageDialog();
    // New stage starts with no routes, so nothing anchors it near existing
    // content — pan/zoom to it so the author can see where it actually
    // landed instead of hunting for it off-viewport.
    requestAnimationFrame(() => this._bridge?.centerOnNode(stageNodeId(newStage.stateKey)));
  }

  private _openCreateGatewayDialog(returnTarget?: HTMLElement | null) {
    if (!this.serviceBlueprint) {
      return;
    }
    this._dialogReturnTarget = returnTarget ?? null;
    // Prefer a queue an existing stage already lives in — defaulting to
    // availableQueues[0] can pick a host-supplied queue the service blueprint itself
    // never uses, which silently creates a same-labelled duplicate lane
    // (the new gateway's queue key looks identical to an existing one in
    // the UI but isn't, since lanes group by key, not label).
    const defaultQueue = stageQueueKey(this.serviceBlueprint.stages[0])
      || serviceBlueprintQueueOptions(this.serviceBlueprint, this.availableQueues)[0]
      || 'public';
    this._createGatewayDialog = {
      title: '',
      gatewayKey: '',
      kind: 'Split',
      queueKey: defaultQueue,
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
    if (!this.serviceBlueprint || !this._createGatewayDialog) {
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
      ...this.serviceBlueprint.stages.map(s => s.stateKey),
      ...(this.serviceBlueprint.metadata?.gateways ?? []).map(g => g.key),
    ];
    if (usedKeys.includes(key)) {
      this._createGatewayDialog = { ...dialog, error: 'Gateway key must be unique across all stages and gateways.' };
      return;
    }

    const newGateway: AuthoredGateway = {
      key,
      displayName: title,
      gatewayType: dialog.kind,
      queueKey: dialog.queueKey,
      actor: dialog.queueKey,
      roleGates: [],
    };

    const serviceBlueprint: AuthoredServiceBlueprint = {
      ...this.serviceBlueprint,
      gateways: [...serviceBlueprintGateways(this.serviceBlueprint), newGateway],
    };

    this._emitServiceBlueprintUpdated(serviceBlueprint, { kind: 'gateway', gatewayKey: newGateway.key });
    this._announce(`${title} ${dialog.kind} gateway created.`);
    this._closeCreateGatewayDialog();
    // Same as stage creation: an unconnected gateway has no anchor, so it
    // can land anywhere in its queue's rank-0 row — bring it into view.
    requestAnimationFrame(() => this._bridge?.centerOnNode(gatewayNodeId(newGateway.key)));
  }

  private _openDeleteStageDialog(stageKey: string, returnTarget?: HTMLElement | null) {
    if (!this.serviceBlueprint) {
      return;
    }

    this._dialogReturnTarget = returnTarget ?? this._contextReturnTarget ?? null;
    this._deleteStageDialog = {
      stageKey,
      affectedTransitions: (flattenRoutes(this.serviceBlueprint)).filter(
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
    if (!this.serviceBlueprint || !this._deleteStageDialog) {
      return;
    }

    const stageKey = this._deleteStageDialog.stageKey;
    const deletedLabel = this._labelForStage(stageKey);
    const transitionCount = this._deleteStageDialog.affectedTransitions.length;
    const stages = this.serviceBlueprint.stages.filter(stage => stage.stateKey !== stageKey);

    // Drop any gateway whose source was this stage, and remove any route
    // that targeted this stage. The derived `transitions` view is rebuilt
    // by `withDerivedTransitions` before we hand the service blueprint downstream.
    const gateways = serviceBlueprintGateways(this.serviceBlueprint)
      .filter(gateway => gateway.key !== stageKey)
      .map(gateway => ({
        ...gateway,
        routes: (gateway.routes ?? []).filter(route => route.target !== stageKey),
      }));
    const stagesWithRoutes = stages.map(stage => ({
      ...stage,
      routes: (stage.routes ?? []).filter(route => route.target !== stageKey),
    }));

    const serviceBlueprint: AuthoredServiceBlueprint = pruneLayout({
      ...this.serviceBlueprint,
      stages: stagesWithRoutes,
      gateways,
      initialStage:
        this.serviceBlueprint.initialStage === stageKey
          ? stages[0]?.stateKey ?? ''
          : this.serviceBlueprint.initialStage,
    });

    this._selectedStageKey = null;
    this._selectedTransitionIndex = null;
    this._emitServiceBlueprintUpdated(serviceBlueprint, null);
    this._announce(
      `${deletedLabel} deleted.${transitionCount > 0 ? ` ${transitionCount} affected transition${transitionCount === 1 ? '' : 's'} removed.` : ''}`
    );
    this._closeDeleteStageDialog();
  }

  private async _copyStage(stageKey: string) {
    const stage = this.serviceBlueprint?.stages.find(candidate => candidate.stateKey === stageKey);
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
    const transition = (flattenRoutes(this.serviceBlueprint))[index];
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
    if (!this.serviceBlueprint) {
      return;
    }

    const transition = (flattenRoutes(this.serviceBlueprint))[index];
    if (!transition) {
      return;
    }

    const gatewayKey = transition.key;
    const routeId = transition.routeId;
    if (!gatewayKey || !routeId) {
      return;
    }
    const serviceBlueprint: AuthoredServiceBlueprint = deleteRoute(this.serviceBlueprint, { gatewayKey, routeId });

    this._selectedTransitionIndex = null;
    this._emitServiceBlueprintUpdated(serviceBlueprint, null);
    this._dismissContextMenu(false);
    this._announce(`Transition “${transition.action}” deleted.`);
  }

  private _openContextMenu(
    position: { clientX: number; clientY: number },
    target: ContextMenuTarget,
    returnTarget?: HTMLElement
  ) {
    const hostRect = this.getBoundingClientRect();
    this._contextMenu = {
      ...target,
      x: Math.max(12, position.clientX - hostRect.left),
      y: Math.max(12, position.clientY - hostRect.top),
    };
    this._contextReturnTarget = returnTarget ?? null;

    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLButtonElement>('[data-prism-context-menu] button')
        ?.focus();
    });
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
        ? this.serviceBlueprint?.stages.find(stage => stage.stateKey === referenceStageKey) ?? null
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
                data-prism-create-stage-queue
                .value=${dialog.queueKey}
                list="create-stage-queue-options"
                placeholder="planning"
                @input=${(event: Event) => this._updateCreateStageQueue((event.currentTarget as HTMLInputElement).value)}
              />
              <datalist id="create-stage-queue-options">
                ${this._availableQueueKeys().map(option => html`
                  <option value=${option}>${this._roleLabelForQueue(option)}</option>
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
                data-prism-create-gateway-queue
                .value=${dialog.queueKey}
                list="create-gateway-queue-options"
                placeholder="applicant"
                @input=${(event: Event) => {
                  const queueKey = (event.currentTarget as HTMLInputElement).value;
                  this._createGatewayDialog = this._createGatewayDialog
                    ? { ...this._createGatewayDialog, queueKey }
                    : null;
                }}
              />
              <datalist id="create-gateway-queue-options">
                ${this._availableQueueKeys().map(option => html`
                  <option value=${option}>${this._roleLabelForQueue(option)}</option>
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
    const stages = this.serviceBlueprint?.stages ?? [];
    const gateways = serviceBlueprintGateways(this.serviceBlueprint);
    const isEmpty = stages.length === 0 && gateways.length === 0;

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
                    const selectedStage = this.serviceBlueprint?.stages.find(stage => stage.stateKey === this._selectedStageKey) ?? null;
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
                <button
                  type="button"
                  class="hud-button"
                  data-prism-auto-arrange
                  @click=${() => this._tidyLayout()}
                >
                  Tidy layout
                </button>
              </div>
            `}
        <div class="hud-group">
          <button type="button" class="hud-button" aria-label="Zoom out" @click=${() => this._bridge?.zoomOut()}>
            −
          </button>
          <span class="zoom-indicator" data-prism-zoom>${Math.round(this._zoom * 100)}%</span>
          <button type="button" class="hud-button" aria-label="Zoom in" @click=${() => this._bridge?.zoomIn()}>
            +
          </button>
          <button type="button" class="hud-button" data-prism-fit-screen @click=${() => this._fitToScreen()}>
            Fit
          </button>
        </div>
      </div>

      <p class="graph-hint">
        ${this.readOnly
          ? 'Tab through queues and stages. Enter selects, and arrow keys move between stages.'
          : 'Tab through queues, stages, routes, and gateways. Enter selects, E opens details, and Shift+F10 opens the menu. Drag from a stage or gateway’s edge to connect it to another.'}
      </p>

      ${isEmpty
        ? this._renderWorkspaceEmptyState()
        : html`<div
            class="graph-canvas"
            role="application"
            tabindex="0"
            aria-label=${`Service blueprint graph canvas — ${this.serviceBlueprint?.displayName ?? "service blueprint"}`}
            aria-roledescription=${this.readOnly ? 'Service blueprint graph viewer' : 'Service blueprint graph editor'}
            @click=${() => this._dismissContextMenu(false)}
          >
            <div class="graph-react-host" data-prism-component="service-blueprint-graph" data-prism-mode="graph"></div>
          </div>`}
    `;
  }

  private _fitToScreen() {
    this._bridge?.fitView();
    this._announce('Canvas fit to screen.');
  }

  private _renderWorkspaceEmptyState() {
    return html`
      <section class="workspace-empty-state" role="status" data-prism-empty-state="graph">
        <h2 class="workspace-empty-title">${this.readOnly ? 'No stages to display' : 'Start building your service blueprint'}</h2>
        <p class="workspace-empty-copy">
          ${this.readOnly
            ? 'This serviceBlueprint has no stages.'
            : 'This serviceBlueprint does not have any stages yet. Add the first stage, then connect routes as you model the author journey.'}
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
      <div class="service-blueprint-graph-root" data-prism-component="service-blueprint-graph" data-prism-mode="graph" data-prism-read-only=${String(this.readOnly)}>
        <div class="toolbar">
          <div class="toolbar-title-block">
            <span class="service-blueprint-title">${this.serviceBlueprint?.displayName ?? 'No service blueprint loaded'}</span>
            <span class="service-blueprint-subtitle">${this.readOnly ? 'Published service blueprint — read-only viewer' : 'Visual service blueprint map'}</span>
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

    .service-blueprint-graph-root {
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

    .service-blueprint-title {
      font-size: 1rem;
      font-weight: 700;
      color: #0f172a;
    }

    .service-blueprint-subtitle {
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
      border-radius: 6px;
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
      padding: 18px 20px;
    }

    .lane:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 3px;
    }

    /* Purely decorative band drawn on a separate, sunk-behind layer — see
       graph/lanes/lane-layer.tsx for why this is split from the .lane rule
       above. */
    .lane-band {
      box-sizing: border-box;
      border-radius: 18px;
      border: 1px solid #dbe2ea;
      background: rgba(255, 255, 255, 0.88);
    }

    .lane-band.lane-primary {
      box-shadow: inset 0 0 0 1px rgba(29, 78, 216, 0.08);
    }

    .lane-band.lane-supporting {
      box-shadow: inset 0 0 0 1px rgba(71, 85, 105, 0.14);
      background: rgba(248, 250, 252, 0.96);
    }

    /* The header sits in a layer above route lines (see lane-layer.tsx) so
       it's never occluded, but a line can still run directly behind the
       text — an opaque backdrop keeps the label reading as a clean plate
       rather than a line threading through letter gaps. */
    .lane-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      background: rgba(255, 255, 255, 0.92);
      border-radius: 6px;
      padding: 2px 4px;
      margin: -2px -4px 0;
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
      padding: 0.1rem 0.4rem;
      border: 1px solid #e2e8f0;
      border-radius: 4px;
      background: #ffffff;
      color: #475569;
      font-size: 0.6875rem;
      font-weight: 500;
      box-shadow: none;
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
      align-items: center;
      justify-content: center;
      gap: 0.35rem;
      padding: 0.75rem;
      appearance: none;
      text-align: center;
      border: 1px solid #e2e8f0;
      border-left: 4px solid #7c3aed;
      border-radius: 8px;
      background: #ffffff;
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.06);
      cursor: pointer;
    }

    .gateway-node .node-header {
      justify-content: center;
    }

    .gateway-node .node-icon-chip {
      background: rgba(124, 58, 237, 0.12);
      color: #7c3aed;
    }

    .gateway-node.kind-join {
      border-left-color: #0f766e;
    }

    .gateway-node.kind-join .node-icon-chip {
      background: rgba(15, 118, 110, 0.12);
      color: #0f766e;
    }

    .gateway-node.selected {
      border-color: #1d4ed8;
      box-shadow: 0 0 0 2px rgba(29, 78, 216, 0.35);
    }

    .gateway-node .surface-tag {
      align-self: center;
    }

    /* Single-route Split gateways render as a thin pill — low visual weight
       so straight-through routing reads as "stage → small pill → next stage"
       instead of a full card. Multi-route Splits and all Joins keep the
       full card shape rendered above. */
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
      box-shadow: 0 1px 2px rgba(124, 58, 237, 0.1);
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
      box-shadow: 0 0 0 2px rgba(29, 78, 216, 0.3);
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
      border: 1px solid #e2e8f0;
      border-left: 4px solid #2563eb;
      border-radius: 8px;
      background: #ffffff;
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.06);
      cursor: pointer;
    }

    .stage-node .node-icon-chip {
      background: rgba(37, 99, 235, 0.1);
      color: #2563eb;
    }

    .stage-node.back-stage {
      border-left-color: #64748b;
    }

    .stage-node.back-stage .node-icon-chip {
      background: rgba(100, 116, 139, 0.14);
      color: #475569;
    }

    .stage-node.selected {
      border-color: #1d4ed8;
      box-shadow: 0 0 0 2px rgba(29, 78, 216, 0.35);
    }

    .stage-node.simulation-path {
      border-color: #00703c;
      box-shadow: 0 0 0 2px rgba(0, 112, 60, 0.3);
    }

    .stage-node.simulation-current {
      border-color: #0b0c0c;
      box-shadow: 0 0 0 3px rgba(255, 221, 0, 0.9), 0 0 0 5px rgba(11, 12, 12, 0.18);
    }

    .stage-node.drag-target {
      border-color: #0f766e;
      box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.16);
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

    .node-header {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }

    .node-icon-chip {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;
      width: 22px;
      height: 22px;
      border-radius: 6px;
    }

    .node-icon-glyph {
      display: block;
    }

    .node-label {
      font-size: 1rem;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.3;
      overflow-wrap: anywhere;
    }

    .node-meta {
      font-size: 0.6875rem;
      font-weight: 700;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      color: #64748b;
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
    'prism-service-blueprint-graph': PrismServiceBlueprintGraphElement;
  }
}
