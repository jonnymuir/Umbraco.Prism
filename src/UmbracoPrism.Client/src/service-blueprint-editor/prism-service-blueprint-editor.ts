import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import {
  type ActionCatalogEntry,
  type AuthoredAction,
  type AuthoredGateway,
  type AuthoredRoute,
  type AuthoredStage,
  type AuthoredServiceBlueprint,
  type ServiceBlueprintNodePosition,
  hydrateServiceBlueprintDefinition,
  serviceBlueprintGateways,
} from './types.js';
import { computeServiceBlueprintGraphLayout, parseGraphNodeId } from './graph/service-blueprint-graph-layout.js';
import { projectServiceBlueprintLocally } from './service-request-runtime-projection.js';
import { ServiceBlueprintSaveError, normaliseServiceBlueprintSaveError, type ServiceBlueprintSource } from './service-blueprint-source.js';
import type { ServiceBlueprintActionCatalog } from './action-catalog.js';
import { BuiltInServiceBlueprintActionCatalog } from './action-catalog.js';
import type { ServiceBlueprintAuthorContext } from './service-blueprint-author-context.js';
import type { QueueDefinition } from './stage-assignment.js';
import { availableContexts, contextForTiming, timingForContext, updateActionSummary } from './action-editing.js';
import { isTerminalStage, validateServiceBlueprint, type ServiceBlueprintValidationIssue } from './service-blueprint-validation.js';
import { flattenRoutes, newRouteId } from './route-model.js';
import { findServiceBlueprintShortcut, matchesShortcut, SERVICE_BLUEPRINT_SHORTCUT_GROUPS } from './editor-shortcuts.js';
import './prism-service-blueprint-graph.js';
import './prism-step-inspector.js';
import './prism-stage-preview.js';
import './prism-service-blueprint-simulation.js';
import './prism-service-blueprint-outline.js';
import './prism-confidence-tabs.js';
import './prism-help-panel.js';
import { serializeAuthoredServiceBlueprint, authoredServiceBlueprintJsonEquals } from './service-blueprint-canonical-json.js';
import {
  coerceParsedAuthoredServiceBlueprint,
  lintAuthoredServiceBlueprintDocument,
  type DefinitionLint,
} from './service-blueprint-lint.js';
import type { ConfidenceTab } from './prism-confidence-tabs.js';
import type {
  ServiceBlueprintSimulationHistoryEntry,
  ServiceBlueprintSimulationStopReason,
  ServiceBlueprintSimulationTransitionOption,
} from './prism-service-blueprint-simulation.js';
import type { ProjectServiceBlueprintResult, ProjectedServiceBlueprintState, ProjectedServiceBlueprintTransition } from './service-request-runtime-projection.js';

type ServiceBlueprintSelection =
  | { kind: 'stage'; stageKey: string }
  | { kind: 'gateway'; gatewayKey: string }
  | null;

type ServiceBlueprintHistoryEntry = {
  serviceBlueprint: AuthoredServiceBlueprint;
  selection: ServiceBlueprintSelection;
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
  history: ServiceBlueprintSimulationHistoryEntry[];
  pathTransitionIndices: number[];
};

const HISTORY_LIMIT = 50;
const SAVE_SHORTCUT = findServiceBlueprintShortcut('save');
const UNDO_SHORTCUT = findServiceBlueprintShortcut('undo');
const REDO_SHORTCUT = findServiceBlueprintShortcut('redo');
const COPY_SHORTCUT = findServiceBlueprintShortcut('copy');
const PASTE_SHORTCUT = findServiceBlueprintShortcut('paste');
const HELP_SHORTCUT = findServiceBlueprintShortcut('help');

function cloneServiceBlueprint(serviceBlueprint: AuthoredServiceBlueprint): AuthoredServiceBlueprint {
  return hydrateServiceBlueprintDefinition(JSON.parse(JSON.stringify(serviceBlueprint)) as AuthoredServiceBlueprint);
}

function cloneSelection(selection: ServiceBlueprintSelection): ServiceBlueprintSelection {
  return selection ? { ...selection } : null;
}

function cloneStage(stage: AuthoredStage): AuthoredStage {
  return JSON.parse(JSON.stringify(stage)) as AuthoredStage;
}

function cloneAction(action: AuthoredAction): AuthoredAction {
  return JSON.parse(JSON.stringify(action)) as AuthoredAction;
}

function serviceBlueprintsEqual(left: AuthoredServiceBlueprint | null, right: AuthoredServiceBlueprint | null): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function selectionsEqual(left: ServiceBlueprintSelection, right: ServiceBlueprintSelection): boolean {
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

function makeCopiedStageKey(baseStageKey: string, serviceBlueprint: AuthoredServiceBlueprint): string {
  const usedKeys = new Set(serviceBlueprint.stages.map(stage => stage.stateKey));
  let candidate = `${baseStageKey}-copy`;
  let suffix = 2;
  while (usedKeys.has(candidate)) {
    candidate = `${baseStageKey}-copy-${suffix}`;
    suffix += 1;
  }

  return candidate;
}

/**
 * Top-level editor host page composing the four V1 serviceBlueprint editor components.
 *
 * Layout:
 *   Left  — prism-service-blueprint-graph (with title bar + mode toggle)
 *   Right — prism-step-inspector
 *
 * URL param: ?serviceBlueprint=<key>  (default: "planning")
 * Prop: initialServiceBlueprint — set directly for Storybook / offline use; skips API fetch.
 *
 * Test hooks:
 *   data-prism-component="service-blueprint-editor"
 *   data-prism-service-blueprint-loaded="{key}" (reflected on the custom-element host once ready)
 *   data-prism-toast  (on the toast confirmation banner)
 *   data-prism-save-error (on the persistent save error surface)
 */
@customElement('prism-service-blueprint-editor')
export class PrismServiceBlueprintEditorElement extends LitElement {
  /** ServiceBlueprint key — read from ?serviceBlueprint= URL param or set directly. No implicit default: a
   * host must supply one (directly, or via the shell's own serviceBlueprint list/auto-select) — there
   * is no single serviceBlueprint name that's a sensible fallback across every possible host. */
  @property({ type: String, attribute: 'blueprint-key' })
  blueprintKey = '';

  /**
   * Host-supplied source the editor reads serviceBlueprints from and writes back to.
   * Required for runtime use; Storybook stories pass `initialServiceBlueprint` instead
   * and can leave this unset.
   */
  @property({ attribute: false })
  serviceBlueprintSource?: ServiceBlueprintSource;

  /**
   * Host-supplied catalog of action types the editor can render. Falls back
   * to Prism's built-in catalog when the host does not extend it.
   */
  @property({ attribute: false })
  actionCatalog?: ServiceBlueprintActionCatalog;

  /** Optional UX hint about the current author. Never authoritative. */
  @property({ attribute: false })
  authorContext?: ServiceBlueprintAuthorContext;

  /** Host-supplied queues used for queue labels and authoring pickers. */
  @property({ attribute: false })
  availableQueues: QueueDefinition[] = [];

  /**
   * If set, the component uses this service blueprint directly instead of fetching from
   * the API.  Designed for Storybook stories and offline walkthrough fixtures.
   */
  @property({ attribute: false })
  initialServiceBlueprint: AuthoredServiceBlueprint | null = null;

  @state() private _serviceBlueprint: AuthoredServiceBlueprint | null = null;
  @state() private _selection: ServiceBlueprintSelection = null;
  @state() private _selectedTransitionIndex: number | null = null;
  @state() private _toastMessage: string | null = null;
  private _toastDismissTimer: number | null = null;
  @state() private _serviceBlueprintStale = false;
  @state() private _staleCurrentVersion: number | null = null;
  @state() private _staleBannerDismissed = false;
  @state() private _loading = false;
  @state() private _error: string | null = null;
  @state() private _actionCatalog: ActionCatalogEntry[] = [];
  @state() private _undoHistory: ServiceBlueprintHistoryEntry[] = [];
  @state() private _redoHistory: ServiceBlueprintHistoryEntry[] = [];
  @state() private _historyAnnouncement = '';
  @state() private _actionSelection: ActionSelection = null;
  @state() private _clipboard: ClipboardEntry | null = null;

  /** Prefixed node ids from the canvas's shift-marquee multi-selection. */
  @state() private _graphMultiSelection: string[] = [];
  @state() private _saveState: SaveState = 'idle';
  @state() private _saveMessage: string | null = null;
  @state() private _saveError: ServiceBlueprintSaveError | null = null;
  @state() private _saveErrorCopyStatus: string | null = null;
  @state() private _helpOpen = false;
  @state() private _stagePreviewState: 'idle' | 'loading' | 'ready' | 'error' = 'idle';
  @state() private _stagePreviewError: string | null = null;
  @state() private _projectedServiceBlueprintPreview: ProjectServiceBlueprintResult | null = null;
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
  /** Canonical JSON of the service blueprint at the moment a Definition→Visual sync was committed. */
  private _lastAppliedDefinitionCanonical = '';
  private _definitionDebounceHandle: number | null = null;

  private _savedServiceBlueprintSnapshot: AuthoredServiceBlueprint | null = null;
  private _helpReturnTarget: HTMLElement | null = null;
  private _stagePreviewTimer: number | null = null;
  private _stagePreviewRequestId = 0;
  private _lastLoadedBlueprintKey: string | null = null;
  private _serviceBlueprintLoadRequestId = 0;
  private _versionPollTimer: number | null = null;

  private get _selectedStageKey(): string | null {
    return this._selection?.kind === 'stage' ? this._selection.stageKey : null;
  }

  private get _selectedGatewayKey(): string | null {
    return this._selection?.kind === 'gateway' ? this._selection.gatewayKey : null;
  }

  connectedCallback() {
    super.connectedCallback();
    this.addEventListener('keydown', this._handleEditorKeydown, true);
    this._reflectServiceBlueprintLoadedState();

    // Honour ?serviceBlueprint= URL param when running as a standalone page
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('serviceBlueprint');
      if (keyParam && !this.hasAttribute('blueprint-key')) {
        this.blueprintKey = keyParam;
      }
    }

    void this._loadActionCatalog();

    if (this.initialServiceBlueprint) {
      this._initialiseEditorState(this.initialServiceBlueprint);
      this._lastLoadedBlueprintKey = this.blueprintKey;
    } else {
      void this._loadServiceBlueprint();
    }
  }

  willUpdate(changedProperties: Map<string, unknown>) {
    // Watch for serviceBlueprint key changes and reload
    if (
      changedProperties.has('blueprintKey') &&
      this.blueprintKey !== this._lastLoadedBlueprintKey &&
      !this.initialServiceBlueprint
    ) {
      void this._loadServiceBlueprint();
    }
  }

  updated(_changedProperties: Map<string, unknown>) {
    this._refreshDefinitionTextFromServiceBlueprint();
    if (_changedProperties.has('_saveError') && this._saveError) {
      this.updateComplete.then(() => {
        this.shadowRoot?.querySelector<HTMLElement>('[data-prism-save-error]')?.focus();
      });
    }
  }

  disconnectedCallback() {
    this.removeEventListener('keydown', this._handleEditorKeydown, true);
    this._clearStagePreviewTimer();
    this._clearVersionPollTimer();
    if (this._toastDismissTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this._toastDismissTimer);
    }
    super.disconnectedCallback();
  }

  private async _loadServiceBlueprint() {
    const requestId = ++this._serviceBlueprintLoadRequestId;
    this._loading = true;
    this._error = null;
    this._reflectServiceBlueprintLoadedState();
    this._lastLoadedBlueprintKey = this.blueprintKey;

    if (!this.serviceBlueprintSource) {
      // Empty state — no source wired. The shell renders a developer
      // affordance; the editor element itself stays silently empty so
      // Storybook stories that drive it via `initialServiceBlueprint` are not
      // disturbed.
      this._serviceBlueprint = null;
      this._loading = false;
      this._reflectServiceBlueprintLoadedState();
      return;
    }

    try {
      const serviceBlueprint = await this.serviceBlueprintSource.load(this.blueprintKey);
      if (requestId !== this._serviceBlueprintLoadRequestId) {
        return;
      }
      this._initialiseEditorState(serviceBlueprint);
    } catch (err) {
      if (requestId !== this._serviceBlueprintLoadRequestId) {
        return;
      }
      this._error = err instanceof Error ? err.message : String(err);
      this._serviceBlueprint = null;
      this._reflectServiceBlueprintLoadedState();
    } finally {
      if (requestId === this._serviceBlueprintLoadRequestId) {
        this._loading = false;
      }
    }
  }

  private async _loadActionCatalog() {
    const catalog = this.actionCatalog ?? new BuiltInServiceBlueprintActionCatalog();
    this._actionCatalog = await catalog.entries();
  }

  private _initialiseEditorState(serviceBlueprint: AuthoredServiceBlueprint) {
    this._serviceBlueprint = cloneServiceBlueprint(serviceBlueprint);
    this._reflectServiceBlueprintLoadedState();
    this._savedServiceBlueprintSnapshot = cloneServiceBlueprint(this._serviceBlueprint);
    this._undoHistory = [];
    this._redoHistory = [];
    this._actionSelection = null;
    this._saveState = 'idle';
    this._saveMessage = null;
    this._saveError = null;
    this._saveErrorCopyStatus = null;
    this._projectedServiceBlueprintPreview = null;
    this._stagePreviewState = 'idle';
    this._stagePreviewError = null;
    this._simulation = null;
    this._simulationAnnouncement = '';
    this._lastAppliedDefinitionCanonical = '';
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];
    this._applySelection(null, this._serviceBlueprint);
    this._announceHistory('Service blueprint loaded. Undo history is ready for your next edit.');
    this._serviceBlueprintStale = false;
    this._staleCurrentVersion = null;
    this._staleBannerDismissed = false;
    this._scheduleVersionPoll();
  }

  private _reflectServiceBlueprintLoadedState() {
    const loadedKey = this.blueprintKey?.trim() || this._serviceBlueprint?.definitionKey?.trim();
    if (loadedKey) {
      this.setAttribute('data-prism-service-blueprint-loaded', loadedKey);
      return;
    }

    this.removeAttribute('data-prism-service-blueprint-loaded');
  }

  private get _selectedStage(): AuthoredStage | null {
    if (!this._serviceBlueprint || !this._selectedStageKey) {
      return null;
    }

    return this._serviceBlueprint.stages.find(stage => stage.stateKey === this._selectedStageKey) ?? null;
  }

  private get _previewedStage(): ProjectedServiceBlueprintState | null {
    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._projectedServiceBlueprintPreview) {
      return null;
    }

    return this._projectedServiceBlueprintPreview.file.stages.find(state => state.stateKey === selectedStage.stateKey) ?? null;
  }

  private get _previewedTransitions(): ProjectedServiceBlueprintTransition[] {
    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._projectedServiceBlueprintPreview || !this._serviceBlueprint) {
      return [];
    }

    const gatewayMap = new Map(serviceBlueprintGateways(this._serviceBlueprint).map(g => [g.key, g]));
    const stageRoutes = (this._projectedServiceBlueprintPreview.file.stages.find(stage => stage.stateKey === selectedStage.stateKey)?.routes ?? [])
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
    if (!this._serviceBlueprint) {
      return null;
    }

    return this._serviceBlueprint.stages.find(stage => stage.stateKey === this._serviceBlueprint?.initialStage) ?? null;
  }

  private get _simulationCurrentStage(): AuthoredStage | null {
    const simulation = this._simulation;
    if (!this._serviceBlueprint || !simulation) {
      return null;
    }

    return this._serviceBlueprint.stages.find(stage => stage.stateKey === simulation.currentStageKey) ?? null;
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
      ?? 'Pick an initial stage before you simulate this service blueprint.';
  }

  private get _simulationCanStart() {
    return Boolean(this._serviceBlueprint && this._initialSimulationStage);
  }

  private _simulationBlockersForTransition(transitionIndex: number) {
    if (!this._serviceBlueprint) {
      return [];
    }

    const transition = (flattenRoutes(this._serviceBlueprint))[transitionIndex];
    if (!transition) {
      return ['This transition is no longer available.'];
    }

    const targetStage = this._serviceBlueprint.stages.find(stage => stage.stateKey === transition.toStage);
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

  private get _simulationStopReason(): ServiceBlueprintSimulationStopReason {
    const currentStage = this._simulationCurrentStage;
    if (!currentStage || !this._simulation) {
      return null;
    }

    if (isTerminalStage(currentStage)) {
      return 'terminal';
    }

    return this._simulationTransitionOptions.length === 0 ? 'no-transitions' : null;
  }

  private get _simulationTransitionOptions(): ServiceBlueprintSimulationTransitionOption[] {
    if (!this._serviceBlueprint || !this._simulationCurrentStage) {
      return [];
    }

    return (flattenRoutes(this._serviceBlueprint))
      .map((transition, transitionIndex) => ({ transition, transitionIndex }))
      .filter(({ transition }) => transition.fromStage === this._simulationCurrentStage?.stateKey)
      .map(({ transition, transitionIndex }) => {
        const targetStage = this._serviceBlueprint?.stages.find(stage => stage.stateKey === transition.toStage) ?? null;
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

  private _currentSelection(): ServiceBlueprintSelection {
    return this._selection;
  }

  private _normaliseSelection(
    selection?: { kind: 'stage' | 'gateway' | 'transition'; stageKey?: string; gatewayKey?: string; transitionIndex?: number } | null
  ): ServiceBlueprintSelection {
    if (selection?.kind === 'stage' && selection.stageKey) {
      return { kind: 'stage', stageKey: selection.stageKey };
    }

    if (selection?.kind === 'gateway' && selection.gatewayKey) {
      return { kind: 'gateway', gatewayKey: selection.gatewayKey };
    }

    return null;
  }

  private _applySelection(selection: ServiceBlueprintSelection, serviceBlueprint: AuthoredServiceBlueprint | null = this._serviceBlueprint) {
    if (!serviceBlueprint) {
      this._selection = null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    if (selection?.kind === 'stage') {
      const exists = serviceBlueprint.stages.some(stage => stage.stateKey === selection.stageKey);
      this._selection = exists ? { kind: 'stage', stageKey: selection.stageKey } : null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    if (selection?.kind === 'gateway') {
      const exists = serviceBlueprint.metadata?.gateways?.some(gateway => gateway.key === selection.gatewayKey) ?? false;
      this._selection = exists ? { kind: 'gateway', gatewayKey: selection.gatewayKey } : null;
      this._selectedTransitionIndex = null;
      this._syncStagePreview();
      return;
    }

    this._selection = null;
    this._selectedTransitionIndex = null;
    this._syncStagePreview();
  }

  private _applyTransitionHighlight(transitionIndex: number, serviceBlueprint: AuthoredServiceBlueprint | null = this._serviceBlueprint) {
    const transitions = flattenRoutes(serviceBlueprint);
    if (!serviceBlueprint || transitionIndex < 0 || transitionIndex >= transitions.length) {
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

  /**
   * Proactive staleness check, not just reactive-on-save: while a service blueprint is open, poll
   * every 15s for whether someone else (a human, or an AI agent) has saved a newer version.
   * MVP — a `checkVersion(key) => { version }` scalar poll is cheap enough that it doesn't
   * need push infrastructure; Server-Sent Events is the natural upgrade path if that ever
   * stops being true. Only fires if the host's ServiceBlueprintSource implements `checkVersion` —
   * hosts that don't wire up versioning simply don't get this (no error, no polling).
   */
  private static readonly VERSION_POLL_INTERVAL_MS = 15_000;

  private _clearVersionPollTimer() {
    if (this._versionPollTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this._versionPollTimer);
    }
    this._versionPollTimer = null;
  }

  private _scheduleVersionPoll() {
    this._clearVersionPollTimer();

    // No point polling once we already know it's stale — nothing more to learn until reload.
    if (typeof window === 'undefined' || !this.serviceBlueprintSource?.checkVersion || !this._serviceBlueprint || this._serviceBlueprintStale) {
      return;
    }

    this._versionPollTimer = window.setTimeout(() => {
      void this._pollServiceBlueprintVersion();
    }, PrismServiceBlueprintEditorElement.VERSION_POLL_INTERVAL_MS);
  }

  /**
   * Single source of truth for "someone else saved a newer version" — set proactively by
   * polling or reactively by a failed save's 409. Locks the editor read-only (see
   * `_renderStaleServiceBlueprintOverlay`) until `_handleReloadAfterConflict` runs: any further edit
   * would just be heading toward another guaranteed conflict, so there's no honest "keep
   * working" option. The overlay always carries its own Reload action regardless of whether
   * the more detailed banner (`_renderStaleServiceBlueprintBanner`) has been dismissed — dismissing
   * that banner only hides the extra detail, it doesn't give back editing or lose Reload.
   */
  private _markServiceBlueprintStale(currentVersion: number | null) {
    this._serviceBlueprintStale = true;
    this._staleCurrentVersion = currentVersion;
    this._staleBannerDismissed = false;
    this._clearVersionPollTimer();
  }

  private async _pollServiceBlueprintVersion() {
    if (!this.serviceBlueprintSource?.checkVersion || !this._serviceBlueprint) {
      return;
    }

    const key = this.blueprintKey;
    const loadedVersion = this._serviceBlueprint.version;

    try {
      const currentVersion = await this.serviceBlueprintSource.checkVersion(key);
      // The user may have navigated to a different serviceBlueprint, or reloaded, while this was in flight.
      if (key !== this.blueprintKey || !this._serviceBlueprint) {
        return;
      }

      if (currentVersion !== null && currentVersion !== loadedVersion && !this._serviceBlueprintStale) {
        this._markServiceBlueprintStale(currentVersion);
      }
    } catch {
      // Best-effort — a transient poll failure shouldn't disrupt editing.
    } finally {
      this._scheduleVersionPoll();
    }
  }

  private _syncStagePreview() {
    this._clearStagePreviewTimer();

    const selectedStage = this._selectedStage;
    if (!selectedStage || !this._serviceBlueprint) {
      this._stagePreviewState = 'idle';
      this._stagePreviewError = null;
      this._projectedServiceBlueprintPreview = null;
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
    if (!this._serviceBlueprint || !this._selectedStage) {
      return;
    }

    const requestId = ++this._stagePreviewRequestId;
    this._stagePreviewState = 'loading';
    this._stagePreviewError = null;

    try {
      const preview = projectServiceBlueprintLocally(this._serviceBlueprint);
      if (requestId !== this._stagePreviewRequestId) {
        return;
      }

      this._projectedServiceBlueprintPreview = preview;
      this._stagePreviewState = 'ready';

      if (!preview.file.stages.some(state => state.stateKey === this._selectedStage?.stateKey)) {
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

  private _snapshotCurrentState(): ServiceBlueprintHistoryEntry | null {
    if (!this._serviceBlueprint) {
      return null;
    }

    return {
      serviceBlueprint: cloneServiceBlueprint(this._serviceBlueprint),
      selection: cloneSelection(this._currentSelection()),
    };
  }

  private _restoreHistoryEntry(entry: ServiceBlueprintHistoryEntry) {
    this._serviceBlueprint = cloneServiceBlueprint(entry.serviceBlueprint);
    this._applySelection(cloneSelection(entry.selection), this._serviceBlueprint);
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
    if (!this._serviceBlueprint) {
      return 'History unavailable until the service blueprint loads.';
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

  private get _validationIssues(): ServiceBlueprintValidationIssue[] {
    return this._serviceBlueprint ? validateServiceBlueprint(this._serviceBlueprint, this._actionCatalog) : [];
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
    return !serviceBlueprintsEqual(this._serviceBlueprint, this._savedServiceBlueprintSnapshot);
  }

  private get _canSave() {
    return Boolean(this._serviceBlueprint)
      && !this._hasBlockingValidationIssues
      && this._saveState !== 'saving'
      && this._canSaveByContext;
  }

  private get _dirtyStateSummary() {
    if (!this._serviceBlueprint) {
      return 'Service blueprint not loaded yet.';
    }

    return this._isDirty ? 'Unsaved changes' : 'All changes saved';
  }

  private get _validationStatusSummary() {
    if (!this._serviceBlueprint) {
      return 'Validation will appear when the service blueprint loads.';
    }

    if (this._validationIssues.length === 0) {
      return 'No validation issues. The service blueprint is ready to save.';
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
      return 'Saving serviceBlueprint changes…';
    }

    if (this._saveState === 'saved') {
      return this._saveMessage ?? 'Service blueprint changes saved.';
    }

    if (this._saveState === 'error') {
      return this._saveMessage ?? 'Save failed.';
    }

    if (this._hasBlockingValidationIssues) {
      return 'Save is blocked until the blocking validation errors are fixed.';
    }

    return this._saveMessage ?? 'Save is ready.';
  }

  private _commitServiceBlueprintUpdate(nextServiceBlueprint: AuthoredServiceBlueprint, nextSelection: ServiceBlueprintSelection) {
    const previousSelection = this._currentSelection();

    if (serviceBlueprintsEqual(this._serviceBlueprint, nextServiceBlueprint)) {
      if (!selectionsEqual(previousSelection, nextSelection)) {
        this._applySelection(nextSelection, nextServiceBlueprint);
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
    this._serviceBlueprint = nextServiceBlueprint;
    this._saveState = 'idle';
    this._saveMessage = null;
    this._resetSimulation(this._simulation ? 'Simulation reset because the service blueprint changed.' : undefined);
    this._applySelection(nextSelection, nextServiceBlueprint);
    this._announceHistory(`Change recorded. ${this._historyStatusSummary}`);
  }

  private _currentAction(): { action: AuthoredAction; target: 'stage' | 'transition' } | null {
    if (!this._serviceBlueprint || !this._actionSelection) {
      return null;
    }

    if (this._actionSelection.target === 'stage' && this._selectedStageKey) {
      const stage = this._serviceBlueprint.stages.find(candidate => candidate.stateKey === this._selectedStageKey);
      const action = stage?.actions?.[this._actionSelection.index];
      return action ? { action, target: 'stage' } : null;
    }

    if (this._actionSelection.target === 'transition' && this._selectedTransitionIndex !== null) {
      const transition = (flattenRoutes(this._serviceBlueprint))[this._selectedTransitionIndex];
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
    if (!this._serviceBlueprint || !this._clipboard) {
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
    this._announceHistory(`Undid the last serviceBlueprint change. ${this._historyStatusSummary}`);
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
    this._announceHistory(`Redid the service blueprint change. ${this._historyStatusSummary}`);
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
    this._applySelection({ kind: 'stage', stageKey: e.detail.stageKey }, this._serviceBlueprint);
    this._actionSelection = null;
  }

  private _handleGatewaySelected(e: CustomEvent<{ gatewayKey: string }>) {
    this._applySelection({ kind: 'gateway', gatewayKey: e.detail.gatewayKey }, this._serviceBlueprint);
    this._actionSelection = null;
  }

  private _handleTransitionSelected(e: CustomEvent<{ transitionIndex: number }>) {
    this._applyTransitionHighlight(e.detail.transitionIndex, this._serviceBlueprint);
    this._actionSelection = null;
  }

  private _handleActionSelected(e: CustomEvent<{ index: number | null; target: 'stage' | 'transition' }>) {
    this._actionSelection = e.detail.index === null
      ? null
      : { target: e.detail.target, index: e.detail.index };
  }

  private _handleServiceBlueprintUpdated(
    e: CustomEvent<{
      serviceBlueprint: AuthoredServiceBlueprint;
      selection?: { kind: 'stage' | 'gateway' | 'transition'; stageKey?: string; gatewayKey?: string; transitionIndex?: number } | null;
    }>
  ) {
    const nextServiceBlueprint = cloneServiceBlueprint(e.detail.serviceBlueprint);
    const detailSelection = e.detail.selection;
    // Transition selections (e.g. the route just created by drag-to-connect)
    // aren't part of ServiceBlueprintSelection — they live in the separate
    // _selectedTransitionIndex field alongside _applyTransitionHighlight.
    // _normaliseSelection has no case for them, so route this before it
    // drops the selection to null and leaves the properties panel empty.
    if (detailSelection?.kind === 'transition' && typeof detailSelection.transitionIndex === 'number') {
      this._commitServiceBlueprintUpdate(nextServiceBlueprint, null);
      this._applyTransitionHighlight(detailSelection.transitionIndex, nextServiceBlueprint);
      return;
    }
    const nextSelection = this._normaliseSelection(detailSelection);
    this._commitServiceBlueprintUpdate(nextServiceBlueprint, nextSelection);
  }

  private _handleInspectorRequested() {
    this._inspectorCollapsed = false;
    requestAnimationFrame(() => {
      this.shadowRoot?.querySelector<HTMLElement>('prism-step-inspector')?.focus();
    });
  }

  private _handleOutlineStageSelected = (e: CustomEvent<{ stageKey: string }>) => {
    this._applySelection({ kind: 'stage', stageKey: e.detail.stageKey }, this._serviceBlueprint);
    this._actionSelection = null;
  };

  private _handleOutlineGatewaySelected = (e: CustomEvent<{ gatewayKey: string }>) => {
    this._applySelection({ kind: 'gateway', gatewayKey: e.detail.gatewayKey }, this._serviceBlueprint);
    this._actionSelection = null;
    const gateway = this._serviceBlueprint?.metadata?.gateways?.find(g => g.key === e.detail.gatewayKey);
    if (gateway) {
      this._announceHistory(`Selected gateway ${gateway.displayName}`);
    }
  };

  private _handleOutlineTransitionSelected = (e: CustomEvent<{ transitionIndex: number }>) => {
    this._applyTransitionHighlight(e.detail.transitionIndex, this._serviceBlueprint);
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

  private _refreshDefinitionTextFromServiceBlueprint() {
    if (!this._serviceBlueprint) {
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
    const canonical = serializeAuthoredServiceBlueprint(this._serviceBlueprint);
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

    const issues = lintAuthoredServiceBlueprintDocument(parsed, source);
    if (issues.length > 0) {
      this._definitionParseError = null;
      this._definitionSchemaIssues = issues;
      return;
    }

    const next = coerceParsedAuthoredServiceBlueprint(parsed);
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];

    if (authoredServiceBlueprintJsonEquals(this._serviceBlueprint, next)) {
      // No semantic change — just remember the text the user typed.
      this._lastAppliedDefinitionCanonical = serializeAuthoredServiceBlueprint(next);
      return;
    }

    // Mark the canonical so the visual→definition sync doesn't echo this back.
    this._lastAppliedDefinitionCanonical = serializeAuthoredServiceBlueprint(next);
    this._commitServiceBlueprintUpdate(next, this._currentSelection());
    const stageCount = next.stages.length;
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
    if (!this._serviceBlueprint) {
      return;
    }
    if (this._definitionDebounceHandle !== null) {
      window.clearTimeout(this._definitionDebounceHandle);
      this._definitionDebounceHandle = null;
    }
    const canonical = serializeAuthoredServiceBlueprint(this._serviceBlueprint);
    this._definitionText = canonical;
    this._lastAppliedDefinitionCanonical = canonical;
    this._definitionParseError = null;
    this._definitionSchemaIssues = [];
    this._announceDefinition('Definition reverted to the current service blueprint.');
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

    if (!this._serviceBlueprint) {
      return false;
    }

    if (this._graphMultiSelection.length >= 2) {
      const selectedKeys = this._graphMultiSelection.map(parseGraphNodeId);
      const stages = this._serviceBlueprint.stages.filter(stage =>
        selectedKeys.some(parsed => parsed.kind === 'stage' && parsed.key === stage.stateKey));
      const gateways = serviceBlueprintGateways(this._serviceBlueprint).filter(gateway =>
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

    const stage = this._serviceBlueprint.stages.find(candidate => candidate.stateKey === this._selectedStageKey);
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
    if (!this._serviceBlueprint) {
      return false;
    }
    const serviceBlueprint = this._serviceBlueprint;

    const usedKeys = new Set<string>([
      ...serviceBlueprint.stages.map(stage => stage.stateKey),
      ...serviceBlueprintGateways(serviceBlueprint).map(gateway => gateway.key),
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
    const { layout } = computeServiceBlueprintGraphLayout(serviceBlueprint, this.availableQueues);
    const layoutNodes: Record<string, ServiceBlueprintNodePosition> = { ...(serviceBlueprint.layout?.nodes ?? {}) };
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

    const next: AuthoredServiceBlueprint = {
      ...serviceBlueprint,
      stages: [...serviceBlueprint.stages, ...pastedStages],
      gateways: [...serviceBlueprintGateways(serviceBlueprint), ...pastedGateways],
      layout: Object.keys(layoutNodes).length > 0 ? { nodes: layoutNodes } : serviceBlueprint.layout,
    };

    const firstStageKey = pastedStages[0]?.stateKey ?? null;
    this._commitServiceBlueprintUpdate(
      next,
      firstStageKey ? { kind: 'stage', stageKey: firstStageKey } : this._currentSelection()
    );
    this._showToast(`Pasted ${entry.label}.`);
    return true;
  }

  private _pasteClipboard() {
    if (!this._serviceBlueprint || !this._clipboard) {
      return false;
    }

    if (this._clipboard.kind === 'subgraph') {
      return this._pasteSubgraph(this._clipboard);
    }

    if (this._clipboard.kind === 'stage') {
      const copiedStage = cloneStage(this._clipboard.stage);
      const stageKey = makeCopiedStageKey(copiedStage.stateKey, this._serviceBlueprint);
      const pastedStage: AuthoredStage = {
        ...copiedStage,
        stateKey: stageKey,
      };

      const stages = [...this._serviceBlueprint.stages];
      const selectedStageIndex = this._selectedStageKey
        ? stages.findIndex(stage => stage.stateKey === this._selectedStageKey)
        : -1;
      const insertIndex = selectedStageIndex >= 0 ? selectedStageIndex + 1 : stages.length;
      stages.splice(insertIndex, 0, pastedStage);

      this._commitServiceBlueprintUpdate({ ...this._serviceBlueprint, stages: stages }, { kind: 'stage', stageKey });
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

    const stageIndex = this._serviceBlueprint.stages.findIndex(stage => stage.stateKey === currentSelection.stageKey);
    if (stageIndex < 0) {
      return false;
    }

    const stages = [...this._serviceBlueprint.stages];
    const nextActions = [...(stages[stageIndex].actions ?? []), pastedAction];
    stages[stageIndex] = { ...stages[stageIndex], actions: nextActions };
    this._commitServiceBlueprintUpdate({ ...this._serviceBlueprint, stages: stages }, currentSelection);
    this._actionSelection = { target: 'stage', index: nextActions.length - 1 };
    this._showToast(`Pasted action ${this._clipboard.label} into ${stages[stageIndex].displayName}.`);
    return true;
  }

  private _showToast(message: string) {
    if (this._toastDismissTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this._toastDismissTimer);
    }

    this._toastMessage = message;

    const dismiss = () => {
      this._toastMessage = null;
      this._toastDismissTimer = null;
    };

    this._toastDismissTimer = typeof window !== 'undefined'
      ? window.setTimeout(dismiss, 5000)
      : (setTimeout(dismiss, 5000) as unknown as number);
  }

  private _focusInspectorForValidationIssue(issue: ServiceBlueprintValidationIssue) {
    const actionLocation = issue.location.kind === 'action' ? issue.location : null;
    this._inspectorCollapsed = false;
    requestAnimationFrame(() => {
      const inspector = this.shadowRoot?.querySelector<HTMLElement>('prism-step-inspector');
      inspector?.focus();

      if (!actionLocation) {
        return;
      }

      requestAnimationFrame(() => {
        const actionEditor = inspector?.shadowRoot?.querySelector<HTMLElement>('prism-stage-action-editor');
        const selector = actionLocation.fieldKey && actionLocation.fieldKey !== 'fields'
          ? `[data-prism-action-param="${actionLocation.actionIndex}-${actionLocation.fieldKey}"]`
          : typeof actionLocation.formFieldIndex === 'number'
            ? `[data-prism-form-field-key="${actionLocation.actionIndex}-${actionLocation.formFieldIndex}"]`
            : `[data-prism-stage-action="${actionLocation.actionIndex}"]`;
        actionEditor?.shadowRoot?.querySelector<HTMLElement>(selector)?.focus();
      });
    });
  }

  /**
   * Jumps the canvas to a stage named by a save-time diagnostic's path — the server-side
   * counterpart to `_jumpToValidationIssue`'s stage branch, minus the `ServiceBlueprintValidationIssue`
   * object those diagnostics don't have. Selecting the stage is enough to guide someone to the
   * problem; the message itself (already shown in the save-error list) names the specific
   * component and field.
   */
  private _jumpToStage(stageKey: string) {
    if (!this._serviceBlueprint) {
      return;
    }

    this._activeConfidenceTab = 'canvas';
    this._inspectorCollapsed = false;
    this._applySelection({ kind: 'stage', stageKey }, this._serviceBlueprint);
    this._actionSelection = null;
  }

  private _jumpToValidationIssue(issue: ServiceBlueprintValidationIssue) {
    if (!this._serviceBlueprint) {
      return;
    }

    this._activeConfidenceTab = 'canvas';
    this._inspectorCollapsed = false;

    if (issue.location.kind === 'stage') {
      this._applySelection({ kind: 'stage', stageKey: issue.location.stageKey }, this._serviceBlueprint);
      this._actionSelection = null;
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'route') {
      const gatewayKey = issue.location.routeId;
      const routeId = issue.location.routeId;
      const transitions = flattenRoutes(this._serviceBlueprint);
      const targetIndex = transitions.findIndex(view =>
        view.key === gatewayKey && view.routeId === routeId
      );
      if (targetIndex >= 0) {
        this._applyTransitionHighlight(targetIndex, this._serviceBlueprint);
      }
      this._actionSelection = null;
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'action' && issue.location.target === 'route') {
      const gatewayKey = issue.location.routeId;
      const routeId = issue.location.routeId;
      const transitions = flattenRoutes(this._serviceBlueprint);
      const targetIndex = transitions.findIndex(view =>
        view.key === gatewayKey && view.routeId === routeId
      );
      this._applyTransitionHighlight(targetIndex >= 0 ? targetIndex : 0, this._serviceBlueprint);
      this._actionSelection = { target: 'transition', index: issue.location.actionIndex };
      this._focusInspectorForValidationIssue(issue);
      return;
    }

    if (issue.location.kind === 'action' && issue.location.target === 'stage') {
      this._applySelection({ kind: 'stage', stageKey: issue.location.stageKey ?? '' }, this._serviceBlueprint);
      this._actionSelection = { target: 'stage', index: issue.location.actionIndex };
      this._focusInspectorForValidationIssue(issue);
    }
  }

  private async _handleSave() {
    if (!this._serviceBlueprint) {
      return;
    }

    if (this._hasBlockingValidationIssues) {
      this._saveState = 'error';
      this._saveError = new ServiceBlueprintSaveError({
        title: 'Can’t save this service blueprint yet',
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

    if (!this.serviceBlueprintSource) {
      this._saveState = 'error';
      this._saveError = new ServiceBlueprintSaveError({
        title: 'Save unavailable',
        summary: 'No service blueprint source is wired to the editor.',
        detailLines: ['Connect a service blueprint source before trying to save.'],
      });
      this._saveMessage = this._saveError.summary;
      this._saveErrorCopyStatus = null;
      return;
    }

    try {
      await this.serviceBlueprintSource.save(this.blueprintKey, this._serviceBlueprint);
      // A successful save (no conflict thrown) means expectedVersion — this._serviceBlueprint.version at
      // the time of the call — matched what the store had, and every IServiceBlueprintSourceStore
      // increments by exactly 1 on that path. serviceBlueprintSource.save() returns void, not the new
      // version, so bump it locally rather than leaving _serviceBlueprint.version stale: left unbumped,
      // the next _pollServiceBlueprintVersion (15s later) compares that stale local version against the
      // real server version and false-positives "someone else changed this" against the editor's
      // own save.
      this._serviceBlueprint = cloneServiceBlueprint({ ...this._serviceBlueprint, version: this._serviceBlueprint.version + 1 });
      this._savedServiceBlueprintSnapshot = cloneServiceBlueprint(this._serviceBlueprint);
      this._saveState = 'saved';
      this._saveMessage = 'Service blueprint saved.';
      this._saveError = null;
      this._saveErrorCopyStatus = null;
      this._showToast(this._saveMessage);
    } catch (error) {
      const normalised = normaliseServiceBlueprintSaveError(
        error,
        'The editor couldn’t save your changes. Review the details below and try again.'
      );

      if (normalised.isConflict) {
        // Same treatment as a proactively-detected staleness — one consistent path (read-only
        // overlay + banner) regardless of whether we found out via polling or via this failed save.
        this._saveState = 'idle';
        this._saveMessage = null;
        this._saveError = null;
        this._saveErrorCopyStatus = null;
        this._markServiceBlueprintStale(normalised.currentVersion);
        return;
      }

      this._saveState = 'error';
      this._saveError = normalised;
      this._saveMessage = this._saveError.summary;
      this._saveErrorCopyStatus = null;
    }
  }

  private async _handleReloadAfterConflict() {
    this._saveState = 'idle';
    this._saveMessage = null;
    this._saveError = null;
    this._saveErrorCopyStatus = null;
    // Deliberately NOT clearing _serviceBlueprintStale here — that must stay true (read-only
    // overlay up, banner's Reload button available) until _loadServiceBlueprint actually succeeds.
    // _initialiseEditorState clears it on success. If the reload itself fails, we're
    // correctly still stale/read-only rather than briefly unlocked with old content.
    await this._loadServiceBlueprint();
    if (!this._serviceBlueprintStale) {
      this._showToast('Reloaded the latest version.');
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
    if (!this._serviceBlueprint || !this._simulation) {
      return;
    }

    const transition = (flattenRoutes(this._serviceBlueprint))[e.detail.transitionIndex];
    if (!transition) {
      return;
    }

    const blockers = this._simulationBlockersForTransition(e.detail.transitionIndex);
    if (blockers.length > 0) {
      this._announceSimulation(`Transition ${transition.action} is blocked by validation.`);
      return;
    }

    const nextStage = this._serviceBlueprint.stages.find(stage => stage.stateKey === transition.toStage);
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
      <prism-service-blueprint-simulation
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
      ></prism-service-blueprint-simulation>
    `;
  }

  private _renderValidationPanel() {
    if (!this._serviceBlueprint) {
      return html`<div class="validation-empty-panel">No serviceBlueprint loaded</div>`;
    }

    const issues = this._validationIssues;
    const errorCount = this._blockingValidationIssues.length;
    const warningCount = this._warningValidationIssues.length;

    return html`
      <section class="validation-panel" aria-labelledby="service-blueprint-validation-panel-title" data-prism-validation-rail>
        <div class="validation-panel-header">
          <div>
            <h2 id="service-blueprint-validation-panel-title" class="validation-panel-title">Service Blueprint validation</h2>
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
    if (!this._serviceBlueprint) {
      return html`<div class="definition-empty" data-prism-definition-empty>
        Loading the service blueprint definition…
      </div>`;
    }

    const banner = this._renderDefinitionBanner();
    const stageCount = this._serviceBlueprint.stages.length;
    const gatewayCount = this._serviceBlueprint.metadata?.gateways?.length ?? 0;

    return html`
      <div class="definition-panel" data-prism-definition-panel>
        <div class="definition-header">
          <div class="definition-header-copy">
            <h2 class="definition-title">Definition</h2>
            <p class="definition-subtitle">
              Power-user view of the authored serviceBlueprint.
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
      : this._definitionSchemaIssues[0]?.message ?? 'Definition does not match the service blueprint schema.';
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
          aria-labelledby="service-blueprint-shortcut-title"
          aria-describedby="service-blueprint-shortcut-copy"
          data-prism-shortcut-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeShortcutGuide())}
        >
          <div class="shortcut-dialog-header">
            <div>
              <p class="shortcut-dialog-eyebrow">Help and shortcuts</p>
              <h2 id="service-blueprint-shortcut-title" class="shortcut-dialog-title">Service Blueprint editor keyboard reference</h2>
              <p id="service-blueprint-shortcut-copy" class="shortcut-dialog-copy">
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
            ${SERVICE_BLUEPRINT_SHORTCUT_GROUPS.map(group => html`
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
        data-prism-component="service-blueprint-editor"
        data-prism-service-blueprint-loaded="${this.blueprintKey || this._serviceBlueprint?.definitionKey || ''}"
        class="editor-root"
      >
        ${this._renderToast()}
        ${this._loading ? html`<div class="loading-banner" role="status">Loading serviceBlueprint…</div>` : nothing}
        ${this._error ? html`<div class="error-banner" role="alert">${this._error}</div>` : nothing}
        ${this._renderSaveErrorSurface()}
        ${this._renderStaleServiceBlueprintBanner()}

        <!-- Tab-based navigation -->
        <div class="editor-content-wrapper">
        ${this._renderStaleServiceBlueprintOverlay()}
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
                            ${(this._serviceBlueprint?.stages.length ?? 0)} ${(this._serviceBlueprint?.stages.length ?? 0) === 1 ? 'stage' : 'stages'}
                            ${this._serviceBlueprint?.metadata?.gateways?.length ? ` · ${this._serviceBlueprint.metadata?.gateways.length} gateways` : ''}
                          </p>
                        `}
                  </div>
                  <button
                    type="button"
                    class="panel-toggle"
                    data-prism-outline-toggle
                    aria-controls="service-blueprint-editor-outline-panel"
                    aria-expanded=${String(!this._outlineCollapsed)}
                    aria-label=${this._outlineCollapsed ? 'Expand outline panel' : 'Collapse outline panel'}
                    @click=${this._toggleOutlineCollapsed}
                  >
                    <span aria-hidden="true">${this._outlineCollapsed ? '⟩' : '⟨'}</span>
                    <span class="sr-only">${this._outlineCollapsed ? 'Expand outline' : 'Collapse outline'}</span>
                  </button>
                </div>
                <div
                  id="service-blueprint-editor-outline-panel"
                  class="panel-body"
                  ?hidden=${this._outlineCollapsed}
                >
                  <prism-service-blueprint-outline
                    class="editor-outline"
                    data-prism-service-blueprint-outline
                    .serviceBlueprint=${this._serviceBlueprint}
                    .availableQueues=${this.availableQueues}
                    .selectedStageKey=${this._selectedStageKey}
                    .selectedGatewayKey=${this._selectedGatewayKey}
                    .selectedTransitionIndex=${this._selectedTransitionIndex}
                    .showHeader=${false}
                    @outline-stage-selected=${this._handleOutlineStageSelected}
                    @outline-gateway-selected=${this._handleOutlineGatewaySelected}
                    @outline-transition-selected=${this._handleOutlineTransitionSelected}
                  ></prism-service-blueprint-outline>
                </div>
              </section>

              <!-- Center: graph workspace + toolbar -->
              <div class="editor-center">
                <div class="editor-header" role="none">
                  <h1 id="service-blueprint-editor-title" class="editor-title">
                    ${this._serviceBlueprint?.displayName ?? 'Service Blueprint Editor'}
                  </h1>
                  <div class="editor-toolbar" role="toolbar" aria-label="ServiceBlueprint editor tools">
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

                <prism-service-blueprint-graph
                  class="graph-panel"
                  .serviceBlueprint=${this._serviceBlueprint}
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
                  @service-blueprint-updated="${this._handleServiceBlueprintUpdated}"
                  @inspector-requested="${this._handleInspectorRequested}"
                  @graph-multi-selection="${(event: CustomEvent<{ nodeIds: string[] }>) => {
                    this._graphMultiSelection = event.detail.nodeIds;
                  }}"
                ></prism-service-blueprint-graph>
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
                    aria-controls="service-blueprint-editor-inspector-panel"
                    aria-expanded=${String(!this._inspectorCollapsed)}
                    aria-label=${this._inspectorCollapsed ? 'Expand properties drawer' : 'Collapse properties drawer'}
                    @click=${this._toggleInspectorCollapsed}
                  >
                    <span aria-hidden="true">${this._inspectorCollapsed ? '⟨' : '⟩'}</span>
                    <span class="sr-only">${this._inspectorCollapsed ? 'Expand properties drawer' : 'Collapse properties drawer'}</span>
                  </button>
                </div>
                <div
                  id="service-blueprint-editor-inspector-panel"
                  class="panel-body"
                  ?hidden=${this._inspectorCollapsed}
                >
                  <prism-step-inspector
                    class="inspector-panel"
                    tabindex="0"
                    .serviceBlueprint=${this._serviceBlueprint}
                    .availableQueues=${this.availableQueues}
                    selected-stage-key="${this._selectedStageKey ?? ''}"
                    selected-gateway-key="${this._selectedGatewayKey ?? ''}"
                    .selectedActionIndex=${this._selectedActionIndex}
                    .selectedActionTransitionIndex=${this._selectedTransitionIndex}
                    .actionCatalog=${this._actionCatalog}
                    @service-blueprint-updated=${this._handleServiceBlueprintUpdated}
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
        </div>

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

  /**
   * Detailed, dismissible notice at the top of the editor. Dismissing this only hides the
   * detail — it does not clear `_serviceBlueprintStale`, so the read-only overlay (which has its own,
   * always-present Reload action) stays in effect. The only reason to dismiss without
   * reloading is to look at your own in-progress changes first, per the read-only overlay
   * leaving content visible rather than hiding it.
   */
  private _renderStaleServiceBlueprintBanner() {
    if (!this._serviceBlueprintStale || this._staleBannerDismissed) {
      return nothing;
    }

    return html`
      <section
        class="stale-service-blueprint-banner"
        aria-labelledby="service-blueprint-stale-title"
        tabindex="-1"
        data-prism-stale-service-blueprint-banner
      >
        <div class="stale-service-blueprint-header">
          <p class="stale-service-blueprint-eyebrow">Changed elsewhere</p>
          <h2 id="service-blueprint-stale-title" class="stale-service-blueprint-title">This service blueprint was updated elsewhere</h2>
          <p class="stale-service-blueprint-summary" role="alert">
            Someone else — a person in the editor, or an AI agent — saved a newer version
            ${this._staleCurrentVersion != null ? html`(now at version ${this._staleCurrentVersion})` : ''}
            while you were editing. The editor is read-only until you reload; reloading
            replaces your current view with the latest version, so copy anything you want to
            keep first.
          </p>
        </div>
        <div class="stale-service-blueprint-actions">
          <button
            type="button"
            class="toolbar-btn govuk-button"
            data-prism-reload-after-conflict
            @click=${this._handleReloadAfterConflict}
          >
            Reload latest version
          </button>
          <button
            type="button"
            class="toolbar-btn govuk-button govuk-button--secondary"
            aria-label="Dismiss — I just want to look at my changes first"
            data-prism-dismiss-stale-banner
            @click=${() => { this._staleBannerDismissed = true; }}
          >
            Dismiss
          </button>
        </div>
      </section>
    `;
  }

  /**
   * Blocks interaction with the canvas/inspector while `_serviceBlueprintStale` — any edit made now
   * would just be heading toward another conflict. Deliberately a translucent scrim, not an
   * opaque one: the whole point of letting someone dismiss the detailed banner above is so
   * they can still see their own in-progress content before reloading over it. Always carries
   * its own Reload action so it's reachable regardless of whether that banner was dismissed.
   */
  private _renderStaleServiceBlueprintOverlay() {
    if (!this._serviceBlueprintStale) {
      return nothing;
    }

    return html`
      <div class="stale-service-blueprint-overlay" data-prism-stale-service-blueprint-overlay>
        <div class="stale-service-blueprint-overlay-ribbon" role="status">
          <span>Read-only — this service blueprint changed elsewhere.</span>
          <button
            type="button"
            class="toolbar-btn govuk-button stale-service-blueprint-overlay-reload"
            data-prism-reload-after-conflict-overlay
            @click=${this._handleReloadAfterConflict}
          >
            Reload latest version
          </button>
        </div>
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
        aria-labelledby="service-blueprint-save-error-title"
        tabindex="-1"
        data-prism-save-error
      >
        <div class="save-error-header">
          <p class="save-error-eyebrow">Save problem</p>
          <h2 id="service-blueprint-save-error-title" class="save-error-title">${this._saveError.title}</h2>
          ${this._saveError.summaryStageKey
            ? html`
                <p class="save-error-summary" role="alert">
                  <button
                    type="button"
                    class="save-error-detail-link"
                    data-prism-save-error-jump
                    @click=${() => this._jumpToStage(this._saveError!.summaryStageKey!)}
                  >
                    ${this._saveError.summary}
                    <span class="save-error-detail-link-hint">Go to stage</span>
                  </button>
                </p>
              `
            : html`<p class="save-error-summary" role="alert">${this._saveError.summary}</p>`}
        </div>

        ${this._saveError.details.length > 0
          ? html`
              <ul class="save-error-list">
                ${this._saveError.details.map(detail => html`
                  <li>
                    ${detail.stageKey
                      ? html`
                          <button
                            type="button"
                            class="save-error-detail-link"
                            data-prism-save-error-jump
                            @click=${() => this._jumpToStage(detail.stageKey!)}
                          >
                            ${detail.message}
                            <span class="save-error-detail-link-hint">Go to stage</span>
                          </button>
                        `
                      : detail.message}
                  </li>
                `)}
              </ul>
            `
          : nothing}

        ${this._saveError.traceId
          ? html`<p class="save-error-trace"><strong>Reference:</strong> ${this._saveError.traceId}</p>`
          : nothing}

        <label class="save-error-copy-label" for="service-blueprint-save-error-details">Copyable save error details</label>
        <textarea
          id="service-blueprint-save-error-details"
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

    .save-error-detail-link {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
      border: none;
      background: none;
      padding: 0;
      color: #1d70b8;
      text-decoration: underline;
      text-align: left;
      cursor: pointer;
      font: inherit;
    }

    .save-error-detail-link:hover {
      color: #003078;
    }

    .save-error-detail-link:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .save-error-detail-link-hint {
      flex-shrink: 0;
      font-size: 0.8125rem;
      text-decoration: none;
      color: #505a5f;
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

    /* ---- Stale serviceBlueprint (version conflict) ---- */

    .stale-service-blueprint-banner {
      margin: 1rem;
      padding: 1rem 1.25rem 1.25rem;
      border: 4px solid #f47738;
      background: #fff7f0;
      display: grid;
      gap: 0.875rem;
      box-shadow: 0 1px 4px rgba(11, 12, 12, 0.08);
    }

    .stale-service-blueprint-banner:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
    }

    .stale-service-blueprint-header {
      display: grid;
      gap: 0.5rem;
    }

    .stale-service-blueprint-eyebrow,
    .stale-service-blueprint-summary {
      margin: 0;
    }

    .stale-service-blueprint-eyebrow {
      font-weight: 700;
      text-transform: uppercase;
      font-size: 0.8rem;
      letter-spacing: 0.03em;
      color: #b35900;
    }

    .stale-service-blueprint-title {
      margin: 0;
      font-size: 1.2rem;
    }

    .stale-service-blueprint-actions {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .editor-content-wrapper {
      position: relative;
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
    }

    .stale-service-blueprint-overlay {
      position: absolute;
      inset: 0;
      z-index: 150;
      background: rgba(255, 247, 240, 0.55);
      cursor: not-allowed;
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .stale-service-blueprint-overlay-ribbon {
      margin-top: 0.75rem;
      background: #f47738;
      color: #0b0c0c;
      padding: 0.5rem 1rem;
      border-radius: 4px;
      display: flex;
      align-items: center;
      gap: 1rem;
      font-weight: 600;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
      cursor: default;
    }

    .stale-service-blueprint-overlay-reload {
      flex-shrink: 0;
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
    'prism-service-blueprint-editor': PrismServiceBlueprintEditorElement;
  }
}
