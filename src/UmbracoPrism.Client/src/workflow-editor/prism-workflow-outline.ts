import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { AuthoredTransition, AuthoredWorkflow } from './types.js';

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

  private _stageOutboundTransitions(stageKey: string): { transition: AuthoredTransition; index: number }[] {
    if (!this.workflow) {
      return [];
    }

    return this.workflow.transitions
      .map((transition, index) => ({ transition, index }))
      .filter(({ transition }) => transition.fromStage === stageKey);
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

    if (stages.length === 0) {
      return html`
        <div class="outline-empty">
          <p class="outline-empty-text">No stages in workflow</p>
          <p class="outline-empty-hint">Create a stage to get started</p>
        </div>
      `;
    }

    return html`
      <nav class="outline-root" aria-label="Workflow structure outline">
        ${this.showHeader
          ? html`
              <div class="outline-header">
                <h2 class="outline-title">Outline</h2>
                <p class="outline-subtitle">${stages.length} ${stages.length === 1 ? 'stage' : 'stages'}</p>
              </div>
            `
          : nothing}

        <ol class="outline-stage-list">
          ${stages.map((stage) => {
            const isSelected = this.selectedStageKey === stage.stageKey;
            const transitions = this._stageOutboundTransitions(stage.stageKey);

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
                                <span class="outline-transition-target">→ ${transition.toStage}</span>
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
      overflow: hidden;
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

    .outline-stage-list {
      list-style: none;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      flex: 1;
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
