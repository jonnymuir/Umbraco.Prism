import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js'; // Decorators live here now
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_MODAL_MANAGER_CONTEXT, UmbModalManagerContext } from '@umbraco-cms/backoffice/modal';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { PrismCreateTenantModalElement } from './prism-create-tenant-modal.ts';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

console.log('Modal element loaded:', PrismCreateTenantModalElement);

@customElement('prism-dashboard')
export class PrismDashboardElement extends UmbElementMixin(LitElement) {

  @state()
  private _tenants: any[] = [];

  private async _openCreateModal() {
    // Use consumeContext to safely get the manager with the correct type
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance: UmbModalManagerContext | undefined) => {
      if (!instance) return;

      const modalHandler = instance.open(this, 'Prism.CreateTenantModal');
      
      modalHandler.onSubmit().then(() => {
        this._fetchTenants();
      }).catch(() => {
        // User closed the modal without submitting
      });
    });
  }

  async connectedCallback() {
    super.connectedCallback();
    this._fetchTenants();
  }

  async _fetchTenants() {
    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const tokenCustom = await authContext.getLatestToken();

      // 3. Perform the request with the manual header
      const { data, error } = (await tryExecute(
        this,
        umbHttpClient.get({
          url: '/umbraco/management/api/v1/prism/tenants',
          headers: {
            'Authorization': `Bearer ${tokenCustom}`
          }
        })
      )) as any;

      if (error) {
        console.error("Prism API Error", error);
        return;
      }

      this._tenants = data ?? [];
    });
  }

  render() {
    return html`
      <div style="padding: 20px;">
        <uui-box headline="Prism Multi-Tenant Manager">
          <uui-button look="primary" color="positive" @click=${this._openCreateModal} style="margin-bottom: 20px;">
            Add New Tenant
          </uui-button>
          <uui-button look="placeholder" @click=${this._fetchTenants} style="width:100%; margin-bottom: 20px;">
            Refresh Tenants
          </uui-button>
          <uui-table>
            <uui-table-head>
              <uui-table-head-cell>Name</uui-table-head-cell>
              <uui-table-head-cell>Hostname</uui-table-head-cell>
              <uui-table-head-cell>Color</uui-table-head-cell>
            </uui-table-head>
            ${this._tenants.map(t => html`
              <uui-table-row>
                <uui-table-cell>${t.name}</uui-table-cell>
                <uui-table-cell>${t.hostname}</uui-table-cell>
                <uui-table-cell>
                    <div style="background:${t.themeColor}; width:20px; height:20px; border-radius:4px;"></div>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>
        </uui-box>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      color: var(--uui-color-text-main);
    }
  `;
}