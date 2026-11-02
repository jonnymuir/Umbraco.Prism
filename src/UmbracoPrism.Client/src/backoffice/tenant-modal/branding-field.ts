import { type TemplateResult, html } from 'lit';
import {
  type BrandingMetadata,
  type BrandingVariable,
  findVariable,
  linkableTargets,
  parseLink,
  resolveLiteralValue
} from './branding-rules.js';

/** Which set of values a control edits: desktop, mobile, or the dark palette. */
export type BrandingChannel = 'desktop' | 'mobile' | 'dark';

/** What a branding field reads from, and writes to, the section that owns the values. */
export interface BrandingFieldContext {
  metadata: BrandingMetadata | null;
  desktopValues: Record<string, string>;
  mobileValues: Record<string, string>;
  darkValues: Record<string, string>;
  /** Mobile follows desktop unless the author customises it. */
  mobileInherited: Record<string, boolean>;
  /** A colour keeps its light value in dark mode unless the author customises it. */
  darkCustomised: Record<string, boolean>;
  /** Keyed by variable name (desktop) or `${variable}:mobile` / `${variable}:dark`: the linked-token picker is open. */
  linkEditing: Record<string, boolean>;
  setValue(name: string, channel: BrandingChannel, value: string): void;
  setMobileInherited(name: string, inherited: boolean): void;
  setDarkCustomised(name: string, customised: boolean): void;
  setLinkEditing(key: string, editing: boolean): void;
  pickMedia(name: string, channel: BrandingChannel): void;
}

const BADGE = 'font-size: 0.7rem; padding: 1px 6px; border-radius: 10px;';
const WARNING_BADGE = `${BADGE} background: var(--uui-color-warning); color: var(--uui-color-warning-contrast);`;

const hideOnError = (e: Event) => ((e.target as HTMLImageElement).style.display = 'none');

/**
 * One branding variable: the desktop value, the mobile value (inheriting or customised) and, for a
 * colour, its dark mode value (the same as light, or customised), each rendered as the control its type asks for — a colour picker, a media picker, a text box — or as
 * a "Linked to X" badge when its value is a var(--x) reference to another token.
 */
export class BrandingField {
  constructor(
    private readonly _ctx: BrandingFieldContext,
    private readonly _variable: BrandingVariable
  ) {}

  private get _name() {
    return this._variable.variable;
  }

  render(): TemplateResult {
    const { _name: name, _variable: variable, _ctx: ctx } = this;
    const currentValue = ctx.desktopValues[name] ?? variable.currentValue;
    const isInherited = ctx.mobileInherited[name] !== false;
    const effectiveMobileValue = isInherited ? currentValue : (ctx.mobileValues[name] ?? variable.currentValue);

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
            ${this._desktopHeader(currentValue !== variable.currentValue)}
            ${this._valueControl(currentValue, 'desktop')}
          </div>
          <div>
            ${this._mobileHeader(isInherited, currentValue)}
            <div data-testid="mobile-field-${name}" style="${isInherited ? 'display: none;' : ''}">
              ${this._valueControl(effectiveMobileValue, 'mobile')}
            </div>
          </div>
          ${variable.type === 'color' ? this._darkRow(currentValue) : ''}
        </div>
      </div>
    `;
  }

  private _desktopHeader(isOverridden: boolean) {
    return html`
      <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem;">
        <small style="font-weight: 600;">Desktop</small>
        ${isOverridden ? html`
          <span style="${WARNING_BADGE}">modified</span>
          <uui-button look="placeholder" compact style="font-size: 0.7rem;" label="Reset to default" @click=${() => this._reset()}>↺ Reset</uui-button>
        ` : ''}
      </div>
    `;
  }

  private _reset() {
    this._ctx.setValue(this._name, 'desktop', this._variable.currentValue);
    this._ctx.setLinkEditing(this._name, false);
  }

  private _mobileHeader(isInherited: boolean, desktopValue: string) {
    const name = this._name;
    return html`
      <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem;">
        <small style="font-weight: 600;">Mobile</small>
        ${isInherited
          ? html`
            <span data-testid="mobile-inherit-label-${name}" style="${BADGE} background: var(--uui-color-surface-emphasis); color: var(--uui-color-text-alt);">inheriting from desktop</span>
            <uui-button
              look="placeholder"
              compact
              style="font-size: 0.7rem;"
              label="Customise for mobile"
              data-testid="mobile-inherit-toggle-${name}"
              @click=${() => {
                this._ctx.setValue(name, 'mobile', desktopValue);
                this._ctx.setMobileInherited(name, false);
              }}>
              Customise
            </uui-button>
          `
          : html`
            <span data-testid="mobile-custom-badge-${name}" style="${WARNING_BADGE}">custom</span>
            <uui-button
              look="placeholder"
              compact
              style="font-size: 0.7rem;"
              label="Restore mobile inheritance"
              data-testid="mobile-inherit-toggle-${name}"
              @click=${() => this._ctx.setMobileInherited(name, true)}>
              ↺ Reset
            </uui-button>
          `}
      </div>
    `;
  }

  /** Dark mode: the light value until the author customises it for devices in dark mode. */
  private _darkRow(lightValue: string) {
    const name = this._name;
    const customised = this._ctx.darkCustomised[name] === true;
    return html`
      <div>
        <div style="display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem;">
          <small style="font-weight: 600;">Dark mode</small>
          ${customised
            ? html`
              <span data-testid="dark-custom-badge-${name}" style="${WARNING_BADGE}">custom</span>
              <uui-button
                look="placeholder"
                compact
                style="font-size: 0.7rem;"
                label="Use the light value in dark mode"
                data-testid="dark-toggle-${name}"
                @click=${() => this._ctx.setDarkCustomised(name, false)}>
                ↺ Reset
              </uui-button>
            `
            : html`
              <span data-testid="dark-same-label-${name}" style="${BADGE} background: var(--uui-color-surface-emphasis); color: var(--uui-color-text-alt);">same as light</span>
              <uui-button
                look="placeholder"
                compact
                style="font-size: 0.7rem;"
                label="Customise for dark mode"
                data-testid="dark-toggle-${name}"
                @click=${() => {
                  this._ctx.setValue(name, 'dark', this._literal(lightValue) ?? lightValue);
                  this._ctx.setDarkCustomised(name, true);
                }}>
                Customise
              </uui-button>
            `}
        </div>
        ${customised ? html`
          <div data-testid="dark-field-${name}">
            ${this._valueControl(this._ctx.darkValues[name] ?? lightValue, 'dark')}
          </div>
        ` : ''}
      </div>
    `;
  }

  /**
   * A field whose live value is a var(--x) reference (see prism-govuk-bridge.css) renders as a
   * "Linked to X" badge instead of the raw widget, a native colour/text input can't render a var()
   * string. "Customise" swaps the badge for a picker of same-type tokens, plus a "Custom value"
   * escape hatch that drops back to the plain control seeded with the resolved live colour.
   */
  private _valueControl(value: string, channel: BrandingChannel) {
    const link = parseLink(value);
    if (!link) {
      return this._plainControl(value, channel);
    }

    const editKey = channel === 'desktop' ? this._name : `${this._name}:${channel}`;
    return this._ctx.linkEditing[editKey] ? this._linkPicker(value, link.target, editKey, channel) : this._linkBadge(value, link.target, editKey);
  }

  private _literal(value: string) {
    return resolveLiteralValue(this._ctx.metadata, this._ctx.desktopValues, value);
  }

  private _linkBadge(value: string, target: string, editKey: string) {
    const targetLabel = findVariable(this._ctx.metadata, target)?.label ?? target;
    const preview = this._variable.type === 'color' ? this._literal(value) : undefined;
    return html`
      <div data-testid="link-badge-${editKey}" style="display: flex; align-items: center; gap: 0.5rem; padding: 0.4rem 0.6rem; border: 1px dashed var(--uui-color-border); border-radius: 4px;">
        ${preview ? html`<span style="width: 20px; height: 20px; flex-shrink: 0; border-radius: 4px; border: 1px solid var(--uui-color-border); background: ${preview};"></span>` : ''}
        <span style="flex: 1; font-size: 0.85rem; color: var(--uui-color-text-alt);">Linked to ${targetLabel}</span>
        <uui-button
          look="placeholder"
          compact
          style="font-size: 0.7rem;"
          label=${`Customise ${this._variable.label}`}
          data-testid="link-customise-${editKey}"
          @click=${() => this._ctx.setLinkEditing(editKey, true)}>
          Customise
        </uui-button>
      </div>
    `;
  }

  private _linkPicker(value: string, currentTarget: string, editKey: string, channel: BrandingChannel) {
    const options = [
      { name: 'Custom value (not linked)', value: '__custom__' },
      ...linkableTargets(this._ctx.metadata, this._ctx.desktopValues, this._variable).map(t => ({ ...t, selected: t.value === currentTarget }))
    ];

    return html`
      <div style="display: flex; flex-direction: column; gap: 0.5rem;">
        <uui-select
          label="Linked token"
          data-testid="link-picker-${editKey}"
          .options=${options}
          @change=${(e: Event) => {
            const chosen = (e.target as HTMLInputElement).value;
            if (chosen === '__custom__') {
              this._ctx.setValue(this._name, channel, this._literal(value) ?? value);
            } else {
              const fallback = this._literal(`var(${chosen})`);
              this._ctx.setValue(this._name, channel, fallback ? `var(${chosen}, ${fallback})` : `var(${chosen})`);
            }
          }}>
        </uui-select>
        <uui-button
          look="placeholder"
          compact
          style="font-size: 0.7rem; align-self: flex-start;"
          label="Cancel customising"
          data-testid="link-cancel-${editKey}"
          @click=${() => this._ctx.setLinkEditing(editKey, false)}>
          Cancel
        </uui-button>
      </div>
    `;
  }

  private _plainControl(value: string, channel: BrandingChannel) {
    const update = (e: Event) => this._ctx.setValue(this._name, channel, (e.target as HTMLInputElement).value);
    const { type, label, description } = this._variable;

    if (type === 'color') return this._colourControl(value, channel, update);
    if (type === 'image') return this._imageControl(value, channel, update);

    return html`
      <uui-input .value=${value} @input=${update} label=${label} placeholder=${description}></uui-input>
    `;
  }

  private _colourControl(value: string, channel: BrandingChannel, update: (e: Event) => void) {
    const { label } = this._variable;
    return html`
      <div style="display: flex; gap: 0.5rem; align-items: center;">
        <input
          type="color"
          .value=${value}
          @input=${update}
          aria-label=${`${label}${channel === 'desktop' ? '' : ` (${channel === 'dark' ? 'dark mode' : 'mobile'})`} colour picker`}
          style="width: 48px; height: 32px; border: 1px solid var(--uui-color-border); border-radius: 4px; cursor: pointer;">
        <uui-input .value=${value} @input=${update} label=${label} style="flex: 1;"></uui-input>
      </div>
    `;
  }

  private _imagePreview(value: string) {
    if (value && value.includes('gradient')) {
      return html`<div style="width: 100%; height: 40px; background: ${value}; border-radius: 4px; border: 1px solid var(--uui-color-border); margin-bottom: 0.5rem;"></div>`;
    }

    const isUrl = value && (value.startsWith('url(') || value.startsWith('/') || value.startsWith('http'));
    const previewUrl = isUrl ? (value.startsWith('url(') ? value.replace(/^url\(['"]?/, '').replace(/['"]?\)$/, '') : value) : '';
    return previewUrl ? html`<img src=${previewUrl} alt="Preview" class="image-picker__preview" @error=${hideOnError}>` : '';
  }

  private _imageControl(value: string, channel: BrandingChannel, update: (e: Event) => void) {
    const { label } = this._variable;
    return html`
      <div class="image-picker">
        ${this._imagePreview(value)}
        <div class="image-picker__actions">
          <uui-button look="secondary" compact label="Pick from Media Library" @click=${() => this._ctx.pickMedia(this._name, channel)}>
            📷 Pick from Media Library
          </uui-button>
          ${value ? html`
            <uui-button look="secondary" compact color="danger" label="Clear image" @click=${() => this._ctx.setValue(this._name, channel, '')}>
              Clear
            </uui-button>
          ` : ''}
        </div>
        <uui-input
          .value=${value}
          @input=${update}
          label=${label}
          placeholder="/media/... or https://... or url('/media/...')"
          style="width: 100%;">
        </uui-input>
      </div>
    `;
  }
}
