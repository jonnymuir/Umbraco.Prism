import type { ReactiveController, ReactiveControllerHost } from 'lit';
import { html } from 'lit';
import { authHeaders, bearerTokenWithin500ms, type ContextHost } from './auth.js';
import { BrandingField, type BrandingFieldContext } from './branding-field.js';
import {
  type BrandingMetadata,
  type BrandingTab,
  type BrandingVariable,
  nonEmptyOverrides,
  staticTableOverrides,
  tabsWithMobileOverrides,
  toOverrideMap
} from './branding-rules.js';
import { pickMediaUrl } from './media-picker.js';
import type { UmbElement } from '@umbraco-cms/backoffice/element-api';

export interface BrandingContext {
  /** The tenant being edited, if any: its saved overrides seed the editor. */
  tenant(): any | undefined;
}

/**
 * The branding tabs: each CSS custom property Prism finds can be overridden for desktop and, when
 * the author wants it to differ, for mobile. When the server can describe the variables (labels,
 * types, descriptions) the tabs render a friendly editor per variable; until then, or if that
 * request fails, a plain table of name / default / override is shown instead.
 */
export class BrandingSection implements ReactiveController, BrandingFieldContext {
  private _tabs: BrandingTab[] = [];
  private _metadata: BrandingMetadata | null = null;
  private _loading = false;
  private _error: string | null = null;

  desktopValues: Record<string, string> = {};
  mobileValues: Record<string, string> = {};
  mobileInherited: Record<string, boolean> = {};
  linkEditing: Record<string, boolean> = {};

  constructor(
    private readonly _host: ReactiveControllerHost & ContextHost & UmbElement,
    private readonly _context: BrandingContext
  ) {
    _host.addController(this);
  }

  hostConnected() {}

  get metadata() {
    return this._metadata;
  }

  /** The tabs the host should show, one per branding stylesheet. */
  get tabs(): ReadonlyArray<BrandingTab> {
    return this._tabs;
  }

  /** Takes up the tabs the host supplied, with the tenant's saved mobile overrides applied. */
  loadFrom(tabs: BrandingTab[] | undefined, tenant: any | undefined) {
    this._tabs = tabsWithMobileOverrides(tabs ?? [], tenant);
    this._host.requestUpdate();
  }

  // ── what the tenant saves ─────────────────────────────────────────────────

  collectOverrides(): Record<string, string> {
    return this._metadata ? nonEmptyOverrides(this.desktopValues) : staticTableOverrides(this._tabs, 'overrideValue');
  }

  collectMobileOverrides(): Record<string, string> {
    return this._metadata
      ? nonEmptyOverrides(this.mobileValues, name => !this.mobileInherited[name])
      : staticTableOverrides(this._tabs, 'mobileOverrideValue');
  }

  // ── BrandingFieldContext ──────────────────────────────────────────────────

  setValue(name: string, isMobile: boolean, value: string) {
    if (isMobile) {
      this.mobileValues = { ...this.mobileValues, [name]: value };
    } else {
      this.desktopValues = { ...this.desktopValues, [name]: value };
    }
    this._host.requestUpdate();
  }

  setMobileInherited(name: string, inherited: boolean) {
    this.mobileInherited = { ...this.mobileInherited, [name]: inherited };
    this._host.requestUpdate();
  }

  setLinkEditing(key: string, editing: boolean) {
    this.linkEditing = { ...this.linkEditing, [key]: editing };
    this._host.requestUpdate();
  }

  async pickMedia(name: string, isMobile: boolean) {
    const rawUrl = await pickMediaUrl(this._host);
    if (rawUrl) this.setValue(name, isMobile, `url('${rawUrl}')`);
  }

  // ── loading the variable descriptions ─────────────────────────────────────

  async fetchMetadata() {
    if (this._metadata || this._loading || this._error) {
      return;
    }

    this._loading = true;
    this._error = null;
    this._host.requestUpdate();

    try {
      const token = await bearerTokenWithin500ms(this._host);
      const response = await fetch('/umbraco/management/api/v1/prism/branding/metadata', { headers: authHeaders(token) });
      if (!response.ok) {
        throw new Error(`Failed to fetch branding metadata: ${response.status}`);
      }

      this._metadata = (await response.json()) as BrandingMetadata;
      this._seedValues(this._metadata);
    } catch (error) {
      console.error('Error fetching branding metadata:', error);
      this._error = error instanceof Error ? error.message : 'Unknown error';
    } finally {
      this._loading = false;
      this._host.requestUpdate();
    }
  }

  /**
   * Starts every variable at its saved override (or its current value). Annotation
   * (@prism section:/label:/description:) is an enhancement, never a requirement, for a variable to
   * be overridable — any CSS custom property Prism discovers is fair game, it just gets plainer
   * treatment without one — so the un-annotated variables the file scan found are seeded too, or
   * edits to them (rendered by the "Other variables" fallback) would never be collected and saved.
   */
  private _seedValues(metadata: BrandingMetadata) {
    const tenant = this._context.tenant();
    const saved = toOverrideMap(tenant?.brandingOverrides);
    const savedMobile = toOverrideMap(tenant?.mobileBrandingOverrides);
    const desktop: Record<string, string> = {};
    const mobile: Record<string, string> = {};
    const inherited: Record<string, boolean> = {};

    const seed = (name: string, fallback: string) => {
      desktop[name] = saved[name] ?? fallback;
      inherited[name] = !savedMobile[name];
      mobile[name] = savedMobile[name] ?? fallback;
    };

    const annotated = metadata.sections.flatMap(section => section.variables);
    annotated.forEach(variable => seed(variable.variable, variable.currentValue));

    const annotatedNames = new Set(annotated.map(variable => variable.variable));
    this._tabs
      .flatMap(tab => tab.variables)
      .filter(variable => !annotatedNames.has(variable.name))
      .forEach(variable => seed(variable.name, variable.defaultValue ?? ''));

    this.desktopValues = desktop;
    this.mobileValues = mobile;
    this.mobileInherited = inherited;
  }

  // ── static table (before/without metadata) ────────────────────────────────

  private _setStaticOverride(tabIndex: number, variableIndex: number, value: string) {
    this._patchStatic(tabIndex, variableIndex, { overrideValue: value });
  }

  private _setStaticMobileOverride(tabIndex: number, variableIndex: number, value: string) {
    this._patchStatic(tabIndex, variableIndex, { mobileOverrideValue: value });
  }

  private _patchStatic(tabIndex: number, variableIndex: number, change: { overrideValue?: string; mobileOverrideValue?: string }) {
    this._tabs = this._tabs.map((tab, index) =>
      index !== tabIndex ? tab : { ...tab, variables: tab.variables.map((variable, i) => (i === variableIndex ? { ...variable, ...change } : variable)) }
    );
    this._host.requestUpdate();
  }

  // ── render ────────────────────────────────────────────────────────────────

  /** The tab panel for the branding stylesheet at `tabIndex`. */
  render(tabIndex: number) {
    // Try to use dynamic branding metadata first
    if (this._metadata) {
      return this._renderDynamicTab(tabIndex);
    }

    if (this._loading) {
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

    if (this._error) {
      return html`
        <div
          role="tabpanel"
          id="branding-panel-${tabIndex}"
          aria-labelledby="branding-tab-${tabIndex}"
          class="tab-content">
          <uui-box>
            <p style="color: var(--uui-color-danger); margin-bottom: 1rem;">
              Failed to load dynamic branding configuration: ${this._error}
            </p>
            <p style="margin-bottom: 1rem;">Falling back to static fields:</p>
            ${this._renderStaticContent(tabIndex)}
          </uui-box>
        </div>
      `;
    }

    return this._renderStaticTab(tabIndex);
  }

  private _renderField(variable: BrandingVariable) {
    return new BrandingField(this, variable).render();
  }

    private _renderStaticContent(tabIndex: number) {
    const tab = this._tabs[tabIndex];
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
                @input=${(e: InputEvent) => this._setStaticOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
              </uui-input>
            </uui-table-cell>
            <uui-table-cell>
              <uui-input
                class="override-input"
                placeholder="e.g. #0d6efd"
                label="${variable.name} (mobile override)"
                .value=${variable.mobileOverrideValue ?? ''}
                @input=${(e: InputEvent) => this._setStaticMobileOverride(tabIndex, variableIndex, (e.target as HTMLInputElement).value)}>
              </uui-input>
            </uui-table-cell>
          </uui-table-row>
        `)}
      </uui-table>
    `;
  }

    private _renderStaticTab(tabIndex: number) {
    return html`
      <div
        role="tabpanel"
        id="branding-panel-${tabIndex}"
        aria-labelledby="branding-tab-${tabIndex}"
        class="tab-content">
        <uui-box>
          ${this._renderStaticContent(tabIndex)}
        </uui-box>
      </div>
    `;
  }

    private _renderDynamicTab(tabIndex: number) {
    if (!this._metadata) return html``;

    const tab = this._tabs[tabIndex];
    const tabVariables = tab?.variables ?? [];
    const tabVariableNames = new Set(tabVariables.map(v => v.name));

    const annotatedNames = new Set(
      this._metadata.sections.flatMap(section => section.variables.map(v => v.variable))
    );

    const sectionsToShow = this._metadata.sections
      .map(section => ({
        ...section,
        variables: section.variables.filter(v => tabVariableNames.has(v.variable))
      }))
      .filter(section => section.variables.length > 0);

    // Every CSS custom property Prism finds is overridable here, annotated or not — an
    // @prism annotation only ever adds a friendlier label/description/picker, it's never a
    // requirement. A variable this tab declares that carries no annotation still needs to
    // show up and be genuinely editable, not disappear or fall back to showing every other
    // tab's fully-annotated content (which is confusing and unrelated to this file).
    const plainVariables = tabVariables.filter(v => !annotatedNames.has(v.name));

    return html`
      <div role="tabpanel" id="branding-panel-${tabIndex}" aria-labelledby="branding-tab-${tabIndex}" class="tab-content">
        ${sectionsToShow.map(section => html`
          <uui-box headline="${section.name}" style="margin-bottom: 1.5rem;">
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem;">
              ${section.variables.map(variable => this._renderField(variable))}
            </div>
          </uui-box>
        `)}
        ${plainVariables.length > 0 ? html`
          <uui-box headline="${sectionsToShow.length > 0 ? 'Other variables' : 'Variables'}" style="margin-bottom: 1.5rem;">
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1.5rem;">
              ${plainVariables.map(variable => this._renderField({
                variable: variable.name,
                label: variable.name,
                description: '',
                type: 'text',
                syntax: '*',
                currentValue: variable.defaultValue ?? ''
              }))}
            </div>
          </uui-box>
        ` : ''}
      </div>
    `;
  }
}
