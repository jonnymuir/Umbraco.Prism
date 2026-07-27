import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';

export type ConfidenceTab = 'canvas' | 'validation' | 'preview' | 'simulation' | 'definition' | 'help';

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-confidence-tabs')
export class PrismConfidenceTabs extends LitElement {
  @property({ attribute: 'active-tab' })
  activeTab: ConfidenceTab = 'canvas';

  @property({ type: Number, attribute: 'error-count' })
  errorCount = 0;

  @property({ type: Number, attribute: 'warning-count' })
  warningCount = 0;

  private static readonly _tabs: ConfidenceTab[] = ['canvas', 'validation', 'preview', 'simulation', 'definition', 'help'];

  private _handleTabClick(tab: ConfidenceTab) {
    if (this.activeTab !== tab) {
      this.dispatchEvent(
        new CustomEvent('tab-changed', {
          detail: { tab },
          bubbles: true,
          composed: true,
        })
      );
    }
  }

  private _moveTabFocus(currentTab: ConfidenceTab, direction: -1 | 1) {
    const tabs = PrismConfidenceTabs._tabs;
    const currentIndex = tabs.indexOf(currentTab);
    const nextIndex = (currentIndex + direction + tabs.length) % tabs.length;
    const nextTab = tabs[nextIndex];
    this._handleTabClick(nextTab);
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLButtonElement>(`#confidence-tab-${nextTab}`)
        ?.focus();
    });
  }

  private _handleTabKeydown(event: KeyboardEvent, tab: ConfidenceTab) {
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        event.preventDefault();
        this._moveTabFocus(tab, 1);
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        event.preventDefault();
        this._moveTabFocus(tab, -1);
        break;
      case 'Home':
        event.preventDefault();
        this._handleTabClick('canvas');
        requestAnimationFrame(() => {
          this.shadowRoot?.querySelector<HTMLButtonElement>('#confidence-tab-canvas')?.focus();
        });
        break;
      case 'End':
        event.preventDefault();
        this._handleTabClick('help');
        requestAnimationFrame(() => {
          this.shadowRoot?.querySelector<HTMLButtonElement>('#confidence-tab-help')?.focus();
        });
        break;
      default:
        break;
    }
  }

  private _renderTabButton(tab: ConfidenceTab, label: string, badge?: number) {
    const isActive = this.activeTab === tab;
    const badgeHtml = typeof badge === 'number' && badge > 0
      ? html`<span class="tab-badge" data-prism-tab-badge="${tab}">${badge}</span>`
      : nothing;

    return html`
      <button
        type="button"
        class="tab-button ${isActive ? 'tab-button-active' : ''}"
        role="tab"
        aria-selected="${isActive}"
        aria-controls="confidence-panel-${tab}"
        id="confidence-tab-${tab}"
        tabindex=${isActive ? '0' : '-1'}
        data-prism-confidence-tab="${tab}"
        @click=${() => this._handleTabClick(tab)}
        @keydown=${(event: KeyboardEvent) => this._handleTabKeydown(event, tab)}
      >
        <span class="tab-label">${label}</span>
        ${badgeHtml}
      </button>
    `;
  }

  render() {
    const validationBadge = this.errorCount + this.warningCount;

    return html`
      <div class="tabs-root" data-prism-confidence-tabs>
        <div class="tab-bar" role="tablist" aria-label="Editor tools">
          ${this._renderTabButton('canvas', 'Canvas')}
          ${this._renderTabButton('validation', 'Validation', validationBadge)}
          ${this._renderTabButton('preview', 'Preview')}
          ${this._renderTabButton('simulation', 'Simulation')}
          ${this._renderTabButton('definition', 'Definition')}
          ${this._renderTabButton('help', 'Help')}
        </div>

        <div class="tab-panel-container">
          <div
            id="confidence-panel-canvas"
            class="tab-panel tab-panel-canvas ${this.activeTab === 'canvas' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-canvas"
            data-prism-confidence-panel="canvas"
            ?hidden=${this.activeTab !== 'canvas'}
          >
            <slot name="canvas"></slot>
          </div>

          <div
            id="confidence-panel-validation"
            class="tab-panel ${this.activeTab === 'validation' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-validation"
            data-prism-confidence-panel="validation"
            ?hidden=${this.activeTab !== 'validation'}
          >
            <slot name="validation"></slot>
          </div>

          <div
            id="confidence-panel-preview"
            class="tab-panel ${this.activeTab === 'preview' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-preview"
            data-prism-confidence-panel="preview"
            ?hidden=${this.activeTab !== 'preview'}
          >
            <slot name="preview"></slot>
          </div>

          <div
            id="confidence-panel-simulation"
            class="tab-panel ${this.activeTab === 'simulation' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-simulation"
            data-prism-confidence-panel="simulation"
            ?hidden=${this.activeTab !== 'simulation'}
          >
            <slot name="simulation"></slot>
          </div>

          <div
            id="confidence-panel-definition"
            class="tab-panel tab-panel-definition ${this.activeTab === 'definition' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-definition"
            data-prism-confidence-panel="definition"
            ?hidden=${this.activeTab !== 'definition'}
          >
            <slot name="definition"></slot>
          </div>

          <div
            id="confidence-panel-help"
            class="tab-panel ${this.activeTab === 'help' ? 'tab-panel-active' : ''}"
            role="tabpanel"
            aria-labelledby="confidence-tab-help"
            data-prism-confidence-panel="help"
            ?hidden=${this.activeTab !== 'help'}
          >
            <slot name="help"></slot>
          </div>
        </div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      background: #ffffff;
      border-top: 2px solid #b1b4b6;
      font-family: "GDS Transport", arial, sans-serif;
      overflow: hidden;
    }

    .tabs-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow: hidden;
    }

    .tab-bar {
      display: flex;
      gap: 0;
      border-bottom: 2px solid #b1b4b6;
      background: #f8f8f8;
      flex-shrink: 0;
    }

    .tab-button {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.875rem 1.25rem;
      border: none;
      border-bottom: 3px solid transparent;
      background: transparent;
      color: #0b0c0c;
      font: inherit;
      font-size: 0.9375rem;
      font-weight: 600;
      cursor: pointer;
      transition: background-color 0.15s, border-color 0.15s;
      position: relative;
      bottom: -2px;
    }

    .tab-button:hover {
      background: #ffffff;
    }

    .tab-button:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: -3px;
      z-index: 1;
    }

    .tab-button-active {
      background: #ffffff;
      border-bottom-color: #1d70b8;
      color: #1d70b8;
    }

    .tab-label {
      line-height: 1.3;
    }

    .tab-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 1.5rem;
      height: 1.5rem;
      padding: 0 0.4rem;
      border-radius: 999px;
      background: #d4351c;
      color: #ffffff;
      font-size: 0.75rem;
      font-weight: 700;
      line-height: 1;
    }

    .tab-button-active .tab-badge {
      background: #1d70b8;
    }

    .tab-panel-container {
      flex: 1;
      min-height: 0;
      overflow: hidden;
      position: relative;
      display: flex;
      flex-direction: column;
    }

    .tab-panel {
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      display: none;
    }

    .tab-panel-canvas {
      overflow: hidden;
    }

    .tab-panel-definition {
      overflow: hidden;
      padding: 0;
    }

    .tab-panel-active {
      display: flex;
      flex-direction: column;
    }

    ::slotted(*) {
      flex: 1;
      min-height: 0;
      display: flex !important;
      flex-direction: column !important;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-confidence-tabs': PrismConfidenceTabs;
  }
}
