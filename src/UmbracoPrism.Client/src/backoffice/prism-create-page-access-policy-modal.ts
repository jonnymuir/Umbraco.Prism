import { LitElement, html, css } from 'lit';
import { customElement, state, property } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import '@umbraco-cms/backoffice/document';

interface PrismPageAccessPolicy {
  id: number;
  contentKey: string;
  requiresSignIn: boolean;
  allowedTenantNames: string[];
  contentName: string | null;
}

interface PrismTenant {
  id: number;
  name: string;
}

@customElement('prism-create-page-access-policy-modal')
export class PrismCreatePageAccessPolicyModalElement extends UmbElementMixin(LitElement) {

  /**
   * Data passed in from the Modal Manager. If 'policy' is present, we are in Edit mode.
   */
  @property({ type: Object })
  public data?: {
    policy?: PrismPageAccessPolicy;
    tenants?: PrismTenant[];
  };

  modalContext?: any;

  @state() private _id: number | null = null;
  @state() private _contentKey = '';
  @state() private _requiresSignIn = false;
  @state() private _allowedTenantNames: string[] = [];

  private get _tenants(): PrismTenant[] {
    return this.data?.tenants ?? [];
  }

  connectedCallback() {
    super.connectedCallback();
    this.setAttribute('role', 'dialog');
    this.setAttribute('aria-modal', 'true');
    this.setAttribute('aria-label', this.data?.policy ? 'Edit Page Access Policy' : 'Add Page Access Policy');

    if (this.data?.policy) {
      this._populateFromPolicy(this.data.policy);
    }
  }

  protected willUpdate(changedProperties: Map<string, unknown>) {
    super.willUpdate(changedProperties);

    if (changedProperties.has('data')) {
      this.setAttribute('aria-label', this.data?.policy ? 'Edit Page Access Policy' : 'Add Page Access Policy');
      if (this.data?.policy) {
        this._populateFromPolicy(this.data.policy);
      }
    }
  }

  private _populateFromPolicy(policy: PrismPageAccessPolicy) {
    this._id = policy.id;
    this._contentKey = policy.contentKey;
    this._requiresSignIn = policy.requiresSignIn;
    this._allowedTenantNames = [...policy.allowedTenantNames];
  }

  private _onDocumentSelectionChange(event: Event) {
    const picker = event.target as HTMLElement & { selection: string[] };
    this._contentKey = picker.selection?.[0] ?? '';
  }

  private _toggleTenant(tenantName: string, checked: boolean) {
    this._allowedTenantNames = checked
      ? [...this._allowedTenantNames, tenantName]
      : this._allowedTenantNames.filter((name) => name !== tenantName);
  }

  private _isValid() {
    return this._contentKey.trim().length > 0;
  }

  private async _handleSubmit() {
    if (!this._isValid()) return;

    const payload = {
      contentKey: this._contentKey,
      requiresSignIn: this._requiresSignIn,
      allowedTenantNames: this._allowedTenantNames
    };

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const isUpdate = this._id !== null;
      const endpoint = isUpdate
        ? `/umbraco/management/api/v1/prism/page-access/${this._id}`
        : '/umbraco/management/api/v1/prism/page-access';

      const { error } = (await tryExecute(
        this,
        umbHttpClient[isUpdate ? 'put' : 'post']({
          url: endpoint,
          body: payload,
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this.modalContext?.submit();
      } else {
        console.error('Failed to save page-access policy', error);
      }
    });
  }

  render() {
    const isUpdate = this._id !== null;

    return html`
      <uui-dialog-layout>
        <div slot="headline" class="dialog-headline">
          <div class="dialog-headline-actions">
            <button
              class="dialog-action-btn dialog-action-btn--primary"
              aria-label=${isUpdate ? 'Update Policy' : 'Create Policy'}
              ?disabled=${!this._isValid()}
              autofocus
              @click=${this._handleSubmit}>
              ${isUpdate ? 'Update Policy' : 'Create Policy'}
            </button>
            <button
              class="dialog-action-btn"
              aria-label="Cancel"
              @click=${() => this.modalContext?.reject()}>
              Cancel
            </button>
          </div>
          <div class="dialog-headline-icons">
            <button
              class="dialog-icon-btn"
              aria-label="Close"
              title="Close"
              @click=${() => this.modalContext?.reject()}>
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6L6 18"/><path d="M6 6l12 12"/></svg>
            </button>
          </div>
        </div>

        <div class="container">
          <uui-box>
            <div class="field">
              <uui-label>Page</uui-label>
              <p class="description">The content node this policy guards.</p>
              <umb-input-document
                max="1"
                .selection=${this._contentKey ? [this._contentKey] : []}
                @change=${this._onDocumentSelectionChange}>
              </umb-input-document>
            </div>

            <div class="field">
              <div class="toggle-label">
                <span>Require sign-in</span>
                <span class="toggle-hint">Anonymous visitors are redirected to sign in before seeing this page.</span>
              </div>
              <label class="toggle-switch" title="${this._requiresSignIn ? 'Sign-in required' : 'Sign-in not required'}">
                <input
                  type="checkbox"
                  aria-label="Require sign-in"
                  .checked=${this._requiresSignIn}
                  @change=${(e: Event) => { this._requiresSignIn = (e.target as HTMLInputElement).checked; }}
                />
                <span class="toggle-slider"></span>
              </label>
            </div>

            <div class="field">
              <uui-label>Available to</uui-label>
              <p class="description">
                Leave every tenant unchecked to make this page available to all tenants. Check
                specific tenants to restrict it — an unlisted tenant gets a 404 for this page.
              </p>
              ${this._tenants.length === 0 ? html`
                <p class="description">No tenants configured yet.</p>
              ` : html`
                <div class="tenant-list">
                  ${this._tenants.map((tenant) => html`
                    <uui-checkbox
                      label=${tenant.name}
                      .checked=${this._allowedTenantNames.includes(tenant.name)}
                      @change=${(e: any) => this._toggleTenant(tenant.name, Boolean(e.target.checked))}>
                      ${tenant.name}
                    </uui-checkbox>
                  `)}
                </div>
              `}
            </div>
          </uui-box>
        </div>
      </uui-dialog-layout>
    `;
  }

  static styles = css`
    :host {
      display: block;
      width: 500px;
      height: 100%;
      min-height: 400px;
      background-color: var(--uui-color-surface);
      position: relative;
      max-width: 95vw;
      max-height: 90vh;
      overflow: auto;
    }
    .dialog-headline {
      display: flex;
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      gap: var(--uui-size-space-3, 9px);
      flex-shrink: 0;
      padding: var(--uui-size-space-3, 9px) 0;
      background: var(--uui-color-surface);
    }
    .dialog-headline-actions {
      display: flex;
      flex-direction: row;
      gap: 8px;
      align-items: center;
    }
    .dialog-headline-icons {
      display: flex;
      flex-direction: row;
      gap: 6px;
      align-items: center;
      flex-shrink: 0;
    }
    .dialog-action-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: none;
      border: 1px solid var(--uui-color-border-standalone, #c2c2c2);
      cursor: pointer;
      padding: 0 var(--uui-size-space-4, 12px);
      height: 30px;
      font-size: var(--uui-type-small-size, 13px);
      font-family: inherit;
      color: var(--uui-color-text, #060606);
      border-radius: var(--uui-border-radius, 3px);
    }
    .dialog-action-btn--primary {
      background-color: var(--uui-color-positive, #2bc37b);
      border-color: var(--uui-color-positive, #2bc37b);
      color: var(--uui-color-positive-contrast, #fff);
    }
    .dialog-action-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .dialog-icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: none;
      border: none;
      cursor: pointer;
      padding: 4px;
      color: var(--uui-color-text-alt, #605e5c);
      border-radius: var(--uui-border-radius, 3px);
    }
    .container {
      min-height: 300px;
    }
    .field {
      display: flex;
      flex-direction: column;
      margin-bottom: var(--uui-size-space-5);
    }
    uui-label {
      margin-bottom: var(--uui-size-space-2);
      font-weight: bold;
    }
    .description {
      color: var(--uui-color-text-alt);
      margin-bottom: var(--uui-size-space-3);
      font-size: 0.85rem;
    }
    .tenant-list {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2);
    }
    .toggle-label {
      display: flex;
      flex-direction: column;
      margin-bottom: var(--uui-size-space-2);
    }
    .toggle-hint {
      color: var(--uui-color-text-alt);
      font-size: 0.8rem;
    }
    .toggle-switch {
      position: relative;
      display: inline-block;
      width: 40px;
      height: 22px;
    }
    .toggle-switch input {
      opacity: 0;
      width: 0;
      height: 0;
    }
    .toggle-slider {
      position: absolute;
      cursor: pointer;
      inset: 0;
      background-color: var(--uui-color-border-standalone, #c2c2c2);
      transition: 0.2s;
      border-radius: 22px;
    }
    .toggle-slider::before {
      position: absolute;
      content: '';
      height: 16px;
      width: 16px;
      left: 3px;
      bottom: 3px;
      background-color: white;
      transition: 0.2s;
      border-radius: 50%;
    }
    .toggle-switch input:checked + .toggle-slider {
      background-color: var(--uui-color-positive, #2bc37b);
    }
    .toggle-switch input:checked + .toggle-slider::before {
      transform: translateX(18px);
    }
  `;
}
