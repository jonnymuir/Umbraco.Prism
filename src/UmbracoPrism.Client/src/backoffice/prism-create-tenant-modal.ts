import { LitElement, html } from 'lit';
import { customElement, state, property } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { BrandingSection } from './tenant-modal/branding-section.js';
import type { BrandingTab } from './tenant-modal/branding-rules.js';
import { IdentitySection } from './tenant-modal/identity-section.js';
import { MobileSection } from './tenant-modal/mobile-section.js';
import { TokenStatusSection } from './tenant-modal/token-status-section.js';
import { tenantModalStyles } from './tenant-modal/tenant-modal.styles.js';

const BRANDING_TAB_PREFIX = 'branding-';
const brandingTabKey = (index: number) => `${BRANDING_TAB_PREFIX}${index}`;

const MAXIMIZE_ICON = html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 3h6v6"/><path d="M9 21H3v-6"/><path d="M21 3l-7 7"/><path d="M3 21l7-7"/></svg>`;
const RESTORE_ICON = html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3v5H3"/><path d="M21 8h-5V3"/><path d="M3 16h5v5"/><path d="M16 21v-5h5"/></svg>`;
const CLOSE_ICON = html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6L6 18"/><path d="M6 6l12 12"/></svg>`;

/**
 * The create/edit tenant dialog: a tabbed form whose sections each own their part of the tenant —
 * identity, mobile app, branding, token status — while this element holds the basics (name, host,
 * biometric login), the tabs, and saving.
 */
@customElement('prism-create-tenant-modal')
export class PrismCreateTenantModalElement extends UmbElementMixin(LitElement) {
  /**
   * Data passed in from the Modal Manager.
   * If 'tenant' is present, we are in Edit mode.
   */
  @property({ type: Object })
  public data?: {
    tenant?: any;
    brandingTabs?: BrandingTab[];
  };

  @state() private _activeTab = 'general';
  @state() private _maximized = false;

  // Form State
  @state() private _id: number | null = null;
  @state() private _name = '';
  @state() private _hostname = '';
  @state() private _allowBiometricLogin = true;

  private readonly _identity = new IdentitySection(this, { tenantId: () => this._id, hostname: () => this._hostname });
  private readonly _mobile = new MobileSection(this, {
    tenantId: () => this._id,
    tenantName: () => this._name,
    hostname: () => this._hostname,
    biometricLoginAllowed: () => this._allowBiometricLogin
  });
  private readonly _branding = new BrandingSection(this, { tenant: () => this.data?.tenant });
  private readonly _tokens = new TokenStatusSection(this);

  modalContext?: any;

  private _tenantPayload() {
    return {
      id: this._id,
      name: this._name,
      hostname: this._hostname,
      ...this._identity.payload,
      brandingOverrides: this._branding.collectOverrides(),
      mobileBrandingOverrides: this._branding.collectMobileOverrides(),
      mobileAppConfig: this._mobile.appConfig,
      allowBiometricLogin: this._allowBiometricLogin
    };
  }

  /** The payload the Save button sends. (Specs read it directly, so the name stays.) */
  private _buildTenantPayload() {
    return this._tenantPayload();
  }

  connectedCallback() {
    super.connectedCallback();
    this.setAttribute('role', 'dialog');
    this.setAttribute('aria-modal', 'true');
    this.setAttribute('aria-label', this.data?.tenant ? 'Edit Tenant' : 'Create Tenant');
    document.addEventListener('keydown', this._handleKeyDown, true);
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

  protected willUpdate(changedProperties: Map<string, unknown>) {
    super.willUpdate(changedProperties);

    if (changedProperties.has('data')) {
      this._loadFromData();
    }
  }

  /** Fills the form from the tenant being edited, or blank for a new one. */
  private _loadFromData() {
    const tenant = this.data?.tenant;
    this.setAttribute('aria-label', tenant ? 'Edit Tenant' : 'Create Tenant');
    this._id = tenant?.id ?? null;
    this._name = tenant?.name ?? '';
    this._hostname = tenant?.hostname ?? '';
    this._allowBiometricLogin = tenant?.allowBiometricLogin ?? true;
    this._identity.loadFrom(tenant);
    this._mobile.loadFrom(tenant);
    this._branding.loadFrom(this.data?.brandingTabs, tenant);
    this._ensureActiveTab();
  }

  protected updated(changedProperties: Map<string, unknown>) {
    super.updated(changedProperties);

    if (changedProperties.has('_maximized')) {
      this.classList.toggle('maximized', this._maximized);
    }
  }

  private _ensureActiveTab() {
    const brandingKeys = this._branding.tabs.map((_, index) => brandingTabKey(index));
    const allowedTabs = new Set(['general', 'identity', 'mobile', 'tokens', ...brandingKeys]);

    if (!allowedTabs.has(this._activeTab)) {
      this._activeTab = 'general';
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
    const tab = path.find(item => item instanceof HTMLElement && item.dataset?.tabKey) as HTMLElement | undefined;

    const nextTab = tab?.dataset.tabKey;
    if (!nextTab || nextTab === this._activeTab) return;

    this._activeTab = nextTab;
    if (nextTab.startsWith(BRANDING_TAB_PREFIX)) {
      this._branding.fetchMetadata();
    }
    if (nextTab === 'tokens') {
      this._tokens.refresh(this._id);
    }
  }

  private async _handleSubmit() {
    if (!this._name || !this._hostname) {
      this._activeTab = 'general';
      return;
    }

    if (this._identity.validationMessage) {
      this._activeTab = 'identity';
      return;
    }

    const tenant = this._buildTenantPayload();

    this.consumeContext(UMB_AUTH_CONTEXT, async authContext => {
      if (!authContext) return;
      const token = await authContext.getLatestToken();

      const isUpdate = this._id !== null;
      const endpoint = isUpdate ? `/umbraco/management/api/v1/prism/tenants/${this._id}` : '/umbraco/management/api/v1/prism/tenants';

      // Use 'put' for updates and 'post' for new records
      const { error } = (await tryExecute(
        this,
        umbHttpClient[isUpdate ? 'put' : 'post']({
          url: endpoint,
          body: tenant,
          headers: { Authorization: `Bearer ${token}` }
        })
      )) as any;

      if (!error) {
        this.modalContext?.submit();
      } else {
        console.error('Failed to save tenant', error);
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

  private _renderHeadline() {
    const isUpdate = this._id !== null;
    const label = isUpdate ? 'Update Tenant' : 'Create Tenant';
    const sizeLabel = this._maximized ? 'Restore' : 'Maximize';
    return html`
      <div slot="headline" class="dialog-headline">
        <div class="dialog-headline-actions">
          <button
            class="dialog-action-btn dialog-action-btn--primary"
            data-testid="modal-submit-btn"
            aria-label=${label}
            autofocus
            @click=${this._handleSubmit}>
            ${label}
          </button>
          <button class="dialog-action-btn" data-testid="modal-cancel-btn" aria-label="Cancel" @click=${() => this.modalContext?.reject()}>
            Cancel
          </button>
        </div>
        <div class="dialog-headline-icons">
          <button class="dialog-icon-btn" aria-label="${sizeLabel}" title="${sizeLabel}" @click=${this._toggleMaximize}>
            ${this._maximized ? RESTORE_ICON : MAXIMIZE_ICON}
          </button>
          <button class="dialog-icon-btn" aria-label="Close" title="Close" @click=${() => this.modalContext?.reject()}>
            ${CLOSE_ICON}
          </button>
        </div>
      </div>
    `;
  }

  private _renderTabs() {
    const tab = (id: string, key: string, label: string) => html`
      <uui-tab id=${id} label=${label} data-tab-key=${key} ?active=${this._activeTab === key}>
        ${label}
      </uui-tab>
    `;

    return html`
      <uui-tab-group @click=${this._handleTabGroupClick} aria-label="Tenant settings sections">
        ${tab('general-tab', 'general', 'General')}
        ${tab('identity-tab', 'identity', 'Identity')}
        ${tab('mobile-tab', 'mobile', 'Produce Mobile')}
        ${tab('tokens-tab', 'tokens', 'Environment Tokens')}
        ${this._branding.tabs.map((brandingTab, index) => tab(`branding-tab-${index}`, brandingTabKey(index), brandingTab.label))}
      </uui-tab-group>
    `;
  }

  private _renderActiveTab() {
    switch (this._activeTab) {
      case 'general':
        return this._renderGeneralTab();
      case 'identity':
        return this._identity.render();
      case 'mobile':
        return this._mobile.render();
      case 'tokens':
        return this._tokens.render(this._id);
      default:
        return this._branding.tabs.map((_, index) => (this._activeTab === brandingTabKey(index) ? this._branding.render(index) : ''));
    }
  }

  render() {
    return html`
      <uui-dialog-layout>
        ${this._renderHeadline()}
        ${this._renderTabs()}
        <div class="container" role="region" aria-label="Tenant settings content">
          ${this._renderActiveTab()}
        </div>
      </uui-dialog-layout>
    `;
  }

  static styles = tenantModalStyles;
}
