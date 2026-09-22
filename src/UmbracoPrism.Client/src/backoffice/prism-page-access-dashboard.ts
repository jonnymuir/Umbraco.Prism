import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_MODAL_MANAGER_CONTEXT, UmbModalManagerContext } from '@umbraco-cms/backoffice/modal';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

interface PrismPageAccessPolicy {
  id: number;
  contentKey: string;
  requiresSignIn: boolean;
  allowedTenantNames: string[];
  contentName: string | null;
  contentRoute: string | null;
}

interface PrismTenant {
  id: number;
  name: string;
}

@customElement('prism-page-access-dashboard')
export class PrismPageAccessDashboardElement extends UmbElementMixin(LitElement) {

  @state()
  private _policies: PrismPageAccessPolicy[] = [];

  @state()
  private _tenants: PrismTenant[] = [];

  async connectedCallback() {
    super.connectedCallback();
    this._fetchPolicies();
    this._fetchTenants();
  }

  async _fetchPolicies() {
    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const { data, error } = (await tryExecute(
        this,
        umbHttpClient.get({
          url: '/umbraco/management/api/v1/prism/page-access',
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (error) {
        console.error('Prism Page Access API Error', error);
        return;
      }

      this._policies = data ?? [];
    });
  }

  async _fetchTenants() {
    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const { data, error } = (await tryExecute(
        this,
        umbHttpClient.get({
          url: '/umbraco/management/api/v1/prism/tenants',
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (error) {
        console.error('Prism Tenants API Error (for page-access dashboard)', error);
        return;
      }

      this._tenants = data ?? [];
    });
  }

  private _tenantAvailabilityLabel(policy: PrismPageAccessPolicy) {
    if (policy.allowedTenantNames.length === 0) return 'Every tenant';
    return policy.allowedTenantNames.join(', ');
  }

  private async _openCreateModal() {
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance: UmbModalManagerContext | undefined) => {
      if (!instance) return;

      const modalHandler = instance.open(this, 'Prism.CreatePageAccessPolicyModal', {
        type: 'sidebar',
        size: 'small',
        data: { tenants: this._tenants }
      } as any);

      modalHandler.onSubmit().then(() => {
        this._fetchPolicies();
      }).catch(() => {
        // Modal cancelled
      });
    });
  }

  private async _editPolicy(policy: PrismPageAccessPolicy) {
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (instance: UmbModalManagerContext | undefined) => {
      if (!instance) return;

      const modalHandler = instance.open(this, 'Prism.CreatePageAccessPolicyModal', {
        type: 'sidebar',
        size: 'small',
        data: { policy, tenants: this._tenants }
      } as any);

      modalHandler.onSubmit().then(() => {
        this._fetchPolicies();
      });
    });
  }

  private async _deletePolicy(id: number) {
    if (!confirm('Are you sure you want to delete this page-access policy? This cannot be undone.')) return;

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const { error } = (await tryExecute(
        this,
        umbHttpClient.delete({
          url: `/umbraco/management/api/v1/prism/page-access/${id}`,
          headers: { 'Authorization': `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this._fetchPolicies();
      }
    });
  }

  render() {
    return html`
      <div class="dashboard-container">
        <uui-box headline="Prism Page Access">
          <p class="description">
            Pages that require sign-in and/or are only available to specific tenants. A tenant a
            page isn't available to gets a plain 404 for it — nothing about the page is revealed.
          </p>

          <div slot="header-actions">
             <uui-button look="primary" color="positive" label="Add Page Policy" @click=${this._openCreateModal}>
                <uui-icon name="add"></uui-icon> Add Page Policy
             </uui-button>
          </div>

          <uui-table>
            <uui-table-column style="width: 35%"></uui-table-column>
            <uui-table-column style="width: 20%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>
            <uui-table-column style="width: 15%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Page</uui-table-head-cell>
              <uui-table-head-cell>Requires sign-in</uui-table-head-cell>
              <uui-table-head-cell>Available to</uui-table-head-cell>
              <uui-table-head-cell>Actions</uui-table-head-cell>
            </uui-table-head>

            ${this._policies.map(p => html`
              <uui-table-row>
                <uui-table-cell>
                  <strong>${p.contentName ?? 'Unknown page'}</strong>
                  <br />
                  <code>${p.contentRoute ?? p.contentKey}</code>
                </uui-table-cell>
                <uui-table-cell>
                  ${p.requiresSignIn
                    ? html`<uui-tag look="primary" color="positive">Required</uui-tag>`
                    : html`<uui-tag look="secondary">Not required</uui-tag>`}
                </uui-table-cell>
                <uui-table-cell>${this._tenantAvailabilityLabel(p)}</uui-table-cell>
                <uui-table-cell>
                    <uui-button-group>
                        <uui-button look="outline" label="Edit" @click=${() => this._editPolicy(p)}>
                            <uui-icon name="edit"></uui-icon>
                        </uui-button>
                        <uui-button color="danger" look="outline" label="Delete" @click=${() => this._deletePolicy(p.id)}>
                            <uui-icon name="delete"></uui-icon>
                        </uui-button>
                    </uui-button-group>
                </uui-table-cell>
              </uui-table-row>
            `)}
          </uui-table>

          ${this._policies.length === 0 ? html`
            <p class="empty-state">No page-access policies configured yet. Click "Add Page Policy" to guard a page.</p>
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

    .description {
      color: var(--uui-color-text-alt);
      margin-bottom: var(--uui-size-space-5);
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
      font-size: 0.75rem;
    }
  `;
}
