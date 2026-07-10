import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import {
  type ActionCatalogEntry,
  type AuthoredAction,
  type AuthoredGateway,
  type AuthoredRoute,
  type AuthoredStage,
  type AuthoredWorkflow,
  type WorkflowNodePosition,
  hydrateWorkflowDefinition,
  workflowGateways,
} from './types.js';
import { computeWorkflowGraphLayout, parseGraphNodeId } from './graph/workflow-graph-layout.js';
import { projectWorkflowLocally } from './workflow-runtime-projection.js';
import { WorkflowSaveError, normaliseWorkflowSaveError, type WorkflowSource } from './workflow-source.js';
import type { WorkflowActionCatalog } from './workflow-action-catalog.js';
import { BuiltInWorkflowActionCatalog } from './workflow-action-catalog.js';
import type { WorkflowAuthorContext } from './workflow-author-context.js';
import type { WorkflowQueueDefinition } from './workflow-stage-assignment.js';
import { availableContexts, contextForTiming, timingForContext, updateActionSummary } from './workflow-action-editing.js';
import { isTerminalStage, validateWorkflow, type WorkflowValidationIssue } from './workflow-validation.js';
import { flattenRoutes, newRouteId } from './workflow-routes.js';
import { findWorkflowShortcut, matchesShortcut, WORKFLOW_SHORTCUT_GROUPS } from './workflow-shortcuts.js';
import './prism-workflow-graph.js';
import './prism-step-inspector.js';
import './prism-stage-preview.js';
import './prism-workflow-simulation.js';
import './prism-workflow-outline.js';
import './prism-confidence-tabs.js';
import './prism-help-panel.js';
import { serializeAuthoredWorkflow, authoredWorkflowJsonEquals } from './workflow-canonical-json.js';
import {
  coerceParsedAuthoredWorkflow,
  lintAuthoredWorkflowDocument,
  type DefinitionLint,
} from './workflow-definition-lint.js';
import type { ConfidenceTab } from './prism-confidence-tabs.js';
import type {
  WorkflowSimulationHistoryEntry,
  WorkflowSimulationStopReason,
  WorkflowSimulationTransitionOption,
} from './prism-workflow-simulation.js';
import type { ProjectWorkflowResult, ProjectedWorkflowState, ProjectedWorkflowTransition } from './workflow-runtime-projection.js';

type WorkflowSelection =
  | { kind: 'stage'; stageKey: string }
  | { kind: 'gateway'; gatewayKey: string }
  | null;

type WorkflowHistoryEntry = {
  workflow: AuthoredWorkflow;
  selection: WorkflowSelection;
};

type ActionSelection = {
  target: 'stage' | 'transition';
  index: number;
} | null;

type ClipboardEntry =
  | { kind: 'stage'; stage: AuthoredStage; label: string }
  | { kind: 'subgraph'; stages: AuthoredStage[]; gateways: AuthoredGateway[]; label: string }
  | { kind: 'action'; action: AuthoredAction; label: string; sourceTarget: 'stage' | 'transition' };

type SaveState = 'idle' | 'saving' | 'saved' | 'error';

type SimulationState = {
  currentStageKey: string;
  history: WorkflowSimulationHistoryEntry[];
  pathTransitionIndices: number[];
};

const HISTORY_LIMIT = 50;
const SAVE_SHORTCUT = findWorkflowShortcut('save');
const UNDO_SHORTCUT = findWorkflowShortcut('undo');
const REDO_SHORTCUT = findWorkflowShortcut('redo');
const COPY_SHORTCUT = findWorkflowShortcut('copy');
const PASTE_SHORTCUT = findWorkflowShortcut('paste');
const HELP_SHORTCUT = findWorkflowShortcut('help');

function cloneWorkflow(workflow: AuthoredWorkflow): AuthoredWorkflow {
  return hydrateWorkflowDefinition(JSON.parse(JSON.stringify(workflow)) as AuthoredWorkflow);
}

function cloneSelection(selection: WorkflowSelection): WorkflowSelection {
  return selection ? { ...selection } : null;
}

function cloneStage(stage: AuthoredStage): AuthoredStage {
  return JSON.parse(JSON.stringify(stage)) as AuthoredStage;
}

function cloneAction(action: AuthoredAction): AuthoredAction {
  return JSON.parse(JSON.stringify(action)) as AuthoredAction;
}

function workflowsEqual(left: AuthoredWorkflow | null, right: AuthoredWorkflow | null): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function selectionsEqual(left: WorkflowSelection, right: WorkflowSelection): boolean {
  if (left?.kind !== right?.kind) {
    return false;
  }

  if (left?.kind === 'stage' && right?.kind === 'stage') {
    return left.stageKey === right.stageKey;
  }

  if (left?.kind === 'gateway' && right?.kind === 'gateway') {
    return left.gatewayKey === right.gatewayKey;
  }

  return left === right;
}

function makeCopiedStageKey(baseStageKey: string, workflow: AuthoredWorkflow): string {
  const usedKeys = new Set(workflow.states.map(stage => stage.stateKey));
  let candidate = `${baseStageKey}-copy`;
  let suffix = 2;
  while (usedKeys.has(candidate)) {
    candidate = `${baseStageKey}-copy-${suffix}`;
    suffix += 1;
  }

  return candidate;
}

/**
 * Top-level editor host page composing the four V1 workflow editor components.
 *
 * Layout:
 *   Left  — prism-workflow-graph (with title bar + mode toggle)
 *   Right — prism-step-inspector
 *
 * URL param: ?workflow=<key>  (default: "planning")
 * Prop: initialWorkflow — set directly for Storybook / offline use; skips API fetch.
 *
 * Test hooks:
 *   data-prism-component="workflow-editor"
 *   data-prism-workflow-loaded="{key}" (reflected on the custom-element host once ready)
 *   data-prism-toast  (on the toast confirmation banner)
 *   data-prism-save-error (on the persistent save error surface)
 */
@customElement('prism-workflow-editor')
export class PrismWorkflowEditorElement extends LitElement {
  /** Workflow key — read from ?workflow= URL param or set directly. */
  @property({ type: String, attribute: 'workflow-key' })
  workflowKey = 'planning';

  /**
   * Host-supplied source the editor reads workflows from and writes back to.
   * Required for runtime use; Storybook stories pass `initialWorkflow` instead
   * and can leave this unset.
   */
  @property({ attribute: false })
  workflowSource?: WorkflowSource;

  /**
   * Host-supplied catalog of action types the editor can render. Falls back
   * to Prism's built-in catalog when the host does not extend it.
   */
  @property({ attribute: false })
  actionCatalog?: WorkflowActionCatalog;

  /** Optional UX hint about the current author. Never authoritative. */
  @property({ attribute: false })
  authorContext?: WorkflowAuthorContext;

  /** Host-supplied queues used for queue labels and authoring pickers. */
  @property({ attribute: false })
  availableQueues: WorkflowQueueDefinition[] = [];

  /**
   * If set, the component uses this workflow directly instead of fetching from
   * the API.  Designed for Storybook stories and offline walkthrough fixtures.
   */
  @property({ attribute: false })
  initialWorkflow: AuthoredWorkflow | null = null;

  @state() private _workflow: AuthoredWorkflow | null = null;
  @state() private _selection: WorkflowSelection = null;
  @state() private _selectedTransitionIndex: number | null = null;
  @state() private _toastMessage: string | null = null;
  @state() private _loading = false;
  @state() private _error: string | null = null;
  @state() private _actionCatalog: ActionCatalogEntry[] = [];
  @state() private _undoHistory: WorkflowHistoryEntry[] = [];
  @state() private _redoHistory: WorkflowHistoryEntry[] = [];
  @state() private _historyAnnouncement = '';
  @state() private _actionSelection: ActionSelection = null;
  @state() private _clipboard: ClipboardEntry | null = null;

  /** Prefixed node ids from the canvas's shift-marquee multi-selection. */
  @state() private _graphMultiSelection: string[] = [];
  @state() private _saveState: SaveState = 'idle';
  @state() private _saveMessage: string | null = null;
  @state() private _saveError: WorkflowSaveError | null = null;
  @state() private _saveErrorCopyStatus: string | null = null;
  @state() private _helpOpen = false;
  @state() private _stagePreviewState: 'idle' | 'loading' | 'ready' | 'error' = 'idle';
  @state() private _stagePreviewError: string | null = null;
  @state() private _projectedWorkflowPreview: ProjectWorkflowResult | null = null;
  @state() private _simulation: SimulationState | null = null;
  @state() private _simulationAnnouncement = '';
  @state() private _activeConfidenceTab: ConfidenceTab = 'canvas';
  @state() private _outlineCollapsed = false;
  @state() private _inspectorCollapsed = false;
  @state() private _definitionEditorLoaded = false;
  @state() private _definitionText = '';
  @state() private _definitionParseError: string | null = null;
  @state() private _definitionSchemaIssues: DefinitionLint[] = [];
  @state() private _definitionAnnouncement = '';
  /** Canonical JSON of the workflow at the moment a Definition→Visual sync was committed. */
  private _lastAppliedDefinitionCanonical = '';
  private _definitionDebounceHandle: number | null = null;

  private _savedWorkflowSnapshot: AuthoredWorkflow | null = null;
  private _helpReturnTarget: HTMLElement | null = null;
  private _stagePreviewTimer: number | null = null;
  private _stagePreviewRequestId = 0;
  private _lastLoadedWorkflowKey: string | null = null;
  private _workflowLoadRequestId = 0;

  private get _selectedStageKey(): string | null {
    return this._selection?.kind === 'stage' ? this._selection.stageKey : null;
  }

  private get _selectedGatewayKey(): string | null {
    return this._selection?.kind === 'gateway' ? this._selection.gatewayKey : null;
  }

  connectedCallback() {
    super.connectedCallback();
    this.addEventListener('keydown', this._handleEditorKeydown, true);
    this._reflectWorkflowLoadedState();

    // Honour ?workflow= URL param when running as a standalone page
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('workflow');
      if (keyParam && !this.hasAttribute('workflow-key')) {
        this.workflowKey = keyParam;
      }
    }

    void this._loadActionCatalog();

    if (this.initialWorkflow) {
      this._initialiseEditorState(this.initialWorkflow);
      this._lastLoadedWorkflowKey = this.workflowKey;
    } else {
      void this._loadWorkflow();
    }
  }

  willUpdate(changedProperties: Map<string, unknown>) {
    // Watch for workflow key changes and reload
    if (
      changedProperties.has('workflowKey') &&
      this.workflowKey !== this._lastLoadedWorkflowKey &&
      !this.initialWorkflow
    ) {
      void this._loadWorkflow();
    }
  }

  updated(_changedProperties: Map<string, unknown>) {
    this._refreshDefinitionTextFromWorkflow();
    if (_changedProperties.has('_saveError') && this._saveError) {
      this.updateComplete.then(() => {
        this.shadowRoot?.querySelector<HTMLElement>('[data-prism-save-error]')?.focus();
      });
    }
  }

  disconnectedCallback() {
    this.removeEventListener('keydown', this._handleEditorKeydown, true);
    this._clearStagePreviewTimer();
    super.disconnectedCallback();
  }

  private async _loadWorkflow() {
    const requestId = ++this._workflowLoadRequestId;
    this._loading = true;
    this._error = null;
    this._reflectWorkflowLoadedState();
    this._lastLoadedWorkflowKey = this.workflowKey;

    if (!this.workflowSource) {
      // Empty state — no source wired. The shell renders a developer
      // affordance; the editor element itself stays silently empty so
      // Storybook stories that drive it via `initialWorkflow` are not
      // disturbed.
      this._workflow = null;
      this._loading = false;
      this._reflectWorkflowLoadedState();
      return;
    }

    try {
      const workflow = await this.workflowSource.load(this.workflowKey);
      if (requestId !== this._workflowLoadRequestId) {
        return;
      }
      this._initialiseEditorState(workflow);
    } catch (err) {
      if (requestId !== this._workflowLoadRequestId) {
        return;
      }
      this._error = err instanceof Error ? err.message : String(err);
      this._workflow = null;
      this._reflectWorkflowLoadedState();
    } finally {
      if (requestId === this._workflowLoadRequestId) {
        this._loading = false;
      }
    }
  }

  private async _loadActionCatalog() {
    const catalog = this.actionCatalog ?? new BuiltInWorkflowActionCatalog();
    this._actionCatalog = await catalog.entries();
  }

  private _initialiseEditorState(workflow: AuthoredWorkflow) {
    this._workflow = cloneWorkflow(workflow);
    this._reflectWorkflowLoadedState();
    this._savedWorkflowSnapshot = cloneWorkflow(this._workflow);
    this._undoHistory = [];
    this._redoHistory = [];
    this._actionSelection = null;
    this._saveState = 'idle';
    this._saveMessage = null;
    this._saveError = null;
    this._saveErrorCopyStatus = null;
    this._projectedWorkflowPreview = null;
    this._stagePreviewState = 'idle';
    this._stagePreviewError = null;
    this._simulation = null;
    this._simulationAnnouncement = '';
    this._lastAppliedDefinitionCanonical = '';
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];
    this._applySelection(null, this._workflow);
    this._announceHistory('Workflow loaded. Undo history is ready for your next edit.');
  }

  private _reflectWorkflowLoadedState() {
    const loadedKey = this.workflowKey?.trim() || this._workflow?.definitionKey?.trim();
    if (loadedKey) {
      this.setAttribute('data-prism-workflow-loaded', loadedKey);
      return;
    }

    this.removeAttribute('data-prism-workflow-loaded');
  }

  private get _selectedStage(): AuthoredStage | null {
    if (!this._workflow || !this._selectedStageKey) {
      return null;
    }

    return this._workflow.states.find(stage => stage.stateKey === this._selectedStageKey) ?? null;
  }

  private get _previewedStage(): ProjectedWorkflowState | null {
    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._projectedWorkflowPreview) {
      return null;
    }

    return this._projectedWorkflowPreview.file.states.find(state => state.stateKey === selectedStage.stateKey) ?? null;
  }

  private get _previewedTransitions(): ProjectedWorkflowTransition[] {
    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._projectedWorkflowPreview || !this._workflow) {
      return [];
    }

    const gatewayMap = new Map(workflowGateways(this._workflow).map(g => [g.key, g]));
    const stageRoutes = (this._projectedWorkflowPreview.file.states.find(stage => stage.stateKey === selectedStage.stateKey)?.routes ?? [])
      .filter(route => route.target.trim().length > 0);

    return stageRoutes.flatMap(route => {
      const gateway = gatewayMap.get(route.target);
      if (gateway) {
        return (gateway.routes ?? []).filter(r => r.target.trim().length > 0);
      }
      return [route];
    });
  }

  private get _initialSimulationStage(): AuthoredStage | null {
    if (!this._workflow) {
      return null;
    }

    return this._workflow.states.find(stage => stage.stateKey === this._workflow?.initialState) ?? null;
  }

  private get _simulationCurrentStage(): AuthoredStage | null {
    const simulation = this._simulation;
    if (!this._workflow || !simulation) {
      return null;
    }

    return this._workflow.states.find(stage => stage.stateKey === simulation.currentStageKey) ?? null;
  }

  private _announceSimulation(message: string) {
    this._simulationAnnouncement = '';
    requestAnimationFrame(() => {
      this._simulationAnnouncement = message;
    });
  }

  private _resetSimulation(announcement?: string) {
    if (!this._simulation && !announcement) {
      return;
    }

    this._simulation = null;
    if (announcement) {
      this._announceSimulation(announcement);
    } else {
      this._simulationAnnouncement = '';
    }
  }

  private get _simulationStartBlocker() {
    const initialStage = this._initialSimulationStage;
    if (initialStage) {
      return '';
    }

    return this._validationIssues.find(issue => issue.code === 'initial-stage-missing')?.message
      ?? 'Pick an initial stage before you simulate this workflow.';
  }

  private get _simulationCanStart() {
    return Boolean(this._workflow && this._initialSimulationStage);
  }

  private _simulationBlockersForTransition(transitionIndex: number) {
    if (!this._workflow) {
      return [];
    }

    const transition = (flattenRoutes(this._workflow))[transitionIndex];
    if (!transition) {
      return ['This transition is no longer available.'];
    }

    const targetStage = this._workflow.states.find(stage => stage.stateKey === transition.toStage);
    const blockingIssues = this._blockingValidationIssues.filter(issue => {
      if (issue.location.kind === 'route') {
        return issue.location.routeId === transition.key
          && issue.location.routeId === transition.routeId;
      }

      if (issue.location.kind === 'action' && issue.location.target === 'route') {
        return issue.location.routeId === transition.key
          && issue.location.routeId === transition.routeId
          && issue.blocking;
      }

      if (issue.location.kind === 'stage') {
        return issue.location.stageKey === transition.toStage;
      }

      return false;
    });

    const messages = blockingIssues.map(issue => issue.message);
    if (!targetStage && messages.length === 0) {
      messages.push(`Target stage “${transition.toStage}” is missing.`);
    }

    return messages;
  }

  private get _simulationStopReason(): WorkflowSimulationStopReason {
    const currentStage = this._simulationCurrentStage;
    if (!currentStage || !this._simulation) {
      return null;
    }

    if (isTerminalStage(currentStage)) {
      return 'terminal';
    }

    return this._simulationTransitionOptions.length === 0 ? 'no-transitions' : null;
  }

  private get _simulationTransitionOptions(): WorkflowSimulationTransitionOption[] {
    if (!this._workflow || !this._simulationCurrentStage) {
      return [];
    }

    return (flattenRoutes(this._workflow))
      .map((transition, transitionIndex) => ({ transition, transitionIndex }))
      .filter(({ transition }) => transition.fromStage === this._simulationCurrentStage?.stateKey)
      .map(({ transition, transitionIndex }) => {
        const targetStage = this._workflow?.states.find(stage => stage.stateKey === transition.toStage) ?? null;
        const blockerMessages = this._simulationBlockersForTransition(transitionIndex);
        return {
          transitionIndex,
          label: transition.action,
          targetStageKey: transition.toStage,
          targetStageLabel: targetStage?.displayName ?? transition.toStage,
          targetStageKind: targetStage?.kind,
          blocked: blockerMessages.length > 0,
          blockerMessages,
          conditionSummary: transition.condition ? `Condition: ${transition.condition}` : undefined,
          roleSummary: transition.requiresRole ? `Role guard: ${transition.requiresRole}` : undefined,
        };
      });
  }

  private _currentSelection(): WorkflowSelection {
    return this._selection;
  }

  private _normaliseSelection(
    selection?: { kind: 'stage' | 'gateway' | 'transition'; stageKey?: string; gatewayKey?: string; transitionIndex?: number } | null
  ): WorkflowSelection {
    if (selection?.kind === 'stage' && selection.stageKey) {
      return { kind: 'stage', stageKey: selection.stageKey };
    }

    if (selection?.kind === 'gateway' && selection.gatewayKey) {
      return { kind: 'gateway', gatewayKey: selection.gatewayKey };
    }

    return null;
  }

  private _applySelection(selection: WorkflowSelection, workflow: AuthoredWorkflow | null = this._workflow) {
    if (!workflow) {
      this._selection = null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    if (selection?.kind === 'stage') {
      const exists = workflow.states.some(stage => stage.stateKey === selection.stageKey);
      this._selection = exists ? { kind: 'stage', stageKey: selection.stageKey } : null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    if (selection?.kind === 'gateway') {
      const exists = workflow.metadata?.gateways?.some(gateway => gateway.key === selection.gatewayKey) ?? false;
      this._selection = exists ? { kind: 'gateway', gatewayKey: selection.gatewayKey } : null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    this._selection = null;
    this._selectedTransitionIndex = null;
    this._syncStagePreview();
  }

  private _applyTransitionHighlight(transitionIndex: number, workflow: AuthoredWorkflow | null = this._workflow) {
    const transitions = flattenRoutes(workflow);
    if (!workflow || transitionIndex < 0 || transitionIndex >= transitions.length) {
      this._selectedTransitionIndex = null;
      return;
    }
    // prism-step-inspector has no standalone "route" view — a transition is
    // only ever shown nested inside the stage or gateway whose routes[]
    // array actually owns it (mapRouteView sets fromGateway when the owner
    // is a gateway; fromStage always holds the owner's key either way).
    // Without also selecting that owner, the inspector falls through to its
    // empty state and a newly-connected or outline-clicked route never
    // becomes editable.
    const route = transitions[transitionIndex];
    this._selection = route.fromGateway
      ? { kind: 'gateway', gatewayKey: route.fromGateway }
      : { kind: 'stage', stageKey: route.fromStage };
    this._selectedTransitionIndex = transitionIndex;
    this._syncStagePreview();
  }

  private _clearStagePreviewTimer() {
    if (this._stagePreviewTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this._stagePreviewTimer);
    }
    this._stagePreviewTimer = null;
  }

  private _syncStagePreview() {
    this._clearStagePreviewTimer();

    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._workflow) {
      this._stagePreviewState = 'idle';
      this._stagePreviewError = null;
      this._projectedWorkflowPreview = null;
      return;
    }

    if (typeof window === 'undefined') {
      void this._refreshStagePreview();
      return;
    }

    this._stagePreviewTimer = window.setTimeout(() => {
      void this._refreshStagePreview();
    }, 180);
  }

  private async _refreshStagePreview() {
    if (!this._workflow || !this._selectedStage) {
      return;
    }

    const requestId = ++this._stagePreviewRequestId;
    this._stagePreviewState = 'loading';
    this._stagePreviewError = null;

    try {
      const preview = projectWorkflowLocally(this._workflow);
      if (requestId !== this._stagePreviewRequestId) {
        return;
      }

      this._projectedWorkflowPreview = preview;
      this._stagePreviewState = 'ready';

      if (!preview.file.states.some(state => state.stateKey === this._selectedStage?.stateKey)) {
        this._stagePreviewState = 'error';
        this._stagePreviewError = `The selected stage could not be found in the projected runtime preview.`;
      }
    } catch (error) {
      if (requestId !== this._stagePreviewRequestId) {
        return;
      }

      this._stagePreviewState = 'error';
      this._stagePreviewError = error instanceof Error ? error.message : 'The runtime preview could not be rendered.';
    }
  }

  private _snapshotCurrentState(): WorkflowHistoryEntry | null {
    if (!this._workflow) {
      return null;
    }

    return {
      workflow: cloneWorkflow(this._workflow),
      selection: cloneSelection(this._currentSelection()),
    };
  }

  private _restoreHistoryEntry(entry: WorkflowHistoryEntry) {
    this._workflow = cloneWorkflow(entry.workflow);
    this._applySelection(cloneSelection(entry.selection), this._workflow);
    this._actionSelection = null;
  }

  private _announceHistory(message: string) {
    this._historyAnnouncement = '';
    requestAnimationFrame(() => {
      this._historyAnnouncement = message;
    });
  }

  private get _canUndo() {
    return this._undoHistory.length > 0;
  }

  private get _canRedo() {
    return this._redoHistory.length > 0;
  }

  private get _historyStatusSummary() {
    if (!this._workflow) {
      return 'History unavailable until the workflow loads.';
    }

    if (this._undoHistory.length === 0 && this._redoHistory.length === 0) {
      return 'No editor changes yet. Undo and redo will appear as you edit.';
    }

    const undoLabel = `${this._undoHistory.length} change${this._undoHistory.length === 1 ? '' : 's'} available to undo`;
    const redoLabel = this._redoHistory.length > 0
      ? `${this._redoHistory.length} change${this._redoHistory.length === 1 ? '' : 's'} available to redo`
      : 'Redo disabled — you are at the latest change';

    return `${undoLabel}. ${redoLabel}.`;
  }

  private get _selectedActionIndex() {
    const currentSelection = this._currentSelection();
    if (!currentSelection || !this._actionSelection) {
      return null;
    }

    return currentSelection.kind === 'stage' && this._actionSelection.target === 'stage'
      ? this._actionSelection.index
      : null;
  }

  private get _clipboardSummary() {
    if (!this._clipboard) {
      return 'Clipboard empty — copy a stage or action to paste it elsewhere.';
    }

    return this._clipboard.kind === 'stage'
      ? `Clipboard: stage “${this._clipboard.label}” ready to paste.`
      : `Clipboard: action “${this._clipboard.label}” ready to paste.`;
  }

  private get _validationIssues(): WorkflowValidationIssue[] {
    return this._workflow ? validateWorkflow(this._workflow, this._actionCatalog) : [];
  }

  private get _blockingValidationIssues() {
    return this._validationIssues.filter(issue => issue.blocking);
  }

  private get _warningValidationIssues() {
    return this._validationIssues.filter(issue => !issue.blocking);
  }

  private get _hasBlockingValidationIssues() {
    return this._blockingValidationIssues.length > 0;
  }

  private get _isDirty() {
    return !workflowsEqual(this._workflow, this._savedWorkflowSnapshot);
  }

  private get _canSave() {
    return Boolean(this._workflow)
      && !this._hasBlockingValidationIssues
      && this._saveState !== 'saving'
      && this._canSaveByContext;
  }

  private get _dirtyStateSummary() {
    if (!this._workflow) {
      return 'Workflow not loaded yet.';
    }

    return this._isDirty ? 'Unsaved changes' : 'All changes saved';
  }

  private get _validationStatusSummary() {
    if (!this._workflow) {
      return 'Validation will appear when the workflow loads.';
    }

    if (this._validationIssues.length === 0) {
      return 'No validation issues. The workflow is ready to save.';
    }

    const parts: string[] = [];
    if (this._blockingValidationIssues.length > 0) {
      parts.push(`${this._blockingValidationIssues.length} blocking error${this._blockingValidationIssues.length === 1 ? '' : 's'}`);
    }
    if (this._warningValidationIssues.length > 0) {
      parts.push(`${this._warningValidationIssues.length} warning${this._warningValidationIssues.length === 1 ? '' : 's'}`);
    }
    return `${parts.join(' and ')} in the validation rail.`;
  }

  private get _saveStatusSummary() {
    if (this._saveState === 'saving') {
      return 'Saving workflow changes…';
    }

    if (this._saveState === 'saved') {
      return this._saveMessage ?? 'Workflow changes saved.';
    }

    if (this._saveState === 'error') {
      return this._saveMessage ?? 'Save failed.';
    }

    if (this._hasBlockingValidationIssues) {
      return 'Save is blocked until the blocking validation errors are fixed.';
    }

    return this._saveMessage ?? 'Save is ready.';
  }

  private _commitWorkflowUpdate(nextWorkflow: AuthoredWorkflow, nextSelection: WorkflowSelection) {
    const previousSelection = this._currentSelection();

    if (workflowsEqual(this._workflow, nextWorkflow)) {
      if (!selectionsEqual(previousSelection, nextSelection)) {
        this._applySelection(nextSelection, nextWorkflow);
        this._actionSelection = null;
      }
      return;
    }

    const currentState = this._snapshotCurrentState();
    if (currentState) {
      this._undoHistory = [...this._undoHistory, currentState].slice(-HISTORY_LIMIT);
    }

    if (!selectionsEqual(previousSelection, nextSelection)) {
      this._actionSelection = null;
    }

    this._redoHistory = [];
    this._workflow = nextWorkflow;
    this._saveState = 'idle';
    this._saveMessage = null;
    this._resetSimulation(this._simulation ? 'Simulation reset because the workflow changed.' : undefined);
    this._applySelection(nextSelection, nextWorkflow);
    this._announceHistory(`Change recorded. ${this._historyStatusSummary}`);
  }

  private _currentAction(): { action: AuthoredAction; target: 'stage' | 'transition' } | null {
    if (!this._workflow || !this._actionSelection) {
      return null;
    }

    if (this._actionSelection.target === 'stage' && this._selectedStageKey) {
      const stage = this._workflow.states.find(candidate => candidate.stateKey === this._selectedStageKey);
      const action = stage?.actions?.[this._actionSelection.index];
      return action ? { action, target: 'stage' } : null;
    }

    if (this._actionSelection.target === 'transition' && this._selectedTransitionIndex !== null) {
      const transition = (flattenRoutes(this._workflow))[this._selectedTransitionIndex];
      const action = transition?.actions?.[this._actionSelection.index];
      return action ? { action, target: 'transition' } : null;
    }

    return null;
  }

  private _canPasteActionIntoSelection(action: AuthoredAction) {
    const currentSelection = this._currentSelection();
    if (!currentSelection || currentSelection.kind === 'gateway') {
      return false;
    }

    const target = currentSelection.kind === 'stage' ? 'stage' : 'transition';
    const entry = this._actionCatalog.find(candidate => candidate.type === action.type) ?? null;
    return entry ? availableContexts(entry, target).length > 0 : true;
  }

  private get _canCopy() {
    return this._currentAction() !== null
      || this._currentSelection()?.kind === 'stage'
      || this._graphMultiSelection.length >= 2;
  }

  private get _canPaste() {
    if (!this._workflow || !this._clipboard) {
      return false;
    }

    if (this._clipboard.kind === 'stage' || this._clipboard.kind === 'subgraph') {
      return true;
    }
    return this._canPasteActionIntoSelection(this._clipboard.action);
  }

  private _normalisePastedAction(action: AuthoredAction, target: 'stage' | 'transition'): AuthoredAction | null {
    const nextAction = cloneAction(action);
    const entry = this._actionCatalog.find(candidate => candidate.type === nextAction.type) ?? null;

    if (!entry) {
      return {
        ...nextAction,
        timing: target === 'transition'
          ? 'OnTransition'
          : nextAction.timing === 'OnExit'
            ? 'OnExit'
            : 'OnEntry',
      };
    }

    const contexts = availableContexts(entry, target);
    if (contexts.length === 0) {
      return null;
    }

    const preferredContext = target === 'transition'
      ? 'transition'
      : contexts.includes(contextForTiming(nextAction.timing, 'stage'))
        ? contextForTiming(nextAction.timing, 'stage')
        : contexts[0];

    return updateActionSummary(entry, {
      ...nextAction,
      timing: timingForContext(preferredContext),
    });
  }

  private _undo = () => {
    if (!this._canUndo) {
      return;
    }

    const previous = this._undoHistory[this._undoHistory.length - 1];
    const current = this._snapshotCurrentState();
    if (!current) {
      return;
    }

    this._undoHistory = this._undoHistory.slice(0, -1);
    this._redoHistory = [...this._redoHistory, current].slice(-HISTORY_LIMIT);
    this._restoreHistoryEntry(previous);
    this._announceHistory(`Undid the last workflow change. ${this._historyStatusSummary}`);
  };

  private _redo = () => {
    if (!this._canRedo) {
      return;
    }

    const next = this._redoHistory[this._redoHistory.length - 1];
    const current = this._snapshotCurrentState();
    if (!current) {
      return;
    }

    this._redoHistory = this._redoHistory.slice(0, -1);
    this._undoHistory = [...this._undoHistory, current].slice(-HISTORY_LIMIT);
    this._restoreHistoryEntry(next);
    this._announceHistory(`Redid the workflow change. ${this._historyStatusSummary}`);
  };

  private _isEditableTarget(event: KeyboardEvent) {
    return event.composedPath().some(target =>
      target instanceof HTMLElement
      && (
        target instanceof HTMLInputElement
        || target instanceof HTMLTextAreaElement
        || target instanceof HTMLSelectElement
        || target.isContentEditable
      )
    );
  }

  private _handleEditorKeydown = (event: KeyboardEvent) => {
    if (!event.defaultPrevented && HELP_SHORTCUT && matchesShortcut(event, HELP_SHORTCUT)) {
      event.preventDefault();
      this._openShortcutGuide(this.shadowRoot?.activeElement as HTMLElement | null);
      return;
    }

    if (this._helpOpen || event.defaultPrevented || event.altKey) {
      return;
    }

    if (SAVE_SHORTCUT && matchesShortcut(event, SAVE_SHORTCUT)) {
      event.preventDefault();
      void this._handleSave();
      return;
    }

    if (
      ((COPY_SHORTCUT && matchesShortcut(event, COPY_SHORTCUT))
        || (PASTE_SHORTCUT && matchesShortcut(event, PASTE_SHORTCUT)))
      && this._isEditableTarget(event)
    ) {
      return;
    }

    if (COPY_SHORTCUT && matchesShortcut(event, COPY_SHORTCUT)) {
      if (this._copySelection()) {
        event.preventDefault();
      }
      return;
    }

    if (PASTE_SHORTCUT && matchesShortcut(event, PASTE_SHORTCUT)) {
      if (this._pasteClipboard()) {
        event.preventDefault();
      }
      return;
    }

    if (REDO_SHORTCUT && matchesShortcut(event, REDO_SHORTCUT)) {
      event.preventDefault();
      if (this._canRedo) {
        this._redo();
      }
      return;
    }

    if (!UNDO_SHORTCUT || !matchesShortcut(event, UNDO_SHORTCUT)) {
      return;
    }

    event.preventDefault();
    if (this._canUndo) {
      this._undo();
    }
  };

  private _openShortcutGuide(activator?: HTMLElement | null) {
    this._helpReturnTarget = activator ?? null;
    this._helpOpen = true;
    requestAnimationFrame(() => {
      this.shadowRoot?.querySelector<HTMLElement>('[data-prism-help-close]')?.focus();
    });
  }

  private _closeShortcutGuide() {
    this._helpOpen = false;
    this._helpReturnTarget?.focus();
    this._helpReturnTarget = null;
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

  // ---------------------------------------------------------------------------
  // Event handlers
  // ---------------------------------------------------------------------------

  private _handleStageSelected(e: CustomEvent<{ stageKey: string }>) {
    this._applySelection({ kind: 'stage', stageKey: e.detail.stageKey }, this._workflow);
    this._actionSelection = null;
  }

  private _handleGatewaySelected(e: CustomEvent<{ gatewayKey: string }>) {
    this._applySelection({ kind: 'gateway', gatewayKey: e.detail.gatewayKey }, this._workflow);
    this._actionSelection = null;
  }

  private _handleTransitionSelected(e: CustomEvent<{ transitionIndex: number }>) {
    this._applyTransitionHighlight(e.detail.transitionIndex, this._workflow);
    this._actionSelection = null;
  }

  private _handleActionSelected(e: CustomEvent<{ index: number | null; target: 'stage' | 'transition' }>) {
    this._actionSelection = e.detail.index === null
      ? null
      : { target: e.detail.target, index: e.detail.index };
  }

  private _handleWorkflowUpdated(
    e: CustomEvent<{
      workflow: AuthoredWorkflow;
      selection?: { kind: 'stage' | 'gateway' | 'transition'; stageKey?: string; gatewayKey?: string; transitionIndex?: number } | null;
    }>
  ) {
    const nextWorkflow = cloneWorkflow(e.detail.workflow);
    const detailSelection = e.detail.selection;
    // Transition selections (e.g. the route just created by drag-to-connect)
    // aren't part of WorkflowSelection — they live in the separate
    // _selectedTransitionIndex field alongside _applyTransitionHighlight.
    // _normaliseSelection has no case for them, so route this before it
    // drops the selection to null and leaves the properties panel empty.
    if (detailSelection?.kind === 'transition' && typeof detailSelection.transitionIndex === 'number') {
      this._commitWorkflowUpdate(nextWorkflow, null);
      this._applyTransitionHighlight(detailSelection.transitionIndex, nextWorkflow);
      return;
    }
    const nextSelection = this._normaliseSelection(detailSelection);
    this._commitWorkflowUpdate(nextWorkflow, nextSelection);
  }

  private _handleInspectorRequested() {
    this._inspectorCollapsed = false;
    requestAnimationFrame(() => {
      this.shadowRoot?.querySelector<HTMLElement>('prism-step-inspector')?.focus();
    });
  }

  private _handleOutlineStageSelected = (e: CustomEvent<{ stageKey: string }>) => {
    this._applySelection({ kind: 'stage', stageKey: e.detail.stageKey }, this._workflow);
    this._actionSelection = null;
  };

  private _handleOutlineGatewaySelected = (e: CustomEvent<{ gatewayKey: string }>) => {
    this._applySelection({ kind: 'gateway', gatewayKey: e.detail.gatewayKey }, this._workflow);
    this._actionSelection = null;
    const gateway = this._workflow?.metadata?.gateways?.find(g => g.key === e.detail.gatewayKey);
    if (gateway) {
      this._announceHistory(`Selected gateway ${gateway.displayName}`);
    }
  };

  private _handleOutlineTransitionSelected = (e: CustomEvent<{ transitionIndex: number }>) => {
    this._applyTransitionHighlight(e.detail.transitionIndex, this._workflow);
    this._actionSelection = null;
  };

  private _handleConfidenceTabChanged = (e: CustomEvent<{ tab: ConfidenceTab }>) => {
    this._activeConfidenceTab = e.detail.tab;
    if (e.detail.tab === 'definition') {
      void this._ensureDefinitionEditorLoaded();
    }
  };

  // ---------------------------------------------------------------------------
  // Definition tab — JSON twin-pane sync
  // ---------------------------------------------------------------------------

  private async _ensureDefinitionEditorLoaded() {
    if (this._definitionEditorLoaded) {
      return;
    }
    await import('./prism-definition-editor.js');
    this._definitionEditorLoaded = true;
  }

  private _refreshDefinitionTextFromWorkflow() {
    if (!this._workflow) {
      if (this._definitionText !== '') {
        this._definitionText = '';
      }
      if (this._definitionParseError !== null) {
        this._definitionParseError = null;
      }
      if (this._definitionSchemaIssues.length > 0) {
        this._definitionSchemaIssues = [];
      }
      this._lastAppliedDefinitionCanonical = '';
      return;
    }
    const canonical = serializeAuthoredWorkflow(this._workflow);
    if (canonical === this._lastAppliedDefinitionCanonical) {
      return;
    }
    this._definitionText = canonical;
    this._lastAppliedDefinitionCanonical = canonical;
    if (this._definitionParseError !== null) {
      this._definitionParseError = null;
    }
    if (this._definitionSchemaIssues.length > 0) {
      this._definitionSchemaIssues = [];
    }
  }

  private _handleDefinitionInput = (e: CustomEvent<{ value: string }>) => {
    this._definitionText = e.detail.value;
    if (this._definitionDebounceHandle !== null) {
      window.clearTimeout(this._definitionDebounceHandle);
    }
    this._definitionDebounceHandle = window.setTimeout(() => {
      this._definitionDebounceHandle = null;
      this._tryApplyDefinitionText();
    }, 250);
  };

  private _tryApplyDefinitionText() {
    const source = this._definitionText;
    let parsed: unknown;
    try {
      parsed = JSON.parse(source);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      this._definitionParseError = message;
      this._definitionSchemaIssues = [];
      return;
    }

    const issues = lintAuthoredWorkflowDocument(parsed, source);
    if (issues.length > 0) {
      this._definitionParseError = null;
      this._definitionSchemaIssues = issues;
      return;
    }

    const next = coerceParsedAuthoredWorkflow(parsed);
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];

    if (authoredWorkflowJsonEquals(this._workflow, next)) {
      // No semantic change — just remember the text the user typed.
      this._lastAppliedDefinitionCanonical = serializeAuthoredWorkflow(next);
      return;
    }

    // Mark the canonical so the visual→definition sync doesn't echo this back.
    this._lastAppliedDefinitionCanonical = serializeAuthoredWorkflow(next);
    this._commitWorkflowUpdate(next, this._currentSelection());
    const stageCount = next.states.length;
    const gatewayCount = next.metadata?.gateways?.length ?? 0;
    this._announceDefinition(
      `Definition updated. ${stageCount} ${stageCount === 1 ? 'stage' : 'stages'}, ${gatewayCount} ${gatewayCount === 1 ? 'gateway' : 'gateways'}.`
    );
  }

  private _announceDefinition(message: string) {
    this._definitionAnnouncement = '';
    requestAnimationFrame(() => {
      this._definitionAnnouncement = message;
    });
  }

  private _revertDefinitionText() {
    if (!this._workflow) {
      return;
    }
    if (this._definitionDebounceHandle !== null) {
      window.clearTimeout(this._definitionDebounceHandle);
      this._definitionDebounceHandle = null;
    }
    const canonical = serializeAuthoredWorkflow(this._workflow);
    this._definitionText = canonical;
    this._lastAppliedDefinitionCanonical = canonical;
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];
    this._announceDefinition('Definition reverted to the current workflow.');
  }

  private _applyDefinitionTextImmediately() {
    if (this._definitionDebounceHandle !== null) {
      window.clearTimeout(this._definitionDebounceHandle);
      this._definitionDebounceHandle = null;
    }
    this._tryApplyDefinitionText();
  }
  // Public hook for tests/host: flush debounce and apply if valid.
  applyDefinitionPending() { this._applyDefinitionTextImmediately(); }

  private get _definitionHasIssues() {
    return this._definitionParseError !== null || this._definitionSchemaIssues.length > 0;
  }

  private get _definitionDiagnostics() {
    const out: Array<{ line: number; severity: 'error' | 'warning'; message: string }> = [];
    if (this._definitionParseError) {
      // Try to pull a "line N column M" hint out of JSON.parse errors.
      const lineMatch = /line (\d+)/i.exec(this._definitionParseError);
      out.push({
        line: lineMatch ? Number(lineMatch[1]) : 1,
        severity: 'error',
        message: this._definitionParseError,
      });
    }
    for (const issue of this._definitionSchemaIssues) {
      if (issue.line) {
        out.push({ line: issue.line, severity: 'error', message: issue.message });
      }
    }
    return out;
  }


  private _copySelection() {
    const selectedAction = this._currentAction();
    if (selectedAction) {
      const label = selectedAction.action.summary?.trim()
        || this._actionCatalog.find(entry => entry.type === selectedAction.action.type)?.label
        || selectedAction.action.type;
      this._clipboard = {
        kind: 'action',
        action: cloneAction(selectedAction.action),
        label,
        sourceTarget: selectedAction.target,
      };
      this._showToast(`Copied action ${label}.`);
      return true;
    }

    if (!this._workflow) {
      return false;
    }

    if (this._graphMultiSelection.length >= 2) {
      const selectedKeys = this._graphMultiSelection.map(parseGraphNodeId);
      const stages = this._workflow.states.filter(stage =>
        selectedKeys.some(parsed => parsed.kind === 'stage' && parsed.key === stage.stateKey));
      const gateways = workflowGateways(this._workflow).filter(gateway =>
        selectedKeys.some(parsed => parsed.kind === 'gateway' && parsed.key === gateway.key));
      if (stages.length + gateways.length >= 2) {
        const label = [
          stages.length > 0 ? `${stages.length} stage${stages.length === 1 ? '' : 's'}` : null,
          gateways.length > 0 ? `${gateways.length} gateway${gateways.length === 1 ? '' : 's'}` : null,
        ].filter(Boolean).join(' and ');
        this._clipboard = {
          kind: 'subgraph',
          stages: stages.map(cloneStage),
          gateways: gateways.map(gateway => JSON.parse(JSON.stringify(gateway)) as AuthoredGateway),
          label,
        };
        this._showToast(`Copied ${label}.`);
        return true;
      }
    }

    if (!this._selectedStageKey) {
      return false;
    }

    const stage = this._workflow.states.find(candidate => candidate.stateKey === this._selectedStageKey);
    if (!stage) {
      return false;
    }

    this._clipboard = {
      kind: 'stage',
      stage: cloneStage(stage),
      label: stage.displayName,
    };
    this._showToast(`Copied stage ${stage.displayName}.`);
    return true;
  }

  /**
   * Paste a copied subgraph: every stage and gateway gets a fresh unique key,
   * routes between members of the copied set are remapped to the new keys
   * (routes leaving the set keep their original targets), and the copies are
   * positioned at a small offset from their sources.
   */
  private _pasteSubgraph(entry: Extract<ClipboardEntry, { kind: 'subgraph' }>): boolean {
    if (!this._workflow) {
      return false;
    }
    const workflow = this._workflow;

    const usedKeys = new Set<string>([
      ...workflow.states.map(stage => stage.stateKey),
      ...workflowGateways(workflow).map(gateway => gateway.key),
    ]);
    const uniqueKey = (base: string) => {
      let candidate = `${base}-copy`;
      let suffix = 2;
      while (usedKeys.has(candidate)) {
        candidate = `${base}-copy-${suffix}`;
        suffix += 1;
      }
      usedKeys.add(candidate);
      return candidate;
    };

    const keyMap = new Map<string, string>();
    entry.stages.forEach(stage => keyMap.set(stage.stateKey, uniqueKey(stage.stateKey)));
    entry.gateways.forEach(gateway => keyMap.set(gateway.key, uniqueKey(gateway.key)));

    const remapRoutes = (ownerNewKey: string, routes: AuthoredRoute[] | undefined): AuthoredRoute[] =>
      (routes ?? []).map(route => {
        const target = keyMap.get(route.target) ?? route.target;
        return { ...route, target, id: newRouteId(ownerNewKey, route.trigger, target) };
      });

    const pastedStages: AuthoredStage[] = entry.stages.map(stage => {
      const stateKey = keyMap.get(stage.stateKey)!;
      return { ...cloneStage(stage), stateKey, routes: remapRoutes(stateKey, stage.routes) };
    });
    const pastedGateways: AuthoredGateway[] = entry.gateways.map(gateway => {
      const key = keyMap.get(gateway.key)!;
      const clone = JSON.parse(JSON.stringify(gateway)) as AuthoredGateway;
      return { ...clone, key, routes: remapRoutes(key, gateway.routes) };
    });

    // Copies land offset from their source's current position.
    const { layout } = computeWorkflowGraphLayout(workflow, this.availableQueues);
    const layoutNodes: Record<string, WorkflowNodePosition> = { ...(workflow.layout?.nodes ?? {}) };
    keyMap.forEach((newKey, oldKey) => {
      const isStage = entry.stages.some(stage => stage.stateKey === oldKey);
      const placement = layout.placements.get(`${isStage ? 'stage' : 'gateway'}:${oldKey}`);
      if (placement) {
        layoutNodes[`${isStage ? 'stage' : 'gateway'}:${newKey}`] = {
          x: Math.round(placement.x + 48),
          y: Math.round(placement.y + 48),
        };
      }
    });

    const next: AuthoredWorkflow = {
      ...workflow,
      states: [...workflow.states, ...pastedStages],
      gateways: [...workflowGateways(workflow), ...pastedGateways],
      layout: Object.keys(layoutNodes).length > 0 ? { nodes: layoutNodes } : workflow.layout,
    };

    const firstStageKey = pastedStages[0]?.stateKey ?? null;
    this._commitWorkflowUpdate(
      next,
      firstStageKey ? { kind: 'stage', stageKey: firstStageKey } : this._currentSelection()
    );
    this._showToast(`Pasted ${entry.label}.`);
    return true;
  }

  private _pasteClipboard() {
    if (!this._workflow || !this._clipboard) {
      return false;
    }

    if (this._clipboard.kind === 'subgraph') {
      return this._pasteSubgraph(this._clipboard);
    }

    if (this._clipboard.kind === 'stage') {
      const copiedStage = cloneStage(this._clipboard.stage);
      const stageKey = makeCopiedStageKey(copiedStage.stateKey, this._workflow);
      const pastedStage: AuthoredStage = {
        ...copiedStage,
        stageKey,
      };

      const stages = [...this._workflow.states];
      const selectedStageIndex = this._selectedStageKey
        ? stages.findIndex(stage => stage.stateKey === this._selectedStageKey)
        : -1;
      const insertIndex = selectedStageIndex >= 0 ? selectedStageIndex + 1 : stages.length;
      stages.splice(insertIndex, 0, pastedStage);

      this._commitWorkflowUpdate({ ...this._workflow, states: stages }, { kind: 'stage', stageKey });
      this._showToast(`Pasted stage ${pastedStage.displayName}.`);
      this._handleInspectorRequested();
      return true;
    }

    const currentSelection = this._currentSelection();
    if (!currentSelection || currentSelection.kind !== 'stage') {
      return false;
    }

    const pastedAction = this._normalisePastedAction(this._clipboard.action, 'stage');
    if (!pastedAction) {
      this._showToast(`Action ${this._clipboard.label} cannot be pasted into the current stage.`);
      return false;
    }

    const stageIndex = this._workflow.states.findIndex(stage => stage.stateKey === currentSelection.stageKey);
    if (stageIndex < 0) {
      return false;
    }

    const stages = [...this._workflow.states];
    const nextActions = [...(stages[stageIndex].actions ?? []), pastedAction];
    stages[stageIndex] = { ...stages[stageIndex], actions: nextActions };
    this._commitWorkflowUpdate({ ...this._workflow, states: stages }, currentSelection);
    this._actionSelection = { target: 'stage', index: nextActions.length - 1 };
    this._showToast(`Pasted action ${this._clipboard.label} into ${stages[stageIndex].displayName}.`);
    return true;
  }

  private _showToast(message: string) {
    this._toastMessage = message;
    setTimeout(() => {
      this._toastMessage = null;
    }, 5000);
  }

  private _focusInspectorForValidationIssue(issue: WorkflowValidationIssue) {
    const actionLocation = issue.location.kind === 'action' ? issue.location : null;
    this._inspectorCollapsed = false;
    requestAnimationFrame(() => {
      const inspector = this.shadowRoot?.querySelector<HTMLElement>('prism-step-inspector');
      inspector?.focus();

      if (!actionLocation) {
        return;
      }

      requestAnimationFrame(() => {
        const actionEditor = inspector?.shadowRoot?.querySelector<HTMLElement>('prism-workflow-action-editor');
        const selector = actionLocation.fieldKey && actionLocation.fieldKey !== 'fields'
          ? `[data-prism-action-param="${actionLocation.actionIndex}-${actionLocation.fieldKey}"]`
          : typeof actionLocation.formFieldIndex === 'number'
            ? `[data-prism-form-field-key="${actionLocation.actionIndex}-${actionLocation.formFieldIndex}"]`
            : `[data-prism-stage-action="${actionLocation.actionIndex}"]`;
        actionEditor?.shadowRoot?.querySelector<HTMLElement>(selector)?.focus();
      });
    });
  }

  private _jumpToValidationIssue(issue: WorkflowValidationIssue) {
    if (!this._workflow) {
      return;
    }

    this._activeConfidenceTab = 'canvas';
    this._inspectorCollapsed = false;

    if (issue.location.kind === 'stage') {
      this._applySelection({ kind: 'stage', stageKey: issue.location.stageKey }, this._workflow);
      this._actionSelection = null;
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'route') {
      const gatewayKey = issue.location.routeId;
      const routeId = issue.location.routeId;
      const transitions = flattenRoutes(this._workflow);
      const targetIndex = transitions.findIndex(view =>
        view.key === gatewayKey && view.routeId === routeId
      );
      if (targetIndex >= 0) {
        this._applyTransitionHighlight(targetIndex, this._workflow);
      }
      this._actionSelection = null;
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'action' && issue.location.target === 'route') {
      const gatewayKey = issue.location.routeId;
      const routeId = issue.location.routeId;
      const transitions = flattenRoutes(this._workflow);
      const targetIndex = transitions.findIndex(view =>
        view.key === gatewayKey && view.routeId === routeId
      );
      this._applyTransitionHighlight(targetIndex >= 0 ? targetIndex : 0, this._workflow);
      this._actionSelection = { target: 'transition', index: issue.location.actionIndex };
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'action' && issue.location.target === 'stage') {
      this._applySelection({ kind: 'stage', stageKey: issue.location.stageKey ?? '' }, this._workflow);
      this._actionSelection = { target: 'stage', index: issue.location.actionIndex };
      this._focusInspectorForValidationIssue(issue);
    }
  }

  private async _handleSave() {
    if (!this._workflow) {
      return;
    }

    if (this._hasBlockingValidationIssues) {
      this._saveState = 'error';
      this._saveError = new WorkflowSaveError({
        title: 'Can’t save this workflow yet',
        summary: 'Fix the blocking validation errors first.',
        detailLines: ['Open Validation to review each blocking error before trying again.'],
      });
      this._saveMessage = this._saveError.summary;
      this._saveErrorCopyStatus = null;
      return;
    }

    this._saveState = 'saving';
    this._saveMessage = null;
    this._saveErrorCopyStatus = null;

    if (!this.workflowSource) {
      this._saveState = 'error';
      this._saveError = new WorkflowSaveError({
        title: 'Save unavailable',
        summary: 'No workflow source is wired to the editor.',
        detailLines: ['Connect a workflow source before trying to save.'],
      });
      this._saveMessage = this._saveError.summary;
      this._saveErrorCopyStatus = null;
      return;
    }

    try {
      await this.workflowSource.save(this.workflowKey, this._workflow);
      this._savedWorkflowSnapshot = cloneWorkflow(this._workflow);
      this._saveState = 'saved';
      this._saveMessage = 'Workflow saved.';
      this._saveError = null;
      this._saveErrorCopyStatus = null;
      this._showToast(this._saveMessage);
    } catch (error) {
      this._saveState = 'error';
      this._saveError = normaliseWorkflowSaveError(
        error,
        'The editor couldn’t save your changes. Review the details below and try again.'
      );
      this._saveMessage = this._saveError.summary;
      this._saveErrorCopyStatus = null;
    }
  }

  private async _copySaveErrorDetails() {
    if (!this._saveError) {
      return;
    }

    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(this._saveError.copyText);
        this._saveErrorCopyStatus = 'Save error details copied.';
        return;
      }
    } catch {
      // Fall through to manual copy support below.
    }

    const copyField = this.shadowRoot?.querySelector<HTMLTextAreaElement>('[data-prism-save-error-details]');
    copyField?.focus();
    copyField?.select();
    this._saveErrorCopyStatus = 'Clipboard access is unavailable. Select and copy the details manually.';
  }

  private _startSimulation() {
    const initialStage = this._initialSimulationStage;
    if (!initialStage) {
      this._announceSimulation(this._simulationStartBlocker);
      return;
    }

    this._simulation = {
      currentStageKey: initialStage.stateKey,
      history: [{
        stageKey: initialStage.stateKey,
        stageLabel: initialStage.displayName,
        enteredByTransitionIndex: null,
      }],
      pathTransitionIndices: [],
    };
    this._announceSimulation(`Simulation started at ${initialStage.displayName}.`);
  }

  private _handleSimulationTransitionSelected(e: CustomEvent<{ transitionIndex: number }>) {
    if (!this._workflow || !this._simulation) {
      return;
    }

    const transition = (flattenRoutes(this._workflow))[e.detail.transitionIndex];
    if (!transition) {
      return;
    }

    const blockers = this._simulationBlockersForTransition(e.detail.transitionIndex);
    if (blockers.length > 0) {
      this._announceSimulation(`Transition ${transition.action} is blocked by validation.`);
      return;
    }

    const nextStage = this._workflow.states.find(stage => stage.stateKey === transition.toStage);
    if (!nextStage) {
      this._announceSimulation(`Transition ${transition.action} cannot continue because the target stage is missing.`);
      return;
    }

    this._simulation = {
      currentStageKey: nextStage.stateKey,
      history: [
        ...this._simulation.history,
        {
          stageKey: nextStage.stateKey,
          stageLabel: nextStage.displayName,
          enteredByLabel: transition.action,
          enteredByTransitionIndex: e.detail.transitionIndex,
        },
      ],
      pathTransitionIndices: [...this._simulation.pathTransitionIndices, e.detail.transitionIndex],
    };

    const stopReason = isTerminalStage(nextStage) ? 'terminal' : null;
    this._announceSimulation(
      stopReason === 'terminal'
        ? `Simulation reached end stage ${nextStage.displayName}.`
        : `Simulation moved to ${nextStage.displayName}.`
    );
  }

  private _renderSimulationPanel() {
    return html`
      <prism-workflow-simulation
        .initialStage=${this._initialSimulationStage}
        .currentStage=${this._simulationCurrentStage}
        .history=${this._simulation?.history ?? []}
        .transitionOptions=${this._simulationStopReason ? [] : this._simulationTransitionOptions}
        .active=${Boolean(this._simulation)}
        .canStart=${this._simulationCanStart}
        .startBlocker=${this._simulationStartBlocker}
        .stopReason=${this._simulationStopReason}
        .announcement=${this._simulationAnnouncement}
        @simulation-started=${this._startSimulation}
        @simulation-reset=${() => this._resetSimulation('Simulation cleared.')}
        @simulation-transition-selected=${this._handleSimulationTransitionSelected}
      ></prism-workflow-simulation>
    `;
  }

  private _renderValidationPanel() {
    if (!this._workflow) {
      return html`<div class="validation-empty-panel">No workflow loaded</div>`;
    }

    const issues = this._validationIssues;
    const errorCount = this._blockingValidationIssues.length;
    const warningCount = this._warningValidationIssues.length;

    return html`
      <section class="validation-panel" aria-labelledby="workflow-validation-panel-title" data-prism-validation-rail>
        <div class="validation-panel-header">
          <div>
            <h2 id="workflow-validation-panel-title" class="validation-panel-title">Workflow validation</h2>
            <p class="validation-panel-summary">${this._validationStatusSummary}</p>
          </div>
          <div class="validation-panel-meta">
            <span class="validation-count validation-count-error" data-prism-validation-errors>${errorCount} errors</span>
            <span class="validation-count validation-count-warning" data-prism-validation-warnings>${warningCount} warnings</span>
          </div>
        </div>

        <div class="validation-panel-save-status" data-prism-save-status>
          <span class="validation-save-label">Save status</span>
          <span>${this._saveStatusSummary}</span>
        </div>

        ${issues.length === 0
          ? html`<p class="validation-empty">No validation issues. You can save whenever you are ready.</p>`
          : html`
              <ol class="validation-issue-list">
                ${issues.map(issue => html`
                  <li>
                    <button
                      type="button"
                      class="validation-issue-link"
                      data-prism-validation-issue=${issue.id}
                      @click=${() => this._jumpToValidationIssue(issue)}
                    >
                      <span class=${`validation-issue-badge validation-issue-badge-${issue.severity}`}>
                        ${issue.severity === 'error' ? 'Error' : 'Warning'}
                      </span>
                      <span>${issue.message}</span>
                    </button>
                  </li>
                `)}
              </ol>
            `}
      </section>
    `;
  }

  private _renderDefinitionPanel() {
    if (!this._workflow) {
      return html`<div class="definition-empty" data-prism-definition-empty>
        Loading the workflow definition…
      </div>`;
    }

    const banner = this._renderDefinitionBanner();
    const stageCount = this._workflow.states.length;
    const gatewayCount = this._workflow.metadata?.gateways?.length ?? 0;

    return html`
      <div class="definition-panel" data-prism-definition-panel>
        <div class="definition-header">
          <div class="definition-header-copy">
            <h2 class="definition-title">Definition</h2>
            <p class="definition-subtitle">
              Power-user view of the authored workflow.
              ${stageCount} ${stageCount === 1 ? 'stage' : 'stages'},
              ${gatewayCount} ${gatewayCount === 1 ? 'gateway' : 'gateways'}.
              Edits apply when valid (250&nbsp;ms after typing stops).
            </p>
          </div>
        </div>
        ${banner}
        <div class="definition-editor-frame">
          ${this._definitionEditorLoaded
            ? html`
                <prism-definition-editor
                  data-prism-definition-editor
                  .value=${this._definitionText}
                  .diagnostics=${this._definitionDiagnostics}
                  @definition-input=${this._handleDefinitionInput}
                ></prism-definition-editor>
              `
            : html`<p class="definition-loading" role="status" data-prism-definition-tab-loading>
                Preparing the JSON editor…
              </p>`}
        </div>
        <div class="sr-only" role="status" aria-live="polite" data-prism-definition-announcement>
          ${this._definitionAnnouncement}
        </div>
      </div>
    `;
  }

  private _renderDefinitionBanner() {
    if (!this._definitionHasIssues) {
      return nothing;
    }
    const summary = this._definitionParseError
      ? `JSON is not valid: ${this._definitionParseError}`
      : this._definitionSchemaIssues[0]?.message ?? 'Definition does not match the workflow schema.';
    const additional = !this._definitionParseError && this._definitionSchemaIssues.length > 1
      ? html`<ul class="definition-banner-list">
          ${this._definitionSchemaIssues.slice(1, 5).map(issue => html`<li>${issue.message}</li>`)}
        </ul>`
      : nothing;

    return html`
      <div
        class="definition-banner"
        role="alert"
        data-prism-definition-banner
      >
        <p class="definition-banner-summary">
          <strong>Definition can't be applied:</strong> ${summary}
        </p>
        ${additional}
        <div class="definition-banner-actions">
          <button
            type="button"
            class="govuk-button"
            data-prism-definition-apply
            disabled
            aria-disabled="true"
          >
            Apply when valid
          </button>
          <button
            type="button"
            class="govuk-button govuk-button--secondary"
            data-prism-definition-revert
            @click=${this._revertDefinitionText}
          >
            Revert to current
          </button>
        </div>
      </div>
    `;
  }

  private _renderShortcutGuide() {
    if (!this._helpOpen) {
      return nothing;
    }

    return html`
      <div
        class="modal-backdrop"
        role="presentation"
        @click=${(event: MouseEvent) => {
          if (event.target === event.currentTarget) {
            this._closeShortcutGuide();
          }
        }}
      >
        <section
          class="shortcut-dialog"
          role="dialog"
          aria-modal="true"
          aria-labelledby="workflow-shortcut-title"
          aria-describedby="workflow-shortcut-copy"
          data-prism-shortcut-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeShortcutGuide())}
        >
          <div class="shortcut-dialog-header">
            <div>
              <p class="shortcut-dialog-eyebrow">Help and shortcuts</p>
              <h2 id="workflow-shortcut-title" class="shortcut-dialog-title">Workflow editor keyboard reference</h2>
              <p id="workflow-shortcut-copy" class="shortcut-dialog-copy">
                These shortcuts stay visible in the editor so authors do not have to memorise them. Open this guide any time with F1.
              </p>
            </div>
            <button
              type="button"
              class="toolbar-btn shortcut-dialog-close"
              data-prism-help-close
              @click=${() => this._closeShortcutGuide()}
            >
              Close
            </button>
          </div>

          <div class="shortcut-groups">
            ${WORKFLOW_SHORTCUT_GROUPS.map(group => html`
              <section class="shortcut-group" data-prism-shortcut-group=${group.id}>
                <h3 class="shortcut-group-title">${group.title}</h3>
                <ol class="shortcut-list">
                  ${group.shortcuts.map(shortcut => html`
                    <li class="shortcut-item" data-prism-shortcut=${shortcut.id}>
                      <div class="shortcut-copy">
                        <p class="shortcut-command">${shortcut.command}</p>
                        <p class="shortcut-description">${shortcut.description}</p>
                      </div>
                      <div class="shortcut-keys" aria-label=${`${shortcut.command} shortcuts`}>
                        ${shortcut.labels.map(label => html`<kbd>${label}</kbd>`)}
                      </div>
                      <p class="shortcut-context">${shortcut.context}</p>
                    </li>
                  `)}
                </ol>
              </section>
            `)}
          </div>
        </section>
      </div>
    `;
  }

  private get _canSaveByContext(): boolean {
    return this.authorContext?.canSave !== false;
  }

  private _renderStagePreview() {
    const selectedStage = this._selectedStage;
    return html`
      <prism-stage-preview
        .stage=${selectedStage}
        .projectedState=${this._previewedStage}
        .outgoingTransitions=${this._previewedTransitions}
        .previewState=${this._stagePreviewState}
        .errorMessage=${this._stagePreviewError ?? ''}
      ></prism-stage-preview>
    `;
  }

  private _toggleOutlineCollapsed = () => {
    this._outlineCollapsed = !this._outlineCollapsed;
  };

  private _toggleInspectorCollapsed = () => {
    this._inspectorCollapsed = !this._inspectorCollapsed;
  };

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  render() {
    return html`
      <div
        data-prism-component="workflow-editor"
        data-prism-workflow-loaded="${this.workflowKey || this._workflow?.definitionKey || ''}"
        class="editor-root"
      >
        ${this._renderToast()}
        ${this._loading ? html`<div class="loading-banner" role="status">Loading workflow…</div>` : nothing}
        ${this._error ? html`<div class="error-banner" role="alert">${this._error}</div>` : nothing}
        ${this._renderSaveErrorSurface()}

        <!-- Tab-based navigation -->
        <prism-confidence-tabs
          class="editor-tabs"
          active-tab="${this._activeConfidenceTab}"
          error-count="${this._blockingValidationIssues.length}"
          warning-count="${this._warningValidationIssues.length}"
          @tab-changed=${this._handleConfidenceTabChanged}
        >
          <!-- Canvas tab: main workspace -->
          <div slot="canvas" class="canvas-workspace">
            <div
              class="editor-shell"
              style=${`--outline-width:${this._outlineCollapsed ? '3.5rem' : '240px'};--inspector-width:${this._inspectorCollapsed ? '3.5rem' : '380px'};`}
            >
              <!-- Left: outline -->
              <section class=${`editor-outline-shell ${this._outlineCollapsed ? 'panel-collapsed' : ''}`}>
                <div class="panel-header">
                  <div class="panel-header-copy">
                    <h2 class="panel-title">Outline</h2>
                    ${this._outlineCollapsed
                      ? nothing
                      : html`
                          <p class="panel-subtitle">
                            ${(this._workflow?.states.length ?? 0)} ${(this._workflow?.states.length ?? 0) === 1 ? 'stage' : 'stages'}
                            ${this._workflow?.metadata?.gateways?.length ? ` · ${this._workflow.metadata?.gateways.length} gateways` : ''}
                          </p>
                        `}
                  </div>
                  <button
                    type="button"
                    class="panel-toggle"
                    data-prism-outline-toggle
                    aria-controls="workflow-editor-outline-panel"
                    aria-expanded=${String(!this._outlineCollapsed)}
                    aria-label=${this._outlineCollapsed ? 'Expand outline panel' : 'Collapse outline panel'}
                    @click=${this._toggleOutlineCollapsed}
                  >
                    <span aria-hidden="true">${this._outlineCollapsed ? '⟩' : '⟨'}</span>
                    <span class="sr-only">${this._outlineCollapsed ? 'Expand outline' : 'Collapse outline'}</span>
                  </button>
                </div>
                <div
                  id="workflow-editor-outline-panel"
                  class="panel-body"
                  ?hidden=${this._outlineCollapsed}
                >
                  <prism-workflow-outline
                    class="editor-outline"
                    data-prism-workflow-outline
                    .workflow=${this._workflow}
                    .availableQueues=${this.availableQueues}
                    .selectedStageKey=${this._selectedStageKey}
                    .selectedGatewayKey=${this._selectedGatewayKey}
                    .selectedTransitionIndex=${this._selectedTransitionIndex}
                    .showHeader=${false}
                    @outline-stage-selected=${this._handleOutlineStageSelected}
                    @outline-gateway-selected=${this._handleOutlineGatewaySelected}
                    @outline-transition-selected=${this._handleOutlineTransitionSelected}
                  ></prism-workflow-outline>
                </div>
              </section>

              <!-- Center: graph workspace + toolbar -->
              <div class="editor-center">
                <div class="editor-header" role="none">
                  <h1 id="workflow-editor-title" class="editor-title">
                    ${this._workflow?.displayName ?? 'Workflow Editor'}
                  </h1>
                  <div class="editor-toolbar" role="toolbar" aria-label="Workflow editor tools">
                    <button
                      class="toolbar-btn govuk-button"
                      data-prism-save
                      ?disabled=${!this._canSave}
                      title=${!this._canSaveByContext ? 'Saving is disabled for the current author.' : nothing}
                      aria-keyshortcuts=${SAVE_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${this._handleSave}
                    >
                      ${this._saveState === 'saving' ? 'Saving…' : 'Save'}
                    </button>
                    <button
                      class="toolbar-btn govuk-button govuk-button--secondary"
                      data-prism-undo
                      ?disabled=${!this._canUndo}
                      aria-keyshortcuts=${UNDO_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${this._undo}
                    >
                      Undo
                    </button>
                    <button
                      class="toolbar-btn govuk-button govuk-button--secondary"
                      data-prism-redo
                      ?disabled=${!this._canRedo}
                      aria-keyshortcuts=${REDO_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${this._redo}
                    >
                      Redo
                    </button>
                    <button
                      class="toolbar-btn govuk-button govuk-button--secondary"
                      data-prism-copy
                      ?disabled=${!this._canCopy}
                      aria-keyshortcuts=${COPY_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${() => this._copySelection()}
                    >
                      Copy
                    </button>
                    <button
                      class="toolbar-btn govuk-button govuk-button--secondary"
                      data-prism-paste
                      ?disabled=${!this._canPaste}
                      aria-keyshortcuts=${PASTE_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${() => this._pasteClipboard()}
                    >
                      Paste
                    </button>
                    <button
                      class="toolbar-btn govuk-button govuk-button--secondary"
                      data-prism-help
                      aria-keyshortcuts=${HELP_SHORTCUT?.ariaKeys ?? nothing}
                      @click=${(event: Event) => this._openShortcutGuide(event.currentTarget as HTMLElement)}
                    >
                      Help
                    </button>
                    <span class="clipboard-chip" data-prism-clipboard-state>${this._clipboardSummary}</span>
                  </div>
                </div>
                <div class="editor-statusbar" data-prism-history-status>
                  <span class="status-chip">${this._dirtyStateSummary}</span>
                  <span class="status-chip">${this._canUndo ? 'Undo ready' : 'Undo idle'}</span>
                  <span class="status-chip">${this._canRedo ? 'Redo ready' : 'Redo idle'}</span>
                  <span class="status-chip">${this._hasBlockingValidationIssues ? 'Save blocked' : 'Save ready'}</span>
                  <span class="status-chip">Help F1</span>
                  <span class="status-text">${this._historyStatusSummary}</span>
                </div>
                ${(() => {
                  const errorCount = this._blockingValidationIssues.length;
                  const warningCount = this._warningValidationIssues.length;
                  const total = errorCount + warningCount;
                  if (total === 0) return nothing;
                  const summary = errorCount > 0 && warningCount > 0
                    ? `${errorCount} error${errorCount === 1 ? '' : 's'} and ${warningCount} warning${warningCount === 1 ? '' : 's'} need attention.`
                    : errorCount > 0
                      ? `${errorCount} validation error${errorCount === 1 ? '' : 's'} need attention.`
                      : `${warningCount} validation warning${warningCount === 1 ? '' : 's'} need attention.`;
                  return html`
                    <div
                      class=${`canvas-health-hint ${errorCount > 0 ? 'is-error' : 'is-warning'}`}
                      data-prism-canvas-health-hint
                      role="status"
                    >
                      <span class="canvas-health-summary">${summary}</span>
                      <button
                        type="button"
                        class="canvas-health-action"
                        data-prism-open-validation
                        @click=${() => { this._activeConfidenceTab = 'validation'; }}
                      >Open Validation</button>
                    </div>
                  `;
                })()}
                <div class="sr-only" role="status" aria-live="polite">${this._historyAnnouncement}</div>

                <prism-workflow-graph
                  class="graph-panel"
                  .workflow=${this._workflow}
                  .availableQueues=${this.availableQueues}
                  .selectedStageKey=${this._selectedStageKey}
                  .selectedGatewayKey=${this._selectedGatewayKey}
                  .selectedTransitionIndex=${this._selectedTransitionIndex}
                  .simulationCurrentStageKey=${this._simulationCurrentStage?.stateKey ?? null}
                  .simulationPathStageKeys=${this._simulation?.history.map(entry => entry.stageKey) ?? []}
                  .simulationPathTransitionIndices=${this._simulation?.pathTransitionIndices ?? []}
                  @stage-selected="${this._handleStageSelected}"
                  @gateway-selected="${this._handleGatewaySelected}"
                  @transition-selected="${this._handleTransitionSelected}"
                  @workflow-updated="${this._handleWorkflowUpdated}"
                  @inspector-requested="${this._handleInspectorRequested}"
                  @graph-multi-selection="${(event: CustomEvent<{ nodeIds: string[] }>) => {
                    this._graphMultiSelection = event.detail.nodeIds;
                  }}"
                ></prism-workflow-graph>
              </div>

              <!-- Right: inspector -->
              <section class=${`editor-right ${this._inspectorCollapsed ? 'panel-collapsed' : ''}`}>
                <div class="panel-header">
                  <div class="panel-header-copy">
                    <h2 class="panel-title">Properties</h2>
                    ${this._inspectorCollapsed
                      ? nothing
                      : html`<p class="panel-subtitle">Selected stage, gateway, or route details</p>`}
                  </div>
                  <button
                    type="button"
                    class="panel-toggle"
                    data-prism-inspector-toggle
                    aria-controls="workflow-editor-inspector-panel"
                    aria-expanded=${String(!this._inspectorCollapsed)}
                    aria-label=${this._inspectorCollapsed ? 'Expand properties drawer' : 'Collapse properties drawer'}
                    @click=${this._toggleInspectorCollapsed}
                  >
                    <span aria-hidden="true">${this._inspectorCollapsed ? '⟨' : '⟩'}</span>
                    <span class="sr-only">${this._inspectorCollapsed ? 'Expand properties drawer' : 'Collapse properties drawer'}</span>
                  </button>
                </div>
                <div
                  id="workflow-editor-inspector-panel"
                  class="panel-body"
                  ?hidden=${this._inspectorCollapsed}
                >
                  <prism-step-inspector
                    class="inspector-panel"
                    tabindex="0"
                    .workflow=${this._workflow}
                    .availableQueues=${this.availableQueues}
                    selected-stage-key="${this._selectedStageKey ?? ''}"
                    selected-gateway-key="${this._selectedGatewayKey ?? ''}"
                    .selectedActionIndex=${this._selectedActionIndex}
                    .selectedActionTransitionIndex=${this._selectedTransitionIndex}
                    .actionCatalog=${this._actionCatalog}
                    @workflow-updated=${this._handleWorkflowUpdated}
                    @action-selected=${this._handleActionSelected}
                  ></prism-step-inspector>
                </div>
              </section>
            </div>
          </div>

          <!-- Other tabs -->
          <div slot="validation">${this._renderValidationPanel()}</div>
          <div slot="preview">${this._renderStagePreview()}</div>
          <div slot="simulation">${this._renderSimulationPanel()}</div>
          <div slot="definition">${this._renderDefinitionPanel()}</div>
          <prism-help-panel slot="help"></prism-help-panel>
        </prism-confidence-tabs>

        ${this._renderShortcutGuide()}
      </div>
    `;
  }

  private _renderToast() {
    if (!this._toastMessage) return nothing;
    return html`
      <div
        class="toast-banner"
        role="status"
        aria-live="assertive"
        data-prism-toast
      >
        ${this._toastMessage}
      </div>
    `;
  }

  private _renderSaveErrorSurface() {
    if (!this._saveError) {
      return nothing;
    }

    return html`
      <section
        class="save-error-surface"
        aria-labelledby="workflow-save-error-title"
        tabindex="-1"
        data-prism-save-error
      >
        <div class="save-error-header">
          <p class="save-error-eyebrow">Save problem</p>
          <h2 id="workflow-save-error-title" class="save-error-title">${this._saveError.title}</h2>
          <p class="save-error-summary" role="alert">${this._saveError.summary}</p>
        </div>

        ${this._saveError.detailLines.length > 0
          ? html`
              <ul class="save-error-list">
                ${this._saveError.detailLines.map(line => html`<li>${line}</li>`)}
              </ul>
            `
          : nothing}

        ${this._saveError.traceId
          ? html`<p class="save-error-trace"><strong>Reference:</strong> ${this._saveError.traceId}</p>`
          : nothing}

        <label class="save-error-copy-label" for="workflow-save-error-details">Copyable save error details</label>
        <textarea
          id="workflow-save-error-details"
          class="save-error-copy-field"
          readonly
          rows="6"
          .value=${this._saveError.copyText}
          data-prism-save-error-details
        ></textarea>

        <div class="save-error-actions">
          <button
            type="button"
            class="toolbar-btn govuk-button govuk-button--secondary save-error-copy-button"
            data-prism-copy-save-error
            @click=${this._copySaveErrorDetails}
          >
            Copy details
          </button>
          <button
            type="button"
            class="toolbar-btn govuk-button govuk-button--secondary"
            aria-label="Dismiss save error"
            data-prism-dismiss-save-error
            @click=${() => { this._saveError = null; this._saveErrorCopyStatus = null; }}
          >
            Dismiss
          </button>
          <p class="save-error-copy-status" role="status" aria-live="polite" data-prism-save-error-copy-status>
            ${this._saveErrorCopyStatus ?? ''}
          </p>
        </div>
      </section>
    `;
  }

  // ---------------------------------------------------------------------------
  // Styles
  // ---------------------------------------------------------------------------

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 0;
      overflow: hidden;
      font-family: "GDS Transport", arial, sans-serif;
      font-size: 1rem;
      color: #0b0c0c;
      background: #f3f2f1;
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

    .editor-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      position: relative;
    }

    /* ---- Banners ---- */

    .loading-banner,
    .error-banner {
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
    }

    .loading-banner {
      background: #f0f4f9;
      color: #1d70b8;
    }

    .error-banner {
      background: #fce8e6;
      color: #d4351c;
    }

    .toast-banner {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 200;
      background: #00703c;
      color: #fff;
      padding: 0.75rem 1.25rem;
      border-radius: 4px;
      font-size: 1rem;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
    }

    .save-error-surface {
      margin: 1rem;
      padding: 1rem 1.25rem 1.25rem;
      border: 4px solid #d4351c;
      background: #ffffff;
      display: grid;
      gap: 0.875rem;
      box-shadow: 0 1px 4px rgba(11, 12, 12, 0.08);
    }

    .save-error-surface:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
    }

    .save-error-header,
    .save-error-actions {
      display: grid;
      gap: 0.5rem;
    }

    .save-error-eyebrow,
    .save-error-summary,
    .save-error-trace,
    .save-error-copy-label,
    .save-error-copy-status {
      margin: 0;
    }

    .save-error-eyebrow {
      font-size: 0.875rem;
      font-weight: 700;
      color: #b10e1e;
    }

    .save-error-title {
      margin: 0;
      font-size: 1.1875rem;
      font-weight: 700;
      color: #0b0c0c;
    }

    .save-error-summary,
    .save-error-trace,
    .save-error-copy-label,
    .save-error-copy-status {
      font-size: 0.9375rem;
      line-height: 1.5;
      color: #0b0c0c;
    }

    .save-error-list {
      margin: 0;
      padding-left: 1.25rem;
      display: grid;
      gap: 0.375rem;
    }

    .save-error-copy-label {
      font-weight: 700;
    }

    .save-error-copy-field {
      width: 100%;
      min-height: 8.5rem;
      resize: vertical;
      padding: 0.75rem;
      border: 2px solid #0b0c0c;
      border-radius: 4px;
      font: inherit;
      line-height: 1.5;
      color: #0b0c0c;
      background: #f8f8f8;
      box-sizing: border-box;
    }

    .save-error-copy-field:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
    }

    .save-error-actions {
      align-items: start;
    }

    .save-error-copy-button {
      justify-self: start;
    }

    /* ---- Tabs ---- */

    .editor-tabs {
      flex: 1;
      min-height: 0;
    }

    .canvas-workspace {
      height: 100%;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    /* ---- Shell ---- */

    .editor-shell {
      display: grid;
      grid-template-columns: var(--outline-width, 240px) 1fr var(--inspector-width, 380px);
      flex: 1;
      overflow: hidden;
      min-height: 0;
    }

    /* ---- Left panel ---- */

    .editor-outline-shell,
    .editor-right {
      min-width: 0;
      display: flex;
      flex-direction: column;
      overflow: hidden;
      background: #fff;
    }

    .editor-outline-shell {
      border-right: 2px solid #b1b4b6;
    }

    .panel-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.875rem 0.875rem 0.75rem;
      border-bottom: 1px solid #d8dde3;
      background: #ffffff;
      flex-shrink: 0;
    }

    .panel-header-copy {
      min-width: 0;
    }

    .panel-title {
      margin: 0;
      font-size: 1rem;
      font-weight: 700;
      color: #0b0c0c;
    }

    .panel-subtitle {
      margin: 0.25rem 0 0;
      font-size: 0.8125rem;
      color: #505a5f;
      line-height: 1.4;
    }

    .panel-toggle {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.25rem;
      min-width: 2.25rem;
      min-height: 2.25rem;
      border: 1px solid #b1b4b6;
      border-radius: 999px;
      background: #ffffff;
      color: #0b0c0c;
      cursor: pointer;
      font: inherit;
      font-weight: 700;
    }

    .panel-toggle:hover {
      background: #f3f2f1;
    }

    .panel-toggle:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .panel-body {
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    .panel-collapsed .panel-header {
      align-items: center;
      justify-content: center;
      padding: 0.75rem 0.5rem;
      min-height: 100%;
      border-bottom: none;
      writing-mode: vertical-rl;
      transform: rotate(180deg);
    }

    .panel-collapsed .panel-header-copy {
      display: contents;
    }

    .panel-collapsed .panel-title {
      font-size: 0.875rem;
    }

    .panel-collapsed .panel-toggle {
      transform: rotate(180deg);
    }

    .editor-outline {
      height: 100%;
    }

    .editor-center {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
      overflow: hidden;
    }

    .editor-header {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0.75rem 1rem;
      background: #1d70b8;
      color: #fff;
      flex-shrink: 0;
    }

    .editor-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      flex: 1;
    }

    .editor-toolbar {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 0.5rem;
    }

    .toolbar-btn,
    .mode-toggle-btn {
      font-size: 0.875rem;
      padding: 0.4rem 0.75rem;
      background: #fff;
      color: #1d70b8;
      border: 2px solid #fff;
      border-radius: 4px;
      cursor: pointer;
      font-weight: 600;
      white-space: nowrap;
      margin: 0;
    }

    .toolbar-btn[disabled],
    .mode-toggle-btn[disabled] {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .toolbar-btn:hover:not([disabled]),
    .mode-toggle-btn:hover:not([disabled]) {
      background: #e8f0fb;
    }

    .toolbar-btn:focus-visible,
    .mode-toggle-btn:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .clipboard-chip {
      display: inline-flex;
      align-items: center;
      max-width: 20rem;
      min-height: 2.25rem;
      padding: 0.35rem 0.75rem;
      border-radius: 999px;
      background: #ffffff;
      color: #003078;
      font-size: 0.8125rem;
      font-weight: 600;
      line-height: 1.35;
    }

    .editor-statusbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 1rem;
      background: #ffffff;
      border-bottom: 1px solid #b1b4b6;
      flex-shrink: 0;
      font-size: 0.875rem;
    }

    .status-chip {
      display: inline-flex;
      align-items: center;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      background: #d8dde3;
      color: #0b0c0c;
      font-weight: 700;
    }

    .status-text {
      color: #505a5f;
    }

    .canvas-health-hint {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 1rem;
      border-bottom: 1px solid #b1b4b6;
      font-size: 0.875rem;
      flex-shrink: 0;
    }

    .canvas-health-hint.is-error {
      background: #fef2f2;
      color: #7a1f1f;
    }

    .canvas-health-hint.is-warning {
      background: #fff7e6;
      color: #594400;
    }

    .canvas-health-summary {
      font-weight: 600;
    }

    .canvas-health-action {
      margin-left: auto;
      background: #ffffff;
      border: 2px solid currentColor;
      color: inherit;
      font-weight: 700;
      padding: 0.3rem 0.75rem;
      cursor: pointer;
      border-radius: 4px;
    }

    .canvas-health-action:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .graph-panel {
      flex: 1;
      overflow: hidden;
    }

    /* ---- Right panel ---- */

    .editor-right {
      border-left: 2px solid #b1b4b6;
    }

    /* ---- Confidence panel ---- */

    .validation-panel {
      padding: 1.5rem;
      background: #ffffff;
      display: grid;
      gap: 1rem;
      overflow-y: auto;
      height: 100%;
    }

    .inspector-panel {
      flex: 1;
      overflow: hidden;
      min-height: 0;
    }

    .validation-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }

    .validation-panel-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
    }

    .validation-panel-summary,
    .validation-empty,
    .validation-empty-panel,
    .validation-panel-save-status {
      margin: 0;
      color: #505a5f;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .validation-empty-panel {
      padding: 2rem 1.5rem;
      text-align: center;
      color: #626a6e;
    }

    .validation-panel-meta {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }

    .validation-count,
    .validation-save-label,
    .validation-issue-badge {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      padding: 0.2rem 0.55rem;
      font-size: 0.75rem;
      font-weight: 700;
      white-space: nowrap;
    }

    .validation-count-error,
    .validation-issue-badge-error {
      background: #f8d7da;
      color: #6f1d1b;
    }

    .validation-count-warning,
    .validation-issue-badge-warning {
      background: #fff1cc;
      color: #594100;
    }

    .validation-panel-save-status {
      display: flex;
      gap: 0.75rem;
      align-items: center;
      flex-wrap: wrap;
    }

    .validation-save-label {
      background: #d8dde3;
      color: #0b0c0c;
    }

    .validation-issue-list {
      list-style: none;
      padding: 0;
      margin: 0;
      display: grid;
      gap: 0.625rem;
    }

    .validation-issue-link {
      width: 100%;
      display: flex;
      align-items: flex-start;
      gap: 0.75rem;
      padding: 0.75rem 0.875rem;
      border: 1px solid #b1b4b6;
      border-radius: 6px;
      background: #ffffff;
      color: #0b0c0c;
      text-align: left;
      cursor: pointer;
      font: inherit;
    }

    .validation-issue-link:hover {
      background: #f8f8f8;
    }

    .validation-issue-link:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    /* ---- Modal overlay ---- */

    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(11, 12, 12, 0.65);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
      padding: 1rem;
    }

    .shortcut-dialog {
      width: min(64rem, 100%);
      max-height: 90vh;
      overflow: auto;
      padding: 1.25rem;
      border-radius: 16px;
      background: #ffffff;
      box-shadow: 0 24px 60px rgba(15, 23, 42, 0.28);
      display: grid;
      gap: 1rem;
    }

    .shortcut-dialog-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }

    .shortcut-dialog-eyebrow {
      margin: 0 0 0.25rem;
      color: #1d4ed8;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .shortcut-dialog-title {
      margin: 0;
      color: #0b0c0c;
      font-size: 1.35rem;
      line-height: 1.3;
    }

    .shortcut-dialog-copy {
      margin: 0.5rem 0 0;
      color: #505a5f;
      font-size: 0.9375rem;
      line-height: 1.5;
      max-width: 48rem;
    }

    .shortcut-dialog-close {
      flex-shrink: 0;
    }

    .shortcut-groups {
      display: grid;
      gap: 1rem;
    }

    .shortcut-group {
      border: 1px solid #d8dde3;
      border-radius: 12px;
      padding: 1rem;
      background: #f8f8f8;
    }

    .shortcut-group-title {
      margin: 0 0 0.875rem;
      font-size: 1rem;
      color: #0b0c0c;
    }

    .shortcut-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
    }

    .shortcut-item {
      display: grid;
      grid-template-columns: minmax(0, 2.2fr) minmax(12rem, 1fr) minmax(0, 1.3fr);
      gap: 0.875rem;
      align-items: start;
      padding: 0.875rem;
      border-radius: 10px;
      background: #ffffff;
      border: 1px solid #e5e7eb;
    }

    .shortcut-command,
    .shortcut-description,
    .shortcut-context {
      margin: 0;
    }

    .shortcut-command {
      color: #0b0c0c;
      font-weight: 700;
    }

    .shortcut-description,
    .shortcut-context {
      color: #505a5f;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .shortcut-description {
      margin-top: 0.25rem;
    }

    .shortcut-keys {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      align-items: center;
    }

    .shortcut-keys kbd {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 2rem;
      padding: 0.25rem 0.625rem;
      border: 1px solid #b1b4b6;
      border-bottom-width: 3px;
      border-radius: 6px;
      background: #ffffff;
      color: #0b0c0c;
      font-size: 0.8125rem;
      font-weight: 700;
      line-height: 1;
      white-space: nowrap;
    }

    @media (max-width: 960px) {
      .shortcut-item {
        grid-template-columns: 1fr;
      }
    }

    /* Responsive: Narrow viewports (tablets, small laptops) */
    @media (max-width: 1024px) {
      .editor-shell {
        grid-template-columns: var(--outline-width, 240px) 1fr var(--inspector-width, 320px);
      }

      .editor-header {
        flex-direction: column;
        gap: 0.75rem;
        align-items: stretch;
      }

      .editor-toolbar {
        flex-wrap: wrap;
      }
    }

    /* Responsive: Mobile/narrow screens */
    @media (max-width: 640px) {
      .editor-shell {
        grid-template-columns: var(--outline-width, 3.5rem) 1fr var(--inspector-width, 3.5rem);
      }

      .panel-collapsed {
        min-width: 3.5rem;
      }

      .panel-collapsed .panel-body {
        display: none;
      }

      .panel-collapsed .panel-header-copy {
        display: none;
      }

      .panel-toggle {
        writing-mode: vertical-rl;
        text-orientation: mixed;
        min-height: 8rem;
      }

      .editor-header {
        padding: 0.625rem 0.875rem;
      }

      .editor-title {
        font-size: 1.125rem;
      }

      .editor-toolbar {
        gap: 0.375rem;
      }

      .toolbar-btn {
        padding: 0.5rem 0.75rem;
        font-size: 0.875rem;
      }
    }

    /* ---- Definition tab ---- */

    .definition-panel {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 0;
      background: #ffffff;
    }

    .definition-header {
      padding: 1rem 1.25rem 0.75rem;
      border-bottom: 1px solid #b1b4b6;
    }

    .definition-title {
      margin: 0 0 0.25rem;
      font-size: 1.125rem;
      font-weight: 700;
    }

    .definition-subtitle {
      margin: 0;
      font-size: 0.875rem;
      color: #505a5f;
    }

    .definition-banner {
      margin: 0.75rem 1.25rem;
      padding: 0.875rem 1rem;
      background: #fbeaec;
      border-left: 4px solid #b10e1e;
      color: #0b0c0c;
      border-radius: 4px;
    }

    .definition-banner-summary {
      margin: 0 0 0.5rem;
      font-size: 0.9375rem;
    }

    .definition-banner-list {
      margin: 0 0 0.5rem 1.25rem;
      padding: 0;
      font-size: 0.875rem;
    }

    .definition-banner-actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .definition-banner-actions button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .definition-editor-frame {
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
      padding: 0 1.25rem 1.25rem;
    }

    .definition-editor-frame prism-definition-editor {
      flex: 1;
      min-height: 0;
      border: 1px solid #b1b4b6;
      border-radius: 4px;
      /* overflow: hidden removed — was blocking mouse wheel events from reaching CodeMirror */
    }

    .definition-loading,
    .definition-empty {
      margin: 1rem 1.25rem;
      font-size: 0.9375rem;
      color: #505a5f;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-editor': PrismWorkflowEditorElement;
  }
}
