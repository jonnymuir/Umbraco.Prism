import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

@customElement('prism-create-tenant-modal')
export class PrismCreateTenantModalElement extends UmbElementMixin(LitElement) {
  @state()
  private _name = '';

  @state()
  private _hostname = '';

  // This is required by the Umbraco Modal System
  modalContext?: any;

  private _handleClose() {
    this.modalContext?.submit();
  }

  private async _handleSubmit() {
    const tenant = {
      name: this._name,
      hostname: this._hostname,
      themeColor: '#3544b1'
    };

    // 1. Consume Auth Context to get the token (as we did in the dashboard)
    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {

      if (!authContext) return;
      const token = await authContext.getLatestToken();

      // 2. Use the single-object pattern for umbHttpClient.post
      const { error } = (await tryExecute(
        this,
        umbHttpClient.post({
          url: '/umbraco/management/api/v1/prism/tenants',
          body: tenant,
          headers: {
            'Authorization': `Bearer ${token}`
          }
        })
      )) as any;

      if (error) {
        console.error("Save failed", error);
        // You could add a notification here later!
        return;
      }

      // 3. Success! Close the modal and trigger the refresh in dashboard
      this.modalContext?.submit();
    });
  }

  render() {
    return html`
      <uui-dialog-layout headline="Create New Tenant">
        <div style="display:flex; flex-direction:column; gap: 16px;">
          <uui-label>Name</uui-label>
          <uui-input .value=${this._name} @input=${(e: any) => this._name = e.target.value}></uui-input>
          
          <uui-label>Hostname</uui-label>
          <uui-input .value=${this._hostname} @input=${(e: any) => this._hostname = e.target.value}></uui-input>
        </div>
        
        <uui-button slot="actions" @click=${this._handleClose}>Cancel</uui-button>
        <uui-button slot="actions" look="primary" color="positive" @click=${this._handleSubmit}>Create Tenant</uui-button>
      </uui-dialog-layout>
    `;
  }

  static styles = css`
    :host {
      display: block;
      width: 500px;
      min-height: 800px;
      height: 100%;
      background-color: var(--uui-color-surface);
      padding: var(--uui-size-layout-1);
      box-sizing: border-box;
    }

    form {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-4);
    }
  `;
}