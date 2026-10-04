import type { ReactiveController, ReactiveControllerHost } from 'lit';
import { type TemplateResult, html } from 'lit';
import { ifDefined } from 'lit/directives/if-defined.js';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import type { UmbElement } from '@umbraco-cms/backoffice/element-api';
import { pickMediaUrl } from './media-picker.js';
import {
  type MobileDraft,
  bundleRequest,
  emptyMobile,
  isLikelyLocalhostUrl,
  isValidAbsoluteUrl,
  isValidMobileAppId,
  mobileAppConfig,
  mobileFromTenant,
  withTenantDefaults,
  defaultMobileAppId
} from './mobile-rules.js';

export interface MobileContext {
  tenantId(): number | null;
  tenantName(): string;
  hostname(): string;
  biometricLoginAllowed(): boolean;
}

/** Saves a generated bundle through the browser's download mechanism. */
function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.dispatchEvent(new MouseEvent('click', { bubbles: false, cancelable: true }));
  document.body.removeChild(anchor);
  setTimeout(() => URL.revokeObjectURL(url), 100);
}

/** The "Produce Mobile" tab: the tenant's mobile app settings, and generating the Capacitor bundle from them. */
export class MobileSection implements ReactiveController {
  draft: MobileDraft = emptyMobile();
  private _producing = false;
  private _generated = false;
  private _copied = '';

  constructor(
    private readonly _host: ReactiveControllerHost & UmbElement,
    private readonly _context: MobileContext
  ) {
    _host.addController(this);
  }

  hostConnected() {}

  /** Takes up a saved tenant's mobile settings, or starts from the defaults for a new one. */
  loadFrom(tenant: any | undefined) {
    this.draft = tenant ? mobileFromTenant(tenant) : emptyMobile();
    this._generated = false;
    this._host.requestUpdate();
  }

  /** The mobile config saved with the tenant. */
  get appConfig() {
    return mobileAppConfig(this.draft);
  }

  private _patch(change: Partial<MobileDraft>) {
    this.draft = { ...this.draft, ...change };
    this._host.requestUpdate();
  }

  private readonly _suggestAppId = () => {
    this._patch({ appId: defaultMobileAppId(this.draft.appName || this._context.tenantName() || 'tenant') });
  };

  private readonly _useTenantDefaults = () => {
    this.draft = withTenantDefaults(this.draft, this._context.tenantName(), this._context.hostname());
    this._host.requestUpdate();
  };

  private async _pickMedia(field: 'icon' | 'splash') {
    const rawUrl = await pickMediaUrl(this._host, 'Mobile ');
    if (!rawUrl) return;

    const absoluteUrl = rawUrl.startsWith('http') ? rawUrl : `${window.location.origin}${rawUrl}`;
    if (absoluteUrl.toLowerCase().endsWith('.svg')) {
      if (field === 'icon') {
        this._patch({ iconPickerError: 'SVG files are not supported for app icons. Please pick a PNG or JPG image.' });
      } else {
        this._patch({ splashPickerError: 'SVG files are not recommended for splash screens. Please pick a PNG or JPG image.' });
      }
      return;
    }

    this._patch(field === 'icon' ? { iconPickerError: '', iconUrl: absoluteUrl } : { splashPickerError: '', splashUrl: absoluteUrl });
  }

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

      this._copied = command;
      this._host.requestUpdate();
      window.setTimeout(() => {
        if (this._copied === command) {
          this._copied = '';
          this._host.requestUpdate();
        }
      }, 1500);
    } catch (error) {
      console.error('Failed to copy command', error);
    }
  }

  private readonly _produceBundle = async (e?: Event) => {
    e?.preventDefault();
    e?.stopPropagation();
    const tenantId = this._context.tenantId();
    if (tenantId === null || this._producing) return;

    this._setProducing(true);

    this._host.consumeContext(UMB_AUTH_CONTEXT, async authContext => {
      if (!authContext) {
        this._setProducing(false);
        return;
      }

      try {
        const bundle = await this._requestBundle(tenantId, await authContext.getLatestToken());
        if (bundle) {
          downloadBlob(bundle.blob, bundle.fileName);
          this._generated = true;
          this._host.requestUpdate();
        }
      } catch (error) {
        console.error('Failed to produce mobile bundle', error);
      } finally {
        this._setProducing(false);
      }
    });
  };

  /** Asks the server to generate the bundle; null (after logging why) if it could not. */
  private async _requestBundle(tenantId: number, token: string | undefined): Promise<{ blob: Blob; fileName: string } | null> {
    const payload = bundleRequest(this.draft, this._context.biometricLoginAllowed());
    console.log('[Prism] Producing mobile bundle — request payload:', JSON.stringify(payload));
    const response = await fetch(`/umbraco/management/api/v1/prism/tenants/${tenantId}/produce-mobile`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      console.error('Failed to produce mobile bundle', await response.text());
      return null;
    }

    const fileNameHeader = response.headers.get('Content-Disposition') ?? '';
    const nameMatch = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(fileNameHeader);
    return {
      blob: await response.blob(),
      fileName: nameMatch?.[1] ? decodeURIComponent(nameMatch[1]) : `prism-mobile-${tenantId}.zip`
    };
  }

  private _setProducing(producing: boolean) {
    this._producing = producing;
    this._host.requestUpdate();
  }

  // ── render ────────────────────────────────────────────────────────────────

  private _textField(field: { id: string; label: string; key: keyof MobileDraft; placeholder?: string; invalid?: boolean; after?: TemplateResult }) {
    return html`
      <div class="field">
        <uui-label for=${field.id}>${field.label}</uui-label>
        <uui-input
          id=${field.id}
          label=${field.label}
          .value=${this.draft[field.key] as string}
          @input=${(e: any) => this._patch({ [field.key]: e.target.value })}
          placeholder=${ifDefined(field.placeholder)}
          aria-invalid=${ifDefined(field.invalid === undefined ? undefined : field.invalid ? 'true' : 'false')}>
        </uui-input>
        ${field.after ?? ''}
      </div>
    `;
  }

  /** The icon and the splash screen are the same control: a preview, a Media Library picker, a URL box. */
  private _assetField(asset: {
    kind: 'icon' | 'splash';
    heading: string;
    description: string;
    url: string;
    valid: boolean;
    pickerError: string;
    placeholder: string;
    previewAlt: string;
    pickLabel: string;
    clearLabel: string;
    inputLabel: string;
    urlError: string;
  }) {
    const id = `mobile-${asset.kind}-url`;
    const descId = `mobile-${asset.kind}-desc`;
    const urlKey = asset.kind === 'icon' ? 'iconUrl' : 'splashUrl';
    return html`
      <div class="field">
        <uui-label for=${id}>${asset.heading}</uui-label>
        <small id=${descId}>${asset.description}</small>
        ${asset.url ? html`
          <img class="mobile-asset-preview"
            src=${asset.url}
            alt=${asset.previewAlt}
            @error=${(e: Event) => ((e.target as HTMLImageElement).style.display = 'none')}>
        ` : ''}
        <div style="display:flex;gap:8px;align-items:center;margin-bottom:4px;">
          <uui-button look="secondary" compact label=${asset.pickLabel} aria-describedby=${descId} @click=${() => this._pickMedia(asset.kind)}>
            Pick from Media Library
          </uui-button>
          ${asset.url ? html`
            <uui-button look="secondary" compact color="danger" label=${asset.clearLabel} @click=${() => this._patch({ [urlKey]: '' })}>
              Clear
            </uui-button>
          ` : ''}
        </div>
        <uui-input
          id=${id}
          label=${asset.inputLabel}
          .value=${asset.url}
          @input=${(e: any) => this._patch({ [urlKey]: e.target.value })}
          placeholder=${asset.placeholder}
          aria-invalid=${!asset.valid ? 'true' : 'false'}
          aria-describedby=${descId}>
        </uui-input>
        ${asset.valid ? html`` : html`<small class="error-text">${asset.urlError}</small>`}
        ${asset.pickerError ? html`<small class="error-text">${asset.pickerError}</small>` : html``}
      </div>
    `;
  }

  private _toggle(toggle: { title: string; hint: string; checked: boolean; enabledTitle: string; disabledTitle: string; onChange: (checked: boolean) => void }) {
    return html`
      <div class="field">
        <div class="toggle-label">
          <span>${toggle.title}</span>
          <span class="toggle-hint">${toggle.hint}</span>
        </div>
        <label class="toggle-switch" title="${toggle.checked ? toggle.enabledTitle : toggle.disabledTitle}">
          <input
            type="checkbox"
            aria-label=${toggle.title}
            .checked=${toggle.checked}
            @change=${(e: Event) => toggle.onChange((e.target as HTMLInputElement).checked)}
          />
          <span class="toggle-slider"></span>
        </label>
      </div>
    `;
  }

  private _commandRow(command: string, label: string) {
    return html`
      <div class="command-row">
        <code>${command}</code>
        <uui-button look="outline" label=${label} @click=${() => this._copyCommand(command)}>
          ${this._copied === command ? 'Copied' : 'Copy'}
        </uui-button>
      </div>
    `;
  }

  private _renderGeneratedHelper() {
    const localhost = isLikelyLocalhostUrl(this.draft.startUrl);
    return html`
      <div class="generated-helper">
        <small><strong>Bundle ready.</strong> From the extracted folder, run:</small>
        ${this._commandRow('npm install && npm run doctor', 'Copy npm install && npm run doctor')}
        ${this._commandRow('npm run bootstrap:ios', 'Copy npm run bootstrap:ios')}
        ${this._commandRow('npm run bootstrap:android', 'Copy npm run bootstrap:android')}
        ${localhost
          ? html`
              <small>Localhost tip: if iOS trust prompts appear, run:</small>
              ${this._commandRow('bash scripts/trust-ios-localhost-cert.sh && npm run run:ios', 'Copy trust-ios-localhost-cert && run:ios')}
            `
          : html``}
      </div>
    `;
  }

  private _renderErrorScreenFields() {
    return html`
      <h5 class="section-title">Startup Error Screen</h5>
      <p class="description">Shown if the app cannot reach your Start URL during launch.</p>

      ${this._textField({ id: 'mobile-error-title', label: 'Error Title', key: 'errorTitle', placeholder: 'We’re having trouble connecting' })}
      ${this._textField({ id: 'mobile-error-message', label: 'Error Message', key: 'errorMessage', placeholder: 'Please check your connection and try again.' })}
      ${this._textField({ id: 'mobile-error-bg', label: 'Error Background Color', key: 'errorBackgroundColor', placeholder: '#0f172a' })}
      ${this._textField({ id: 'mobile-error-text', label: 'Error Text Color', key: 'errorTextColor', placeholder: '#f8fafc' })}

      <div class="field checkbox-field">
        <uui-checkbox
          label="Show technical diagnostics"
          .checked=${this.draft.showErrorDiagnostics}
          @change=${(e: any) => this._patch({ showErrorDiagnostics: Boolean(e.target.checked) })}>
          Show technical diagnostics
        </uui-checkbox>
        <small>When enabled, users can expand technical details (status, timeout, and last error) for debugging.</small>
      </div>
    `;
  }

  render() {
    const d = this.draft;
    const isEditMode = this._context.tenantId() !== null;
    const appIdValid = isValidMobileAppId(d.appId);
    const startUrlValid = isValidAbsoluteUrl(d.startUrl);
    const iconUrlValid = !d.iconUrl || isValidAbsoluteUrl(d.iconUrl);
    const splashUrlValid = !d.splashUrl || isValidAbsoluteUrl(d.splashUrl);
    const canProduce = isEditMode && !this._producing && appIdValid && startUrlValid && iconUrlValid && splashUrlValid;

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

          ${this._textField({ id: 'mobile-app-name', label: 'App Name', key: 'appName' })}
          ${this._textField({
            id: 'mobile-app-id',
            label: 'App ID',
            key: 'appId',
            placeholder: 'com.example.portal',
            invalid: !appIdValid,
            after: html`
              <small>Reverse-domain format. Example: <code>com.acme.portal</code></small>
              ${appIdValid ? html`` : html`<small class="error-text">App ID must be reverse-domain style (e.g. <code>com.example.portal</code>).</small>`}
            `
          })}
          ${this._textField({ id: 'mobile-version', label: 'Version', key: 'version', placeholder: '1.0.0' })}
          ${this._textField({
            id: 'mobile-start-url',
            label: 'Start URL',
            key: 'startUrl',
            placeholder: 'https://tenant.example.com',
            invalid: !startUrlValid,
            after: html`
              ${startUrlValid ? html`` : html`<small class="error-text">Start URL must be an absolute URL, e.g. <code>https://tenant.example.com</code>.</small>`}
              ${isLikelyLocalhostUrl(d.startUrl) ? html`<small class="error-text">Localhost is supported for simulator/device testing, but iOS requires trusting your HTTPS cert first (or use a LAN/tunnel/public URL).</small>` : html``}
            `
          })}
          ${this._textField({ id: 'mobile-ua-marker', label: 'User Agent Marker', key: 'userAgentMarker', placeholder: 'PrismMobile' })}

          ${this._assetField({
            kind: 'icon',
            heading: 'App Icon',
            description: 'Square PNG, ideally 1024×1024px. Used to generate all device icon sizes.',
            url: d.iconUrl,
            valid: iconUrlValid,
            pickerError: d.iconPickerError,
            placeholder: 'https://tenant.example.com/favicon.ico',
            previewAlt: 'App icon preview',
            pickLabel: 'Pick app icon from Media Library',
            clearLabel: 'Clear app icon',
            inputLabel: 'Icon URL',
            urlError: 'Icon URL must be an absolute URL.'
          })}
          ${this._assetField({
            kind: 'splash',
            heading: 'Splash Screen',
            description: 'Full-screen image shown while the app loads. Recommended 2732×2732px PNG.',
            url: d.splashUrl,
            valid: splashUrlValid,
            pickerError: d.splashPickerError,
            placeholder: 'https://tenant.example.com/splash.png',
            previewAlt: 'Splash screen preview',
            pickLabel: 'Pick splash screen from Media Library',
            clearLabel: 'Clear splash screen',
            inputLabel: 'Splash URL',
            urlError: 'Splash URL must be an absolute URL.'
          })}

          ${this._renderErrorScreenFields()}

          ${this._toggle({
            title: 'Push Notifications',
            hint: 'Enable push notifications support in the mobile bundle. Users will be prompted to allow notifications after their first biometric login.',
            checked: d.pushNotificationsEnabled,
            enabledTitle: 'Push notifications enabled',
            disabledTitle: 'Push notifications disabled',
            onChange: checked => this._patch({ pushNotificationsEnabled: checked })
          })}

          <div class="helper-actions">
            <uui-button look="outline" label="Use tenant defaults" @click=${this._useTenantDefaults}>Use tenant defaults</uui-button>
            <uui-button look="outline" label="Suggest app id" @click=${this._suggestAppId}>Suggest app id</uui-button>
          </div>

          <uui-button
            look="primary"
            color="positive"
            label="Generate & Download App Bundle"
            ?disabled=${!canProduce}
            @click=${this._produceBundle}>
            ${this._producing ? 'Generating…' : 'Generate & Download App Bundle'}
          </uui-button>

          ${this._generated ? this._renderGeneratedHelper() : html``}
        </uui-box>
      </div>
    `;
  }
}
