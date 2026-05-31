import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { AuthoredGateway, RouteView, AuthoredWorkflow } from './types.js';
import { deriveGatewayBindings } from './workflow-gateway-representation.js';
import { flattenRoutes } from './workflow-routes.js';
import { stageLaneKey, stageLaneLabel } from './workflow-stage-assignment.js';

/**
 * @internal Composition detail of <prism-workflow-editor>; not part of the public API surface.
 */
@customElement('prism-workflow-outline')
export class PrismWorkflowOutline extends LitElement {
  @property({ type: Object })
  workflow: AuthoredWorkflow | null = null;

  @property({ type: Boolean, attribute: 'show-header' })
  showHeader = true;

  @property({ attribute: 'selected-stage-key' })
  selectedStageKey: string | null = null;

  @property({ attribute: 'selected-transition-index', type: Number })
  selectedTransitionIndex: number | null = null;

  @property({ attribute: 'selected-gateway-key' })
  selectedGatewayKey: string | null = null;

  private _handleStageClick(stageKey: string) {
    this.dispatchEvent(
      new CustomEvent('outline-stage-selected', {
        detail: { stageKey },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _handleTransitionClick(transitionIndex: number) {
    this.dispatchEvent(
      new CustomEvent('outline-transition-selected', {
        detail: { transitionIndex },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _handleGatewayClick(gatewayKey: string) {
    this.dispatchEvent(
      new CustomEvent('outline-gateway-selected', {
        detail: { gatewayKey },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _gatewayLabel(gatewayKey: string | undefined | null): string {
    if (!gatewayKey) return '';
    return this.workflow?.gateways?.find(g => g.gatewayKey === gatewayKey)?.displayName ?? gatewayKey;
  }

  private _stageOutboundTransitions(stageKey: string): { transition: RouteView; index: number }[] {
    if (!this.workflow) {
      return [];
    }

    return (flattenRoutes(this.workflow))
      .map((transition, index) => ({ transition, index }))
      .filter(({ transition }) => transition.fromStage === stageKey);
  }

  private _splitGatewaysForStage(stageKey: string): AuthoredGateway[] {
    if (!this.workflow) {
      return [];
    }

    return deriveGatewayBindings(this.workflow)
      .filter(binding => binding.gateway.kind === 'Split' && binding.anchorStageKey === stageKey)
      .map(binding => binding.gateway);
  }

  private _laneGroups() {
    if (!this.workflow) {
      return [];
    }

    const groups = new Map<string, { key: string; label: string; stages: AuthoredWorkflow['stages'] }>();
    for (const stage of this.workflow.stages) {
      const laneKey = stageLaneKey(stage) || stage.actor || 'public';
      const existing = groups.get(laneKey);
      if (existing) {
        existing.stages.push(stage);
        continue;
      }

      groups.set(laneKey, {
        key: laneKey,
        label: stageLaneLabel(this.workflow, laneKey),
        stages: [stage],
      });
    }

    return [...groups.values()];
  }

  render() {
    if (!this.workflow) {
      return html`
        <div class="outline-empty">
          <p class="outline-empty-text">No workflow loaded</p>
        </div>
      `;
    }

    const stages = this.workflow.stages || [];
    const laneGroups = this._laneGroups();

    if (stages.length === 0) {
      return html`
        <div class="outline-empty">
          <p class="outline-empty-text">Start by adding your first stage</p>
          <p class="outline-empty-hint">The outline will group stages by lane once the workflow starts taking shape.</p>
        </div>
      `;
    }

    return html`
      <nav class="outline-root" aria-label="Workflow structure outline">
        ${this.showHeader
          ? html`
              <div class="outline-header">
                <h2 class="outline-title">Outline</h2>
                <p class="outline-subtitle">
                  ${stages.length} ${stages.length === 1 ? 'stage' : 'stages'}
                  ${this.workflow.gateways?.length ? html` · ${this.workflow.gateways.length} gateways` : nothing}
                </p>
              </div>
            `
          : nothing}

        <div class="outline-lane-groups">
          ${laneGroups.map(group => html`
            <section class="outline-lane-section" data-prism-outline-lane=${group.key}>
              <div class="outline-lane-header">
                <h3 class="outline-lane-title">${group.label}</h3>
                <p class="outline-lane-meta">Read top to bottom</p>
              </div>
              <ol class="outline-stage-list">
                ${group.stages.map((stage: AuthoredWorkflow['stages'][number]) => {
            const isSelected = this.selectedStageKey === stage.stageKey;
            const transitions = this._stageOutboundTransitions(stage.stageKey);
            const splitGateways = this._splitGatewaysForStage(stage.stageKey);

            return html`
              <li class="outline-stage-item">
                <button
                  type="button"
                  class="outline-stage-button ${isSelected ? 'outline-stage-button-selected' : ''}"
                  @click=${() => this._handleStageClick(stage.stageKey)}
                  aria-current=${isSelected ? 'location' : nothing}
                  data-prism-outline-stage="${stage.stageKey}"
                >
                  <span class="outline-stage-title">${stage.displayName}</span>
                  <span class="outline-stage-meta">${stage.actor}</span>
                </button>

                ${splitGateways.length > 0
                  ? html`
                      <ul class="outline-gateway-list">
                        ${splitGateways.map(gateway => {
                          const isGatewaySelected = this.selectedGatewayKey === gateway.gatewayKey;
                          return html`
                            <li class="outline-gateway-item">
                              <button
                                type="button"
                                class="outline-gateway-button ${isGatewaySelected ? 'outline-gateway-button-selected' : ''}"
                                @click=${() => this._handleGatewayClick(gateway.gatewayKey)}
                                aria-current=${isGatewaySelected ? 'location' : nothing}
                                data-prism-outline-gateway="${gateway.gatewayKey}"
                              >
                                <span class="outline-gateway-shape" aria-hidden="true"></span>
                                <span class="outline-gateway-copy">
                                  <span class="outline-gateway-title">${gateway.displayName}</span>
                                  <span class="outline-gateway-meta">${gateway.kind} gateway</span>
                                </span>
                              </button>
                            </li>
                          `;
                        })}
                      </ul>
                    `
                  : nothing}

                ${transitions.length > 0
                  ? html`
                      <ol class="outline-transition-list">
                        ${transitions.map(({ transition, index }) => {
                          const isTransitionSelected = this.selectedTransitionIndex === index;
                          return html`
                            <li class="outline-transition-item">
                              <button
                                type="button"
                                class="outline-transition-button ${isTransitionSelected
                                  ? 'outline-transition-button-selected'
                                  : ''}"
                                @click=${() => this._handleTransitionClick(index)}
                                aria-current=${isTransitionSelected ? 'location' : nothing}
                              >
                                <span class="outline-transition-label">${transition.action}</span>
                                <span class="outline-transition-target">
                                  ${transition.fromGateway ? `via ${this._gatewayLabel(transition.fromGateway)} → ` : ''}
                                  ${transition.toGateway ? `${this._gatewayLabel(transition.toGateway)} → ` : ''}
                                  ${transition.toStage}
                                </span>
                              </button>
                            </li>
                          `;
                        })}
                      </ol>
                    `
                  : nothing}
              </li>
            `;
               })}
             </ol>
            </section>
          `)}
        </div>
      </nav>
    `;
  }

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: #ffffff;
      border-right: 2px solid #b1b4b6;
      font-family: "GDS Transport", arial, sans-serif;
      overflow: hidden;
    }

    .outline-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow-y: auto;
    }

    .outline-header {
      padding: 1rem;
      border-bottom: 1px solid #d8dde3;
      flex-shrink: 0;
    }

    .outline-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #0b0c0c;
      line-height: 1.3;
    }

    .outline-subtitle {
      margin: 0.25rem 0 0;
      font-size: 0.875rem;
      color: #505a5f;
      line-height: 1.4;
    }

    .outline-lane-groups {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      padding: 0.75rem;
    }

    .outline-lane-section {
      border: 1px solid #d8dde3;
      border-radius: 14px;
      overflow: hidden;
      background: #ffffff;
    }

    .outline-lane-header {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #eef2f6;
      background: linear-gradient(180deg, #f8fbff 0%, #ffffff 100%);
    }

    .outline-lane-title {
      margin: 0;
      font-size: 0.9375rem;
      font-weight: 700;
      color: #0b0c0c;
    }

    .outline-lane-meta {
      margin: 0.2rem 0 0;
      font-size: 0.75rem;
      color: #475569;
    }

    .outline-stage-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .outline-stage-item {
      border-bottom: 1px solid #f3f2f1;
    }

    .outline-stage-button {
      width: 100%;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.25rem;
      padding: 0.875rem 1rem;
      border: none;
      background: transparent;
      color: #0b0c0c;
      text-align: left;
      cursor: pointer;
      font: inherit;
      transition: background-color 0.15s;
    }

    .outline-stage-button:hover {
      background: #f8f8f8;
    }

    .outline-stage-button:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: -3px;
      z-index: 1;
    }

    .outline-stage-button-selected {
      background: #1d70b8;
      color: #ffffff;
    }

    .outline-stage-button-selected:hover {
      background: #003078;
    }

    .outline-stage-button-selected .outline-stage-meta {
      color: #ffffff;
    }

    .outline-stage-title {
      font-weight: 600;
      font-size: 0.9375rem;
      line-height: 1.3;
    }

    .outline-stage-meta {
      font-size: 0.8125rem;
      color: #505a5f;
      line-height: 1.3;
    }

    .outline-transition-list {
      list-style: none;
      margin: 0;
      padding: 0;
      background: #f8f8f8;
    }

    .outline-gateway-list {
      list-style: none;
      margin: 0;
      padding: 0 1rem 0.5rem;
      background: #f8f8fc;
    }

    .outline-gateway-item {
      margin: 0;
    }

    .outline-gateway-item + .outline-gateway-item {
      margin-top: 0.375rem;
    }

    .outline-gateway-button {
      width: 100%;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.625rem 0.75rem;
      border: 1px solid #e9d5ff;
      border-radius: 12px;
      background: #ffffff;
      cursor: pointer;
      text-align: left;
      font: inherit;
    }

    .outline-gateway-button:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: -3px;
      z-index: 1;
    }

    .outline-gateway-button-selected {
      border-color: #7c3aed;
      box-shadow: 0 0 0 2px rgba(124, 58, 237, 0.12);
    }

    .outline-gateway-shape {
      width: 0.9rem;
      height: 0.9rem;
      flex-shrink: 0;
      border: 2px solid #7c3aed;
      background: #f5f3ff;
      transform: rotate(45deg);
    }

    .outline-gateway-copy {
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }

    .outline-gateway-title {
      font-size: 0.875rem;
      font-weight: 700;
      color: #0b0c0c;
    }

    .outline-gateway-meta {
      font-size: 0.75rem;
      color: #6d28d9;
    }

    .outline-transition-item {
      border-top: 1px solid #e5e7eb;
    }

    .outline-transition-button {
      width: 100%;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.2rem;
      padding: 0.625rem 1rem 0.625rem 2rem;
      border: none;
      background: transparent;
      color: #0b0c0c;
      text-align: left;
      cursor: pointer;
      font: inherit;
      transition: background-color 0.15s;
    }

    .outline-transition-button:hover {
      background: #ffffff;
    }

    .outline-transition-button:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: -3px;
      z-index: 1;
    }

    .outline-transition-button-selected {
      background: #ffffff;
      border-left: 3px solid #1d70b8;
      padding-left: calc(2rem - 3px);
    }

    .outline-transition-label {
      font-size: 0.875rem;
      font-weight: 600;
      line-height: 1.3;
    }

    .outline-transition-target {
      font-size: 0.8125rem;
      color: #505a5f;
      line-height: 1.3;
    }

    .outline-empty {
      padding: 1.5rem 1rem;
      text-align: center;
    }

    .outline-empty-text {
      margin: 0 0 0.5rem;
      font-weight: 600;
      color: #505a5f;
      font-size: 0.9375rem;
    }

    .outline-empty-hint {
      margin: 0;
      font-size: 0.875rem;
      color: #626a6e;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-outline': PrismWorkflowOutline;
  }
}
