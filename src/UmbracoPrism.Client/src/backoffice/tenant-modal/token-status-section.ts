import type { ReactiveController, ReactiveControllerHost } from 'lit';
import { html } from 'lit';
import { authHeaders, bearerTokenWithin500ms, type ContextHost } from './auth.js';

interface TokenStatus {
  fieldName: string;
  rawValue: string;
  tokenName: string;
  resolvedValue: string | null;
  isResolved: boolean;
}

/**
 * The "Environment Tokens" tab: which <c>{{TOKEN}}</c> placeholders the saved tenant uses and
 * whether each resolves from the host's configuration. Fetched on demand when the tab is opened.
 */
export class TokenStatusSection implements ReactiveController {
  private _status: TokenStatus[] = [];
  private _loading = false;
  private _error: string | null = null;

  constructor(private readonly _host: ReactiveControllerHost & ContextHost) {
    _host.addController(this);
  }

  hostConnected() {}

  async refresh(tenantId: number | null) {
    if (!tenantId) return;

    this._loading = true;
    this._error = null;
    this._host.requestUpdate();

    try {
      const token = await bearerTokenWithin500ms(this._host);
      const response = await fetch(`/umbraco/management/api/v1/prism/tenants/${tenantId}/token-status`, { headers: authHeaders(token) });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      this._status = await response.json();
    } catch (err) {
      this._error = err instanceof Error ? err.message : 'Unknown error';
    } finally {
      this._loading = false;
      this._host.requestUpdate();
    }
  }

    render(tenantId: number | null) {
    const isEditMode = tenantId !== null;
    const missingTokens = this._status.filter(t => !t.isResolved);

    const missingSnippet = missingTokens.length > 0
      ? '{\n' + missingTokens.map(t => `  "${t.tokenName}": "your-value-here"`).join(',\n') + '\n}'
      : null;

    return html`
      <div role="tabpanel" id="tokens-panel" aria-labelledby="tokens-tab" class="tab-content">
        <uui-box>
          <h3 style="margin-top:0">Environment Tokens</h3>
          <p class="description">
            Any field value in this tenant can hold a <code>{{TOKEN_NAME}}</code> placeholder instead of
            a literal string. Prism replaces these at runtime from your application configuration.
            Token names must be <strong>UPPERCASE with underscores</strong>.
          </p>
          <p class="description">
            Tokens resolve from the <strong>root level</strong> of <code>appsettings.json</code> — not
            nested under any section. For example, to use <code>{{OIDC_AUTHORITY}}</code> in the
            OIDC Authority field, add this to your <code>appsettings.json</code>:
          </p>
          <pre class="token-snippet">{
  "OIDC_AUTHORITY": "https://auth.example.com/realms/prod"
}</pre>
          <p class="description">
            You can also set tokens as environment variables (<code>OIDC_AUTHORITY=https://…</code>),
            or via any other .NET configuration source. The Hostname field is resolved at uSync
            import time rather than runtime.
          </p>

          ${!isEditMode ? html`
            <div class="info-banner" role="note">
              Save this tenant first to inspect its token status.
            </div>
          ` : this._loading ? html`
            <p class="description">Loading token status…</p>
          ` : this._error ? html`
            <small class="error-text" role="alert">Could not load token status: ${this._error}</small>
          ` : this._status.length === 0 ? html`
            <div class="info-banner" role="note">
              No <code>{{TOKEN_NAME}}</code> placeholders are currently used in this tenant's identity fields.
              You can add them by typing <code>{{MY_TOKEN}}</code> directly into any field on the Identity tab and saving.
            </div>
          ` : html`
            <table class="token-table" aria-label="Token resolution status">
              <thead>
                <tr>
                  <th scope="col">Token</th>
                  <th scope="col">Field</th>
                  <th scope="col">Resolved value</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                ${this._status.map(t => html`
                  <tr>
                    <td><code>{{${t.tokenName}}}</code></td>
                    <td>${t.fieldName}</td>
                    <td>
                      ${t.isResolved
                        ? html`<code class="resolved-value">${t.resolvedValue}</code>`
                        : html`<span class="token-missing">—</span>`}
                    </td>
                    <td>
                      ${t.isResolved
                        ? html`<span class="token-badge token-badge--ok" aria-label="Resolved">✓ Resolved</span>`
                        : html`<span class="token-badge token-badge--missing" aria-label="Missing">⚠ Missing</span>`}
                    </td>
                  </tr>
                `)}
              </tbody>
            </table>

            ${missingSnippet ? html`
              <div class="missing-tokens-hint">
                <strong>Add these to your <code>appsettings.json</code> (root level):</strong>
                <pre class="token-snippet">${missingSnippet}</pre>
                <p style="margin:0.25rem 0 0">
                  Or as environment variables, one per line:
                  <code>${missingTokens.map(t => t.tokenName + '=your-value').join(' · ')}</code>
                </p>
              </div>
            ` : ''}

            <p style="margin-top:0.75rem">
              <uui-button
                look="secondary"
                compact
                label="Refresh token status"
                @click=${() => this.refresh(tenantId)}>
                Refresh
              </uui-button>
            </p>
          `}
        </uui-box>
      </div>
    `;
  }
}
