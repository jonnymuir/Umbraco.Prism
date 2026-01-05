import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

@customElement('prism-create-tenant-modal')
export class PrismCreateTenantModalElement extends UmbElementMixin(LitElement) {
  @state() private _activeTab = 'general';
  
  @state() private _name = '';
  @state() private _hostname = '';
  @state() private _entraTenantId = '';
  @state() private _entraClientId = '';
  @state() private _secretKeyName = '';

  modalContext?: any;

  private async _handleSubmit() {
    // Basic Accessibility Validation: Ensure the user is alerted to empty fields
    if (!this._name || !this._hostname) {
      this._activeTab = 'general';
      return;
    }

    const tenant = {
      name: this._name,
      hostname: this._hostname,
      themeColor: '#3544b1',
      entraTenantId: this._entraTenantId,
      entraClientId: this._entraClientId,
      secretKeyName: this._secretKeyName
    };

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const { error } = (await tryExecute(
        this,
        umbHttpClient.post({
          url: '/umbraco/management/api/v1/prism/tenants',
          body: tenant,
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this.modalContext?.submit();
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

  render() {
    return html`
      <uui-dialog-layout headline="New Tenant Registration">
        
        <uui-tab-group>
          <uui-tab 
            label="General" 
            ?active=${this._activeTab === 'general'} 
            @click=${() => this._activeTab = 'general'}>
            General
          </uui-tab>
          <uui-tab 
            label="Identity" 
            ?active=${this._activeTab === 'identity'} 
            @click=${() => this._activeTab = 'identity'}>
            Identity
          </uui-tab>
        </uui-tab-group>

        <div class="container">
          ${this._activeTab === 'general' ? this._renderGeneralTab() : this._renderIdentityTab()}
        </div>
        
        <uui-button slot="actions" @click=${() => this.modalContext?.reject()}>Cancel</uui-button>
        <uui-button slot="actions" look="primary" color="positive" @click=${this._handleSubmit}>Create Tenant</uui-button>
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
    }
    .container { 
      min-height: 350px; 
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
    }

    small { 
      display: block; 
      margin-top: var(--uui-size-space-2); 
      color: var(--uui-color-text-alt); 
    }
  `;
}