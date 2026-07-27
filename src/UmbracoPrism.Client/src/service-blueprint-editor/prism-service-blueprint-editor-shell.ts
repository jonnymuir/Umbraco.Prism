import { LitElement, css, html, nothing } from 'lit';
import { keyed } from 'lit/directives/keyed.js';
import { live } from 'lit/directives/live.js';
import { customElement, property, state } from 'lit/decorators.js';
import './prism-service-blueprint-editor.js';
import type { ServiceBlueprintSource, ServiceBlueprintSummary } from './service-blueprint-source.js';
import type { ServiceBlueprintActionCatalog } from './action-catalog.js';
import type { ServiceBlueprintAuthorContext } from './service-blueprint-author-context.js';
import type { QueueDefinition } from './stage-assignment.js';

@customElement('prism-service-blueprint-editor-shell')
export class PrismServiceBlueprintEditorShellElement extends LitElement {
  @property({ type: String, attribute: 'blueprint-key' })
  blueprintKey = 'planning';

  /**
   * Host-supplied source of authored serviceBlueprints. The shell lists serviceBlueprints
   * via `source.list()` and forwards the selected serviceBlueprint to
   * `<prism-service-blueprint-editor>`.
   */
  @property({ attribute: false })
  serviceBlueprintSource?: ServiceBlueprintSource;

  /** Optional host-supplied action catalog forwarded to the editor. */
  @property({ attribute: false })
  actionCatalog?: ServiceBlueprintActionCatalog;

  /** Optional host-supplied UX hints forwarded to the editor. */
  @property({ attribute: false })
  authorContext?: ServiceBlueprintAuthorContext;

  /** Optional host-supplied queue catalog forwarded to the editor. */
  @property({ attribute: false })
  availableQueues: QueueDefinition[] = [];

  @state() private _draftBlueprintKey = '';
  @state() private _serviceBlueprintOptions: ServiceBlueprintSummary[] = [];
  @state() private _sourceError: string | null = null;

  protected updated(changed: Map<string, unknown>): void {
    if (changed.has('blueprintKey')) {
      this._draftBlueprintKey = this.blueprintKey;
      this._syncUrlToServiceBlueprint();
    }
    if (changed.has('serviceBlueprintSource')) {
      void this._loadServiceBlueprintOptions();
    }
  }

  connectedCallback(): void {
    super.connectedCallback();

    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('serviceBlueprint');
      if (keyParam) {
        this.blueprintKey = keyParam;
      }
    }

    this._draftBlueprintKey = this.blueprintKey;
    void this._loadServiceBlueprintOptions();
  }

  private async _loadServiceBlueprintOptions(): Promise<void> {
    if (!this.serviceBlueprintSource) {
      this._serviceBlueprintOptions = [];
      this._sourceError = null;
      return;
    }

    try {
      const options = await this.serviceBlueprintSource.list();
      this._serviceBlueprintOptions = options;
      this._sourceError = null;
    } catch (error) {
      this._serviceBlueprintOptions = [];
      this._sourceError = error instanceof Error ? error.message : String(error);
    }
  }

  private _syncUrlToServiceBlueprint(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set('serviceBlueprint', this.blueprintKey);
    window.history.replaceState({}, '', url);
  }

  private _renderServiceBlueprintOptions() {
    if (this._serviceBlueprintOptions.length === 0) {
      return html`
        <option value="${this._draftBlueprintKey}" ?selected="${true}">
          ${this._draftBlueprintKey}
        </option>
      `;
    }

    return this._serviceBlueprintOptions.map(
      option => html`
        <option value="${option.blueprintKey}" ?selected="${option.blueprintKey === this._draftBlueprintKey}">
          ${option.displayName} (${option.blueprintKey}${option.definitionKey !== option.blueprintKey ? ` → ${option.definitionKey}` : ''})
        </option>
      `
    );
  }

  private _renderEditorOrPlaceholder() {
    if (!this.serviceBlueprintSource) {
      // Developer affordance — fail loudly when a host forgot to wire a source.
      // Storybook stories that drive `<prism-service-blueprint-editor>` directly via
      // `initialServiceBlueprint` should not be using the shell.
      return html`
        <div class="empty-state" role="status" data-prism-shell-empty="no-source">
          <h2>No serviceBlueprint source configured</h2>
          <p>
            Set <code>element.serviceBlueprintSource</code> on
            <code>&lt;prism-service-blueprint-editor-shell&gt;</code> to a
            <code>ServiceBlueprintSource</code> implementation. The in-memory reference
            implementation lives in <code>in-memory-service-blueprint-source.ts</code>.
          </p>
        </div>
      `;
    }

    if (this._sourceError) {
      return html`
        <div class="empty-state" role="alert" data-prism-shell-empty="source-error">
          <h2>Service Blueprint source unavailable</h2>
          <p>${this._sourceError}</p>
        </div>
      `;
    }

    return keyed(
      this.blueprintKey,
      html`
        <prism-service-blueprint-editor
          blueprint-key="${this.blueprintKey}"
          .serviceBlueprintSource=${this.serviceBlueprintSource}
          .actionCatalog=${this.actionCatalog}
          .authorContext=${this.authorContext}
          .availableQueues=${this.availableQueues}
        ></prism-service-blueprint-editor>
      `
    );
  }

  render() {
    return html`
      <a class="skip-link" href="#service-blueprint-editor-reference-main">Skip to editor</a>

      <div
        class="shell"
        data-prism-component="service-blueprint-editor-shell"
        data-prism-active-service-blueprint="${this.blueprintKey}"
      >
        <header class="topbar">
          <div class="topbar-content">
            <h1>Service Blueprint Editor</h1>
            ${this._serviceBlueprintOptions.length > 0
             ? html`
                 <select
                   class="service-blueprint-selector"
                   .value="${live(this._draftBlueprintKey)}"
                   @change="${(event: Event) => {
                     this._draftBlueprintKey = (event.target as HTMLSelectElement).value;
                     this.blueprintKey = this._draftBlueprintKey;
                   }}"
                   aria-label="Select service blueprint"
                 >
                   ${this._renderServiceBlueprintOptions()}
                 </select>
               `
             : this.serviceBlueprintSource
               ? html`<p class="service-blueprint-label">${this.blueprintKey}</p>`
               : nothing}
          </div>
        </header>

        <main id="service-blueprint-editor-reference-main" class="content">
          <div class="editor-frame">
            ${this._renderEditorOrPlaceholder()}
          </div>
        </main>
      </div>
    `;
  }

  static styles = css`
    /* Sizing is host-configurable via CSS custom properties — the standalone runtime-only
       host (MockBusinessApp, Storybook, the reference shell) legitimately owns the whole
       viewport, so the defaults below are unchanged for it. A host embedding this shell
       inside its own chrome (e.g. the Umbraco backoffice) overrides these instead of fighting
       a hardcoded 100vh/overflow:hidden that traps content below its own nav bars where
       neither the shell nor the outer page can reach it. */
    :host {
      display: block;
      height: var(--prism-service-blueprint-editor-height, 100vh);
      min-height: var(--prism-service-blueprint-editor-min-height, 100vh);
      overflow: var(--prism-service-blueprint-editor-overflow, hidden);
      color: #0b0c0c;
      background: #f3f2f1;
      font-family: "GDS Transport", arial, sans-serif;
    }

    * {
      box-sizing: border-box;
    }

    /* Hides itself via clipping to a 1px box, not via a negative offset relying on an
       ancestor's overflow:hidden to clip it — some hosts (e.g. the Umbraco backoffice)
       override --prism-service-blueprint-editor-overflow to "visible" to fix page scrolling, which
       would otherwise leave this rendered on-screen at all times instead of only on focus. */
    .skip-link {
      position: absolute;
      width: 1px;
      height: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      z-index: 10;
      background: #0b0c0c;
      color: #fff;
      text-decoration: none;
    }

    .skip-link:focus {
      left: 1rem;
      top: 1rem;
      width: auto;
      height: auto;
      margin: 0;
      padding: 0.75rem 1rem;
      overflow: visible;
      clip: auto;
      white-space: normal;
    }

    .shell {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: var(--prism-service-blueprint-editor-shell-min-height, 0);
      overflow: hidden;
    }

    .topbar {
      display: flex;
      align-items: center;
      padding: 1rem 2rem;
      background: #fff;
      border-bottom: 1px solid #b1b4b6;
      box-shadow: 0 2px 8px rgba(11, 12, 12, 0.08);
    }

    .topbar-content {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      width: 100%;
      max-width: 1400px;
      margin: 0 auto;
    }

    h1 {
      margin: 0;
      font-size: 1.25rem;
      line-height: 1.2;
      font-weight: 700;
    }

    .service-blueprint-selector {
      min-width: 250px;
      padding: 0.625rem 0.75rem;
      border: 2px solid #505a5f;
      border-radius: 6px;
      font: inherit;
      background: #fff;
      color: #0b0c0c;
      cursor: pointer;
    }

    .service-blueprint-selector:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .service-blueprint-label {
      margin: 0;
      font-size: 0.95rem;
      color: #505a5f;
    }

    .content {
      display: flex;
      flex-direction: column;
      flex: 1;
      padding: 1.5rem 2rem;
      min-height: 0;
      overflow: hidden;
    }

    .editor-frame {
      flex: 1;
      min-height: 0;
      border: 1px solid #b1b4b6;
      border-radius: 16px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(11, 12, 12, 0.08);
      overflow: hidden;
    }

    prism-service-blueprint-editor {
      display: block;
      height: 100%;
      width: 100%;
    }

    .empty-state {
      padding: 2rem;
      max-width: 60ch;
      margin: 2rem auto;
      color: #0b0c0c;
    }

    .empty-state h2 {
      margin-top: 0;
      font-size: 1.1rem;
    }

    .empty-state code {
      background: #f3f2f1;
      padding: 0.1rem 0.35rem;
      border-radius: 3px;
      font-size: 0.92em;
    }

    @media (max-width: 768px) {
      .topbar {
        padding: 0.75rem 1rem;
      }

      .topbar-content {
        flex-direction: column;
        align-items: start;
        gap: 0.75rem;
      }

      .service-blueprint-selector {
        width: 100%;
        min-width: auto;
      }

      .content {
        padding: 1rem;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-service-blueprint-editor-shell': PrismServiceBlueprintEditorShellElement;
  }
}
