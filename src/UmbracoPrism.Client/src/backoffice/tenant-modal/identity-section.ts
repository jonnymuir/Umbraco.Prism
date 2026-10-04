import type { ReactiveController, ReactiveControllerHost } from 'lit';
import { html } from 'lit';
import {
  type IdentityDraft,
  emptyIdentity,
  identityFromTenant,
  identityPayload,
  isRepoOwnedLocalDemoTenant,
  secretStatusMessage,
  secretValidationMessage,
  usesGenericOidc
} from './identity-rules.js';

export interface IdentityContext {
  tenantId(): number | null;
  hostname(): string;
}

/** The "Identity" tab: Entra or generic-OIDC settings, and the rules for the client secret. */
export class IdentitySection implements ReactiveController {
  draft: IdentityDraft = emptyIdentity();

  constructor(
    private readonly _host: ReactiveControllerHost,
    private readonly _context: IdentityContext
  ) {
    _host.addController(this);
  }

  hostConnected() {}

  /** Takes up a saved tenant's settings, or starts blank for a new one. */
  loadFrom(tenant: any | undefined) {
    this.draft = tenant ? identityFromTenant(tenant) : emptyIdentity();
    this._host.requestUpdate();
  }

  /** What is still missing before this can be saved, or '' when nothing is. */
  get validationMessage() {
    return secretValidationMessage(this.draft, this._context.hostname(), this._context.tenantId());
  }

  get payload() {
    return identityPayload(this.draft, this._context.hostname());
  }

  private _patch(change: Partial<IdentityDraft>) {
    this.draft = { ...this.draft, ...change };
    this._host.requestUpdate();
  }

    render() {
    const d = this.draft;
    const generic = usesGenericOidc(d);
    const isRepoOwnedLocalDemo = isRepoOwnedLocalDemoTenant(d, this._context.hostname());
    const tenantId = this._context.tenantId();
    const oidcSecretStatusMessage = secretStatusMessage(d, this._context.hostname());
    const oidcSecretValidationMessage = secretValidationMessage(d, this._context.hostname(), tenantId);
    const secretNameLabel = generic
      ? `OIDC Key Vault Secret Name ${tenantId !== null ? '(replace only)' : ''}`
      : 'Key Vault Secret Name';
    const secretNameHint = generic
      ? 'Reference only — Prism resolves the actual generic OIDC client secret from Azure Key Vault at runtime. Leave blank on edit to keep the current secret, or use Reset configured OIDC secret to clear it.'
      : 'Use the Azure Key Vault secret name for Entra confidential clients. Generic OIDC tenants use the OIDC section below.';

    return html`
      <div role="tabpanel" id="identity-panel" aria-labelledby="identity-tab" class="tab-content">
        <uui-box>
          <p class="description">Configure Microsoft Entra ID integration. Branding is managed in the Azure Portal.</p>
          
          <div class="field">
            <uui-label for="tenant-id">Directory (Tenant) ID</uui-label>
            <uui-input 
              id="tenant-id" 
              label="Directory ID" 
              .value=${d.entraTenantId} 
              @input=${(e: any) => this._patch({ entraTenantId: e.target.value })}>
            </uui-input>
          </div>
          
          <div class="field">
            <uui-label for="client-id">Application (Client) ID</uui-label>
            <uui-input 
              id="client-id" 
              label="Client ID" 
              .value=${d.entraClientId} 
              @input=${(e: any) => this._patch({ entraClientId: e.target.value })}>
            </uui-input>
          </div>

          ${isRepoOwnedLocalDemo ? html`` : html`
            <div class="field">
              <uui-label for="secret-name">${secretNameLabel}</uui-label>
              <uui-input 
                id="secret-name" 
                label=${secretNameLabel}
                placeholder=${generic ? 'northwind-oidc-secret' : 'tenant-a-secret'}
                .value=${d.secretKeyName} 
                @input=${(e: any) => this._patch({ secretKeyName: e.target.value })}
                ?disabled=${generic && d.clearOidcClientSecret}
                aria-describedby="secret-hint">
              </uui-input>
              <small id="secret-hint">${secretNameHint}</small>
            </div>
          `}

          <div class="section-divider"></div>
          
          <h3>OIDC Provider (non-Entra)</h3>
          <p class="help-text">Use this section if your identity provider is not Microsoft Entra (e.g. Keycloak, Auth0). Leave blank to use Entra. Production tenants should use a Key Vault secret reference; the localhost demo is the only inline-secret exception.</p>

          <div class="field">
            <uui-label for="oidc-authority">OIDC Authority</uui-label>
            <uui-input 
              id="oidc-authority" 
              label="OIDC Authority"
              type="url" 
              placeholder="https://auth.example.com/realms/my-realm" 
              .value=${d.oidcAuthority} 
              @input=${(e: any) => this._patch({ oidcAuthority: e.target.value })}>
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="oidc-client-id">OIDC Client ID</uui-label>
            <uui-input 
              id="oidc-client-id" 
              label="OIDC Client ID" 
              placeholder="my-client-id" 
              .value=${d.oidcClientId} 
              @input=${(e: any) => this._patch({ oidcClientId: e.target.value })}>
            </uui-input>
          </div>

          ${generic ? html`
            <p class="help-text" role="status" aria-live="polite">${oidcSecretStatusMessage}</p>

            ${d.hasStoredOidcClientSecret ? html`
              <div class="field">
                <div class="toggle-label">
                  <span>Reset configured OIDC secret</span>
                  <span class="toggle-hint">Use this only if you want to remove the current generic OIDC secret and replace it later.</span>
                </div>
                <label class="toggle-switch" title="${d.clearOidcClientSecret ? 'Configured OIDC secret will be cleared' : 'Configured OIDC secret will be preserved'}">
                  <input
                    type="checkbox"
                    aria-label="Reset configured OIDC secret"
                    .checked=${d.clearOidcClientSecret}
                    @change=${(e: Event) => {
                      const clear = (e.target as HTMLInputElement).checked;
                      this._patch(clear ? { clearOidcClientSecret: true, oidcClientSecretReference: '' } : { clearOidcClientSecret: false });
                    }}
                  />
                  <span class="toggle-slider"></span>
                </label>
              </div>
            ` : html``}

            ${isRepoOwnedLocalDemo ? html`
              <div class="field">
                <uui-label for="oidc-client-secret-reference">Local Demo OIDC Client Secret ${tenantId !== null ? '(replace only)' : ''}</uui-label>
                <uui-input 
                  id="oidc-client-secret-reference" 
                  label="Local Demo OIDC Client Secret"
                  type="password"
                  placeholder="prism-dev-secret" 
                  .value=${d.oidcClientSecretReference} 
                  @input=${(e: any) => {
                    const reference: string = e.target.value;
                    this._patch(reference ? { oidcClientSecretReference: reference, clearOidcClientSecret: false } : { oidcClientSecretReference: reference });
                  }}
                  ?disabled=${d.clearOidcClientSecret}
                  aria-label="Local Demo OIDC Client Secret"
                  aria-describedby="oidc-client-secret-hint">
                </uui-input>
                <small id="oidc-client-secret-hint">Only the repo-owned localhost Keycloak demo uses an inline secret. This field never receives the currently stored value; enter a new one only when replacing the demo secret.</small>
              </div>
            ` : html``}

            ${oidcSecretValidationMessage ? html`
              <small class="error-text" role="alert">${oidcSecretValidationMessage}</small>
            ` : html``}
          ` : html``}
        </uui-box>
      </div>
    `;
  }
}
