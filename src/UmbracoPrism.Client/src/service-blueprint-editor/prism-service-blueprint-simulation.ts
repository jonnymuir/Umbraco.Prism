import { LitElement, css, html, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { AuthoredStage, StageKind } from './types.js';

export type ServiceBlueprintSimulationStopReason = 'waiting' | 'terminal' | 'no-transitions' | null;

export interface ServiceBlueprintSimulationHistoryEntry {
  stageKey: string;
  stageLabel: string;
  enteredByLabel?: string;
  enteredByTransitionIndex?: number | null;
}

export interface ServiceBlueprintSimulationTransitionOption {
  transitionIndex: number;
  label: string;
  targetStageKey: string;
  targetStageLabel: string;
  targetStageKind?: StageKind;
  blocked: boolean;
  blockerMessages: string[];
  conditionSummary?: string;
  roleSummary?: string;
}

const STOP_COPY: Record<Exclude<ServiceBlueprintSimulationStopReason, null>, string> = {
  waiting: 'Simulation stopped at a waiting stage.',
  terminal: 'Simulation reached an end stage.',
  'no-transitions': 'Simulation stopped because this stage has no outbound transitions.',
};

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-service-blueprint-simulation')
export class PrismServiceBlueprintSimulationElement extends LitElement {
  @property({ attribute: false })
  initialStage: AuthoredStage | null = null;

  @property({ attribute: false })
  currentStage: AuthoredStage | null = null;

  @property({ attribute: false })
  history: ServiceBlueprintSimulationHistoryEntry[] = [];

  @property({ attribute: false })
  transitionOptions: ServiceBlueprintSimulationTransitionOption[] = [];

  @property({ type: Boolean })
  active = false;

  @property({ type: Boolean })
  canStart = false;

  @property({ type: String })
  startBlocker = '';

  @property({ type: String })
  stopReason: ServiceBlueprintSimulationStopReason = null;

  @property({ type: String })
  announcement = '';

  private _startSimulation() {
    this.dispatchEvent(new CustomEvent('simulation-started', {
      bubbles: true,
      composed: true,
    }));
  }

  private _resetSimulation() {
    this.dispatchEvent(new CustomEvent('simulation-reset', {
      bubbles: true,
      composed: true,
    }));
  }

  private _advance(transitionIndex: number) {
    this.dispatchEvent(new CustomEvent<{ transitionIndex: number }>('simulation-transition-selected', {
      detail: { transitionIndex },
      bubbles: true,
      composed: true,
    }));
  }

  render() {
    const startLabel = this.active ? 'Restart simulation' : 'Start simulation';

    return html`
      <section class="simulation-panel" aria-labelledby="serviceBlueprint-simulation-title" data-prism-simulation-panel>
        <div class="simulation-header">
          <div>
            <p class="simulation-eyebrow">Preview and simulation</p>
            <h2 id="serviceBlueprint-simulation-title" class="simulation-title">Path simulation</h2>
            <p class="simulation-summary">
              Walk a likely route from the start stage, choose transitions, and confirm where the authored path ends.
            </p>
          </div>
          <div class="simulation-actions">
            <button
              type="button"
              class="simulation-button simulation-button-primary"
              data-prism-simulation-start
              ?disabled=${!this.canStart}
              @click=${this._startSimulation}
            >
              ${startLabel}
            </button>
            <button
              type="button"
              class="simulation-button"
              data-prism-simulation-reset
              ?disabled=${!this.active}
              @click=${this._resetSimulation}
            >
              Clear path
            </button>
          </div>
        </div>

        <div class="sr-only" role="status" aria-live="polite">${this.announcement}</div>

        ${this.initialStage
          ? html`
              <p class="simulation-meta">
                Start stage:
                <strong data-prism-simulation-initial-stage>${this.initialStage.displayName}</strong>
              </p>
            `
          : html`
              <p class="simulation-empty">
                Choose an initial stage before you simulate this service blueprint.
              </p>
            `}

        ${this.startBlocker
          ? html`
              <p class="simulation-blocker" role="alert" data-prism-simulation-start-blocker>
                ${this.startBlocker}
              </p>
            `
          : nothing}

        ${!this.active || !this.currentStage
          ? html`
              <div class="simulation-empty" data-prism-simulation-empty>
                <p class="govuk-body">
                  Start the simulation to highlight the path in the graph and show the available transitions at each step.
                </p>
              </div>
            `
          : html`
              <article class="simulation-current-stage" data-prism-simulation-current=${this.currentStage.stateKey}>
                <div class="simulation-current-header">
                  <div>
                    <p class="simulation-current-eyebrow">Current stage</p>
                    <h3 class="simulation-current-title" data-prism-simulation-current-stage>
                      ${this.currentStage.displayName}
                    </h3>
                  </div>
                  <span class="simulation-kind">${this.currentStage.metadata?.stageType ?? "Question"}</span>
                </div>
              </article>

              <div class="simulation-history">
                <h3 class="simulation-section-title">History</h3>
                <ol class="simulation-breadcrumbs" data-prism-simulation-history>
                  ${this.history.map(entry => html`
                    <li class="simulation-breadcrumb" data-prism-simulation-breadcrumb-stage=${entry.stageKey}>
                      <span class="simulation-breadcrumb-stage">${entry.stageLabel}</span>
                      ${entry.enteredByLabel
                        ? html`<span class="simulation-breadcrumb-via">via ${entry.enteredByLabel}</span>`
                        : nothing}
                    </li>
                  `)}
                </ol>
              </div>

              ${this.stopReason
                ? html`
                    <p class="simulation-stop" role="status" data-prism-simulation-stop-reason=${this.stopReason}>
                      ${STOP_COPY[this.stopReason]}
                    </p>
                  `
                : html`
                    <div class="simulation-transitions">
                      <h3 class="simulation-section-title">Available transitions</h3>
                      <ol class="simulation-transition-list">
                        ${this.transitionOptions.map(option => html`
                          <li class="simulation-transition">
                            <button
                              type="button"
                              class="simulation-transition-button"
                              data-prism-simulation-transition=${String(option.transitionIndex)}
                              ?disabled=${option.blocked}
                              @click=${() => this._advance(option.transitionIndex)}
                            >
                              <span class="simulation-transition-label">${option.label}</span>
                              <span class="simulation-transition-target">Go to ${option.targetStageLabel}</span>
                            </button>
                            <div class="simulation-transition-meta">
                              ${option.conditionSummary
                                ? html`<span>${option.conditionSummary}</span>`
                                : nothing}
                              ${option.roleSummary
                                ? html`<span>${option.roleSummary}</span>`
                                : nothing}
                            </div>
                            ${option.blockerMessages.length > 0
                              ? html`
                                  <ul class="simulation-transition-blockers" data-prism-simulation-blocker=${String(option.transitionIndex)}>
                                    ${option.blockerMessages.map(message => html`<li>${message}</li>`)}
                                  </ul>
                                `
                              : nothing}
                          </li>
                        `)}
                      </ol>
                    </div>
                  `}
            `}
      </section>
    `;
  }

  static styles = css`
    :host {
      display: block;
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

    .simulation-panel {
      padding: 1rem;
      border: 1px solid #d8dde3;
      border-radius: 12px;
      background: #ffffff;
      display: grid;
      gap: 0.875rem;
    }

    .simulation-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }

    .simulation-eyebrow,
    .simulation-current-eyebrow {
      margin: 0 0 0.25rem;
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: #1d70b8;
    }

    .simulation-title,
    .simulation-current-title,
    .simulation-section-title {
      margin: 0;
      color: #0b0c0c;
    }

    .simulation-title {
      font-size: 1rem;
    }

    .simulation-summary,
    .simulation-meta,
    .simulation-empty,
    .simulation-transition-meta {
      margin: 0;
      font-size: 0.875rem;
      line-height: 1.5;
      color: #505a5f;
    }

    .simulation-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: flex-end;
    }

    .simulation-button,
    .simulation-transition-button {
      font: inherit;
      border-radius: 8px;
      cursor: pointer;
    }

    .simulation-button {
      min-height: 2.5rem;
      padding: 0.5rem 0.9rem;
      border: 2px solid #1d70b8;
      background: #ffffff;
      color: #1d70b8;
      font-weight: 700;
    }

    .simulation-button-primary {
      background: #1d70b8;
      color: #ffffff;
    }

    .simulation-button[disabled],
    .simulation-transition-button[disabled] {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .simulation-button:focus-visible,
    .simulation-transition-button:focus-visible {
      outline: 3px solid #0b0c0c;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px #ffdd00;
    }

    .simulation-blocker,
    .simulation-stop {
      margin: 0;
      padding: 0.75rem 0.875rem;
      border-radius: 10px;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .simulation-blocker {
      background: #fff1f0;
      border-left: 4px solid #d4351c;
      color: #8a1538;
    }

    .simulation-stop {
      background: #f3f2f1;
      border-left: 4px solid #1d70b8;
      color: #0b0c0c;
    }

    .simulation-current-stage,
    .simulation-history,
    .simulation-transitions {
      border: 1px solid #d8dde3;
      border-radius: 10px;
      padding: 0.875rem;
      background: #f8fafc;
    }

    .simulation-current-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }

    .simulation-kind {
      display: inline-flex;
      align-items: center;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      background: #d2e2f1;
      color: #003078;
      font-size: 0.75rem;
      font-weight: 700;
    }

    .simulation-breadcrumbs,
    .simulation-transition-list,
    .simulation-transition-blockers {
      margin: 0.75rem 0 0;
      padding-left: 1.25rem;
    }

    .simulation-breadcrumbs,
    .simulation-transition-list {
      display: grid;
      gap: 0.5rem;
    }

    .simulation-breadcrumb {
      display: grid;
      gap: 0.1rem;
    }

    .simulation-breadcrumb-stage {
      font-weight: 700;
      color: #0b0c0c;
    }

    .simulation-breadcrumb-via {
      font-size: 0.8125rem;
      color: #505a5f;
    }

    .simulation-transition {
      display: grid;
      gap: 0.35rem;
    }

    .simulation-transition-button {
      display: grid;
      gap: 0.2rem;
      width: 100%;
      padding: 0.75rem 0.875rem;
      border: 1px solid #1d70b8;
      background: #ffffff;
      text-align: left;
    }

    .simulation-transition-label {
      font-weight: 700;
      color: #0b0c0c;
    }

    .simulation-transition-target {
      font-size: 0.875rem;
      color: #1d70b8;
    }

    .simulation-transition-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }

    .simulation-transition-blockers {
      color: #8a1538;
      font-size: 0.875rem;
    }

    @media (max-width: 720px) {
      .simulation-header,
      .simulation-current-header {
        flex-direction: column;
      }

      .simulation-actions {
        justify-content: flex-start;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-service-blueprint-simulation': PrismServiceBlueprintSimulationElement;
  }
}
