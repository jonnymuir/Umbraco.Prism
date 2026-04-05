import { LitElement, html, css } from 'lit';
import { customElement, state, property } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_MEDIA_PICKER_MODAL } from '@umbraco-cms/backoffice/media';

interface BrandingMetadata {
  sections: Array<{
    name: string;
    variables: Array<{
      variable: string;
      label: string;
      description: string;
      type: string;
      syntax: string;
      currentValue: string;
    }>;
  }>;
}

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
  @state() private _maximized = false;
  @state() private _brandingTabs: Array<{
    label: string;
    variables: Array<{
      name: string;
      defaultValue?: string;
      overrideValue?: string;
      mobileOverrideValue?: string;
    }>;
  }> = [];
  @state() private _brandingMetadata: BrandingMetadata | null = null;
  @state() private _brandingMetadataLoading = false;
  @state() private _brandingMetadataError: string | null = null;
  @state() private _dynamicBrandingValues: Record<string, string> = {};
  @state() private _dynamicMobileBrandingValues: Record<string, string> = {};
  @state() private _mobileInherited: Record<string, boolean> = {};
  
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
  @state() private _pushNotificationsEnabled = false;
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
      showErrorDiagnostics: this._readMobileConfigValue(config, 'showErrorDiagnostics'),
      pushNotificationsEnabled: this._readMobileConfigValue(config, 'pushNotificationsEnabled')
    };
  }


  modalContext?: any;

  /**
   * Lifecycle method that runs when the element is added to the DOM.
   * We use this to populate the form if we are editing an existing tenant.
   */
  connectedCallback() {
    super.connectedCallback();
    this.setAttribute('role', 'dialog');
    this.setAttribute('aria-modal', 'true');
    this.setAttribute('aria-label', this.data?.tenant ? 'Edit Tenant' : 'Create Tenant');
    document.addEventListener('keydown', this._handleKeyDown, true);
    
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
      this._pushNotificationsEnabled = mobileConfig?.pushNotificationsEnabled ?? false;
    }
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener('keydown', this._handleKeyDown, true);
  }

  protected firstUpdated() {
    // Seed focus on open so keyboard users enter the focus trap at the primary
    // action button — this is the Shadow DOM focus-seeding pattern for modals.
    requestAnimationFrame(() => {
      this.shadowRoot?.querySelector<HTMLButtonElement>('.dialog-action-btn--primary')?.focus();
    });
  }

  protected updated(changedProperties: Map<string, unknown>) {
    super.updated(changedProperties);

    if (changedProperties.has('_maximized')) {
      this.classList.toggle('maximized', this._maximized);
    }

    if (changedProperties.has('data')) {
      this.setAttribute('aria-label', this.data?.tenant ? 'Edit Tenant' : 'Create Tenant');
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
        this._pushNotificationsEnabled = mobileConfig?.pushNotificationsEnabled ?? false;
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

  private async _fetchBrandingMetadata() {
    if (this._brandingMetadata || this._brandingMetadataLoading || this._brandingMetadataError) {
      return;
    }

    this._brandingMetadataLoading = true;
    this._brandingMetadataError = null;

    try {
      let token: string | undefined;
      await Promise.race([
        new Promise<void>(resolve => {
          this.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
            if (authContext) token = await authContext.getLatestToken();
            resolve();
          });
        }),
        new Promise<void>(resolve => setTimeout(resolve, 500))
      ]);

      const response = await fetch('/umbraco/management/api/v1/prism/branding/metadata', {
        headers: token ? { 'Authorization': `Bearer ${token}` } : {}
      });
      
      if (!response.ok) {
        throw new Error(`Failed to fetch branding metadata: ${response.status}`);
      }

      const data = await response.json() as BrandingMetadata;
      this._brandingMetadata = data;

      // Initialize dynamic values from current tenant overrides
      const tenantOverrides = this._toOverrideMap(this.data?.tenant?.brandingOverrides);
      const tenantMobileOverrides = this._toOverrideMap(this.data?.tenant?.mobileBrandingOverrides);

      this._dynamicBrandingValues = {};
      this._dynamicMobileBrandingValues = {};
      this._mobileInherited = {};

      data.sections.forEach(section => {
        section.variables.forEach(variable => {
          const varName = variable.variable;
          this._dynamicBrandingValues[varName] = tenantOverrides[varName] ?? variable.currentValue;
          const explicitMobileOverride = tenantMobileOverrides[varName];
          this._mobileInherited[varName] = !explicitMobileOverride;
          this._dynamicMobileBrandingValues[varName] = explicitMobileOverride ?? variable.currentValue;
        });
      });
    } catch (error) {
      console.error('Error fetching branding metadata:', error);
      this._brandingMetadataError = error instanceof Error ? error.message : 'Unknown error';
    } finally {
      this._brandingMetadataLoading = false;
    }
  }

  private _handleKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && this._maximized) {
      event.stopPropagation();
      this._maximized = false;
    }
  };

  private _toggleMaximize() {
    this._maximized = !this._maximized;
  }

  private _handleTabGroupClick(event: MouseEvent) {
    const path = event.composedPath() as Array<EventTarget>;
    const tab = path.find((item) => item instanceof HTMLElement && item.dataset?.tabKey) as HTMLElement | undefined;

    if (!tab) return;

    const nextTab = tab.dataset.tabKey;
    if (nextTab && nextTab !== this._activeTab) {
      this._activeTab = nextTab;
      
      // Fetch branding metadata when switching to a branding tab
      if (nextTab.startsWith('branding-')) {
        this._fetchBrandingMetadata();
      }
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
              required
              aria-required="true">
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
              required
              aria-required="true">
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
              @input=${(e: any) => this._secretKeyName = e.target.value}
              aria-describedby="secret-hint">
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
              placeholder="com.example.portal"
              aria-invalid=${!appIdValid ? 'true' : 'false'}>
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
              placeholder="https://tenant.example.com"
              aria-invalid=${!startUrlValid ? 'true' : 'false'}>
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
              placeholder="https://tenant.example.com/favicon.ico"
              aria-invalid=${!iconUrlValid ? 'true' : 'false'}>
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
              placeholder="https://tenant.example.com/media/splash.png"
              aria-invalid=${!splashUrlValid ? 'true' : 'false'}>
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

          <div class="field">
            <div class="toggle-label">
              <span>Push Notifications</span>
              <span class="toggle-hint">Enable push notifications support in the mobile bundle. Users will be prompted to allow notifications after their first biometric login.</span>
            </div>
            <label class="toggle-switch" title="${this._pushNotificationsEnabled ? 'Push notifications enabled' : 'Push notifications disabled'}">
              <input
                type="checkbox"
                aria-label="Push Notifications"
                .checked=${this._pushNotificationsEnabled}
                @change=${(e: Event) => { this._pushNotificationsEnabled = (e.target as HTMLInputElement).checked; }}
              />
              <span class="toggle-slider"></span>
            </label>
          </div>

          <div class="helper-actions">
            <uui-button look="outline" label="Use tenant defaults" @click=${this._applyMobileDefaultsFromTenant}>Use tenant defaults</uui-button>
            <uui-button look="outline" label="Suggest app id" @click=${this._suggestMobileAppId}>Suggest app id</uui-button>
          </div>

          <uui-button
            look="primary"
            color="positive"
            label="Generate & Download App Bundle"
            ?disabled=${!canProduce}
            @click=${this._handleProduceMobile}>
            ${this._isProducingMobileBundle ? 'Generating…' : 'Generate & Download App Bundle'}
          </uui-button>

          ${this._mobileBundleGenerated ? html`
            <div class="generated-helper">
              <small><strong>Bundle ready.</strong> From the extracted folder, run:</small>
              <div class="command-row">
                <code>npm install && npm run doctor</code>
                <uui-button look="outline" label="Copy npm install && npm run doctor" @click=${() => this._copyCommand('npm install && npm run doctor')}>
                  ${this._copiedCommand === 'npm install && npm run doctor' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              <div class="command-row">
                <code>npm run bootstrap:ios</code>
                <uui-button look="outline" label="Copy npm run bootstrap:ios" @click=${() => this._copyCommand('npm run bootstrap:ios')}>
                  ${this._copiedCommand === 'npm run bootstrap:ios' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              <div class="command-row">
                <code>npm run bootstrap:android</code>
                <uui-button look="outline" label="Copy npm run bootstrap:android" @click=${() => this._copyCommand('npm run bootstrap:android')}>
                  ${this._copiedCommand === 'npm run bootstrap:android' ? 'Copied' : 'Copy'}
                </uui-button>
              </div>
              ${this._isLikelyLocalhostUrl(this._mobileStartUrl)
                ? html`
                    <small>Localhost tip: if iOS trust prompts appear, run:</small>
                    <div class="command-row">
                      <code>bash scripts/trust-ios-localhost-cert.sh && npm run run:ios</code>
                      <uui-button look="outline" label="Copy trust-ios-localhost-cert && run:ios" @click=${() => this._copyCommand('bash scripts/trust-ios-localhost-cert.sh && npm run run:ios')}>
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
    // Try to use dynamic branding metadata first
    if (this._brandingMetadata) {
      return this._renderDynamicBrandingTab(tabIndex);
    }

    // Show loading state if fetching
    if (this._brandingMetadataLoading) {
      return html`
        <div
          role="tabpanel"
          id="branding-panel-${tabIndex}"
          aria-labelledby="branding-tab-${tabIndex}"
          class="tab-content">
          <uui-box style="padding: 2rem; text-align: center;">
            <uui-loader></uui-loader>
            <p style="margin-top: 1rem;">Loading branding configuration...</p>
          </uui-box>
        </div>
      `;
    }

    // Show error state if fetch failed
    if (this._brandingMetadataError) {
      return html`
        <div
          role="tabpanel"
          id="branding-panel-${tabIndex}"
          aria-labelledby="branding-tab-${tabIndex}"
          class="tab-content">
          <uui-box>
            <p style="color: var(--uui-color-danger); margin-bottom: 1rem;">
              Failed to load dynamic branding configuration: ${this._brandingMetadataError}
            </p>
            <p style="margin-bottom: 1rem;">Falling back to static fields:</p>
            ${this._renderStaticBrandingContent(tabIndex)}
          </uui-box>
        </div>
      `;
    }

    // Fallback to static branding tab
    return this._renderStaticBrandingTab(tabIndex);
  }

  private _renderStaticBrandingContent(tabIndex: number) {
    const tab = this._brandingTabs[tabIndex];
    if (!tab) return html``;

    return html`
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
          <uui-table-row data-variable="${variable.name}">
            <uui-table-cell><code>${variable.name}</code></uui-table-cell>
            <uui-table-cell><code>${variable.defaultValue ?? '—'}</code></uui-table-cell>
            <uui-table-cell>
              <uui-input
                class="override-input"
                placeholder="e.g. #0d6efd"
                label="${variable.name} (desktop override)"
                .value=${variable.overrideValue ?? ''}
                @input=${(e: InputEvent) => this._updateBrandingOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
              </uui-input>
            </uui-table-cell>
            <uui-table-cell>
              <uui-input
                class="override-input"
                placeholder="e.g. #0d6efd"
                label="${variable.name} (mobile override)"
                .value=${variable.mobileOverrideValue ?? ''}
                @input=${(e: InputEvent) => this._updateMobileBrandingOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
              </uui-input>
            </uui-table-cell>
          </uui-table-row>
        `)}
      </uui-table>
    `;
  }

  private _renderStaticBrandingTab(tabIndex: number) {
    return html`
      <div
        role="tabpanel"
        id="branding-panel-${tabIndex}"
        aria-labelledby="branding-tab-${tabIndex}"
        class="tab-content">
        <uui-box>
          ${this._renderStaticBrandingContent(tabIndex)}
        </uui-box>
      </div>
    `;
  }

  private _renderDynamicBrandingTab(tabIndex: number) {
    if (!this._brandingMetadata) return html``;

    const tabVariableNames = new Set(
      this._brandingTabs[tabIndex]?.variables.map(v => v.name) ?? []
    );

    const sectionsToShow = tabVariableNames.size > 0
      ? this._brandingMetadata.sections
          .map(section => ({
            ...section,
            variables: section.variables.filter(v => tabVariableNames.has(v.variable))
          }))
          .filter(section => section.variables.length > 0)
      : this._brandingMetadata.sections;

    const displaySections = sectionsToShow.length > 0 ? sectionsToShow : this._brandingMetadata.sections;

    return html`
      <div role="tabpanel" id="branding-panel-${tabIndex}" aria-labelledby="branding-tab-${tabIndex}" class="tab-content">
        ${displaySections.map(section => html`
          <uui-box headline="${section.name}" style="margin-bottom: 1.5rem;">
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem;">
              ${section.variables.map(variable => this._renderDynamicField(variable))}
            </div>
          </uui-box>
        `)}
      </div>
    `;
  }

  private _renderDynamicField(variable: BrandingMetadata['sections'][0]['variables'][0]) {
    const varName = variable.variable;
    const currentValue = this._dynamicBrandingValues[varName] ?? variable.currentValue;
    const isInherited = this._mobileInherited[varName] !== false;
    const effectiveMobileValue = isInherited ? currentValue : (this._dynamicMobileBrandingValues[varName] ?? variable.currentValue);

    const isDesktopOverridden = currentValue !== variable.currentValue;

    const resetValue = () => {
      this._dynamicBrandingValues = { ...this._dynamicBrandingValues, [varName]: variable.currentValue };
    };

    const renderField = (value: string, isMobile: boolean) => {
      const updateHandler = (e: Event) => {
        const newValue = (e.target as HTMLInputElement).value;
        if (isMobile) {
          this._dynamicMobileBrandingValues = {
            ...this._dynamicMobileBrandingValues,
            [varName]: newValue
          };
        } else {
          this._dynamicBrandingValues = {
            ...this._dynamicBrandingValues,
            [varName]: newValue
          };
        }
      };

      // Render color picker for color types
      if (variable.type === 'color') {
        return html`
          <div style="display: flex; gap: 0.5rem; align-items: center;">
            <input
              type="color"
              .value=${value}
              @input=${updateHandler}
              aria-label=${`${variable.label}${isMobile ? ' (mobile)' : ''} colour picker`}
              style="width: 48px; height: 32px; border: 1px solid var(--uui-color-border); border-radius: 4px; cursor: pointer;">
            <uui-input
              .value=${value}
              @input=${updateHandler}
              label=${variable.label}
              style="flex: 1;">
            </uui-input>
          </div>
        `;
      }

      // Render media picker for image types
      if (variable.type === 'image') {
        const isGradient = value && value.includes('gradient');
        const isUrl = value && !isGradient && (value.startsWith('url(') || value.startsWith('/') || value.startsWith('http'));
        const previewUrl = isUrl
          ? (value.startsWith('url(') ? value.replace(/^url\(['"]?/, '').replace(/['"]?\)$/, '') : value)
          : '';
        return html`
          <div class="image-picker">
            ${isGradient ? html`
              <div style="width: 100%; height: 40px; background: ${value}; border-radius: 4px; border: 1px solid var(--uui-color-border); margin-bottom: 0.5rem;"></div>
            ` : isUrl && previewUrl ? html`
              <img
                src=${previewUrl}
                alt="Preview"
                class="image-picker__preview"
                @error=${(e: Event) => ((e.target as HTMLImageElement).style.display = 'none')}>
            ` : ''}
            <div class="image-picker__actions">
              <uui-button
                look="secondary"
                compact
                label="Pick from Media Library"
                @click=${() => this._pickMediaForVariable(varName, isMobile)}>
                📷 Pick from Media Library
              </uui-button>
              ${value ? html`
                <uui-button
                  look="secondary"
                  compact
                  color="danger"
                  label="Clear image"
                  @click=${() => {
                    if (isMobile) {
                      this._dynamicMobileBrandingValues = { ...this._dynamicMobileBrandingValues, [varName]: '' };
                    } else {
                      this._dynamicBrandingValues = { ...this._dynamicBrandingValues, [varName]: '' };
                    }
                  }}>
                  Clear
                </uui-button>
              ` : ''}
            </div>
            <uui-input
              .value=${value}
              @input=${updateHandler}
              label=${variable.label}
              placeholder="/media/... or https://... or url('/media/...')"
              style="width: 100%;">
            </uui-input>
          </div>
        `;
      }

      // Default to text input for all other types
      return html`
        <uui-input
          .value=${value}
          @input=${updateHandler}
          label=${variable.label}
          placeholder=${variable.description}>
        </uui-input>
      `;
    };

    return html`
      <div style="display: flex; flex-direction: column; gap: 0.75rem;">
        <div>
          <label style="font-weight: 600; font-size: 0.875rem; display: block; margin-bottom: 0.25rem;">
            ${variable.label}
          </label>
          <small style="color: var(--uui-color-text-alt); display: block; margin-bottom: 0.5rem;">
            ${variable.description}
          </small>
          <div style="margin-bottom: 0.5rem;">
            <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem;">
              <small style="font-weight: 600;">Desktop</small>
              ${isDesktopOverridden ? html`
                <span style="font-size: 0.7rem; background: var(--uui-color-warning); color: var(--uui-color-warning-contrast); padding: 1px 6px; border-radius: 10px;">modified</span>
                <uui-button look="placeholder" compact style="font-size: 0.7rem;" label="Reset to default" @click=${() => resetValue()}>↺ Reset</uui-button>
              ` : ''}
            </div>
            ${renderField(currentValue, false)}
          </div>
          <div>
            <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem;">
              <small style="font-weight: 600;">Mobile</small>
              ${isInherited
                ? html`
                  <span data-testid="mobile-inherit-label-${varName}" style="font-size: 0.7rem; background: var(--uui-color-surface-emphasis); color: var(--uui-color-text-alt); padding: 1px 6px; border-radius: 10px;">inheriting from desktop</span>
                  <uui-button
                    look="placeholder"
                    compact
                    style="font-size: 0.7rem;"
                    label="Customise for mobile"
                    data-testid="mobile-inherit-toggle-${varName}"
                    @click=${() => {
                      this._dynamicMobileBrandingValues = { ...this._dynamicMobileBrandingValues, [varName]: currentValue };
                      this._mobileInherited = { ...this._mobileInherited, [varName]: false };
                    }}>
                    Customise
                  </uui-button>
                `
                : html`
                  <span data-testid="mobile-custom-badge-${varName}" style="font-size: 0.7rem; background: var(--uui-color-warning); color: var(--uui-color-warning-contrast); padding: 1px 6px; border-radius: 10px;">custom</span>
                  <uui-button
                    look="placeholder"
                    compact
                    style="font-size: 0.7rem;"
                    label="Restore mobile inheritance"
                    data-testid="mobile-inherit-toggle-${varName}"
                    @click=${() => {
                      this._mobileInherited = { ...this._mobileInherited, [varName]: true };
                    }}>
                    ↺ Reset
                  </uui-button>
                `
              }
            </div>
            <div data-testid="mobile-field-${varName}" style="${isInherited ? 'display: none;' : ''}">
              ${renderField(effectiveMobileValue, true)}
            </div>
          </div>
        </div>
      </div>
    `;
  }

  private async _pickMediaForVariable(varName: string, isMobile: boolean) {
    const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
    if (!modalManager) return;

    const modal = modalManager.open(this, UMB_MEDIA_PICKER_MODAL, {
      data: { multiple: false }
    });

    const result = await modal.onSubmit().catch(() => null);
    if (!result?.selection?.length) return;

    const unique = result.selection[0];
    if (!unique) return;

    try {
      const authContext = await this.getContext(UMB_AUTH_CONTEXT);
      const token = authContext ? await authContext.getLatestToken() : undefined;

      const res = await fetch(`/umbraco/management/api/v1/media/urls?id=${unique}`, {
        headers: token ? { 'Authorization': `Bearer ${token}` } : {}
      });
      if (!res.ok) return;
      const data = await res.json();
      const items: Array<{ id: string; urlInfos: Array<{ culture: string | null; url: string | null }> }> = Array.isArray(data) ? data : [data];
      const rawUrl: string = items[0]?.urlInfos?.[0]?.url ?? '';
      if (!rawUrl) {
        console.warn('[Prism] Media URL response had no URL', data);
        return;
      }
      const wrappedUrl = `url('${rawUrl}')`;
      if (isMobile) {
        this._dynamicMobileBrandingValues = { ...this._dynamicMobileBrandingValues, [varName]: wrappedUrl };
      } else {
        this._dynamicBrandingValues = { ...this._dynamicBrandingValues, [varName]: wrappedUrl };
      }
    } catch (err) {
      console.error('[Prism] Failed to fetch media URL', err);
    }
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
    
    // Use dynamic values if available (from metadata API)
    if (this._brandingMetadata) {
      Object.entries(this._dynamicBrandingValues).forEach(([varName, value]) => {
        if (value && value.trim().length > 0) {
          overrides[varName] = value.trim();
        }
      });
    } else {
      // Fallback to static branding tabs
      this._brandingTabs.forEach(tab => {
        tab.variables.forEach(variable => {
          if (variable.overrideValue && variable.overrideValue.trim().length > 0) {
            overrides[variable.name] = variable.overrideValue.trim();
          }
        });
      });
    }

    return overrides;
  }

  private _collectMobileBrandingOverrides() {
    const overrides: Record<string, string> = {};
    
    if (this._brandingMetadata) {
      Object.entries(this._dynamicMobileBrandingValues).forEach(([varName, value]) => {
        if (!this._mobileInherited[varName] && value && value.trim().length > 0) {
          overrides[varName] = value.trim();
        }
      });
    } else {
      // Fallback to static branding tabs
      this._brandingTabs.forEach(tab => {
        tab.variables.forEach(variable => {
          if (variable.mobileOverrideValue && variable.mobileOverrideValue.trim().length > 0) {
            overrides[variable.name] = variable.mobileOverrideValue.trim();
          }
        });
      });
    }

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
          biometricAuthEnabled: this._allowBiometricLogin,
          pushNotificationsEnabled: this._pushNotificationsEnabled
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
        document.body.appendChild(anchor);
        anchor.dispatchEvent(new MouseEvent('click', { bubbles: false, cancelable: true }));
        document.body.removeChild(anchor);
        setTimeout(() => URL.revokeObjectURL(url), 100);
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
      <uui-dialog-layout>
        <div slot="headline" class="dialog-headline">
          <div class="dialog-headline-actions">
          <button
            class="dialog-action-btn dialog-action-btn--primary"
            data-testid="modal-submit-btn"
            aria-label=${isUpdate ? 'Update Tenant' : 'Create Tenant'}
            autofocus
            @click=${this._handleSubmit}>
            ${isUpdate ? 'Update Tenant' : 'Create Tenant'}
          </button>
          <button
            class="dialog-action-btn"
            data-testid="modal-cancel-btn"
            aria-label="Cancel"
            @click=${() => this.modalContext?.reject()}>
            Cancel
          </button>
        </div>
        <div class="dialog-headline-icons">
          <button
            class="dialog-icon-btn"
            aria-label="${this._maximized ? 'Restore' : 'Maximize'}"
            title="${this._maximized ? 'Restore' : 'Maximize'}"
            @click=${this._toggleMaximize}>
            ${this._maximized
              ? html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3v5H3"/><path d="M21 8h-5V3"/><path d="M3 16h5v5"/><path d="M16 21v-5h5"/></svg>`
              : html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 3h6v6"/><path d="M9 21H3v-6"/><path d="M21 3l-7 7"/><path d="M3 21l7-7"/></svg>`
            }
          </button>
          <button
            class="dialog-icon-btn"
            aria-label="Close"
            title="Close"
            @click=${() => this.modalContext?.reject()}>
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6L6 18"/><path d="M6 6l12 12"/></svg>
          </button>
        </div>
      </div>

      <uui-tab-group @click=${this._handleTabGroupClick} aria-label="Tenant settings sections">
        <uui-tab
          id="general-tab"
          label="General"
          data-tab-key="general"
          ?active=${this._activeTab === 'general'}>
          General
        </uui-tab>
        <uui-tab
          id="identity-tab"
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

      <div class="container" role="region" aria-label="Tenant settings content">
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
    :host(.maximized) {
      position: fixed !important;
      inset: 0 !important;
      width: 100vw !important;
      height: 100vh !important;
      max-width: 100vw !important;
      max-height: 100vh !important;
      resize: none !important;
      z-index: 10000;
      border-radius: 0;
      overflow: hidden;
    }
    .dialog-headline {
      display: flex;
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      gap: var(--uui-size-space-3, 9px);
      flex-shrink: 0;
      padding: var(--uui-size-space-3, 9px) 0;
      background: var(--uui-color-surface);
    }
    .dialog-headline-actions {
      display: flex;
      flex-direction: row;
      gap: 8px;
      align-items: center;
    }
    .dialog-headline-icons {
      display: flex;
      flex-direction: row;
      gap: 6px;
      align-items: center;
      flex-shrink: 0;
    }
    .dialog-action-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: none;
      border: 1px solid var(--uui-color-border-standalone, #c2c2c2);
      cursor: pointer;
      padding: 0 var(--uui-size-space-4, 12px);
      height: 30px;
      font-size: var(--uui-type-small-size, 13px);
      font-family: inherit;
      color: var(--uui-color-text, #060606);
      border-radius: var(--uui-border-radius, 3px);
      transition: background-color 0.1s, border-color 0.1s, color 0.1s;
    }
    .dialog-action-btn:hover {
      background-color: var(--uui-color-surface-emphasis, rgba(0, 0, 0, 0.06));
    }
    .dialog-action-btn:focus-visible {
      outline: 2px solid var(--uui-color-focus, #3879d9);
      outline-offset: 1px;
    }
    .dialog-action-btn--primary {
      background-color: var(--uui-color-positive, #2bc37b);
      border-color: var(--uui-color-positive, #2bc37b);
      color: var(--uui-color-positive-contrast, #fff);
    }
    .dialog-action-btn--primary:hover {
      background-color: var(--uui-color-positive-emphasis, #27a96b);
      border-color: var(--uui-color-positive-emphasis, #27a96b);
    }
    .dialog-icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: none;
      border: none;
      cursor: pointer;
      padding: 4px;
      color: var(--uui-color-text-alt, #605e5c);
      border-radius: var(--uui-border-radius, 3px);
      line-height: 0;
      transition: background-color 0.1s, color 0.1s;
    }
    .dialog-icon-btn:hover {
      background-color: var(--uui-color-surface-emphasis, rgba(0, 0, 0, 0.06));
      color: var(--uui-color-text, #060606);
    }
    .dialog-icon-btn:focus-visible {
      outline: 2px solid var(--uui-color-focus, #3879d9);
      outline-offset: 1px;
    }
    uui-tab-group {
      position: sticky;
      top: 0;
      z-index: 10;
      background: var(--uui-color-surface);
      border-bottom: 1px solid var(--uui-color-border-standalone);
    }
    .container { 
      min-height: 350px;
    }
    .override-input {
      width: 100%;
    }
    .image-picker {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .image-picker__preview {
      max-height: 60px;
      max-width: 100%;
      object-fit: cover;
      border-radius: 4px;
      border: 1px solid var(--uui-color-border);
    }
    .image-picker__actions {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      flex-wrap: wrap;
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
    .toggle-switch input:focus-visible + .toggle-slider {
      outline: 2px solid var(--uui-color-focus, #3879d9);
      outline-offset: 2px;
    }
    @media (prefers-reduced-motion: reduce) {
      .dialog-action-btn,
      .dialog-icon-btn,
      .toggle-slider,
      .toggle-slider::before {
        transition: none;
      }
    }
  `;
}