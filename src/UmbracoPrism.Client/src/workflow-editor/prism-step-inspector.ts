import { LitElement, css, html, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type {
  ActionCatalogEntry,
  AuthoredAction,
  AuthoredGateway,
  AuthoredStage,
  AuthoredTransition,
  AuthoredWorkflow,
  EditorStageType,
} from './types.js';
import {
  editorStageTypeToStageKind,
  stageKindToEditorStageType,
} from './types.js';
import {
  applyLaneToStage,
  stageLaneKey,
  stageLaneLabel,
  workflowLaneOptions,
} from './workflow-stage-assignment.js';
import { deriveGatewayBindings, gatewayLaneKey } from './workflow-gateway-representation.js';
import {
  describeTransitionCondition,
  parseTransitionCondition,
  serialiseTransitionCondition,
  TRANSITION_ACTION_OPTIONS,
  transitionQuickAction,
  type TransitionConditionMode,
} from './workflow-transition-editing.js';
import {
  isTerminalStage,
  workflowDeadEndStages,
  workflowOrphanedStages,
  workflowOutgoingTransitions,
  workflowUnreachableStages,
} from './workflow-validation.js';
import './prism-workflow-action-editor.js';
import './prism-inline-help.js';

const STAGE_TYPE_OPTIONS: Array<{ value: EditorStageType; label: string }> = [
  { value: 'form', label: 'Form' },
  { value: 'review', label: 'Review' },
  { value: 'decision', label: 'Decision' },
  { value: 'confirmation', label: 'Confirmation' },
  { value: 'system-work', label: 'System work' },
];

type GraphSelectionDetail = {
  kind: 'stage' | 'transition' | 'gateway';
  stageKey?: string;
  transitionIndex?: number;
  gatewayKey?: string;
};

type WorkflowUpdatedDetail = {
  workflow: AuthoredWorkflow;
  selection?: GraphSelectionDetail | null;
};

type ActionsUpdatedDetail = {
  actions: AuthoredAction[];
};

type ActionSelectedDetail = {
  index: number | null;
  target: 'stage' | 'transition';
};

@customElement('prism-step-inspector')
export class PrismStepInspectorElement extends LitElement {
  @property({ attribute: false })
  workflow: AuthoredWorkflow | null = null;

  @property({ type: String, attribute: 'selected-stage-key' })
  selectedStageKey: string | null = null;

  @property({ type: Number, attribute: false })
  selectedTransitionIndex: number | null = null;

  @property({ type: String, attribute: 'selected-gateway-key' })
  selectedGatewayKey: string | null = null;

  @property({ attribute: false })
  actionCatalog: ActionCatalogEntry[] = [];

  @property({ type: Number, attribute: false })
  selectedActionIndex: number | null = null;

  @state() private _stageKeyError: string | null = null;
  @state() private _statusMessage: string | null = null;

  private get _selectedStage(): AuthoredStage | null {
    if (!this.workflow || !this.selectedStageKey) {
      return null;
    }

    return this.workflow.stages.find(stage => stage.stageKey === this.selectedStageKey) ?? null;
  }

  private get _selectedTransition(): AuthoredTransition | null {
    if (!this.workflow || this.selectedTransitionIndex === null) {
      return null;
    }

    return this.workflow.transitions[this.selectedTransitionIndex] ?? null;
  }

  private get _selectedGateway(): AuthoredGateway | null {
    if (!this.workflow || !this.selectedGatewayKey) {
      return null;
    }

    return this.workflow.gateways?.find(gateway => gateway.gatewayKey === this.selectedGatewayKey) ?? null;
  }

  protected updated(changed: Map<string, unknown>) {
    if (changed.has('selectedStageKey')) {
      this._stageKeyError = null;
    }
    if (changed.has('selectedGatewayKey')) {
      this._gatewayKeyError = null;
    }
  }

  private _announce(message: string) {
    this._statusMessage = '';
    requestAnimationFrame(() => {
      this._statusMessage = message;
    });
  }

  private _emitWorkflowUpdated(workflow: AuthoredWorkflow, selection?: GraphSelectionDetail | null) {
    this.dispatchEvent(
      new CustomEvent<WorkflowUpdatedDetail>('workflow-updated', {
        detail: { workflow, selection },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _handleActionSelected(event: CustomEvent<ActionSelectedDetail>) {
    event.stopPropagation();
    this.dispatchEvent(
      new CustomEvent<ActionSelectedDetail>('action-selected', {
        detail: event.detail,
        bubbles: true,
        composed: true,
      })
    );
  }

  private _stageLabel(stageKey: string) {
    return this.workflow?.stages.find(stage => stage.stageKey === stageKey)?.displayName ?? stageKey;
  }

  private _gatewayLabel(gatewayKey: string) {
    return this.workflow?.gateways?.find(gateway => gateway.gatewayKey === gatewayKey)?.displayName ?? gatewayKey;
  }

  private _routeDescriptor(transition: AuthoredTransition) {
    return [
      this._stageLabel(transition.fromStage),
      transition.fromGateway ? this._gatewayLabel(transition.fromGateway) : null,
      transition.toGateway ? this._gatewayLabel(transition.toGateway) : null,
      this._stageLabel(transition.toStage),
    ].filter(Boolean).join(' → ');
  }

  private _availableSplitGatewaysForStage(stageKey: string) {
    if (!this.workflow) {
      return [];
    }

    const stage = this.workflow.stages.find(candidate => candidate.stageKey === stageKey);
    const laneKey = stage ? stageLaneKey(stage) : '';

    return deriveGatewayBindings(this.workflow)
      .filter(binding => binding.gateway.kind === 'Split')
      .filter(binding => binding.anchorStageKey === stageKey || (!binding.anchorStageKey && binding.laneKey === laneKey))
      .map(binding => binding.gateway);
  }

  private _availableJoinGatewaysForStage(stageKey: string) {
    if (!this.workflow) {
      return [];
    }

    const stage = this.workflow.stages.find(candidate => candidate.stageKey === stageKey);
    const laneKey = stage ? stageLaneKey(stage) : '';

    return deriveGatewayBindings(this.workflow)
      .filter(binding => binding.gateway.kind === 'Join')
      .filter(binding => binding.anchorStageKey === stageKey || (!binding.anchorStageKey && binding.laneKey === laneKey))
      .map(binding => binding.gateway);
  }

  private _selectedStageOutgoing(stage: AuthoredStage) {
    return this.workflow ? workflowOutgoingTransitions(this.workflow, stage.stageKey) : [];
  }

  private _replaceSelectedTransition(nextTransition: AuthoredTransition) {
    if (!this.workflow || this.selectedTransitionIndex === null) {
      return;
    }

    const transitions = [...this.workflow.transitions];
    if (!transitions[this.selectedTransitionIndex]) {
      return;
    }

    transitions[this.selectedTransitionIndex] = nextTransition;
    this._emitWorkflowUpdated(
      { ...this.workflow, transitions },
      { kind: 'transition', transitionIndex: this.selectedTransitionIndex }
    );
  }

  private _replaceSelectedStage(nextStage: AuthoredStage, previousStageKey = this._selectedStage?.stageKey) {
    if (!this.workflow || !previousStageKey) {
      return;
    }

    const stageIndex = this.workflow.stages.findIndex(stage => stage.stageKey === previousStageKey);
    if (stageIndex < 0) {
      return;
    }

    const stages = [...this.workflow.stages];
    stages[stageIndex] = nextStage;

    let transitions = [...this.workflow.transitions];
    let initialStageKey = this.workflow.initialStageKey;

    if (nextStage.stageKey !== previousStageKey) {
      transitions = transitions.map(transition => ({
        ...transition,
        fromStage: transition.fromStage === previousStageKey ? nextStage.stageKey : transition.fromStage,
        toStage: transition.toStage === previousStageKey ? nextStage.stageKey : transition.toStage,
      }));
      if (initialStageKey === previousStageKey) {
        initialStageKey = nextStage.stageKey;
      }
    }

    const workflow: AuthoredWorkflow = {
      ...this.workflow,
      initialStageKey,
      stages,
      transitions,
    };

    this._emitWorkflowUpdated(workflow, { kind: 'stage', stageKey: nextStage.stageKey });
  }

  private _updateSelectedStageActions(event: CustomEvent<ActionsUpdatedDetail>) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    this._replaceSelectedStage({
      ...stage,
      actions: event.detail.actions,
    });
  }

  private _updateSelectedTransitionActions(event: CustomEvent<ActionsUpdatedDetail>) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    this._replaceSelectedTransition({
      ...transition,
      actions: event.detail.actions,
    });
  }

  private _updateStageTitle(event: Event) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    const nextTitle = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextTitle || nextTitle === stage.displayName) {
      return;
    }

    this._replaceSelectedStage({ ...stage, displayName: nextTitle });
    this._announce(`${nextTitle} title updated.`);
  }

  private _updateStageKey(event: Event) {
    const stage = this._selectedStage;
    if (!stage || !this.workflow) {
      return;
    }

    const nextKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextKey) {
      this._stageKeyError = 'Stage key is required.';
      this._announce('Stage key is required.');
      return;
    }

    const duplicate = this.workflow.stages.some(candidate =>
      candidate.stageKey === nextKey && candidate.stageKey !== stage.stageKey
    );
    if (duplicate) {
      this._stageKeyError = 'Stage key must be unique.';
      this._announce(`Stage key ${nextKey} is already in use.`);
      return;
    }

    if (nextKey === stage.stageKey) {
      this._stageKeyError = null;
      return;
    }

    this._stageKeyError = null;
    this._replaceSelectedStage({ ...stage, stageKey: nextKey }, stage.stageKey);
    this._announce(`Stage key updated to ${nextKey}.`);
  }

  private _updateStageDescription(event: Event) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    const nextDescription = (event.currentTarget as HTMLTextAreaElement).value.trim();
    const previousDescription = stage.description?.trim() ?? '';
    if (nextDescription === previousDescription) {
      return;
    }

    this._replaceSelectedStage({
      ...stage,
      description: nextDescription || undefined,
    });
    this._announce(`${stage.displayName} description updated.`);
  }

  private _updateStageLane(event: Event) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    const laneKey = (event.currentTarget as HTMLInputElement).value;
    const nextStage = applyLaneToStage(stage, laneKey);

    this._replaceSelectedStage(nextStage);
    this._announce(`${stage.displayName} lane updated.`);
  }

  private _updateStageType(event: Event) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    const nextType = (event.currentTarget as HTMLSelectElement).value as EditorStageType;
    const nextKind = editorStageTypeToStageKind(nextType);
    const nextStage: AuthoredStage = {
      ...stage,
      kind: nextKind,
      waiting: nextKind === 'Waiting' ? stage.waiting ?? { allowDefer: false } : stage.waiting,
    };

    this._replaceSelectedStage(nextStage);
    this._announce(`${stage.displayName} type updated.`);
  }

  private _updateTransitionLabel(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const action = (event.currentTarget as HTMLInputElement).value.trim();
    if (!action || action === transition.action) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, action });
    this._announce(`Route label updated to ${action}.`);
  }

  private _updateTransitionActionPreset(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const nextAction = (event.currentTarget as HTMLSelectElement).value;
    if (nextAction === 'custom' || nextAction === transition.action) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, action: nextAction });
    this._announce(`Route preset updated to ${nextAction}.`);
  }

  private _updateTransitionTarget(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const toStage = (event.currentTarget as HTMLSelectElement).value;
    if (!toStage || toStage === transition.toStage) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, toStage });
    this._announce(`Route now arrives at ${this._stageLabel(toStage)}.`);
  }

  private _updateTransitionFromGateway(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const fromGateway = (event.currentTarget as HTMLSelectElement).value || undefined;
    if (fromGateway === transition.fromGateway) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, fromGateway });
    this._announce(
      fromGateway
        ? `Route now leaves through ${this._gatewayLabel(fromGateway)}.`
        : 'Route now leaves directly from the stage.'
    );
  }

  private _updateTransitionToGateway(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const toGateway = (event.currentTarget as HTMLSelectElement).value || undefined;
    if (toGateway === transition.toGateway) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, toGateway });
    this._announce(
      toGateway
        ? `Route now arrives through ${this._gatewayLabel(toGateway)}.`
        : 'Route now arrives directly at the target stage.'
    );
  }

  private _updateTransitionConditionMode(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const mode = (event.currentTarget as HTMLSelectElement).value as TransitionConditionMode;
    const current = parseTransitionCondition(transition.condition);
    const condition = serialiseTransitionCondition(mode, mode === current.mode ? current.value : '');

    this._replaceSelectedTransition({ ...transition, condition });
    this._announce(
      mode === 'always'
        ? 'Route condition cleared.'
        : `${mode === 'event' ? 'Event' : 'Guard'} condition enabled.`
    );
  }

  private _updateTransitionConditionValue(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const parsed = parseTransitionCondition(transition.condition);
    const condition = serialiseTransitionCondition(
      parsed.mode === 'always' ? 'guard' : parsed.mode,
      (event.currentTarget as HTMLInputElement).value
    );

    this._replaceSelectedTransition({ ...transition, condition });
    this._announce('Route condition updated.');
  }

  private _updateTransitionRole(event: Event) {
    const transition = this._selectedTransition;
    if (!transition) {
      return;
    }

    const requiresRole = (event.currentTarget as HTMLInputElement).value.trim() || undefined;
    if (requiresRole === transition.requiresRole) {
      return;
    }

    this._replaceSelectedTransition({ ...transition, requiresRole });
    this._announce(requiresRole ? `Role guard updated to ${requiresRole}.` : 'Role guard cleared.');
  }

  private _deleteSelectedTransition() {
    if (!this.workflow || this.selectedTransitionIndex === null) {
      return;
    }

    const transition = this.workflow.transitions[this.selectedTransitionIndex];
    if (!transition) {
      return;
    }

    const transitions = this.workflow.transitions.filter((_, index) => index !== this.selectedTransitionIndex);
    this._emitWorkflowUpdated({ ...this.workflow, transitions }, null);
    this._announce(`Route ${transition.action} deleted.`);
  }

  private _renderEmpty() {
    return html`
      <div class="empty-state" role="status">
        <p>Select a stage, gateway, or route from the workspace to inspect its details.</p>
      </div>
    `;
  }

  private _renderTransition(transition: AuthoredTransition) {
    const condition = parseTransitionCondition(transition.condition);
    const targetOptions = (this.workflow?.stages ?? []).filter(stage => stage.stageKey !== transition.fromStage);
    const splitGateways = this._availableSplitGatewaysForStage(transition.fromStage);
    const joinGateways = this._availableJoinGatewaysForStage(transition.toStage);

    return html`
      <article
        class="inspector-panel"
        data-prism-transition-detail="${transition.fromStage}-${transition.action}-${transition.toStage}"
        data-prism-inspector-kind="transition"
        aria-labelledby="inspector-transition-title"
      >
        <div class="inspector-header">
          <div>
            <p class="eyebrow">Route</p>
            <h2 id="inspector-transition-title" class="stage-title">${transition.action}</h2>
          </div>
          <span class="stage-kind-badge transition-badge">Routing rule</span>
        </div>

        <section class="inspector-section" aria-labelledby="section-transition-route">
          <h3 id="section-transition-route" class="section-heading">Authored shape</h3>
          <dl class="meta-list">
            <div class="meta-row">
              <dt>From</dt>
              <dd>${this._stageLabel(transition.fromStage)}</dd>
            </div>
            ${transition.fromGateway
              ? html`
                  <div class="meta-row">
                    <dt>Leaves through</dt>
                    <dd>${this._gatewayLabel(transition.fromGateway)}</dd>
                  </div>
                `
              : nothing}
            ${transition.toGateway
              ? html`
                  <div class="meta-row">
                    <dt>Arrives through</dt>
                    <dd>${this._gatewayLabel(transition.toGateway)}</dd>
                  </div>
                `
              : nothing}
            <div class="meta-row">
              <dt>To</dt>
              <dd>${this._stageLabel(transition.toStage)}</dd>
            </div>
            <div class="meta-row">
              <dt>Condition</dt>
              <dd>${describeTransitionCondition(transition.condition)}</dd>
            </div>
          </dl>
          <p class="action-summary gateway-routing-hint">${this._routeDescriptor(transition)}</p>
        </section>

        <section class="inspector-section" aria-labelledby="section-transition-edit">
          <div class="section-header-row">
            <h3 id="section-transition-edit" class="section-heading">Edit route</h3>
            <span class="section-meta">${transitionQuickAction(transition.action) === 'custom' ? 'Custom label' : 'Preset label'}</span>
          </div>
          <div class="field-grid">
            <label class="field-block">
              <span class="field-label">Route label</span>
              <input
                class="field-control"
                data-prism-transition-label
                .value=${transition.action}
                @change=${this._updateTransitionLabel}
              />
            </label>
            <label class="field-block">
              <span class="field-label">Route preset</span>
              <select class="field-control" data-prism-transition-action @change=${this._updateTransitionActionPreset}>
                ${TRANSITION_ACTION_OPTIONS.map(option => html`
                  <option value=${option.value} ?selected=${transitionQuickAction(transition.action) === option.value}>${option.label}</option>
                `)}
                <option value="custom" ?selected=${transitionQuickAction(transition.action) === 'custom'}>Custom label</option>
              </select>
            </label>
            <label class="field-block">
              <span class="field-label">Leave through</span>
              <select class="field-control" data-prism-transition-from-gateway @change=${this._updateTransitionFromGateway}>
                <option value="">No split gateway</option>
                ${splitGateways.map(gateway => html`
                  <option value=${gateway.gatewayKey} ?selected=${gateway.gatewayKey === transition.fromGateway}>
                    ${gateway.displayName}
                  </option>
                `)}
              </select>
            </label>
            <label class="field-block">
              <span class="field-label">Target stage</span>
              <select class="field-control" data-prism-transition-target @change=${this._updateTransitionTarget}>
                ${targetOptions.map(stage => html`
                  <option value=${stage.stageKey} ?selected=${stage.stageKey === transition.toStage}>${stage.displayName}</option>
                `)}
              </select>
            </label>
            <label class="field-block">
              <span class="field-label">Arrive through</span>
              <select class="field-control" data-prism-transition-to-gateway @change=${this._updateTransitionToGateway}>
                <option value="">No join gateway</option>
                ${joinGateways.map(gateway => html`
                  <option value=${gateway.gatewayKey} ?selected=${gateway.gatewayKey === transition.toGateway}>
                    ${gateway.displayName}
                  </option>
                `)}
              </select>
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Role guard</span>
                <prism-inline-help
                  label="Role guard help"
                  message="Add a role only when this route should be limited to a specific actor such as reviewer or caseworker. Leave it blank when everyone on the route can use it."
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-transition-role
                .value=${transition.requiresRole ?? ''}
                placeholder="reviewer"
                @change=${this._updateTransitionRole}
              />
            </label>
          </div>

          <div class="field-grid">
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Condition type</span>
                <prism-inline-help
                  label="Condition type help"
                  message="Choose Always available for a standard route, Event for named workflow triggers, or Guard expression when runtime data decides whether this route can run."
                ></prism-inline-help>
              </span>
              <select class="field-control" data-prism-transition-condition-mode @change=${this._updateTransitionConditionMode}>
                <option value="always" ?selected=${condition.mode === 'always'}>Always available</option>
                <option value="event" ?selected=${condition.mode === 'event'}>Event</option>
                <option value="guard" ?selected=${condition.mode === 'guard'}>Guard expression</option>
              </select>
            </label>
            <label class="field-block ${condition.mode === 'always' ? 'field-block-disabled' : ''}">
              <span class="field-label-row">
                <span class="field-label">${condition.mode === 'event' ? 'Event name' : 'Condition value'}</span>
                <prism-inline-help
                  label="Condition value help"
                  message=${condition.mode === 'event'
                    ? 'Use the exact event name your runtime emits, for example submit-clicked.'
                    : 'Use a concise guard expression that explains when this route should unlock, for example application.isComplete == true.'}
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-transition-condition-value
                .value=${condition.value}
                ?disabled=${condition.mode === 'always'}
                placeholder=${condition.mode === 'event' ? 'submit-clicked' : 'application.isComplete == true'}
                @change=${this._updateTransitionConditionValue}
              />
            </label>
          </div>

          <div class="action-buttons">
            <button
              type="button"
              class="icon-button danger-button"
              data-prism-transition-delete
              @click=${this._deleteSelectedTransition}
            >
              Delete route
            </button>
          </div>
        </section>

        <section class="inspector-section" aria-labelledby="section-transition-actions">
          <div class="section-header-row">
            <h3 id="section-transition-actions" class="section-heading">Route actions</h3>
            <span class="section-meta">${transition.actions?.length ?? 0} configured</span>
          </div>
          <prism-workflow-action-editor
            .actions=${transition.actions ?? []}
            .actionCatalog=${this.actionCatalog}
            .selectedActionIndex=${this.selectedActionIndex}
            target="transition"
            subject-label="transition"
            @actions-updated=${this._updateSelectedTransitionActions}
            @action-selected=${this._handleActionSelected}
          ></prism-workflow-action-editor>
        </section>
      </article>
    `;
  }

  @state() private _gatewayKeyError: string | null = null;

  private _replaceSelectedGateway(nextGateway: AuthoredGateway, previousGatewayKey = this._selectedGateway?.gatewayKey) {
    if (!this.workflow || !previousGatewayKey) {
      return;
    }

    const gatewayIndex = (this.workflow.gateways ?? []).findIndex(g => g.gatewayKey === previousGatewayKey);
    if (gatewayIndex < 0) {
      return;
    }

    const gateways = [...(this.workflow.gateways ?? [])];
    gateways[gatewayIndex] = nextGateway;

    let transitions = [...this.workflow.transitions];
    if (nextGateway.gatewayKey !== previousGatewayKey) {
      transitions = transitions.map(t => ({
        ...t,
        fromGateway: t.fromGateway === previousGatewayKey ? nextGateway.gatewayKey : t.fromGateway,
        toGateway: t.toGateway === previousGatewayKey ? nextGateway.gatewayKey : t.toGateway,
      }));
    }

    this._emitWorkflowUpdated(
      { ...this.workflow, gateways, transitions },
      { kind: 'gateway', gatewayKey: nextGateway.gatewayKey }
    );
  }

  private _updateGatewayDisplayName(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway) return;
    const nextName = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextName || nextName === gateway.displayName) return;
    this._replaceSelectedGateway({ ...gateway, displayName: nextName });
    this._announce(`${nextName} gateway name updated.`);
  }

  private _updateGatewayKey(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || !this.workflow) return;
    const nextKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextKey) {
      this._gatewayKeyError = 'Gateway key is required.';
      this._announce('Gateway key is required.');
      return;
    }

    const allKeys = [
      ...this.workflow.stages.map(s => s.stageKey),
      ...(this.workflow.gateways ?? []).map(g => g.gatewayKey).filter(k => k !== gateway.gatewayKey),
    ];
    if (allKeys.includes(nextKey)) {
      this._gatewayKeyError = 'Gateway key must be unique across stages and gateways.';
      this._announce(`Key ${nextKey} is already in use.`);
      return;
    }

    if (nextKey === gateway.gatewayKey) {
      this._gatewayKeyError = null;
      return;
    }

    this._gatewayKeyError = null;
    this._replaceSelectedGateway({ ...gateway, gatewayKey: nextKey }, gateway.gatewayKey);
    this._announce(`Gateway key updated to ${nextKey}.`);
  }

  private _updateGatewayLane(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway) return;
    const laneKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!laneKey || laneKey === gatewayLaneKey(gateway)) return;
    this._replaceSelectedGateway({ ...gateway, laneKey, actor: laneKey });
    this._announce(`${gateway.displayName} lane updated to ${laneKey}.`);
  }

  private _updateGatewayDescription(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway) return;
    const nextDesc = (event.currentTarget as HTMLTextAreaElement).value.trim();
    if ((nextDesc || undefined) === (gateway.description?.trim() || undefined)) return;
    this._replaceSelectedGateway({ ...gateway, description: nextDesc || undefined });
    this._announce(`${gateway.displayName} description updated.`);
  }

  private _updateJoinWaitingContent(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const content = (event.currentTarget as HTMLTextAreaElement).value.trim() || undefined;
    this._replaceSelectedGateway({ ...gateway, waiting: { ...gateway.waiting ?? { allowDefer: false }, content } });
    this._announce(`${gateway.displayName} waiting message updated.`);
  }

  private _updateJoinWaitingExpectedSeconds(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const raw = (event.currentTarget as HTMLInputElement).value;
    const expectedWaitSeconds = raw ? Number(raw) : undefined;
    this._replaceSelectedGateway({ ...gateway, waiting: { ...gateway.waiting ?? { allowDefer: false }, expectedWaitSeconds } });
    this._announce(`${gateway.displayName} expected wait updated.`);
  }

  private _updateJoinWaitingAllowDefer(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const allowDefer = (event.currentTarget as HTMLInputElement).checked;
    const current = gateway.waiting ?? { allowDefer: false };
    this._replaceSelectedGateway({
      ...gateway,
      waiting: { ...current, allowDefer, deferMessage: allowDefer ? current.deferMessage : undefined },
    });
    this._announce(allowDefer ? `${gateway.displayName} defer enabled.` : `${gateway.displayName} defer disabled.`);
  }

  private _updateJoinWaitingDeferMessage(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const deferMessage = (event.currentTarget as HTMLInputElement).value.trim() || undefined;
    this._replaceSelectedGateway({ ...gateway, waiting: { ...gateway.waiting ?? { allowDefer: true }, deferMessage } });
    this._announce(`${gateway.displayName} defer message updated.`);
  }

  private _deleteSelectedGateway() {
    const gateway = this._selectedGateway;
    if (!this.workflow || !gateway) return;
    const gateways = (this.workflow.gateways ?? []).filter(g => g.gatewayKey !== gateway.gatewayKey);
    const transitions = this.workflow.transitions.map(t => ({
      ...t,
      fromGateway: t.fromGateway === gateway.gatewayKey ? undefined : t.fromGateway,
      toGateway: t.toGateway === gateway.gatewayKey ? undefined : t.toGateway,
    }));
    this._emitWorkflowUpdated({ ...this.workflow, gateways, transitions }, null);
    this._announce(`${gateway.displayName} gateway deleted.`);
  }

  private _renderGateway(gateway: AuthoredGateway) {
    const laneKey = gatewayLaneKey(gateway);
    const laneLabel = stageLaneLabel(this.workflow, laneKey);
    const binding = this.workflow
      ? deriveGatewayBindings(this.workflow).find(candidate => candidate.gateway.gatewayKey === gateway.gatewayKey) ?? null
      : null;
    const laneOptionsId = `gateway-lane-options-${gateway.gatewayKey}`;
    const waiting = gateway.waiting;
    const isJoin = gateway.kind === 'Join';

    return html`
      <article
        class="inspector-panel"
        data-prism-gateway-detail="${gateway.gatewayKey}"
        data-prism-inspector-kind="gateway"
        aria-labelledby="inspector-gateway-title"
      >
        <div class="inspector-header">
          <div>
            <p class="eyebrow">${laneLabel} lane</p>
            <h2 id="inspector-gateway-title" class="stage-title" data-prism-inspector-heading>${gateway.displayName}</h2>
          </div>
          <span class="stage-kind-badge transition-badge" data-prism-field="kind">${gateway.kind} gateway</span>
        </div>

        <section class="inspector-section" aria-labelledby="gateway-basics-heading">
          <h3 id="gateway-basics-heading" class="section-heading">Gateway details</h3>
          <div class="field-grid">
            <label class="field-block">
              <span class="field-label">Name</span>
              <input
                class="field-control"
                data-prism-gateway-name
                .value=${gateway.displayName}
                @change=${this._updateGatewayDisplayName}
              />
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Key</span>
                <prism-inline-help
                  label="Gateway key help"
                  message="A stable, unique identifier for this gateway. Must not clash with any stage key or other gateway key. Route bindings reference this key."
                ></prism-inline-help>
              </span>
              <input
                class="field-control ${this._gatewayKeyError ? 'field-control-error' : ''}"
                data-prism-gateway-key
                aria-invalid=${String(Boolean(this._gatewayKeyError))}
                .value=${gateway.gatewayKey}
                @input=${() => { this._gatewayKeyError = null; }}
                @change=${this._updateGatewayKey}
              />
              ${this._gatewayKeyError
                ? html`<span class="field-error" data-prism-gateway-key-error>${this._gatewayKeyError}</span>`
                : nothing}
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Lane owner</span>
                <prism-inline-help
                  label="Lane owner help"
                  message="The lane that owns this gateway. For a join gateway, the owning lane is where waiting information is shown to users."
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-gateway-lane
                .value=${laneKey}
                list=${laneOptionsId}
                placeholder="applicant"
                @change=${this._updateGatewayLane}
              />
              <datalist id=${laneOptionsId}>
                ${workflowLaneOptions(this.workflow).map(option => html`
                  <option value=${option}>${stageLaneLabel(this.workflow, option)}</option>
                `)}
              </datalist>
            </label>
          </div>
          <label class="field-block field-block-full">
            <span class="field-label">Description</span>
            <textarea
              class="field-control field-textarea"
              data-prism-gateway-description
              .value=${gateway.description ?? ''}
              placeholder="Explain what this ${gateway.kind === 'Split' ? 'split' : 'join'} point does and why it exists."
              @change=${this._updateGatewayDescription}
            ></textarea>
          </label>
        </section>

        <section class="inspector-section" aria-labelledby="gateway-routing-heading">
          <h3 id="gateway-routing-heading" class="section-heading">Routing</h3>
          <dl class="meta-list">
            <div class="meta-row">
              <dt>Kind</dt>
              <dd>${isJoin ? 'Join — converges multiple lane paths' : 'Split — branches into multiple lane paths'}</dd>
            </div>
            <div class="meta-row">
              <dt>Related routes</dt>
              <dd>${binding?.relatedTransitionIndices.length ?? 0} transition${(binding?.relatedTransitionIndices.length ?? 0) === 1 ? '' : 's'}</dd>
            </div>
            ${binding?.anchorStageKey
              ? html`
                  <div class="meta-row">
                    <dt>${isJoin ? 'Merge near' : 'Branches from'}</dt>
                    <dd>${this._stageLabel(binding.anchorStageKey)}</dd>
                  </div>
                `
              : nothing}
          </dl>
          <p class="action-summary gateway-routing-hint">
            Use route editing to bind stages through this gateway so the authored flow stays visible as stage → gateway → stage.
            ${isJoin ? ' Join gateways wait for all required incoming paths before releasing.' : ' Split gateways create independent paths for each outgoing transition.'}
          </p>
        </section>

        ${isJoin
          ? html`
              <section class="inspector-section" aria-labelledby="gateway-waiting-heading">
                <div class="section-header-row">
                  <h3 id="gateway-waiting-heading" class="section-heading">Waiting information</h3>
                  <prism-inline-help
                    label="Waiting information help"
                    message="Join gateways own the waiting story for their lane. This message is shown to users in the owning lane while they wait for other lanes to arrive. Authors set it here rather than on a separate waiting stage."
                  ></prism-inline-help>
                </div>
                <div class="field-grid">
                  <label class="field-block field-block-full">
                    <span class="field-label">Waiting message</span>
                    <textarea
                      class="field-control field-textarea"
                      data-prism-gateway-waiting-content
                      .value=${waiting?.content ?? ''}
                      placeholder="Explain what users in this lane are waiting for, for example: Your application is under review by the planning team."
                      @change=${this._updateJoinWaitingContent}
                    ></textarea>
                  </label>
                  <label class="field-block">
                    <span class="field-label-row">
                      <span class="field-label">Expected wait (seconds)</span>
                      <prism-inline-help
                        label="Expected wait help"
                        message="An approximate maximum wait in seconds. Used by the runtime to set a progress indicator. Leave blank if the wait is open-ended."
                      ></prism-inline-help>
                    </span>
                    <input
                      type="number"
                      class="field-control"
                      data-prism-gateway-waiting-seconds
                      min="0"
                      .value=${String(waiting?.expectedWaitSeconds ?? '')}
                      placeholder="3600"
                      @change=${this._updateJoinWaitingExpectedSeconds}
                    />
                  </label>
                  <div class="field-block">
                    <span class="field-label">Allow defer</span>
                    <label class="checkbox-row">
                      <input
                        type="checkbox"
                        data-prism-gateway-waiting-allow-defer
                        ?checked=${waiting?.allowDefer ?? false}
                        @change=${this._updateJoinWaitingAllowDefer}
                      />
                      <span>Users in this lane can defer the wait</span>
                    </label>
                  </div>
                  ${waiting?.allowDefer
                    ? html`
                        <label class="field-block">
                          <span class="field-label">Defer message</span>
                          <input
                            class="field-control"
                            data-prism-gateway-waiting-defer-message
                            .value=${waiting.deferMessage ?? ''}
                            placeholder="You can return to this step when the other team has finished."
                            @change=${this._updateJoinWaitingDeferMessage}
                          />
                        </label>
                      `
                    : nothing}
                </div>
              </section>
            `
          : nothing}

        <section class="inspector-section" aria-labelledby="gateway-danger-heading">
          <h3 id="gateway-danger-heading" class="section-heading">Actions</h3>
          <div class="action-buttons">
            <button
              type="button"
              class="icon-button danger-button"
              data-prism-gateway-delete
              @click=${this._deleteSelectedGateway}
            >
              Delete gateway
            </button>
          </div>
        </section>
      </article>
    `;
  }

  private _renderStage(stage: AuthoredStage) {
    const fields = stage.fields ?? [];
    const actions = stage.actions ?? [];
    const outgoing = this._selectedStageOutgoing(stage);
    const stageType = stageKindToEditorStageType(stage.kind);
    const laneKey = stageLaneKey(stage);
    const laneLabel = stageLaneLabel(this.workflow, laneKey);
    const laneEyebrow = `${laneLabel} lane`;
    const laneOptionsId = `stage-lane-options-${stage.stageKey}`;
    const unreachable = this.workflow
      ? workflowUnreachableStages(this.workflow).some(candidate => candidate.stageKey === stage.stageKey)
      : false;
    const orphaned = this.workflow
      ? workflowOrphanedStages(this.workflow).some(candidate => candidate.stageKey === stage.stageKey)
      : false;
    const deadEnd = this.workflow
      ? workflowDeadEndStages(this.workflow).some(candidate => candidate.stageKey === stage.stageKey)
      : false;
    const validationMessages = [
      ...(this._stageKeyError ? [this._stageKeyError] : []),
      ...(orphaned ? ['This stage is disconnected from the workflow. Add at least one route to connect it.'] : []),
      ...(deadEnd || (outgoing.length === 0 && !isTerminalStage(stage))
        ? ['Add at least one outgoing route before publishing this stage.']
        : []),
      ...(unreachable ? ['This stage is unreachable from the workflow start. Add or retarget an incoming route.'] : []),
    ];

    return html`
      <article
        class="inspector-panel"
        data-prism-stage-detail="${stage.stageKey}"
        data-prism-inspector-kind="stage"
        aria-labelledby="inspector-stage-title"
      >
        <div class="inspector-header">
          <div>
            <p class="eyebrow">${laneEyebrow}</p>
            <h2 id="inspector-stage-title" class="stage-title">${stage.displayName}</h2>
          </div>
          <span class="stage-kind-badge">${STAGE_TYPE_OPTIONS.find(option => option.value === stageType)?.label ?? stage.kind}</span>
        </div>

        ${validationMessages.length > 0
          ? html`
              <section class="inspector-section validation-section" aria-labelledby="stage-validation-heading">
                <h3 id="stage-validation-heading" class="section-heading">Validation</h3>
                <ul class="validation-list">
                  ${validationMessages.map(message => html`<li>${message}</li>`) }
                </ul>
              </section>
            `
          : nothing}

        <section class="inspector-section" aria-labelledby="stage-basics-heading">
          <h3 id="stage-basics-heading" class="section-heading">Stage details</h3>
          <div class="field-grid">
            <label class="field-block">
              <span class="field-label">Title</span>
              <input
                class="field-control"
                data-prism-stage-title
                .value=${stage.displayName}
                @change=${this._updateStageTitle}
              />
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Key</span>
                <prism-inline-help
                  label="Stage key help"
                  message="Use a stable, machine-friendly key. Transitions, validation links, and saved workflow JSON all depend on this value staying predictable."
                ></prism-inline-help>
              </span>
              <input
                class="field-control ${this._stageKeyError ? 'field-control-error' : ''}"
                data-prism-stage-key
                aria-invalid=${String(Boolean(this._stageKeyError))}
                .value=${stage.stageKey}
                @input=${() => {
                  this._stageKeyError = null;
                }}
                @change=${this._updateStageKey}
              />
              ${this._stageKeyError
                ? html`<span class="field-error" data-prism-stage-key-error>${this._stageKeyError}</span>`
                : nothing}
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Lane owner</span>
                <prism-inline-help
                  label="Lane owner help"
                  message="Use the lane key that owns this work, for example applicant, reviewer, finance, or planning-officer. The editor keeps actor and role-gate assignment aligned from this lane value."
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-stage-lane
                .value=${laneKey}
                list=${laneOptionsId}
                placeholder="planning-officer"
                @change=${this._updateStageLane}
              />
              <datalist id=${laneOptionsId}>
                ${workflowLaneOptions(this.workflow).map(option => html`
                  <option value=${option}>${stageLaneLabel(this.workflow, option)}</option>
                `)}
              </datalist>
            </label>
            <label class="field-block">
              <span class="field-label">Type</span>
              <select class="field-control" data-prism-stage-type @change=${this._updateStageType}>
                ${STAGE_TYPE_OPTIONS.map(option => html`
                  <option value=${option.value} ?selected=${stageType === option.value}>${option.label}</option>
                `)}
              </select>
            </label>
          </div>

          <label class="field-block field-block-full">
            <span class="field-label">Description</span>
            <textarea
              class="field-control field-textarea"
              data-prism-stage-description
              .value=${stage.description ?? ''}
              @change=${this._updateStageDescription}
            ></textarea>
          </label>
        </section>

        <section class="inspector-section" aria-labelledby="stage-actions-heading">
          <div class="section-header-row">
            <h3 id="stage-actions-heading" class="section-heading">Actions</h3>
            <span class="section-meta">${actions.length} configured</span>
          </div>
          <prism-workflow-action-editor
            .actions=${actions}
            .actionCatalog=${this.actionCatalog}
            .selectedActionIndex=${this.selectedActionIndex}
            target="stage"
            subject-label="stage"
            @actions-updated=${this._updateSelectedStageActions}
            @action-selected=${this._handleActionSelected}
          ></prism-workflow-action-editor>
        </section>

        <section class="inspector-section" aria-labelledby="stage-transitions-heading">
          <div class="section-header-row">
            <h3 id="stage-transitions-heading" class="section-heading">Outgoing routes</h3>
            <span class="section-meta">${outgoing.length}</span>
          </div>
          ${outgoing.length === 0
            ? html`<p class="section-empty">No outgoing routes yet.</p>`
            : html`
                <ul class="transition-list">
                  ${outgoing.map(transition => html`
                    <li class="transition-item">
                      <span class="transition-action">${transition.action}</span>
                      <span>${this._routeDescriptor(transition)}</span>
                    </li>
                  `)}
                </ul>
              `}
        </section>

        <section class="inspector-section" aria-labelledby="stage-fields-heading">
          <div class="section-header-row">
            <h3 id="stage-fields-heading" class="section-heading">Fields</h3>
            <span class="section-meta">${fields.length}</span>
          </div>
          ${fields.length === 0
            ? html`<p class="section-empty">No fields defined for this stage.</p>`
            : html`
                <ul class="field-list">
                  ${fields.map(field => html`
                    <li class="field-item">
                      <span class="field-item-label">${field.label}</span>
                      <span class="field-item-meta">${field.kind}${field.required ? ' · required' : ''}</span>
                    </li>
                  `)}
                </ul>
              `}
        </section>
      </article>
    `;
  }

  render() {
    const transition = this._selectedTransition;
    const gateway = transition ? null : this._selectedGateway;
    const stage = transition || gateway ? null : this._selectedStage;

    return html`
      <div class="step-inspector-root" data-prism-component="step-inspector" tabindex="0">
        <div id="inspector-announcer" class="sr-only" role="status" aria-live="polite" aria-atomic="true">${this._statusMessage ?? ''}</div>
        ${transition
          ? this._renderTransition(transition)
          : gateway
            ? this._renderGateway(gateway)
          : stage
            ? this._renderStage(stage)
            : this._renderEmpty()}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      height: 100%;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
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

    .step-inspector-root {
      height: 100%;
      overflow-y: auto;
      background: #ffffff;
      border: 1px solid #d1d5db;
      border-radius: 12px;
    }

    .empty-state {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 12rem;
      padding: 2rem;
      color: #475569;
      text-align: center;
    }

    .inspector-header {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 1rem 1.25rem 0.875rem;
      border-bottom: 1px solid #e5e7eb;
      background: linear-gradient(180deg, #f8fafc 0%, #ffffff 100%);
    }

    .eyebrow {
      margin: 0 0 0.25rem;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #1d4ed8;
    }

    .stage-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #111827;
      line-height: 1.3;
    }

    .stage-kind-badge {
      align-self: flex-start;
      padding: 0.25rem 0.625rem;
      border-radius: 999px;
      background: #e2e8f0;
      color: #334155;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .transition-badge {
      background: #dbeafe;
      color: #1d4ed8;
    }

    .inspector-section {
      padding: 0.9375rem 1.25rem;
      border-bottom: 1px solid #f1f5f9;
    }

    .inspector-section:last-child {
      border-bottom: none;
    }

    .section-heading {
      margin: 0;
      font-size: 0.8125rem;
      font-weight: 700;
      color: #334155;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .section-header-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }

    .section-meta {
      color: #475569;
      font-size: 0.8125rem;
      font-weight: 600;
    }

    .section-copy,
    .section-empty {
      margin: 0;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .validation-section {
      background: #fff7ed;
    }

    .validation-list {
      margin: 0.75rem 0 0;
      padding-left: 1rem;
      color: #9a3412;
      display: grid;
      gap: 0.375rem;
      font-size: 0.875rem;
    }

    .field-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.875rem;
      margin-bottom: 0.875rem;
    }

    .field-block {
      display: grid;
      gap: 0.375rem;
      min-width: 0;
    }

    .field-block-full {
      margin-top: 0.25rem;
    }

    .field-block-disabled {
      opacity: 0.7;
    }

    .field-label {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #334155;
    }

    .field-label-row {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      flex-wrap: wrap;
    }

    .field-control {
      width: 100%;
      min-height: 2.5rem;
      padding: 0.625rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #111827;
      font: inherit;
      box-sizing: border-box;
    }

    .field-textarea {
      min-height: 6.5rem;
      resize: vertical;
    }

    .field-control-error {
      border-color: #dc2626;
    }

    .field-error {
      color: #b91c1c;
      font-size: 0.8125rem;
    }

    .field-control:focus-visible,
    .secondary-button:focus-visible,
    .icon-button:focus-visible,
    .drag-button:focus-visible,
    .action-item:focus-visible {
      outline: 3px solid #1d4ed8;
      outline-offset: 2px;
    }

    .action-adder {
      display: flex;
      align-items: end;
      gap: 0.75rem;
      margin-bottom: 0.875rem;
    }

    .action-select-block {
      flex: 1;
    }

    .secondary-button,
    .icon-button,
    .drag-button {
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #111827;
      font: inherit;
      cursor: pointer;
    }

    .secondary-button {
      min-height: 2.5rem;
      padding: 0.625rem 0.875rem;
      font-weight: 600;
    }

    .secondary-button:disabled,
    .icon-button:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .action-list,
    .field-list,
    .transition-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
    }

    .action-item {
      display: grid;
      gap: 0.875rem;
      padding: 0.875rem;
      border: 1px solid #dbe2ea;
      border-radius: 12px;
      background: #f8fafc;
    }

    .action-item-drop {
      border-color: #1d4ed8;
      box-shadow: inset 0 0 0 2px rgba(29, 78, 216, 0.2);
    }

    .action-item-main {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
    }

    .drag-button {
      width: 2.25rem;
      height: 2.25rem;
      flex-shrink: 0;
      font-weight: 700;
    }

    .action-copy {
      min-width: 0;
    }

    .action-title {
      margin: 0 0 0.25rem;
      color: #111827;
      font-weight: 700;
      font-size: 0.9375rem;
    }

    .action-summary {
      margin: 0;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .action-item-controls {
      display: grid;
      grid-template-columns: minmax(0, 11rem) 1fr;
      gap: 0.75rem;
      align-items: end;
    }

    .compact-field {
      margin: 0;
    }

    .action-buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: flex-end;
    }

    .icon-button {
      min-height: 2.25rem;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
    }

    .danger-button {
      border-color: #fecaca;
      color: #b91c1c;
      background: #fff5f5;
    }

    .transition-item,
    .field-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.625rem 0.75rem;
      border-radius: 10px;
      background: #f8fafc;
      color: #111827;
      font-size: 0.875rem;
    }

    .transition-action,
    .field-item-label {
      font-weight: 700;
      color: #111827;
    }

    .transition-arrow,
    .field-item-meta {
      color: #475569;
      font-size: 0.8125rem;
    }

    .meta-list {
      margin: 0;
      display: grid;
      gap: 0.5rem;
    }

    .meta-row {
      display: flex;
      gap: 0.75rem;
      align-items: baseline;
      font-size: 0.875rem;
    }

    .meta-row dt {
      min-width: 6rem;
      color: #334155;
      font-weight: 700;
    }

    .meta-row dd {
      margin: 0;
      color: #111827;
    }

    @media (max-width: 760px) {
      .field-grid,
      .action-item-controls {
        grid-template-columns: 1fr;
      }

      .action-adder {
        flex-direction: column;
        align-items: stretch;
      }

      .action-buttons {
        justify-content: flex-start;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .field-control,
      .secondary-button,
      .icon-button,
      .drag-button,
      .action-item {
        scroll-behavior: auto;
      }
    }

    .checkbox-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.375rem;
      cursor: pointer;
      font-size: 0.875rem;
      color: #111827;
    }

    .checkbox-row input[type="checkbox"] {
      width: 1rem;
      height: 1rem;
      cursor: pointer;
      accent-color: #1d4ed8;
    }

    .gateway-routing-hint {
      margin-top: 0.5rem;
      font-size: 0.8125rem;
      color: #475569;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-step-inspector': PrismStepInspectorElement;
  }
}
