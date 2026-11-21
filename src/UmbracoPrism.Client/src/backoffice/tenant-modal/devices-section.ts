import type { ReactiveController, ReactiveControllerHost } from 'lit';
import { html } from 'lit';
import { authHeaders, bearerTokenWithin500ms, type ContextHost } from './auth.js';

interface Device {
  deviceId: string;
  deviceName: string | null;
  platform: string | null;
  userId: string;
  registeredAt: string;
  lastUsedAt: string | null;
  expiresAt: string;
  revokedAt: string | null;
}

const formatDate = (value: string | null) => (value ? new Date(value).toLocaleDateString() : 'Never');

/**
 * The "Devices" tab: the biometric device credentials registered on a saved tenant, with a
 * Revoke action. A revoked credential is refused at its next biometric sign-in. Fetched on demand
 * when the tab is opened.
 */
export class DevicesSection implements ReactiveController {
  private _devices: Device[] = [];
  private _loading = false;
  private _error: string | null = null;
  private _confirming: string | null = null;

  constructor(private readonly _host: ReactiveControllerHost & ContextHost) {
    _host.addController(this);
  }

  hostConnected() {}

  private _devicesUrl(tenantId: number) {
    return `/umbraco/management/api/v1/prism/tenants/${tenantId}/devices`;
  }

  async refresh(tenantId: number | null) {
    if (!tenantId) return;

    this._loading = true;
    this._error = null;
    this._host.requestUpdate();

    try {
      const token = await bearerTokenWithin500ms(this._host);
      const response = await fetch(this._devicesUrl(tenantId), { headers: authHeaders(token) });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      this._devices = await response.json();
    } catch (err) {
      this._error = err instanceof Error ? err.message : 'Unknown error';
    } finally {
      this._loading = false;
      this._host.requestUpdate();
    }
  }

  private _askToRevoke(deviceId: string) {
    this._confirming = deviceId;
    this._host.requestUpdate();
  }

  private _cancel() {
    this._confirming = null;
    this._host.requestUpdate();
  }

  private async _revoke(tenantId: number, deviceId: string) {
    this._confirming = null;
    this._error = null;

    try {
      const token = await bearerTokenWithin500ms(this._host);
      const response = await fetch(`${this._devicesUrl(tenantId)}/${encodeURIComponent(deviceId)}`, {
        method: 'DELETE',
        headers: authHeaders(token)
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
    } catch (err) {
      this._error = `Could not revoke the device: ${err instanceof Error ? err.message : 'Unknown error'}`;
      this._host.requestUpdate();
      return;
    }

    await this.refresh(tenantId);
  }

  private _renderActions(tenantId: number, device: Device) {
    if (device.revokedAt) return html`<span class="token-badge token-badge--missing">Revoked</span>`;

    if (this._confirming === device.deviceId) {
      return html`
        <uui-button look="primary" color="danger" compact label="Confirm revoke" @click=${() => this._revoke(tenantId, device.deviceId)}>Confirm revoke</uui-button>
        <uui-button look="secondary" compact label="Cancel" @click=${() => this._cancel()}>Cancel</uui-button>
      `;
    }

    return html`<uui-button look="secondary" color="danger" compact label="Revoke ${device.deviceName ?? device.deviceId}" @click=${() => this._askToRevoke(device.deviceId)}>Revoke</uui-button>`;
  }

  private _renderTable(tenantId: number) {
    return html`
      <table class="token-table" aria-label="Biometric devices">
        <thead>
          <tr>
            <th scope="col">Device</th>
            <th scope="col">Platform</th>
            <th scope="col">Member</th>
            <th scope="col">Registered</th>
            <th scope="col">Last used</th>
            <th scope="col">Expires</th>
            <th scope="col"><span class="visually-hidden">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          ${this._devices.map(device => html`
            <tr>
              <td>${device.deviceName ?? html`<code>${device.deviceId}</code>`}</td>
              <td>${device.platform ?? 'Unknown'}</td>
              <td><code class="resolved-value">${device.userId}</code></td>
              <td>${formatDate(device.registeredAt)}</td>
              <td>${formatDate(device.lastUsedAt)}</td>
              <td>${formatDate(device.expiresAt)}</td>
              <td>${this._renderActions(tenantId, device)}</td>
            </tr>
          `)}
        </tbody>
      </table>
    `;
  }

  private _renderBody(tenantId: number | null) {
    if (tenantId === null) {
      return html`<div class="info-banner" role="note">Save this tenant first to see its devices.</div>`;
    }
    if (this._loading) return html`<p class="description">Loading devices…</p>`;
    if (this._devices.length === 0 && !this._error) {
      return html`<div class="info-banner" role="note">No devices have registered for biometric sign-in on this tenant.</div>`;
    }
    return this._renderTable(tenantId);
  }

  render(tenantId: number | null) {
    return html`
      <div role="tabpanel" id="devices-panel" aria-labelledby="devices-tab" class="tab-content">
        <uui-box>
          <h3 style="margin-top:0">Devices</h3>
          <p class="description">
            Phones and tablets a member has set up for biometric sign-in on this tenant. Revoking a
            device means its next biometric sign-in is refused, and the member signs in again with
            their provider. Members can also remove their own devices from the app.
          </p>
          ${this._error ? html`<small class="error-text" role="alert">${this._error}</small>` : ''}
          ${this._renderBody(tenantId)}
        </uui-box>
      </div>
    `;
  }
}
