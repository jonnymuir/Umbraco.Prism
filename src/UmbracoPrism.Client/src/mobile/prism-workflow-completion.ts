// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.

import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { WorkflowAction } from './workflow-api-client';

@customElement('prism-workflow-completion')
export class PrismWorkflowCompletionElement extends LitElement {
  @property({ type: String })
  stateDisplayName: string = 'Complete';

  @property({ type: Array })
  availableActions: WorkflowAction[] = [];

  private _handleAction(actionKey: string): void {
    this.dispatchEvent(
      new CustomEvent('action', {
        detail: { actionKey },
        bubbles: true,
        composed: true,
      })
    );
  }

  render() {
    return html`
      <div class="workflow-completion">
        <div class="govuk-panel govuk-panel--confirmation">
          <h1 class="govuk-panel__title">${this.stateDisplayName}</h1>
        </div>

        ${this.availableActions.length > 0
          ? html`
              <div class="govuk-button-group">
                ${this.availableActions.map((action) => {
                  let buttonClass = 'govuk-button';
                  if (action.style === 'secondary') {
                    buttonClass += ' govuk-button--secondary';
                  }

                  return html`
                    <button
                      type="button"
                      class="${buttonClass}"
                      @click=${() => this._handleAction(action.actionKey)}
                    >
                      ${action.label}
                    </button>
                  `;
                })}
              </div>
            `
          : null}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    .workflow-completion {
      max-width: 640px;
      margin: 0 auto;
    }

    /* GDS Confirmation Panel */
    .govuk-panel {
      padding: 35px;
      border: 5px solid transparent;
      text-align: center;
      box-sizing: border-box;
    }

    .govuk-panel--confirmation {
      background-color: #00703c;
      color: #fff;
    }

    .govuk-panel__title {
      margin: 0;
      font-size: 2rem;
      font-weight: 700;
      line-height: 1.09375;
    }

    @media (min-width: 40.0625em) {
      .govuk-panel__title {
        font-size: 3rem;
        line-height: 1.04167;
      }
    }

    /* GDS Button Group */
    .govuk-button-group {
      margin-top: 30px;
      display: flex;
      gap: 15px;
      flex-wrap: wrap;
      justify-content: center;
    }

    /* GDS Buttons */
    .govuk-button {
      font-size: 1rem;
      font-weight: 600;
      padding: 10px 20px;
      border: 2px solid transparent;
      background-color: #00703c;
      color: #fff;
      cursor: pointer;
      text-align: center;
      text-decoration: none;
      min-height: 44px;
      appearance: none;
      border-radius: 0;
    }

    .govuk-button:hover {
      background-color: #005a30;
    }

    .govuk-button:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
      box-shadow: inset 0 0 0 1px #0b0c0c;
      background-color: #00703c;
    }

    .govuk-button--secondary {
      background-color: #f3f2f1;
      color: #0b0c0c;
      box-shadow: 0 2px 0 #929191;
    }

    .govuk-button--secondary:hover {
      background-color: #dbdad9;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-completion': PrismWorkflowCompletionElement;
  }
}
