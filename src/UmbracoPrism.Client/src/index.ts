import { LitElement, html, css } from 'lit';
import { customElement, state } from 'lit/decorators.js'; // Decorators live here now
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';

@customElement('prism-dashboard')
export class PrismDashboardElement extends UmbElementMixin(LitElement) {
  
  @state()
  private _tenants: any[] = [];

  async connectedCallback() {
    super.connectedCallback();
    this._fetchTenants();
  }

  async _fetchTenants() {
    try {
      // This hits your Swagger/Management API
      const res = await fetch('/umbraco/management/api/v1/prism/tenants');
      this._tenants = await res.json();
    } catch (e) {
      console.error("Prism API Error", e);
    }
  }

  render() {
    return html`
      <div style="padding: 20px;">
        <uui-box headline="Prism Multi-Tenant Manager">
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