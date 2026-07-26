import { LitElement, css, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-inline-help')
export class PrismInlineHelpElement extends LitElement {
  @property({ type: String })
  label = 'More help';

  @property({ type: String })
  message = '';

  private get _tooltipId() {
    const token = this.label.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'help';
    return `inline-help-${token}`;
  }

  private _handleKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      (event.currentTarget as HTMLButtonElement).blur();
    }
  }

  render() {
    return html`
      <span class="inline-help">
        <button
          type="button"
          class="inline-help-button"
          aria-label=${this.label}
          aria-describedby=${this._tooltipId}
          @keydown=${this._handleKeydown}
        >
          ?
        </button>
        <span id=${this._tooltipId} class="inline-help-panel" role="tooltip">
          ${this.message}
        </span>
      </span>
    `;
  }

  static styles = css`
    :host {
      display: inline-flex;
    }

    .inline-help {
      position: relative;
      display: inline-flex;
      align-items: center;
    }

    .inline-help-button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.25rem;
      height: 1.25rem;
      padding: 0;
      border: 1px solid #1d4ed8;
      border-radius: 999px;
      background: #eff6ff;
      color: #1d4ed8;
      font: inherit;
      font-size: 0.75rem;
      font-weight: 800;
      line-height: 1;
      cursor: help;
    }

    .inline-help-button:hover {
      background: #dbeafe;
    }

    .inline-help-button:focus-visible {
      outline: 3px solid #1d4ed8;
      outline-offset: 2px;
    }

    .inline-help-panel {
      position: absolute;
      left: 0;
      top: calc(100% + 0.4rem);
      z-index: 5;
      width: min(18rem, calc(100vw - 2rem));
      padding: 0.625rem 0.75rem;
      border: 1px solid #bfdbfe;
      border-radius: 10px;
      background: #ffffff;
      box-shadow: 0 12px 24px rgba(15, 23, 42, 0.16);
      color: #1f2937;
      font-size: 0.75rem;
      line-height: 1.45;
      opacity: 0;
      visibility: hidden;
      pointer-events: none;
      transform: translateY(-0.15rem);
      transition:
        opacity 120ms ease,
        transform 120ms ease,
        visibility 120ms ease;
    }

    .inline-help:hover .inline-help-panel,
    .inline-help:focus-within .inline-help-panel {
      opacity: 1;
      visibility: visible;
      transform: translateY(0);
    }

    @media (prefers-reduced-motion: reduce) {
      .inline-help-panel {
        transition: none;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-inline-help': PrismInlineHelpElement;
  }
}
