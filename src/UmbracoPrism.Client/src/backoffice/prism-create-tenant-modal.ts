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
        mobileOverrideValue?: string;
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
      mobileOverrideValue?: string;
    }>;
  }> = [];
  
  // Form State
  @state() private _id: number | null = null;
  @state() private _name = '';
  @state() private _hostname = '';
  @state() private _entraTenantId = '';
  @state() private _entraClientId = '';
  @state() private _secretKeyName = '';
  @state() private _mobileAppName = '';
  @state() private _mobileAppId = '';
  @state() private _mobileVersion = '1.0.0';
  @state() private _mobileStartUrl = '';
  @state() private _mobileUserAgentMarker = 'PrismMobile';
  @state() private _mobileIconUrl = '';
  @state() private _mobileSplashUrl = '';
  @state() private _mobileErrorBackgroundColor = '#0f172a';
  @state() private _mobileErrorTextColor = '#f8fafc';
  @state() private _mobileErrorTitle = 'We’re having trouble connecting';
  @state() private _mobileErrorMessage = 'Please check your connection and try again.';
  @state() private _mobileShowErrorDiagnostics = true;
  @state() private _allowBiometricLogin = true;
  @state() private _isProducingMobileBundle = false;
  @state() private _mobileBundleGenerated = false;
  @state() private _copiedCommand = '';

  private _readMobileConfigValue(config: any, camelKey: string) {
    if (!config || typeof config !== 'object') return undefined;

    if (camelKey in config) return config[camelKey];

    const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1);
    if (pascalKey in config) return config[pascalKey];

    return undefined;
  }

  private _readMobileAppConfig(tenant: any) {
    const raw = tenant?.mobileAppConfig;
    if (!raw) return null;

    let config: any = null;

    if (typeof raw === 'string') {
      try {
        config = JSON.parse(raw);
      } catch {
        return null;
      }

      if (!config || typeof config !== 'object') return null;
    } else if (typeof raw === 'object') {
      config = raw;
    } else {
      return null;
    }

    return {
      appName: this._readMobileConfigValue(config, 'appName'),
      appId: this._readMobileConfigValue(config, 'appId'),
      version: this._readMobileConfigValue(config, 'version'),
      startUrl: this._readMobileConfigValue(config, 'startUrl'),
      userAgentMarker: this._readMobileConfigValue(config, 'userAgentMarker'),
      iconUrl: this._readMobileConfigValue(config, 'iconUrl'),
      splashUrl: this._readMobileConfigValue(config, 'splashUrl'),
      errorBackgroundColor: this._readMobileConfigValue(config, 'errorBackgroundColor'),
      errorTextColor: this._readMobileConfigValue(config, 'errorTextColor'),
      errorTitle: this._readMobileConfigValue(config, 'errorTitle'),
      errorMessage: this._readMobileConfigValue(config, 'errorMessage'),
      showErrorDiagnostics: this._readMobileConfigValue(config, 'showErrorDiagnostics')
    };
  }


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
      this._allowBiometricLogin = t.allowBiometricLogin ?? true;

      const mobileConfig = this._readMobileAppConfig(t);
      this._mobileAppName = mobileConfig?.appName ?? t.name ?? '';
      this._mobileAppId = mobileConfig?.appId ?? this._defaultMobileAppId(t.name ?? 'tenant');
      this._mobileVersion = mobileConfig?.version ?? '1.0.0';
      this._mobileStartUrl = mobileConfig?.startUrl ?? this._defaultMobileStartUrl(t.hostname ?? '');
      this._mobileUserAgentMarker = mobileConfig?.userAgentMarker ?? 'PrismMobile';
      this._mobileIconUrl = mobileConfig?.iconUrl ?? this._defaultMobileIconUrl(t.hostname ?? '');
      this._mobileSplashUrl = mobileConfig?.splashUrl ?? '';
      this._mobileErrorBackgroundColor = mobileConfig?.errorBackgroundColor ?? '#0f172a';
      this._mobileErrorTextColor = mobileConfig?.errorTextColor ?? '#f8fafc';
      this._mobileErrorTitle = mobileConfig?.errorTitle ?? 'We’re having trouble connecting';
      this._mobileErrorMessage = mobileConfig?.errorMessage ?? 'Please check your connection and try again.';
      this._mobileShowErrorDiagnostics = mobileConfig?.showErrorDiagnostics ?? true;
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

        const mobileConfig = this._readMobileAppConfig(t);
        this._mobileAppName = mobileConfig?.appName ?? t.name ?? '';
        this._mobileAppId = mobileConfig?.appId ?? this._defaultMobileAppId(t.name ?? 'tenant');
        this._mobileVersion = mobileConfig?.version ?? '1.0.0';
        this._mobileStartUrl = mobileConfig?.startUrl ?? this._defaultMobileStartUrl(t.hostname ?? '');
        this._mobileUserAgentMarker = mobileConfig?.userAgentMarker ?? 'PrismMobile';
        this._mobileIconUrl = mobileConfig?.iconUrl ?? this._defaultMobileIconUrl(t.hostname ?? '');
        this._mobileSplashUrl = mobileConfig?.splashUrl ?? '';
        this._mobileErrorBackgroundColor = mobileConfig?.errorBackgroundColor ?? '#0f172a';
        this._mobileErrorTextColor = mobileConfig?.errorTextColor ?? '#f8fafc';
        this._mobileErrorTitle = mobileConfig?.errorTitle ?? 'We’re having trouble connecting';
        this._mobileErrorMessage = mobileConfig?.errorMessage ?? 'Please check your connection and try again.';
        this._mobileShowErrorDiagnostics = mobileConfig?.showErrorDiagnostics ?? true;
        this._mobileBundleGenerated = false;
        this._allowBiometricLogin = t.allowBiometricLogin ?? true;
      } else {
        this._id = null;
        this._name = '';
        this._hostname = '';
        this._entraTenantId = '';
        this._entraClientId = '';
        this._secretKeyName = '';
        this._mobileAppName = '';
        this._mobileAppId = '';
        this._mobileVersion = '1.0.0';
        this._mobileStartUrl = '';
        this._mobileUserAgentMarker = 'PrismMobile';
        this._mobileIconUrl = '';
        this._mobileSplashUrl = '';
        this._mobileErrorBackgroundColor = '#0f172a';
        this._mobileErrorTextColor = '#f8fafc';
        this._mobileErrorTitle = 'We’re having trouble connecting';
        this._mobileErrorMessage = 'Please check your connection and try again.';
        this._mobileShowErrorDiagnostics = true;
        this._mobileBundleGenerated = false;
        this._allowBiometricLogin = true;
      }

      const tenantMobileOverrides = this._toOverrideMap(this.data?.tenant?.mobileBrandingOverrides);
      this._brandingTabs = (this.data?.brandingTabs ?? []).map((tab) => ({
        ...tab,
        variables: tab.variables.map((variable) => ({
          ...variable,
          mobileOverrideValue: variable.mobileOverrideValue ?? tenantMobileOverrides[variable.name]
        }))
      }));
      this._ensureActiveTab();
    }
  }

  private _toOverrideMap(value: unknown): Record<string, string> {
    if (typeof value === 'string') {
      try {
        const parsed = JSON.parse(value);
        if (!parsed || typeof parsed !== 'object') return {};

        return Object.fromEntries(
          Object.entries(parsed).filter((entry): entry is [string, string] => typeof entry[1] === 'string')
        );
      } catch {
        return {};
      }
    }

    if (!value || typeof value !== 'object') return {};

    return Object.fromEntries(
      Object.entries(value).filter((entry): entry is [string, string] => typeof entry[1] === 'string')
    );
  }

  private _ensureActiveTab() {
    const brandingTabKeys = this._brandingTabs.map((_, index) => this._brandingTabKey(index));
    const allowedTabs = new Set(['general', 'identity', 'mobile', ...brandingTabKeys]);

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
    const mobileBrandingOverrides = this._collectMobileBrandingOverrides();
    const mobileAppConfig = {
      appName: this._mobileAppName,
      appId: this._mobileAppId,
      version: this._mobileVersion,
      startUrl: this._mobileStartUrl,
      userAgentMarker: this._mobileUserAgentMarker,
      iconUrl: this._mobileIconUrl,
      splashUrl: this._mobileSplashUrl,
      errorBackgroundColor: this._mobileErrorBackgroundColor,
      errorTextColor: this._mobileErrorTextColor,
      errorTitle: this._mobileErrorTitle,
      errorMessage: this._mobileErrorMessage,
      showErrorDiagnostics: this._mobileShowErrorDiagnostics
    };

    const tenant = {
      id: this._id,
      name: this._name,
      hostname: this._hostname,
      themeColor: '#3544b1', // Defaulting for now, could be a color picker later
      entraTenantId: this._entraTenantId,
      entraClientId: this._entraClientId,
      secretKeyName: this._secretKeyName,
      brandingOverrides,
      mobileBrandingOverrides,
      mobileAppConfig,
      allowBiometricLogin: this._allowBiometricLogin
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

          <div class="field">
            <div class="toggle-label">
              <span>Allow Biometric Login</span>
              <span class="toggle-hint">When disabled, mobile users cannot register or use biometric authentication for this tenant.</span>
            </div>
            <label class="toggle-switch" title="${this._allowBiometricLogin ? 'Biometric login enabled' : 'Biometric login disabled'}">
              <input
                type="checkbox"
                aria-label="Allow Biometric Login"
                .checked=${this._allowBiometricLogin}
                @change=${(e: Event) => { this._allowBiometricLogin = (e.target as HTMLInputElement).checked; }}
              />
              <span class="toggle-slider"></span>
            </label>
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

  private _renderMobileTab() {
    const isEditMode = this._id !== null;
    const appIdValid = this._isValidMobileAppId(this._mobileAppId);
    const startUrlValid = this._isValidAbsoluteUrl(this._mobileStartUrl);
    const iconUrlValid = !this._mobileIconUrl || this._isValidAbsoluteUrl(this._mobileIconUrl);
    const splashUrlValid = !this._mobileSplashUrl || this._isValidAbsoluteUrl(this._mobileSplashUrl);
    const localhostStartUrl = this._isLikelyLocalhostUrl(this._mobileStartUrl);
    const canProduce = isEditMode && !this._isProducingMobileBundle && appIdValid && startUrlValid && iconUrlValid && splashUrlValid;

    return html`
      <div role="tabpanel" id="mobile-panel" aria-labelledby="mobile-tab" class="tab-content">
        <uui-box>
          <p class="description">
            Generate a Capacitor starter bundle for this tenant. The bundle is intended as a near zero-code mobile shell.
          </p>

          ${!isEditMode ? html`
            <p class="description">
              Save the tenant first to enable mobile bundle generation.
            </p>
          ` : html``}

          <div class="field">
            <uui-label for="mobile-app-name">App Name</uui-label>
            <uui-input
              id="mobile-app-name"
              label="App Name"
              .value=${this._mobileAppName}
              @input=${(e: any) => this._mobileAppName = e.target.value}>
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-app-id">App ID</uui-label>
            <uui-input
              id="mobile-app-id"
              label="App ID"
              .value=${this._mobileAppId}
              @input=${(e: any) => this._mobileAppId = e.target.value}
              placeholder="com.example.portal">
            </uui-input>
            <small>Reverse-domain format. Example: <code>com.acme.portal</code></small>
            ${appIdValid ? html`` : html`<small class="error-text">App ID must be reverse-domain style (e.g. <code>com.example.portal</code>).</small>`}
          </div>

          <div class="field">
            <uui-label for="mobile-version">Version</uui-label>
            <uui-input
              id="mobile-version"
              label="Version"
              .value=${this._mobileVersion}
              @input=${(e: any) => this._mobileVersion = e.target.value}
              placeholder="1.0.0">
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-start-url">Start URL</uui-label>
            <uui-input
              id="mobile-start-url"
              label="Start URL"
              .value=${this._mobileStartUrl}
              @input=${(e: any) => this._mobileStartUrl = e.target.value}
              placeholder="https://tenant.example.com">
            </uui-input>
            ${startUrlValid ? html`` : html`<small class="error-text">Start URL must be an absolute URL, e.g. <code>https://tenant.example.com</code>.</small>`}
            ${localhostStartUrl ? html`<small class="error-text">Localhost is supported for simulator/device testing, but iOS requires trusting your HTTPS cert first (or use a LAN/tunnel/public URL).</small>` : html``}
          </div>

          <div class="field">
            <uui-label for="mobile-ua-marker">User Agent Marker</uui-label>
            <uui-input
              id="mobile-ua-marker"
              label="User Agent Marker"
              .value=${this._mobileUserAgentMarker}
              @input=${(e: any) => this._mobileUserAgentMarker = e.target.value}
              placeholder="PrismMobile">
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-icon-url">Icon URL</uui-label>
            <uui-input
              id="mobile-icon-url"
              label="Icon URL"
              .value=${this._mobileIconUrl}
              @input=${(e: any) => this._mobileIconUrl = e.target.value}
              placeholder="https://tenant.example.com/favicon.ico">
            </uui-input>
            <small>Recommended square icon source, ideally 1024x1024 PNG.</small>
            ${iconUrlValid ? html`` : html`<small class="error-text">Icon URL must be an absolute URL.</small>`}
            ${this._mobileIconUrl ? html`<img class="mobile-asset-preview" src=${this._mobileIconUrl} alt="Icon preview" />` : html``}
          </div>

          <div class="field">
            <uui-label for="mobile-splash-url">Splash URL</uui-label>
            <uui-input
              id="mobile-splash-url"
              label="Splash URL"
              .value=${this._mobileSplashUrl}
              @input=${(e: any) => this._mobileSplashUrl = e.target.value}
              placeholder="https://tenant.example.com/media/splash.png">
            </uui-input>
            <small>Optional splash image source for your generated app assets.</small>
            ${splashUrlValid ? html`` : html`<small class="error-text">Splash URL must be an absolute URL.</small>`}
            ${this._mobileSplashUrl ? html`<img class="mobile-asset-preview" src=${this._mobileSplashUrl} alt="Splash preview" />` : html``}
          </div>

          <h5 class="section-title">Startup Error Screen</h5>
          <p class="description">Shown if the app cannot reach your Start URL during launch.</p>

          <div class="field">
            <uui-label for="mobile-error-title">Error Title</uui-label>
            <uui-input
              id="mobile-error-title"
              label="Error Title"
              .value=${this._mobileErrorTitle}
              @input=${(e: any) => this._mobileErrorTitle = e.target.value}
              placeholder="We’re having trouble connecting">
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-error-message">Error Message</uui-label>
            <uui-input
              id="mobile-error-message"
              label="Error Message"
              .value=${this._mobileErrorMessage}
              @input=${(e: any) => this._mobileErrorMessage = e.target.value}
              placeholder="Please check your connection and try again.">
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-error-bg">Error Background Color</uui-label>
            <uui-input
              id="mobile-error-bg"
              label="Error Background Color"
              .value=${this._mobileErrorBackgroundColor}
              @input=${(e: any) => this._mobileErrorBackgroundColor = e.target.value}
              placeholder="#0f172a">
            </uui-input>
          </div>

          <div class="field">
            <uui-label for="mobile-error-text">Error Text Color</uui-label>
            <uui-input
              id="mobile-error-text"
              label="Error Text Color"
              .value=${this._mobileErrorTextColor}
              @input=${(e: any) => this._mobileErrorTextColor = e.target.value}
              placeholder="#f8fafc">
            </uui-input>
          </div>

          <div class="field checkbox-field">
            <uui-checkbox
              label="Show technical diagnostics"
              .checked=${this._mobileShowErrorDiagnostics}
              @change=${(e: any) => this._mobileShowErrorDiagnostics = Boolean(e.target.checked)}>
              Show technical diagnostics
            </uui-checkbox>
            <small>When enabled, users can expand technical details (status, timeout, and last error) for debugging.</small>
          </div>

          <div class="helper-actions">
            <uui-button look="outline" @click=${this._applyMobileDefaultsFromTenant}>Use tenant defaults</uui-button>
            <uui-button look="outline" @click=${this._suggestMobileAppId}>Suggest app id</uui-button>
          </div>

          <uui-button
            look="primary"
            color="positive"
            ?disabled=${!canProduce}
            @click=${this._handleProduceMobile}>
            ${this._isProducingMobileBundle ? 'Generating…' : 'Generate & Download App Bundle'}
          </uui-button>

          ${this._mobileBundleGenerated ? html`
            <div class="generated-helper">
              <small><strong>Bundle ready.</strong> From the extracted folder, run:</small>
              <div class="command-row">
                <code>npm install && npm run doctor</code>
                <uui-button look="outline" @click=${() => this._copyCommand('npm install && npm run doctor')}>
                  ${this._copiedCommand === 'npm install && npm run doctor' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              <div class="command-row">
                <code>npm run bootstrap:ios</code>
                <uui-button look="outline" @click=${() => this._copyCommand('npm run bootstrap:ios')}>
                  ${this._copiedCommand === 'npm run bootstrap:ios' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              <div class="command-row">
                <code>npm run bootstrap:android</code>
                <uui-button look="outline" @click=${() => this._copyCommand('npm run bootstrap:android')}>
                  ${this._copiedCommand === 'npm run bootstrap:android' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              ${this._isLikelyLocalhostUrl(this._mobileStartUrl)
                ? html`
                    <small>Localhost tip: if iOS trust prompts appear, run:</small>
                    <div class="command-row">
                      <code>bash scripts/trust-ios-localhost-cert.sh && npm run run:ios</code>
                      <uui-button look="outline" @click=${() => this._copyCommand('bash scripts/trust-ios-localhost-cert.sh && npm run run:ios')}>
                        ${this._copiedCommand === 'bash scripts/trust-ios-localhost-cert.sh && npm run run:ios' ? 'Copied' : 'Copy'}
                      </uui-button>
                    </div>
                  `
                : html``}
            </div>
          ` : html``}
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
            <uui-table-column style="width: 20%"></uui-table-column>
            <uui-table-column style="width: 20%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>
            <uui-table-column style="width: 30%"></uui-table-column>

            <uui-table-head>
              <uui-table-head-cell>Variable</uui-table-head-cell>
              <uui-table-head-cell>Default</uui-table-head-cell>
              <uui-table-head-cell>Override</uui-table-head-cell>
              <uui-table-head-cell>Mobile</uui-table-head-cell>
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
                <uui-table-cell>
                  <uui-input
                    class="override-input"
                    placeholder="e.g. #0d6efd"
                    .value=${variable.mobileOverrideValue ?? ''}
                    @input=${(e: InputEvent) => this._updateMobileBrandingOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
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

  private _updateMobileBrandingOverride(tabIndex: number, variableIndex: number, value: string) {
    this._brandingTabs = this._brandingTabs.map((tab, index) => {
      if (index !== tabIndex) return tab;
      return {
        ...tab,
        variables: tab.variables.map((variable, vIndex) =>
          vIndex === variableIndex ? { ...variable, mobileOverrideValue: value } : variable
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

  private _collectMobileBrandingOverrides() {
    const overrides: Record<string, string> = {};
    this._brandingTabs.forEach(tab => {
      tab.variables.forEach(variable => {
        if (variable.mobileOverrideValue && variable.mobileOverrideValue.trim().length > 0) {
          overrides[variable.name] = variable.mobileOverrideValue.trim();
        }
      });
    });

    return overrides;
  }

  private _defaultMobileAppId(name: string) {
    const normalized = (name || 'tenant')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');

    return `com.prism.${normalized || 'tenant'}`;
  }

  private _defaultMobileStartUrl(hostname: string) {
    const host = (hostname || '').trim();
    if (!host) return '';

    if (host.startsWith('http://') || host.startsWith('https://')) return host;
    return `https://${host}`;
  }

  private _defaultMobileIconUrl(hostname: string) {
    const startUrl = this._defaultMobileStartUrl(hostname);
    if (!startUrl) return '';
    return `${startUrl.replace(/\/$/, '')}/favicon.ico`;
  }

  private _isValidMobileAppId(value: string) {
    if (!value) return false;
    return /^[a-zA-Z0-9]+(\.[a-zA-Z0-9_-]+)+$/.test(value.trim());
  }

  private _isValidAbsoluteUrl(value: string) {
    if (!value) return false;
    try {
      const parsed = new URL(value.trim());
      return parsed.protocol === 'http:' || parsed.protocol === 'https:';
    } catch {
      return false;
    }
  }

  private _isLikelyLocalhostUrl(value: string) {
    try {
      const parsed = new URL(value.trim());
      return ['localhost', '127.0.0.1', '::1'].includes(parsed.hostname);
    } catch {
      return false;
    }
  }

  private _suggestMobileAppId = () => {
    this._mobileAppId = this._defaultMobileAppId(this._mobileAppName || this._name || 'tenant');
  };

  private _applyMobileDefaultsFromTenant = () => {
    this._mobileAppName = this._name || this._mobileAppName;
    this._mobileAppId = this._defaultMobileAppId(this._mobileAppName || this._name || 'tenant');
    this._mobileStartUrl = this._defaultMobileStartUrl(this._hostname);
    this._mobileUserAgentMarker = this._mobileUserAgentMarker || 'PrismMobile';
    this._mobileIconUrl = this._defaultMobileIconUrl(this._hostname);
    this._mobileErrorBackgroundColor = this._mobileErrorBackgroundColor || '#0f172a';
    this._mobileErrorTextColor = this._mobileErrorTextColor || '#f8fafc';
    this._mobileErrorTitle = this._mobileErrorTitle || 'We’re having trouble connecting';
    this._mobileErrorMessage = this._mobileErrorMessage || 'Please check your connection and try again.';
  };

  private async _copyCommand(command: string) {
    try {
      if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(command);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = command;
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        textarea.remove();
      }

      this._copiedCommand = command;
      window.setTimeout(() => {
        if (this._copiedCommand === command) {
          this._copiedCommand = '';
        }
      }, 1500);
    } catch (error) {
      console.error('Failed to copy command', error);
    }
  }

  private async _handleProduceMobile(e?: Event) {
    e?.preventDefault();
    e?.stopPropagation();
    if (this._id === null || this._isProducingMobileBundle) return;

    this._isProducingMobileBundle = true;

    this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
      if (!authContext) {
        this._isProducingMobileBundle = false;
        return;
      }

      try {
        const token = await authContext.getLatestToken();
        const bundlePayload = {
          appName: this._mobileAppName,
          appId: this._mobileAppId,
          version: this._mobileVersion,
          startUrl: this._mobileStartUrl,
          userAgentMarker: this._mobileUserAgentMarker,
          iconUrl: this._mobileIconUrl,
          splashUrl: this._mobileSplashUrl,
          errorBackgroundColor: this._mobileErrorBackgroundColor,
          errorTextColor: this._mobileErrorTextColor,
          errorTitle: this._mobileErrorTitle,
          errorMessage: this._mobileErrorMessage,
          showErrorDiagnostics: this._mobileShowErrorDiagnostics,
          biometricAuthEnabled: this._allowBiometricLogin
        };
        console.log('[Prism] Producing mobile bundle — request payload:', JSON.stringify(bundlePayload));
        const response = await fetch(`/umbraco/management/api/v1/prism/tenants/${this._id}/produce-mobile`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(bundlePayload)
        });

        if (!response.ok) {
          const errorBody = await response.text();
          console.error('Failed to produce mobile bundle', errorBody);
          return;
        }

        const blob = await response.blob();
        const fileNameHeader = response.headers.get('Content-Disposition') ?? '';
        const nameMatch = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(fileNameHeader);
        const fileName = nameMatch?.[1] ? decodeURIComponent(nameMatch[1]) : `prism-mobile-${this._id}.zip`;

        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.style.display = 'none';
        anchor.target = '_blank';
        anchor.rel = 'noopener noreferrer';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
        this._mobileBundleGenerated = true;
      } catch (error) {
        console.error('Failed to produce mobile bundle', error);
      } finally {
        this._isProducingMobileBundle = false;
      }
    });
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
          <uui-tab
            label="Produce Mobile"
            id="mobile-tab"
            data-tab-key="mobile"
            ?active=${this._activeTab === 'mobile'}>
            Produce Mobile
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
              : this._activeTab === 'mobile'
                ? this._renderMobileTab()
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
    .helper-actions {
      display: flex;
      gap: var(--uui-size-space-3);
      margin-bottom: var(--uui-size-space-4);
      flex-wrap: wrap;
    }
    .mobile-asset-preview {
      margin-top: var(--uui-size-space-2);
      max-width: 160px;
      max-height: 120px;
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
      object-fit: contain;
      padding: 6px;
    }
    .error-text {
      color: var(--uui-color-danger-standalone);
    }
    .section-title {
      margin: var(--uui-size-space-5) 0 var(--uui-size-space-2);
    }
    .checkbox-field {
      gap: var(--uui-size-space-2);
    }
    .generated-helper {
      margin-top: var(--uui-size-space-4);
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2);
      padding: var(--uui-size-space-3);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
    }
    .command-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--uui-size-space-2);
      flex-wrap: wrap;
    }
    .toggle-label {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      margin-bottom: 0.5rem;
    }
    .toggle-hint {
      font-size: 0.8rem;
      color: #666;
    }
    .toggle-switch {
      position: relative;
      display: inline-block;
      width: 46px;
      height: 24px;
      cursor: pointer;
    }
    .toggle-switch input {
      opacity: 0;
      width: 0;
      height: 0;
      position: absolute;
    }
    .toggle-slider {
      position: absolute;
      inset: 0;
      background-color: #ccc;
      border-radius: 24px;
      transition: background-color 0.2s;
    }
    .toggle-switch input:checked + .toggle-slider {
      background-color: #2563eb;
    }
    .toggle-slider::before {
      content: '';
      position: absolute;
      height: 18px;
      width: 18px;
      left: 3px;
      bottom: 3px;
      background-color: white;
      border-radius: 50%;
      transition: transform 0.2s;
    }
    .toggle-switch input:checked + .toggle-slider::before {
      transform: translateX(22px);
    }
  `;
}