import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_MODAL_MANAGER_CONTEXT, UmbModalManagerContext } from '@umbraco-cms/backoffice/modal';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { PrismCreateTenantModalElement } from './prism-create-tenant-modal.ts';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

// Log the modal element for debugging
console.log('Modal element loaded:', PrismCreateTenantModalElement);

@customElement('prism-dashboard')
export class PrismDashboardElement extends UmbElementMixin(LitElement) {

  @state()
  private _tenants: any[] = [];

  async connectedCallback() {
    super.connectedCallback();
    this._fetchTenants();
  }

  /**
   * Fetches the list of tenants from the Management API
   */
  async _fetchTenants() {
    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const { data, error } = (await tryExecute(
        this,
        umbHttpClient.get({
          url: '/umbraco/management/api/v1/prism/tenants',
          headers: {
            'Authorization': `Bearer ${token}`
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

  /**
   * Opens the modal in "Create" mode (no tenant data passed)
   */
  private async _openCreateModal() {
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance: UmbModalManagerContext | undefined) => {
      if (!instance) return;

      const modalHandler = instance.open(this, 'Prism.CreateTenantModal', {
        type: 'sidebar',
        size: 'small'
      } as any);
      
      modalHandler.onSubmit().then(() => {
        this._fetchTenants();
      }).catch(() => {
        // Modal cancelled
      });
    });
  }

  /**
   * Opens the modal in "Edit" mode (passes the tenant object)
   */
  private async _editTenant(tenant: any) {
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance: UmbModalManagerContext | undefined) => {
      if (!instance) return;

      const modalHandler = instance.open(this, 'Prism.CreateTenantModal', {
        type: 'sidebar',
        size: 'small',
        data: { tenant } // Passing existing data triggers Edit mode in the modal
      } as any);
      
      modalHandler.onSubmit().then(() => {
        this._fetchTenants();
      });
    });
  }

  /**
   * Deletes a tenant by ID
   */
  private async _deleteTenant(id: number) {
    if (!confirm("Are you sure you want to delete this tenant? This cannot be undone.")) return;

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if(authContext === undefined) return;
      const token = await authContext.getLatestToken();
      
      const { error } = (await tryExecute(
        this,
        umbHttpClient.delete({
          url: `/umbraco/management/api/v1/prism/tenants/${id}`,
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this._fetchTenants();
      }
    });
  }

  render() {
    return html`
      <div class="dashboard-container">
        <uui-box headline="Prism Multi-Tenant Manager">
          
          <div slot="header-actions">
             <uui-button look="primary" color="positive" @click=${this._openCreateModal}>
                <uui-icon name="add"></uui-icon> Add New Tenant
             </uui-button>
          </div>

          <uui-table>
            <uui-table-column style="width: 30%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>
            <uui-table-column style="width: 25%"></uui-table-column>
            <uui-table-column style="width: 15%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Name</uui-table-head-cell>
              <uui-table-head-cell>Hostname</uui-table-head-cell>
              <uui-table-head-cell>Entra Client ID</uui-table-head-cell>
              <uui-table-head-cell>Actions</uui-table-head-cell>
            </uui-table-head>

            ${this._tenants.map(t => html`
              <uui-table-row>
                <uui-table-cell><strong>${t.name}</strong></uui-table-cell>
                <uui-table-cell><code>${t.hostname}</code></uui-table-cell>
                <uui-table-cell>
                    ${t.entraClientId 
                        ? html`<uui-tag look="primary" color="positive">${t.entraClientId.substring(0,8)}...</uui-tag>`
                        : html`<uui-tag look="secondary">Not Configured</uui-tag>`}
                </uui-table-cell>
                <uui-table-cell>
                    <uui-button-group>
                        <uui-button look="outline" label="Edit" @click=${() => this._editTenant(t)}>
                            <uui-icon name="edit"></uui-icon>
                        </uui-button>
                        <uui-button color="danger" look="outline" label="Delete" @click=${() => this._deleteTenant(t.id)}>
                            <uui-icon name="delete"></uui-icon>
                        </uui-button>
                    </uui-button-group>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>

          ${this._tenants.length === 0 ? html`
            <p class="empty-state">No tenants found. Click "Add New Tenant" to get started.</p>
          ` : ''}

        </uui-box>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    .dashboard-container {
      max-width: 1200px;
      margin: 0 auto;
    }

    .empty-state {
      text-align: center;
      padding: 40px;
      color: var(--uui-color-text-alt);
    }

    uui-table-head-cell {
      font-weight: bold;
    }

    uui-button-group {
      display: flex;
    }

    uui-button[look='outline'] {
      color: var(--uui-color-default-standalone);
    }

    uui-button[look='outline'][color='danger'] {
      color: var(--uui-color-danger-standalone);
    }

    code {
      background: var(--uui-color-surface-alt);
      padding: 2px 4px;
      border-radius: 4px;
    }
  `;
}