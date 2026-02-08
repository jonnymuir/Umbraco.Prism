import { LitElement, html, css } from 'lit';
import { customElement, state, property } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

@customElement('prism-create-tenant-modal')
export class PrismCreateTenantModalElement extends UmbElementMixin(LitElement) {
  
  /**
   * Data passed in from the Modal Manager.
   * If 'tenant' is present, we are in Edit mode.
   */
  @property({ type: Object })
  public data?: {
    tenant?: any;
    brandingTabs?: Array<{
      label: string;
      variables: Array<{
        name: string;
        defaultValue?: string;
        overrideValue?: string;
      }>;
    }>;
  };

  @state() private _activeTab = 'general';
  @state() private _brandingTabs: Array<{
    label: string;
    variables: Array<{
      name: string;
      defaultValue?: string;
      overrideValue?: string;
    }>;
  }> = [];
  
  // Form State
  @state() private _id: number | null = null;
  @state() private _name = '';
  @state() private _hostname = '';
  @state() private _entraTenantId = '';
  @state() private _entraClientId = '';
  @state() private _secretKeyName = '';


  modalContext?: any;

  /**
   * Lifecycle method that runs when the element is added to the DOM.
   * We use this to populate the form if we are editing an existing tenant.
   */
  connectedCallback() {
    super.connectedCallback();
    
    if (this.data?.tenant) {
      const t = this.data.tenant;
      this._id = t.id;
      this._name = t.name ?? '';
      this._hostname = t.hostname ?? '';
      this._entraTenantId = t.entraTenantId ?? '';
      this._entraClientId = t.entraClientId ?? '';
      this._secretKeyName = t.secretKeyName ?? '';
    }
  }

  protected updated(changedProperties: Map<string, unknown>) {
    super.updated(changedProperties);

    if (changedProperties.has('data')) {
      if (this.data?.tenant) {
        const t = this.data.tenant;
        this._id = t.id ?? null;
        this._name = t.name ?? '';
        this._hostname = t.hostname ?? '';
        this._entraTenantId = t.entraTenantId ?? '';
        this._entraClientId = t.entraClientId ?? '';
        this._secretKeyName = t.secretKeyName ?? '';
      } else {
        this._id = null;
        this._name = '';
        this._hostname = '';
        this._entraTenantId = '';
        this._entraClientId = '';
        this._secretKeyName = '';
      }

      this._brandingTabs = this.data?.brandingTabs ?? [];
      this._ensureActiveTab();
    }
  }

  private _ensureActiveTab() {
    const brandingTabKeys = this._brandingTabs.map((_, index) => this._brandingTabKey(index));
    const allowedTabs = new Set(['general', 'identity', ...brandingTabKeys]);

    if (!allowedTabs.has(this._activeTab)) {
      this._activeTab = 'general';
    }
  }

  private _brandingTabKey(index: number) {
    return `branding-${index}`;
  }

  private _handleTabGroupClick(event: MouseEvent) {
    const path = event.composedPath() as Array<EventTarget>;
    const tab = path.find((item) => item instanceof HTMLElement && item.dataset?.tabKey) as HTMLElement | undefined;

    if (!tab) return;

    const nextTab = tab.dataset.tabKey;
    if (nextTab && nextTab !== this._activeTab) {
      this._activeTab = nextTab;
    }
  }

  private async _handleSubmit() {
    if (!this._name || !this._hostname) {
      this._activeTab = 'general';
      return;
    }

    const brandingOverrides = this._collectBrandingOverrides();

    const tenant = {
      id: this._id,
      name: this._name,
      hostname: this._hostname,
      themeColor: '#3544b1', // Defaulting for now, could be a color picker later
      entraTenantId: this._entraTenantId,
      entraClientId: this._entraClientId,
      secretKeyName: this._secretKeyName,
      brandingOverrides
    };

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const isUpdate = this._id !== null;
      const endpoint = isUpdate 
        ? `/umbraco/management/api/v1/prism/tenants/${this._id}` 
        : '/umbraco/management/api/v1/prism/tenants';

      // Use 'put' for updates and 'post' for new records
      const { error } = (await tryExecute(
        this,
        umbHttpClient[isUpdate ? 'put' : 'post']({
          url: endpoint,
          body: tenant,
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this.modalContext?.submit();
      } else {
        console.error("Failed to save tenant", error);
      }
    });
  }

  private _renderGeneralTab() {
    return html`
      <div role="tabpanel" id="general-panel" aria-labelledby="general-tab" class="tab-content">
        <uui-box>
          <div class="field">
            <uui-label for="tenant-name">Tenant Name</uui-label>
            <uui-input 
              id="tenant-name" 
              label="Tenant Name" 
              .value=${this._name} 
              @input=${(e: any) => this._name = e.target.value}
              required>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="hostname">Hostname</uui-label>
            <uui-input 
              id="hostname" 
              label="Hostname" 
              placeholder="e.g. tenant-a.com" 
              .value=${this._hostname} 
              @input=${(e: any) => this._hostname = e.target.value}
              required>
            </uui-input>
          </div>
        </uui-box>
      </div>
    `;
  }

  private _renderIdentityTab() {
    return html`
      <div role="tabpanel" id="identity-panel" aria-labelledby="identity-tab" class="tab-content">
        <uui-box>
          <p class="description">Configure Microsoft Entra ID integration. Branding is managed in the Azure Portal.</p>
          
          <div class="field">
            <uui-label for="tenant-id">Directory (Tenant) ID</uui-label>
            <uui-input 
              id="tenant-id" 
              label="Directory ID" 
              .value=${this._entraTenantId} 
              @input=${(e: any) => this._entraTenantId = e.target.value}>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="client-id">Application (Client) ID</uui-label>
            <uui-input 
              id="client-id" 
              label="Client ID" 
              .value=${this._entraClientId} 
              @input=${(e: any) => this._entraClientId = e.target.value}>
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="secret-name">Key Vault Secret Name</uui-label>
            <uui-input 
              id="secret-name" 
              label="Secret Name" 
              .value=${this._secretKeyName} 
              @input=${(e: any) => this._secretKeyName = e.target.value}>
            </uui-input>
            <small id="secret-hint">Must match the secret identifier in your configured Azure Key Vault.</small>
          </div>
        </uui-box>
      </div>
    `;
  }

  private _renderBrandingTab(tabIndex: number) {
    const tab = this._brandingTabs[tabIndex];
    if (!tab) return html``;

    return html`
      <div
        role="tabpanel"
        id="branding-panel-${tabIndex}"
        aria-labelledby="branding-tab-${tabIndex}"
        class="tab-content">
        <uui-box>
          <uui-table>
            <uui-table-column style="width: 35%"></uui-table-column>
            <uui-table-column style="width: 35%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Variable</uui-table-head-cell>
              <uui-table-head-cell>Default</uui-table-head-cell>
              <uui-table-head-cell>Override</uui-table-head-cell>
            </uui-table-head>

            ${tab.variables.map((variable, variableIndex) => html`
              <uui-table-row>
                <uui-table-cell><code>${variable.name}</code></uui-table-cell>
                <uui-table-cell><code>${variable.defaultValue ?? '—'}</code></uui-table-cell>
                <uui-table-cell>
                  <uui-input
                    class="override-input"
                    placeholder="e.g. #0d6efd"
                    .value=${variable.overrideValue ?? ''}
                    @input=${(e: InputEvent) => this._updateBrandingOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
                  </uui-input>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>
        </uui-box>
      </div>
    `;
  }

  private _updateBrandingOverride(tabIndex: number, variableIndex: number, value: string) {
    this._brandingTabs = this._brandingTabs.map((tab, index) => {
      if (index !== tabIndex) return tab;
      return {
        ...tab,
        variables: tab.variables.map((variable, vIndex) =>
          vIndex === variableIndex ? { ...variable, overrideValue: value } : variable
        )
      };
    });
  }

  private _collectBrandingOverrides() {
    const overrides: Record<string, string> = {};
    this._brandingTabs.forEach(tab => {
      tab.variables.forEach(variable => {
        if (variable.overrideValue && variable.overrideValue.trim().length > 0) {
          overrides[variable.name] = variable.overrideValue.trim();
        }
      });
    });

    return overrides;
  }

  render() {
    const isUpdate = this._id !== null;
    const brandingTabs = this._brandingTabs.map((tab, index) => ({
      tab,
      key: this._brandingTabKey(index)
    }));

    return html`
      <uui-dialog-layout headline="${isUpdate ? 'Edit' : 'Register New'} Tenant">
        
        <uui-tab-group @click=${this._handleTabGroupClick}>
          <uui-tab 
            label="General" 
            data-tab-key="general"
            ?active=${this._activeTab === 'general'}>
            General
          </uui-tab>
          <uui-tab 
            label="Identity" 
            data-tab-key="identity"
            ?active=${this._activeTab === 'identity'}>
            Identity
          </uui-tab>
          ${brandingTabs.map(({ tab, key }, index) => html`
            <uui-tab
              label=${tab.label}
              id="branding-tab-${index}"
              data-tab-key=${key}
              ?active=${this._activeTab === key}>
              ${tab.label}
            </uui-tab>
          `)}
        </uui-tab-group>

        <div class="container">
          ${this._activeTab === 'general'
            ? this._renderGeneralTab()
            : this._activeTab === 'identity'
              ? this._renderIdentityTab()
              : brandingTabs.map(({ key }, index) =>
                  this._activeTab === key ? this._renderBrandingTab(index) : ''
                )}
        </div>
        
        <uui-button slot="actions" @click=${() => this.modalContext?.reject()}>Cancel</uui-button>
        <uui-button 
            slot="actions" 
            look="primary" 
            color="positive" 
            @click=${this._handleSubmit}>
            ${isUpdate ? 'Update Tenant' : 'Create Tenant'}
        </uui-button>
      </uui-dialog-layout>
    `;
  }

  static styles = css`
    :host {
      display: block;
      width: 700px;
      height: 100%;
      min-height: 550px;
      background-color: var(--uui-color-surface);
      position: relative;
      resize: both;
      overflow: auto;
      max-width: 95vw;
      max-height: 90vh;
    }
    .container { 
      min-height: 350px;
    }
    .override-input {
      width: 100%;
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
    uui-input { width: 100%; }
    .description { 
      color: var(--uui-color-text-alt); 
      margin-bottom: var(--uui-size-space-5); 
      font-size: 0.9rem;
    }
    small { 
      margin-top: var(--uui-size-space-2); 
      color: var(--uui-color-text-alt); 
    }
  `;
}