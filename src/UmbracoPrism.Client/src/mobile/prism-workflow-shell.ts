// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.

import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { WorkflowApiClient } from './workflow-api-client';
import { WorkflowOrchestrator, type StateChangeEvent } from './workflow-orchestrator';
import type { WorkflowResponseEnvelope } from './workflow-api-client';
import './prism-workflow-collect';
import './prism-workflow-completion';

@customElement('prism-workflow-shell')
export class PrismWorkflowShellElement extends LitElement {
  @property({ type: String, attribute: 'definition-key' })
  definitionKey: string = '';

  @property({ type: String, attribute: 'correlation-id' })
  correlationId?: string;

  @state()
  private _orchestrator: WorkflowOrchestrator | null = null;

  @state()
  private _currentState: string = 'idle';

  @state()
  private _envelope: WorkflowResponseEnvelope | null = null;

  connectedCallback(): void {
    super.connectedCallback();
    this._initialize();
  }

  disconnectedCallback(): void {
    super.disconnectedCallback();
    if (this._orchestrator) {
      this._orchestrator.cancel();
    }
  }

  private async _initialize(): Promise<void> {
    if (!this.definitionKey) {
      console.error('prism-workflow-shell: definition-key attribute is required');
      return;
    }

    const apiClient = new WorkflowApiClient();
    this._orchestrator = new WorkflowOrchestrator(apiClient);

    this._orchestrator.addEventListener('state-change', ((event: CustomEvent<StateChangeEvent>) => {
      this._currentState = event.detail.state;
      this._envelope = event.detail.envelope;
      this.requestUpdate();
    }) as EventListener);

    await this._orchestrator.start(this.definitionKey, this.correlationId);
  }

  private _handleAction(event: CustomEvent<{ actionKey: string; fieldValues?: Record<string, unknown> }>): void {
    if (!this._orchestrator) return;

    const { actionKey, fieldValues } = event.detail;
    this._orchestrator.advance(actionKey, fieldValues);
  }

  private _renderErrorSummary(): unknown {
    if (!this._orchestrator || this._orchestrator.validationProblems.length === 0) {
      return null;
    }

    const problems = this._orchestrator.validationProblems;

    return html`
      <div class="error-summary" role="alert" tabindex="-1" aria-labelledby="error-summary-title">
        <h2 id="error-summary-title" class="error-summary__title">There is a problem</h2>
        <ul class="error-summary__list">
          ${problems.map(
            (problem) => html`
              <li>
                ${problem.fieldKey
                  ? html`<a href="#field-${problem.fieldKey}">${problem.message}</a>`
                  : html`<span>${problem.message}</span>`}
              </li>
            `
          )}
        </ul>
      </div>
    `;
  }

  private _renderContent(): unknown {
    if (!this._envelope) {
      return html`<div class="spinner" role="status" aria-label="Loading workflow"></div>`;
    }

    const { render } = this._envelope;
    if (!render) {
      return html`<p>No content to display</p>`;
    }

    switch (render.archetype) {
      case 'Collect':
        return html`
          <prism-workflow-collect
            .fieldGroups=${render.fieldGroups}
            .availableActions=${render.availableActions}
            .problems=${this._orchestrator?.validationProblems || []}
            @action=${this._handleAction}
          ></prism-workflow-collect>
        `;

      case 'Completion':
        return html`
          <prism-workflow-completion
            .stateDisplayName=${render.stateDisplayName}
            .availableActions=${render.availableActions}
            @action=${this._handleAction}
          ></prism-workflow-completion>
        `;

      default:
        return html`<p>Unsupported archetype: ${render.archetype}</p>`;
    }
  }

  render() {
    const isLoading =
      this._currentState === 'creating' ||
      this._currentState === 'submitting' ||
      this._currentState === 'polling';

    const isError = this._currentState === 'error';

    return html`
      <div class="workflow-shell">
        ${isError ? this._renderErrorSummary() : null}
        ${isLoading
          ? html`<div class="spinner" role="status" aria-label="Loading"></div>`
          : this._renderContent()}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--prism-workflow-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
      color: var(--prism-workflow-text-color, #0b0c0c);
    }

    .workflow-shell {
      max-width: 960px;
      margin: 0 auto;
      padding: 20px;
    }

    .error-summary {
      border: 3px solid #d4351c;
      border-left-width: 5px;
      padding: 15px;
      margin-bottom: 30px;
      background: #fff;
    }

    .error-summary:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
    }

    .error-summary__title {
      margin: 0 0 15px 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #d4351c;
    }

    .error-summary__list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .error-summary__list li {
      margin-bottom: 10px;
    }

    .error-summary__list a {
      color: #d4351c;
      font-weight: 700;
      text-decoration: underline;
    }

    .error-summary__list a:hover {
      text-decoration-thickness: 3px;
    }

    .spinner {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 200px;
    }

    .spinner::after {
      content: '';
      width: 40px;
      height: 40px;
      border: 4px solid var(--prism-workflow-accent-color, #1d70b8);
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-shell': PrismWorkflowShellElement;
  }
}
