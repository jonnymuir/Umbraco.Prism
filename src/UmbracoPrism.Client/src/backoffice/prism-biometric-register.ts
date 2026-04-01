import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { biometricBridge, type BiometricBridge } from './biometric-bridge.js';

type ComponentState = 'idle' | 'loading' | 'success' | 'error';

@customElement('prism-biometric-register')
export class PrismBiometricRegisterElement extends LitElement {
  @property({ type: String })
  tenantHost = '';

  @property({ type: String })
  loginHint?: string;

  @state()
  private _state: ComponentState = 'idle';

  @state()
  private _errorMessage = '';

  @state()
  private _isAvailable = false;

  // Allow mocking the bridge for testing/Storybook
  _mockBridge?: BiometricBridge;

  private get _bridge(): BiometricBridge {
    return this._mockBridge || biometricBridge;
  }

  async connectedCallback() {
    super.connectedCallback();
    this._isAvailable = await this._bridge.isAvailable();
    
    if (!this._isAvailable) {
      this.hidden = true;
    }
  }

  private async _handleRegister() {
    if (!this.tenantHost) {
      this._state = 'error';
      this._errorMessage = 'Tenant host is required';
      return;
    }

    this._state = 'loading';
    this._errorMessage = '';

    try {
      await this._bridge.register(this.tenantHost, this.loginHint);
      this._state = 'success';
      
      setTimeout(() => {
        this.dispatchEvent(
          new CustomEvent('prism-biometric-registered', {
            bubbles: true,
            composed: true
          })
        );
      }, 1500);
    } catch (error: any) {
      this._state = 'error';
      
      if (error && error.name === 'BiometricError' && error.code) {
        this._errorMessage = this._mapErrorCodeToMessage(error.code);
      } else {
        this._errorMessage = 'Something went wrong. Please try again.';
      }
    }
  }

  private _mapErrorCodeToMessage(code: string): string {
    switch (code) {
      case 'cancelled':
        return 'Registration cancelled';
      case 'not_enrolled':
        return 'No biometrics enrolled on this device. Please set up Face ID or fingerprint in Settings.';
      case 'locked_out':
        return 'Too many attempts. Please try again later.';
      case 'unavailable':
        return 'Biometric login is not available on this device.';
      default:
        return 'Something went wrong. Please try again.';
    }
  }

  private _handleRetry() {
    this._state = 'idle';
    this._errorMessage = '';
  }

  render() {
    if (!this._isAvailable) {
      return html``;
    }

    return html`
      <div class="container">
        ${this._state === 'idle' ? this._renderIdle() : ''}
        ${this._state === 'loading' ? this._renderLoading() : ''}
        ${this._state === 'success' ? this._renderSuccess() : ''}
        ${this._state === 'error' ? this._renderError() : ''}
      </div>
    `;
  }

  private _renderIdle() {
    return html`
      <button 
        @click=${this._handleRegister}
        aria-label="Enable biometric login for faster authentication"
        class="register-button">
        <svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8z"/>
          <path d="M12 6v6l4 2"/>
        </svg>
        <span>Enable Biometric Login</span>
      </button>
    `;
  }

  private _renderLoading() {
    return html`
      <div class="status-message" aria-busy="true" aria-live="polite">
        <div class="spinner"></div>
        <span>Setting up biometric login...</span>
      </div>
    `;
  }

  private _renderSuccess() {
    return html`
      <div class="status-message success" role="status" aria-live="polite">
        <svg class="icon check" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M20 6L9 17l-5-5"/>
        </svg>
        <span>Biometric login enabled ✓</span>
      </div>
    `;
  }

  private _renderError() {
    return html`
      <div class="error-container">
        <div class="status-message error" role="alert">
          <svg class="icon error-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/>
            <line x1="12" y1="8" x2="12" y2="12"/>
            <line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          <span>${this._errorMessage}</span>
        </div>
        <button 
          @click=${this._handleRetry}
          aria-label="Try enabling biometric login again"
          class="retry-button">
          Try Again
        </button>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif);
    }

    :host([hidden]) {
      display: none;
    }

    .container {
      padding: var(--uui-size-space-4, 1rem);
    }

    .register-button {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3, 0.75rem);
      width: 100%;
      padding: var(--uui-size-space-4, 1rem) var(--uui-size-space-5, 1.25rem);
      background: var(--uui-color-interactive, #3544b1);
      color: var(--uui-color-interactive-contrast, #ffffff);
      border: none;
      border-radius: var(--uui-border-radius, 6px);
      font-size: var(--uui-type-font-size-default, 1rem);
      font-weight: 500;
      cursor: pointer;
      transition: background-color 0.2s ease;
    }

    .register-button:hover {
      background: var(--uui-color-interactive-hover, #2a3690);
    }

    .register-button:focus {
      outline: 2px solid var(--uui-color-focus, #3544b1);
      outline-offset: 2px;
    }

    .register-button:active {
      transform: translateY(1px);
    }

    .status-message {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3, 0.75rem);
      padding: var(--uui-size-space-4, 1rem);
      border-radius: var(--uui-border-radius, 6px);
      font-size: var(--uui-type-font-size-default, 1rem);
    }

    .status-message.success {
      background: var(--uui-color-positive-surface, #f0fdf4);
      color: var(--uui-color-positive, #16a34a);
    }

    .status-message.error {
      background: var(--uui-color-danger-surface, #fef2f2);
      color: var(--uui-color-danger, #dc2626);
    }

    .error-container {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-3, 0.75rem);
    }

    .retry-button {
      padding: var(--uui-size-space-3, 0.75rem) var(--uui-size-space-4, 1rem);
      background: var(--uui-color-surface, #ffffff);
      color: var(--uui-color-text, #1f2937);
      border: 1px solid var(--uui-color-border, #d1d5db);
      border-radius: var(--uui-border-radius, 6px);
      font-size: var(--uui-type-font-size-default, 1rem);
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .retry-button:hover {
      background: var(--uui-color-surface-emphasis, #f9fafb);
      border-color: var(--uui-color-border-emphasis, #9ca3af);
    }

    .retry-button:focus {
      outline: 2px solid var(--uui-color-focus, #3544b1);
      outline-offset: 2px;
    }

    .icon {
      width: 20px;
      height: 20px;
      flex-shrink: 0;
    }

    .check {
      stroke-width: 3;
    }

    .spinner {
      width: 20px;
      height: 20px;
      border: 2px solid var(--uui-color-border, #d1d5db);
      border-top-color: var(--uui-color-interactive, #3544b1);
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
    'prism-biometric-register': PrismBiometricRegisterElement;
  }
}
