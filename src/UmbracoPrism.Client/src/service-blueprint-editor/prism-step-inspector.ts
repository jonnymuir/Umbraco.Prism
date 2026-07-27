import { LitElement, css, html, nothing, svg } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type {
  ActionCatalogEntry,
  AuthoredAction,
  AuthoredComponent,
  AuthoredGateway,
  AuthoredStage,
  RouteView,
  AuthoredServiceBlueprint,
  EditorStageType,
} from './types.js';
import { serviceBlueprintGateways, serviceBlueprintStages } from './types.js';
import { NODE_ICONS, defaultIconForGateway, defaultIconForStage, type NodeIconDef, type NodeIconName } from './graph/node-icons.js';

function renderNodeIconSvg(icon: NodeIconDef) {
  return svg`
    <svg viewBox=${icon.viewBox} width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      ${icon.paths.map(d => svg`<path d=${d}></path>`)}
    </svg>
  `;
}

function describeComponent(component: AuthoredComponent): string {
  switch (component.type) {
    case 'fieldset':
      return component.legend
        ? `${component.legend} · ${component.children.length} item${component.children.length === 1 ? '' : 's'}`
        : `Fieldset · ${component.children.length} item${component.children.length === 1 ? '' : 's'}`;
    case 'accordion':
      return `Accordion · ${component.sections.length} section${component.sections.length === 1 ? '' : 's'}`;
    case 'panel':
      return component.heading;
    case 'waiting':
      return component.content;
    case 'summary-list':
      return `Summary list · ${component.children.length} row${component.children.length === 1 ? '' : 's'}`;
    case 'task-list': {
      const taskCount = (component.sections ?? []).reduce((sum, section) => sum + section.tasks.length, 0);
      return `Task list · ${taskCount} task${taskCount === 1 ? '' : 's'}`;
    }
    case 'body':
    case 'inset-text':
    case 'warning-text':
    case 'details':
      return component.content ?? component.type;
    default:
      return (component as { label?: string }).label
        ?? (component as { fieldKey?: string }).fieldKey
        ?? component.type;
  }
}
import {
  editorStageTypeToStageKind,
  stageKindToEditorStageType,
} from './types.js';
import {
  applyQueueToStage,
  stageQueueKey,
  stageQueueLabel,
  type QueueDefinition,
  serviceBlueprintQueueOptions,
} from './stage-assignment.js';
import { deriveGatewayBindings, gatewayQueueKey, type GatewayBinding } from './gateway-representation.js';
import {
  parseTransitionCondition,
  serialiseTransitionCondition,
  TRANSITION_ACTION_OPTIONS,
  transitionQuickAction,
  type TransitionConditionMode,
} from './gateway-route-conditions.js';
import {
  isTerminalStage,
  serviceBlueprintDeadEndStages,
  serviceBlueprintOrphanedStages,
  serviceBlueprintOutgoingRoutes,
  serviceBlueprintUnreachableStages,
} from './service-blueprint-validation.js';
import { addRoute, deleteRoute, findOrCreateSplitGateway, flattenRoutes, newRouteId, updateRoute } from './route-model.js';
import './prism-stage-action-editor.js';
import './prism-inline-help.js';

const STAGE_TYPE_OPTIONS: Array<{ value: EditorStageType; label: string }> = [
  { value: 'form', label: 'Form' },
  { value: 'review', label: 'Review' },
  { value: 'decision', label: 'Decision' },
  { value: 'confirmation', label: 'Confirmation' },
];

type GraphSelectionDetail = {
  kind: 'stage' | 'gateway';
  stageKey?: string;
  gatewayKey?: string;
};

type ServiceBlueprintUpdatedDetail = {
  serviceBlueprint: AuthoredServiceBlueprint;
  selection?: GraphSelectionDetail | null;
};

type ActionsUpdatedDetail = {
  actions: AuthoredAction[];
};

type ActionSelectedDetail = {
  index: number | null;
  target: 'stage' | 'transition';
  transitionIndex?: number;
};

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-step-inspector')
export class PrismStepInspectorElement extends LitElement {
  @property({ attribute: false })
  serviceBlueprint: AuthoredServiceBlueprint | null = null;

  @property({ type: String, attribute: 'selected-stage-key' })
  selectedStageKey: string | null = null;

  @property({ type: String, attribute: 'selected-gateway-key' })
  selectedGatewayKey: string | null = null;

  @property({ attribute: false })
  actionCatalog: ActionCatalogEntry[] = [];

  @property({ attribute: false })
  availableQueues: QueueDefinition[] = [];

  @property({ type: Number, attribute: false })
  selectedActionIndex: number | null = null;

  @property({ type: Number, attribute: false })
  selectedActionTransitionIndex: number | null = null;

  @state() private _stageKeyError: string | null = null;
  @state() private _statusMessage: string | null = null;

  /** Tracks the route id of a just-created route so updated() can focus its target picker. */
  private _newlyAddedRouteId: string | null = null;

  private get _selectedStage(): AuthoredStage | null {
    if (!this.serviceBlueprint || !this.selectedStageKey) {
      return null;
    }

    return this.serviceBlueprint.stages.find(stage => stage.stateKey === this.selectedStageKey) ?? null;
  }

  private get _selectedGateway(): AuthoredGateway | null {
    if (!this.serviceBlueprint || !this.selectedGatewayKey) {
      return null;
    }

    return serviceBlueprintGateways(this.serviceBlueprint).find(gateway => gateway.key === this.selectedGatewayKey) ?? null;
  }

  protected updated(changed: Map<string, unknown>) {
    if (changed.has('selectedStageKey')) {
      this._stageKeyError = null;
    }
    if (changed.has('selectedGatewayKey')) {
      this._gatewayKeyError = null;
    }

    if (this._newlyAddedRouteId) {
      const routeId = this._newlyAddedRouteId;
      this._newlyAddedRouteId = null;
      requestAnimationFrame(() => {
        const container = this.shadowRoot?.querySelector<HTMLElement>(`[data-prism-route-id="${routeId}"]`);
        const targetPicker = container?.querySelector<HTMLElement>('[data-prism-route-target-select]');
        if (container) {
          container.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
        if (targetPicker) {
          targetPicker.focus();
        }
      });
    }
  }

  private _announce(message: string) {
    this._statusMessage = '';
    requestAnimationFrame(() => {
      this._statusMessage = message;
    });
  }

  private _emitServiceBlueprintUpdated(serviceBlueprint: AuthoredServiceBlueprint, selection?: GraphSelectionDetail | null) {
    this.dispatchEvent(
      new CustomEvent<ServiceBlueprintUpdatedDetail>('serviceBlueprint-updated', {
        detail: { serviceBlueprint, selection },
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
    return this.serviceBlueprint?.stages.find(stage => stage.stateKey === stageKey)?.displayName
      ?? serviceBlueprintGateways(this.serviceBlueprint).find(gateway => gateway.key === stageKey)?.displayName
      ?? stageKey;
  }

  private _gatewayLabel(gatewayKey: string) {
    return serviceBlueprintGateways(this.serviceBlueprint).find(gateway => gateway.key === gatewayKey)?.displayName ?? gatewayKey;
  }

  private _routeDescriptor(transition: RouteView) {
    const fromStage = this._stageLabel(transition.fromStage);
    const fromGateway = transition.fromGateway ? this._gatewayLabel(transition.fromGateway) : null;
    const toGateway = transition.toGateway ? this._gatewayLabel(transition.toGateway) : null;
    const toStage = this._stageLabel(transition.toStage);

    const ariaParts = [`from ${fromStage}`];
    if (fromGateway) ariaParts.push(`via split gateway ${fromGateway}`);
    if (toGateway) ariaParts.push(`via join gateway ${toGateway}`);
    ariaParts.push(`to ${toStage}`);
    const ariaLabel = ariaParts.join(', ');

    const visibleTokens = [fromStage, fromGateway, toGateway, toStage].filter((token): token is string => Boolean(token));
    const arrow = html`<span aria-hidden="true"> → </span>`;
    const visible = visibleTokens.map((token, index) =>
      index === 0 ? html`<span>${token}</span>` : html`${arrow}<span>${token}</span>`
    );

    return html`<span aria-label=${ariaLabel}>${visible}</span>`;
  }

  private _availableJoinGatewaysForStage(stageKey: string) {
    if (!this.serviceBlueprint) {
      return [];
    }

    const stage = this.serviceBlueprint.stages.find(candidate => candidate.stateKey === stageKey);
    const queueKey = stage ? stageQueueKey(stage) : '';

    return deriveGatewayBindings(this.serviceBlueprint)
      .filter(binding => binding.gateway.kind === 'Join')
      .filter(binding => binding.anchorStageKey === stageKey || (!binding.anchorStageKey && binding.queueKey === queueKey))
      .map(binding => binding.gateway);
  }

  private _selectedStageOutgoing(stage: AuthoredStage) {
    return this.serviceBlueprint ? serviceBlueprintOutgoingRoutes(this.serviceBlueprint, stage.stateKey) : [];
  }

  private _replaceSelectedTransition(nextTransition: RouteView, transitionIndex: number) {
    if (!this.serviceBlueprint) {
      return;
    }

    const transitions = flattenRoutes(this.serviceBlueprint);
    const previous = transitions[transitionIndex];
    if (!previous) {
      return;
    }

    // Slice C: edits address a gateway-owned route by (gatewayKey, routeId).
    // Project the mutation onto gateways[].routes so it survives serialisation.
    const gatewayKey = previous.key || nextTransition.key;
    const routeId = previous.routeId || nextTransition.routeId;
    if (!gatewayKey || !routeId) {
      return;
    }
    const nextServiceBlueprint = updateRoute(this.serviceBlueprint, { routeId }, route => ({
      ...route,
      target: nextTransition.toStage || route.target,
      trigger: nextTransition.action || route.trigger,
      condition: nextTransition.condition,
      requiresRole: nextTransition.requiresRole,
      actions: nextTransition.actions ?? route.actions,
      editorComment: nextTransition.editorComment,
    }));

    const selectedGatewayKey = this._selectedGateway?.key;
    this._emitServiceBlueprintUpdated(
      nextServiceBlueprint,
      selectedGatewayKey ? { kind: 'gateway', gatewayKey: selectedGatewayKey } : null
    );
  }

  private _replaceSelectedStage(nextStage: AuthoredStage, previousStageKey = this._selectedStage?.stateKey) {
    if (!this.serviceBlueprint || !previousStageKey) {
      return;
    }

    const stageIndex = this.serviceBlueprint.stages.findIndex(stage => stage.stateKey === previousStageKey);
    if (stageIndex < 0) {
      return;
    }

    let gateways = serviceBlueprintGateways(this.serviceBlueprint);
    let initialStageKey = this.serviceBlueprint.initialStage;
    let stages = [...serviceBlueprintStages(this.serviceBlueprint)];
    stages[stageIndex] = nextStage;

    if (nextStage.stateKey !== previousStageKey) {
      stages = stages.map(stage => stage.stateKey === nextStage.stateKey
        ? stage
        : ({
            ...stage,
            routes: (stage.routes ?? []).map(route => ({
              ...route,
              target: route.target === previousStageKey ? nextStage.stateKey : route.target,
            })),
          }));
      gateways = serviceBlueprintGateways(this.serviceBlueprint).map(gateway => ({
        ...gateway,
        routes: (gateway.routes ?? []).map(route => ({
          ...route,
          target: route.target === previousStageKey ? nextStage.stateKey : route.target,
        })),
      }));
      if (initialStageKey === previousStageKey) {
        initialStageKey = nextStage.stateKey;
      }
    }

    const serviceBlueprint: AuthoredServiceBlueprint = {
      ...this.serviceBlueprint,
      initialStage: initialStageKey,
      stages,
      gateways,
    };

    this._emitServiceBlueprintUpdated(serviceBlueprint, { kind: 'stage', stageKey: nextStage.stateKey });
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

  private _updateRouteActions(event: CustomEvent<ActionsUpdatedDetail>) {
    if (!this.serviceBlueprint) {
      return;
    }
    const target = event.currentTarget as HTMLElement | null;
    const idxAttr = target?.dataset.prismRouteIndex;
    const transitionIndex = idxAttr ? Number(idxAttr) : NaN;
    if (!Number.isInteger(transitionIndex)) {
      return;
    }
    const transition = (flattenRoutes(this.serviceBlueprint))[transitionIndex];
    if (!transition) {
      return;
    }
    this._replaceSelectedTransition(
      { ...transition, actions: event.detail.actions },
      transitionIndex
    );
  }

  private _handleRouteActionSelected(event: CustomEvent<ActionSelectedDetail>) {
    event.stopPropagation();
    const target = event.currentTarget as HTMLElement | null;
    const idxAttr = target?.dataset.prismRouteIndex;
    const transitionIndex = idxAttr ? Number(idxAttr) : NaN;
    const detail: ActionSelectedDetail = {
      ...event.detail,
      target: 'transition',
      transitionIndex: Number.isInteger(transitionIndex) ? transitionIndex : undefined,
    };
    this.dispatchEvent(
      new CustomEvent<ActionSelectedDetail>('action-selected', {
        detail,
        bubbles: true,
        composed: true,
      })
    );
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

  private _updateStageIcon(stage: AuthoredStage, iconName: NodeIconName) {
    if (stage.icon === iconName) {
      return;
    }
    this._replaceSelectedStage({ ...stage, icon: iconName });
    this._announce(`${stage.displayName} icon updated.`);
  }

  private _updateStageKey(event: Event) {
    const stage = this._selectedStage;
    if (!stage || !this.serviceBlueprint) {
      return;
    }

    const nextKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextKey) {
      this._stageKeyError = 'Stage key is required.';
      this._announce('Stage key is required.');
      return;
    }

    const duplicate = this.serviceBlueprint.stages.some(candidate =>
      candidate.stateKey === nextKey && candidate.stateKey !== stage.stateKey
    );
    if (duplicate) {
      this._stageKeyError = 'Stage key must be unique.';
      this._announce(`Stage key ${nextKey} is already in use.`);
      return;
    }

    if (nextKey === stage.stateKey) {
      this._stageKeyError = null;
      return;
    }

    this._stageKeyError = null;
    this._replaceSelectedStage({ ...stage, stateKey: nextKey }, stage.stateKey);
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

  private _updateStageQueue(event: Event) {
    const stage = this._selectedStage;
    if (!stage) {
      return;
    }

    const queueKey = (event.currentTarget as HTMLInputElement).value;
    const nextStage = applyQueueToStage(stage, queueKey);

    this._replaceSelectedStage(nextStage);
    this._announce(`${stage.displayName} queue updated.`);
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
    };

    this._replaceSelectedStage(nextStage);
    this._announce(`${stage.displayName} type updated.`);
  }

  private _routeIndexFromEvent(event: Event): number | null {
    const target = event.currentTarget as HTMLElement | null;
    const raw = target?.dataset.prismRouteIndex;
    const index = raw ? Number(raw) : NaN;
    return Number.isInteger(index) ? index : null;
  }

  private _routeTransitionFromEvent(event: Event): { index: number; transition: RouteView } | null {
    if (!this.serviceBlueprint) {
      return null;
    }
    const index = this._routeIndexFromEvent(event);
    if (index === null) {
      return null;
    }
    const transition = (flattenRoutes(this.serviceBlueprint))[index];
    return transition ? { index, transition } : null;
  }

  private _updateRouteLabel(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const action = (event.currentTarget as HTMLInputElement).value.trim();
    if (!action || action === ctx.transition.action) return;
    this._replaceSelectedTransition({ ...ctx.transition, action }, ctx.index);
    this._announce(`Route label updated to ${action}.`);
  }

  private _updateRouteActionPreset(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const nextAction = (event.currentTarget as HTMLSelectElement).value;
    if (nextAction === 'custom' || nextAction === ctx.transition.action) return;
    this._replaceSelectedTransition({ ...ctx.transition, action: nextAction }, ctx.index);
    this._announce(`Route preset updated to ${nextAction}.`);
  }

  private _updateRouteTarget(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const toStage = (event.currentTarget as HTMLSelectElement).value;
    if (!toStage || toStage === ctx.transition.toStage) return;
    this._replaceSelectedTransition({ ...ctx.transition, toStage }, ctx.index);
    this._announce(`Route now arrives at ${this._stageLabel(toStage)}.`);
  }

  private _updateRouteToGateway(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const toGateway = (event.currentTarget as HTMLSelectElement).value || undefined;
    if (toGateway === ctx.transition.toGateway) return;
    this._replaceSelectedTransition({ ...ctx.transition, toGateway }, ctx.index);
    this._announce(
      toGateway
        ? `Route now arrives through ${this._gatewayLabel(toGateway)}.`
        : 'Route now arrives directly at the target stage.'
    );
  }

  private _updateRouteConditionMode(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const mode = (event.currentTarget as HTMLSelectElement).value as TransitionConditionMode;
    const current = parseTransitionCondition(ctx.transition.condition);
    const condition = serialiseTransitionCondition(mode, mode === current.mode ? current.value : '');
    this._replaceSelectedTransition({ ...ctx.transition, condition }, ctx.index);
    this._announce(
      mode === 'always'
        ? 'Route condition cleared.'
        : `${mode === 'event' ? 'Event' : 'Guard'} condition enabled.`
    );
  }

  private _updateRouteConditionValue(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const parsed = parseTransitionCondition(ctx.transition.condition);
    const condition = serialiseTransitionCondition(
      parsed.mode === 'always' ? 'guard' : parsed.mode,
      (event.currentTarget as HTMLInputElement).value
    );
    this._replaceSelectedTransition({ ...ctx.transition, condition }, ctx.index);
    this._announce('Route condition updated.');
  }

  private _updateRouteRole(event: Event) {
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const requiresRole = (event.currentTarget as HTMLInputElement).value.trim() || undefined;
    if (requiresRole === ctx.transition.requiresRole) return;
    this._replaceSelectedTransition({ ...ctx.transition, requiresRole }, ctx.index);
    this._announce(requiresRole ? `Role guard updated to ${requiresRole}.` : 'Role guard cleared.');
  }

  private _deleteRoute(event: Event) {
    if (!this.serviceBlueprint) return;
    const ctx = this._routeTransitionFromEvent(event);
    if (!ctx) return;
    const gatewayKey = ctx.transition.key;
    const routeId = ctx.transition.routeId;
    if (!gatewayKey || !routeId) return;
    const nextServiceBlueprint = deleteRoute(this.serviceBlueprint, { gatewayKey, routeId });
    const selectedGatewayKey = this._selectedGateway?.key;
    this._emitServiceBlueprintUpdated(
      nextServiceBlueprint,
      selectedGatewayKey ? { kind: 'gateway', gatewayKey: selectedGatewayKey } : null
    );
    this._announce(`Route ${ctx.transition.action} deleted.`);
  }

  private _handleAddRoute() {
    if (!this.serviceBlueprint) return;

    const sourceStageKey = this._selectedStage?.stateKey
      ?? deriveGatewayBindings(this.serviceBlueprint).find(binding => binding.gateway.key === this.selectedGatewayKey)?.anchorStageKey
      ?? null;

    if (!sourceStageKey) return;

    const { serviceBlueprint: withGateway, gatewayKey } = findOrCreateSplitGateway(this.serviceBlueprint, sourceStageKey);

    const routeId = newRouteId(sourceStageKey, '', '') + '-' + Date.now().toString(36);
    const nextRoute = {
      id: routeId,
      target: '',
      trigger: '',
      actions: [],
    };

    const nextServiceBlueprint = addRoute(withGateway, gatewayKey, nextRoute);
    this._newlyAddedRouteId = routeId;
    this._emitServiceBlueprintUpdated(nextServiceBlueprint, { kind: 'gateway', gatewayKey });
    this._announce('Route added — choose a destination.');
  }

  private _renderEmpty() {
    return html`
      <div class="empty-state" role="status">
        <p>Select a stage, gateway, or route from the workspace to inspect its details.</p>
      </div>
    `;
  }

  private _renderGatewayOutgoingRoutes(gateway: AuthoredGateway, binding: GatewayBinding | null) {
    if (!this.serviceBlueprint) return nothing;
    const isJoin = gateway.kind === 'Join';
    const indices = binding?.relatedTransitionIndices ?? [];
    const routeNoun = isJoin ? 'Incoming routes' : 'Outgoing routes';
    const sourceStageLabel = gateway.source
      ? this._stageLabel(gateway.source)
      : gateway.displayName;

    return html`
      <section class="inspector-section" aria-labelledby="section-gateway-routes">
        <div class="section-header-row">
          <h3 id="section-gateway-routes" class="section-heading">${routeNoun}</h3>
          ${!isJoin ? html`
            <button
              type="button"
              class="secondary-button"
              data-prism-add-route
              aria-label="Add route from ${sourceStageLabel}"
              @click=${this._handleAddRoute}
            >+ Add route</button>
          ` : nothing}
        </div>
        ${indices.length === 0
          ? html`
              <p class="empty-section" data-prism-gateway-routes-empty>
                No routes yet. Use <strong>+ Add route</strong> above to send this stage to its next destination.
              </p>
            `
          : html`
              <p class="action-summary" data-prism-gateway-routes-summary>
                ${indices.length} ${indices.length === 1 ? 'route' : 'routes'} ${isJoin ? 'feed into' : 'leave'} this gateway.
              </p>
              <ul class="gateway-route-list" role="list">
                ${indices.map(transitionIndex => {
                  const transition = (flattenRoutes(this.serviceBlueprint))[transitionIndex];
                  if (!transition) return nothing;
                  return html`
                    <li
                      class="gateway-route-item"
                      data-prism-gateway-route="${transitionIndex}"
                      data-prism-route-target="${transition.toStage}"
                      data-prism-route-id="${transition.routeId}"
                    >
                      ${this._renderRouteEditor(transition, transitionIndex)}
                    </li>
                  `;
                })}
              </ul>
            `}
      </section>
    `;
  }

  private _renderRouteEditor(transition: RouteView, transitionIndex: number) {
    const condition = parseTransitionCondition(transition.condition);
    const targetOptions = (this.serviceBlueprint?.stages ?? []).filter(stage => stage.stateKey !== transition.fromStage);
    const joinGateways = this._availableJoinGatewaysForStage(transition.toStage);
    const idx = String(transitionIndex);
    const ariaId = `route-${transitionIndex}-title`;
    const targetEmpty = !transition.toStage;
    const targetWarningId = `route-${transitionIndex}-target-warning`;

    return html`
      <article
        class="gateway-route-editor"
        aria-labelledby="${ariaId}"
        data-prism-route-detail="${transition.fromStage}-${transition.action}-${transition.toStage}"
      >
        <header class="gateway-route-editor-header">
          <h4 id="${ariaId}" class="gateway-route-title">${transition.action}</h4>
          <p class="action-summary gateway-routing-hint" data-prism-route-descriptor>
            ${this._routeDescriptor(transition)}
          </p>
        </header>

        <div class="field-grid">
          <label class="field-block">
            <span class="field-label">Route label</span>
            <input
              class="field-control"
              data-prism-route-label
              data-prism-route-index="${idx}"
              .value=${transition.action}
              @change=${this._updateRouteLabel}
            />
          </label>
          <label class="field-block">
            <span class="field-label">Route preset</span>
            <select
              class="field-control"
              data-prism-route-action
              data-prism-route-index="${idx}"
              @change=${this._updateRouteActionPreset}
            >
              ${TRANSITION_ACTION_OPTIONS.map(option => html`
                <option value=${option.value} ?selected=${transitionQuickAction(transition.action) === option.value}>${option.label}</option>
              `)}
              <option value="custom" ?selected=${transitionQuickAction(transition.action) === 'custom'}>Custom label</option>
            </select>
          </label>
          <label class="field-block">
            <span class="field-label">Target stage</span>
            <select
              class="field-control ${targetEmpty ? 'field-control-error' : ''}"
              data-prism-route-target-select
              data-prism-route-index="${idx}"
              aria-invalid=${String(targetEmpty)}
              aria-describedby=${targetEmpty ? targetWarningId : ''}
              @change=${this._updateRouteTarget}
            >
              <option value="" ?selected=${targetEmpty} disabled>Choose a destination…</option>
              ${targetOptions.map(stage => html`
                <option value=${stage.stateKey} ?selected=${stage.stateKey === transition.toStage}>${stage.displayName}</option>
              `)}
            </select>
            ${targetEmpty
              ? html`<span id="${targetWarningId}" class="field-error" data-prism-route-target-warning>Choose a destination</span>`
              : nothing}
          </label>
          <label class="field-block">
            <span class="field-label">Arrive through</span>
            <select
              class="field-control"
              data-prism-route-to-gateway
              data-prism-route-index="${idx}"
              @change=${this._updateRouteToGateway}
            >
              <option value="">No join gateway</option>
              ${joinGateways.map(g => html`
                <option value=${g.key} ?selected=${g.key === transition.toGateway}>${g.displayName}</option>
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
              data-prism-route-role
              data-prism-route-index="${idx}"
              .value=${transition.requiresRole ?? ''}
              placeholder="reviewer"
              @change=${this._updateRouteRole}
            />
          </label>
        </div>

        <div class="field-grid">
          <label class="field-block">
            <span class="field-label-row">
              <span class="field-label">Condition type</span>
              <prism-inline-help
                label="Condition type help"
                message="Choose Always available for a standard route, Event for named service blueprint triggers, or Guard expression when runtime data decides whether this route can run."
              ></prism-inline-help>
            </span>
            <select
              class="field-control"
              data-prism-route-condition-mode
              data-prism-route-index="${idx}"
              @change=${this._updateRouteConditionMode}
            >
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
              data-prism-route-condition-value
              data-prism-route-index="${idx}"
              .value=${condition.value}
              ?disabled=${condition.mode === 'always'}
              placeholder=${condition.mode === 'event' ? 'submit-clicked' : 'application.isComplete == true'}
              @change=${this._updateRouteConditionValue}
            />
          </label>
        </div>

        <div class="action-buttons">
          <button
            type="button"
            class="icon-button danger-button"
            data-prism-route-delete
            data-prism-route-index="${idx}"
            @click=${this._deleteRoute}
          >
            Delete route
          </button>
        </div>

        <section class="inspector-subsection" aria-labelledby="section-route-actions-${idx}">
          <div class="section-header-row">
            <h5 id="section-route-actions-${idx}" class="section-heading">Route actions</h5>
            <span class="section-meta">${transition.actions?.length ?? 0} configured</span>
          </div>
          <prism-stage-action-editor
            data-prism-route-index="${idx}"
            .actions=${transition.actions ?? []}
            .actionCatalog=${this.actionCatalog}
            .selectedActionIndex=${this.selectedActionTransitionIndex === transitionIndex ? this.selectedActionIndex : null}
            target="transition"
            subject-label="transition"
            @actions-updated=${this._updateRouteActions}
            @action-selected=${this._handleRouteActionSelected}
          ></prism-stage-action-editor>
        </section>
      </article>
    `;
  }

  @state() private _gatewayKeyError: string | null = null;

  private _replaceSelectedGateway(nextGateway: AuthoredGateway, previousGatewayKey = this._selectedGateway?.key) {
    if (!this.serviceBlueprint || !previousGatewayKey) {
      return;
    }

    const gatewayIndex = serviceBlueprintGateways(this.serviceBlueprint).findIndex(g => g.key === previousGatewayKey);
    if (gatewayIndex < 0) {
      return;
    }

    const gateways = [...serviceBlueprintGateways(this.serviceBlueprint)];
    gateways[gatewayIndex] = nextGateway;

    let nextStates = this.serviceBlueprint.stages;
    let nextGateways = gateways;
    if (nextGateway.key !== previousGatewayKey) {
      nextStates = this.serviceBlueprint.stages.map(stage => ({
        ...stage,
        routes: (stage.routes ?? []).map(route => ({
          ...route,
          target: route.target === previousGatewayKey ? nextGateway.key : route.target,
        })),
      }));
      nextGateways = gateways.map((g, idx) => idx === gatewayIndex ? g : ({
        ...g,
        routes: (g.routes ?? []).map(route => ({
          ...route,
          target: route.target === previousGatewayKey ? nextGateway.key : route.target,
        })),
      }));
    }

    this._emitServiceBlueprintUpdated(
      { ...this.serviceBlueprint, stages: nextStates, gateways: nextGateways },
      { kind: 'gateway', gatewayKey: nextGateway.key }
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

  private _updateGatewayIcon(gateway: AuthoredGateway, iconName: NodeIconName) {
    if (gateway.icon === iconName) {
      return;
    }
    this._replaceSelectedGateway({ ...gateway, icon: iconName });
    this._announce(`${gateway.displayName} icon updated.`);
  }

  private _updateGatewayKey(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || !this.serviceBlueprint) return;
    const nextKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!nextKey) {
      this._gatewayKeyError = 'Gateway key is required.';
      this._announce('Gateway key is required.');
      return;
    }

    const allKeys = [
      ...this.serviceBlueprint.stages.map(s => s.stateKey),
      ...serviceBlueprintGateways(this.serviceBlueprint).map(g => g.key).filter(k => k !== gateway.key),
    ];
    if (allKeys.includes(nextKey)) {
      this._gatewayKeyError = 'Gateway key must be unique across stages and gateways.';
      this._announce(`Key ${nextKey} is already in use.`);
      return;
    }

    if (nextKey === gateway.key) {
      this._gatewayKeyError = null;
      return;
    }

    this._gatewayKeyError = null;
    this._replaceSelectedGateway({ ...gateway, key: nextKey }, gateway.key);
    this._announce(`Gateway key updated to ${nextKey}.`);
  }

  private _updateGatewayQueue(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway) return;
    const queueKey = (event.currentTarget as HTMLInputElement).value.trim();
    if (!queueKey || queueKey === gatewayQueueKey(gateway)) return;
    this._replaceSelectedGateway({ ...gateway, queueKey, actor: queueKey.includes('business') ? 'reviewer' : queueKey });
    this._announce(`${gateway.displayName} queue updated to ${queueKey}.`);
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
    this._replaceSelectedGateway({ ...gateway, waitingContent: content });
    this._announce(`${gateway.displayName} waiting message updated.`);
  }

  private _updateJoinWaitingExpectedSeconds(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const raw = (event.currentTarget as HTMLInputElement).value;
    const expectedWaitSeconds = raw ? Number(raw) : undefined;
    this._replaceSelectedGateway({ ...gateway, waitingExpectedSeconds: expectedWaitSeconds });
    this._announce(`${gateway.displayName} expected wait updated.`);
  }

  private _updateJoinWaitingAllowDefer(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const allowDefer = (event.currentTarget as HTMLInputElement).checked;
    this._replaceSelectedGateway({
      ...gateway,
      waitingAllowDefer: allowDefer,
      waitingDeferMessage: allowDefer ? gateway.waitingDeferMessage : undefined,
    });
    this._announce(allowDefer ? `${gateway.displayName} defer enabled.` : `${gateway.displayName} defer disabled.`);
  }

  private _updateJoinWaitingDeferMessage(event: Event) {
    const gateway = this._selectedGateway;
    if (!gateway || gateway.kind !== 'Join') return;
    const deferMessage = (event.currentTarget as HTMLInputElement).value.trim() || undefined;
    this._replaceSelectedGateway({ ...gateway, waitingDeferMessage: deferMessage });
    this._announce(`${gateway.displayName} defer message updated.`);
  }

  private _deleteSelectedGateway() {
    const gateway = this._selectedGateway;
    if (!this.serviceBlueprint || !gateway) return;
    const gateways = serviceBlueprintGateways(this.serviceBlueprint).filter(g => g.key !== gateway.key);
    const nextServiceBlueprint = {
      ...this.serviceBlueprint,
      stages: this.serviceBlueprint.stages.map(stage => ({
        ...stage,
        routes: (stage.routes ?? []).filter(route => route.target !== gateway.key),
      })),
      gateways: gateways.map(candidate => ({
        ...candidate,
        routes: (candidate.routes ?? []).filter(route => route.target !== gateway.key),
      })),
    };
    this._emitServiceBlueprintUpdated(nextServiceBlueprint, null);
    this._announce(`${gateway.displayName} gateway deleted.`);
  }

  private _renderGateway(gateway: AuthoredGateway) {
    const queueKey = gatewayQueueKey(gateway);
    const queueLabel = stageQueueLabel(this.serviceBlueprint, queueKey, this.availableQueues);
    const binding = this.serviceBlueprint
      ? deriveGatewayBindings(this.serviceBlueprint).find(candidate => candidate.gateway.key === gateway.key) ?? null
      : null;
    const queueOptionsId = `gateway-queue-options-${gateway.key}`;
    const waiting = gateway.waiting;
    const isJoin = gateway.kind === 'Join';

    return html`
      <article
        class="inspector-panel"
        data-prism-gateway-detail="${gateway.key}"
        data-prism-inspector-kind="gateway"
        aria-labelledby="inspector-gateway-title"
      >
        <div class="inspector-header">
          <div>
            <p class="eyebrow">${queueLabel} queue</p>
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
                .value=${gateway.key}
                @input=${() => { this._gatewayKeyError = null; }}
                @change=${this._updateGatewayKey}
              />
              ${this._gatewayKeyError
                ? html`<span class="field-error" data-prism-gateway-key-error>${this._gatewayKeyError}</span>`
                : nothing}
            </label>
            <label class="field-block">
              <span class="field-label-row">
                <span class="field-label">Queue</span>
                <prism-inline-help
                  label="Queue help"
                  message="The queue that owns this gateway. For a join gateway, the owning queue is where waiting information is shown to users."
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-gateway-queue
                .value=${queueKey}
                list=${queueOptionsId}
                placeholder="applicant"
                @change=${this._updateGatewayQueue}
              />
              <datalist id=${queueOptionsId}>
                ${serviceBlueprintQueueOptions(this.serviceBlueprint, this.availableQueues).map(option => html`
                  <option value=${option}>${stageQueueLabel(this.serviceBlueprint, option, this.availableQueues)}</option>
                `)}
              </datalist>
            </label>
          </div>

          <div class="field-block field-block-full">
            <span class="field-label">Icon</span>
            ${this._renderIconPicker(gateway.icon ?? defaultIconForGateway(gateway), iconName => this._updateGatewayIcon(gateway, iconName))}
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
              <dd>${isJoin ? 'Join — converges multiple queue paths' : 'Split — branches into multiple queue paths'}</dd>
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
                    message="Join gateways own the waiting story for their queue. This message is shown to users in the owning queue while they wait for other queues to arrive. Authors set it here rather than on a separate waiting stage."
                  ></prism-inline-help>
                </div>
                <div class="field-grid">
                  <label class="field-block field-block-full">
                    <span class="field-label">Waiting message</span>
                    <textarea
                      class="field-control field-textarea"
                      data-prism-gateway-waiting-content
                      .value=${waiting?.content ?? ''}
                      placeholder="Explain what users in this queue are waiting for, for example: Your application is under review by the planning team."
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
                      <span>Users in this queue can defer the wait</span>
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

        ${this._renderGatewayOutgoingRoutes(gateway, binding)}

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

  /** Small icon-glyph button row — the curated set is deliberately short (see graph/node-icons.ts) so a full grid/search UI isn't needed. */
  private _renderIconPicker(selected: NodeIconName, onPick: (icon: NodeIconName) => void) {
    return html`
      <div class="icon-picker" role="radiogroup" aria-label="Icon">
        ${(Object.keys(NODE_ICONS) as NodeIconName[]).map(name => html`
          <button
            type="button"
            class="icon-picker-option ${name === selected ? 'icon-picker-option-selected' : ''}"
            role="radio"
            aria-checked=${name === selected}
            aria-label=${name}
            title=${name}
            data-prism-icon-option=${name}
            @click=${() => onPick(name)}
          >
            ${renderNodeIconSvg(NODE_ICONS[name])}
          </button>
        `)}
      </div>
    `;
  }

  private _renderStage(stage: AuthoredStage) {
    const components = stage.components ?? [];
    const actions = stage.actions ?? [];
    const outgoing = this._selectedStageOutgoing(stage);
    const stageType = stageKindToEditorStageType(stage.kind ?? 'Question');
    const queueKey = stageQueueKey(stage);
    const queueLabel = stageQueueLabel(this.serviceBlueprint, queueKey, this.availableQueues);
    const queueEyebrow = `${queueLabel} queue`;
    const queueOptionsId = `stage-queue-options-${stage.stateKey}`;
    const unreachable = this.serviceBlueprint
      ? serviceBlueprintUnreachableStages(this.serviceBlueprint).some(candidate => candidate.stateKey === stage.stateKey)
      : false;
    const orphaned = this.serviceBlueprint
      ? serviceBlueprintOrphanedStages(this.serviceBlueprint).some(candidate => candidate.stateKey === stage.stateKey)
      : false;
    const deadEnd = this.serviceBlueprint
      ? serviceBlueprintDeadEndStages(this.serviceBlueprint).some(candidate => candidate.stateKey === stage.stateKey)
      : false;
    const validationMessages = [
      ...(this._stageKeyError ? [this._stageKeyError] : []),
      ...(orphaned ? ['This stage is disconnected from the service blueprint. Add at least one route to connect it.'] : []),
      ...(deadEnd || (outgoing.length === 0 && !isTerminalStage(stage))
        ? ['Add at least one outgoing route before publishing this stage.']
        : []),
      ...(unreachable ? ['This stage is unreachable from the service blueprint start. Add or retarget an incoming route.'] : []),
    ];

    return html`
      <article
        class="inspector-panel"
        data-prism-stage-detail="${stage.stateKey}"
        data-prism-inspector-kind="stage"
        aria-labelledby="inspector-stage-title"
      >
        <div class="inspector-header">
          <div>
            <p class="eyebrow">${queueEyebrow}</p>
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
                  message="Use a stable, machine-friendly key. Transitions, validation links, and saved service blueprint JSON all depend on this value staying predictable."
                ></prism-inline-help>
              </span>
              <input
                class="field-control ${this._stageKeyError ? 'field-control-error' : ''}"
                data-prism-stage-key
                aria-invalid=${String(Boolean(this._stageKeyError))}
                .value=${stage.stateKey}
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
                <span class="field-label">Queue</span>
                <prism-inline-help
                  label="Queue help"
                  message="Use the queue name that owns this work, for example applicant, reviewer, finance, or planning. The editor keeps the internal actor and role-gate fields aligned from this queue value."
                ></prism-inline-help>
              </span>
              <input
                class="field-control"
                data-prism-stage-queue
                .value=${queueKey}
                list=${queueOptionsId}
                placeholder="planning-officer"
                @change=${this._updateStageQueue}
              />
              <datalist id=${queueOptionsId}>
                ${serviceBlueprintQueueOptions(this.serviceBlueprint, this.availableQueues).map(option => html`
                  <option value=${option}>${stageQueueLabel(this.serviceBlueprint, option, this.availableQueues)}</option>
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

          <div class="field-block field-block-full">
            <span class="field-label">Icon</span>
            ${this._renderIconPicker(stage.icon ?? defaultIconForStage(stage), iconName => this._updateStageIcon(stage, iconName))}
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
          <prism-stage-action-editor
            .actions=${actions}
            .actionCatalog=${this.actionCatalog}
            .selectedActionIndex=${this.selectedActionIndex}
            target="stage"
            subject-label="stage"
            @actions-updated=${this._updateSelectedStageActions}
            @action-selected=${this._handleActionSelected}
          ></prism-stage-action-editor>
        </section>

        <section class="inspector-section" aria-labelledby="stage-transitions-heading">
          <div class="section-header-row">
            <h3 id="stage-transitions-heading" class="section-heading">Outgoing routes</h3>
            <button
              type="button"
              class="secondary-button"
              data-prism-add-route
              aria-label="Add route from ${stage.displayName}"
              @click=${this._handleAddRoute}
            >+ Add route</button>
          </div>
          ${outgoing.length === 0
            ? html`<p class="section-empty">No routes yet. Use <strong>+ Add route</strong> above to send this stage to its next destination.</p>`
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

        <section class="inspector-section" aria-labelledby="stage-components-heading">
          <div class="section-header-row">
            <h3 id="stage-components-heading" class="section-heading">Components</h3>
            <span class="section-meta">${components.length}</span>
          </div>
          ${components.length === 0
            ? html`<p class="section-empty">No components defined for this stage.</p>`
            : html`
                <ul class="field-list">
                  ${components.map(component => html`
                    <li class="field-item">
                      <span class="field-item-label">${describeComponent(component)}</span>
                      <span class="field-item-meta">${component.type}</span>
                    </li>
                  `)}
                </ul>
              `}
          <p class="section-empty">
            To edit components in detail, switch to the <strong>Definition</strong> tab and edit this stage's
            <code>components</code> block in the JSON editor.
          </p>
        </section>
      </article>
    `;
  }

  render() {
    const gateway = this._selectedGateway;
    const stage = gateway ? null : this._selectedStage;

    return html`
      <div class="step-inspector-root" data-prism-component="step-inspector" tabindex="0">
        <div id="inspector-announcer" class="sr-only" role="status" aria-live="polite" aria-atomic="true">${this._statusMessage ?? ''}</div>
        ${gateway
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

    .icon-picker {
      display: flex;
      flex-wrap: wrap;
      gap: 0.375rem;
    }

    .icon-picker-option {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      background: #ffffff;
      color: #475569;
      cursor: pointer;
    }

    .icon-picker-option:hover {
      border-color: #94a3b8;
      color: #1e293b;
    }

    .icon-picker-option-selected {
      border-color: #1d4ed8;
      background: #eff6ff;
      color: #1d4ed8;
    }

    .icon-picker-option:focus-visible {
      outline: 3px solid #1d4ed8;
      outline-offset: 2px;
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
