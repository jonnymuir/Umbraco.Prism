import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { biometricBridge, type BiometricBridge } from './biometric-bridge';

type UiState = 'idle' | 'confirm-revoke' | 'revoking' | 'revoke-error';

@customElement('prism-biometric-settings')
export class PrismBiometricSettingsElement extends LitElement {
  @property({ type: String })
  tenantHost = '';

  /** Whether biometric login is currently registered for this device/tenant. */
  @property({ type: Boolean })
  registered = false;

  @state()
  private _isAvailable = false;

  @state()
  private _uiState: UiState = 'idle';

  @state()
  private _errorMessage = '';

  /** Allow injecting a mock bridge for Storybook/tests. */
  _mockBridge?: BiometricBridge;

  private get _bridge(): BiometricBridge {
    return this._mockBridge || biometricBridge;
  }

  async connectedCallback() {
    super.connectedCallback();
    this._isAvailable = await this._bridge.isAvailable();
  }

  private _handleSetupRequested() {
    this.dispatchEvent(
      new CustomEvent('prism-biometric-setup-requested', {
        bubbles: true,
        composed: true
      })
    );
  }

  private _handleDisableClick() {
    this._uiState = 'confirm-revoke';
    this._errorMessage = '';
  }

  private _handleCancelRevoke() {
    this._uiState = 'idle';
    this._errorMessage = '';
  }

  private async _handleConfirmRevoke() {
    if (!this.tenantHost) {
      this._uiState = 'revoke-error';
      this._errorMessage = 'Tenant host is required.';
      return;
    }

    this._uiState = 'revoking';
    this._errorMessage = '';

    try {
      await this._bridge.revokeDevice(this.tenantHost);
      this._uiState = 'idle';
      this.registered = false;
      this.dispatchEvent(
        new CustomEvent('prism-biometric-revoked', {
          bubbles: true,
          composed: true
        })
      );
    } catch (error) {
      this._uiState = 'revoke-error';
      this._errorMessage =
        error instanceof Error ? error.message : 'Failed to disable biometric login. Please try again.';
    }
  }

  private _renderUnavailable() {
    return html`
      <p class="status-text">Biometric login is not supported on this device.</p>
    `;
  }

  private _renderNotRegistered() {
    return html`
      <div class="settings-row">
        <p class="status-text">Biometric login is not set up.</p>
        <button class="btn btn-primary" @click=${this._handleSetupRequested}>
          Set up biometric login
        </button>
      </div>
    `;
  }

  private _renderRegistered() {
    return html`
      <div class="settings-row">
        <p class="status-text">Biometric login is enabled.</p>
        <button class="btn btn-danger" @click=${this._handleDisableClick}>
          Disable biometric login
        </button>
      </div>
    `;
  }

  private _renderConfirmRevoke() {
    return html`
      <div
        class="confirm-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        aria-describedby="confirm-dialog-desc"
      >
        <p id="confirm-dialog-title" class="confirm-title">Disable biometric login?</p>
        <p id="confirm-dialog-desc" class="confirm-desc">
          Are you sure? You'll need to set up again to use biometric login.
        </p>
        <div class="confirm-actions">
          <button class="btn btn-secondary" @click=${this._handleCancelRevoke}>Cancel</button>
          <button class="btn btn-danger" @click=${this._handleConfirmRevoke}>Disable</button>
        </div>
      </div>
    `;
  }

  private _renderRevoking() {
    return html`
      <div class="settings-row">
        <span class="loading-spinner" aria-hidden="true"></span>
        <span class="status-text" aria-live="polite" aria-busy="true">Disabling biometric login…</span>
      </div>
    `;
  }

  private _renderError() {
    return html`
      <div class="error-message" role="alert">${this._errorMessage}</div>
      <div class="settings-row">
        <p class="status-text">Biometric login is enabled.</p>
        <button class="btn btn-danger" @click=${this._handleDisableClick}>
          Disable biometric login
        </button>
      </div>
    `;
  }

  render() {
    if (!this._isAvailable) {
      return this._renderUnavailable();
    }

    if (!this.registered) {
      return this._renderNotRegistered();
    }

    switch (this._uiState) {
      case 'confirm-revoke':
        return this._renderConfirmRevoke();
      case 'revoking':
        return this._renderRevoking();
      case 'revoke-error':
        return this._renderError();
      default:
        return this._renderRegistered();
    }
  }

  static styles = css`
    :host {
      display: block;
      font-family: inherit;
    }

    .settings-row {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }

    .status-text {
      margin: 0;
      font-size: 0.9375rem;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
      font-weight: 600;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      transition: opacity 0.15s ease, transform 0.1s ease;
    }

    .btn:focus-visible {
      outline: 3px solid #4a90e2;
      outline-offset: 2px;
    }

    .btn:hover:not(:disabled) {
      opacity: 0.88;
    }

    .btn:active:not(:disabled) {
      transform: scale(0.98);
    }

    .btn:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .btn-primary {
      background-color: #4a90e2;
      color: #fff;
    }

    .btn-secondary {
      background-color: #e0e0e0;
      color: #333;
    }

    .btn-danger {
      background-color: #d9534f;
      color: #fff;
    }

    .confirm-dialog {
      border: 1px solid #d9534f;
      border-radius: 8px;
      padding: 1.25rem;
      background-color: #fff8f8;
      max-width: 28rem;
    }

    .confirm-title {
      margin: 0 0 0.5rem;
      font-size: 1rem;
      font-weight: 700;
    }

    .confirm-desc {
      margin: 0 0 1rem;
      font-size: 0.875rem;
      color: #555;
    }

    .confirm-actions {
      display: flex;
      gap: 0.75rem;
    }

    .error-message {
      padding: 0.75rem 1rem;
      margin-bottom: 0.75rem;
      background-color: #fee;
      color: #c00;
      border: 1px solid #fcc;
      border-radius: 4px;
      font-size: 0.875rem;
    }

    .loading-spinner {
      display: inline-block;
      width: 1rem;
      height: 1rem;
      border: 2px solid rgba(0, 0, 0, 0.15);
      border-top-color: #4a90e2;
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
      flex-shrink: 0;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    @media (prefers-color-scheme: dark) {
      .confirm-dialog {
        background-color: #2a1a1a;
        border-color: #b94a48;
      }

      .confirm-desc {
        color: #aaa;
      }

      .btn-secondary {
        background-color: #444;
        color: #eee;
      }

      .error-message {
        background-color: #400;
        color: #fcc;
        border-color: #600;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-biometric-settings': PrismBiometricSettingsElement;
  }
}
