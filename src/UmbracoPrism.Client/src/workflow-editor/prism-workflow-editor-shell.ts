import { LitElement, css, html } from 'lit';
import { keyed } from 'lit/directives/keyed.js';
import { live } from 'lit/directives/live.js';
import { customElement, property, state } from 'lit/decorators.js';
import type { WorkflowAuthoringSummary } from './workflow-authoring-client.js';
import {
  defaultAuthoringApiBase,
  listWorkflows,
  normaliseAuthoringApiBase,
} from './workflow-authoring-client.js';
import './prism-workflow-editor.js';

@customElement('prism-workflow-editor-shell')
export class PrismWorkflowEditorShellElement extends LitElement {
  @property({ type: String, attribute: 'workflow-key' })
  workflowKey = 'planning';

  @property({ type: String, attribute: 'authoring-api-base' })
  authoringApiBase = '';

  @state() private _draftWorkflowKey = 'planning';
  @state() private _draftApiBase = '';
  @state() private _workflowOptions: WorkflowAuthoringSummary[] = [];
  @state() private _loadingOptions = false;
  @state() private _optionsError: string | null = null;
  @state() private _requestedWorkflowMissing = false;

  connectedCallback(): void {
    super.connectedCallback();

    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('workflow');
      const apiParam = params.get('api');
      if (keyParam) {
        this.workflowKey = keyParam;
      }
      if (apiParam) {
        this.authoringApiBase = apiParam;
      }
    }

    this._draftWorkflowKey = this.workflowKey;
    this._draftApiBase = this._resolvedAuthoringApiBase;
    void this._loadWorkflowOptions();
  }

  private get _resolvedAuthoringApiBase(): string {
    return normaliseAuthoringApiBase(this.authoringApiBase || defaultAuthoringApiBase());
  }

  private async _loadWorkflowOptions(): Promise<void> {
    this._loadingOptions = true;
    this._optionsError = null;
    this._requestedWorkflowMissing = false;

    try {
      const options = await listWorkflows(this._resolvedAuthoringApiBase);
      this._workflowOptions = options;
      const requestedWorkflowAvailable = options.some(option => option.workflowKey === this.workflowKey);
      this._requestedWorkflowMissing = options.length > 0 && !requestedWorkflowAvailable;

      if (requestedWorkflowAvailable) {
        this._draftWorkflowKey = this.workflowKey;
      } else if (!this._draftWorkflowKey.trim() && options.length > 0) {
        this._draftWorkflowKey = options[0].workflowKey;
      }
    } catch (error) {
      this._workflowOptions = [];
      this._optionsError = error instanceof Error ? error.message : String(error);
    } finally {
      this._loadingOptions = false;
    }
  }

  private async _handleLaunch(event: Event): Promise<void> {
    event.preventDefault();

    this.workflowKey = this._draftWorkflowKey.trim() || this.workflowKey;
    this.authoringApiBase = this._draftApiBase.trim();
    this._syncUrl();
    await this._loadWorkflowOptions();
  }

  private _syncUrl(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set('workflow', this.workflowKey);
    url.searchParams.set('api', this._resolvedAuthoringApiBase);
    window.history.replaceState({}, '', url);
  }

  private _renderWorkflowOptions() {
    if (this._workflowOptions.length === 0) {
      return html`
        <option value="${this._draftWorkflowKey}" ?selected="${true}">
          ${this._draftWorkflowKey}
        </option>
      `;
    }

    const requestedOption = this._requestedWorkflowMissing
      ? html`
          <option value="${this._draftWorkflowKey}" ?selected="${true}">
            ${this._draftWorkflowKey} (requested URL — not available from this authoring API)
          </option>
        `
      : null;

    return [
      requestedOption,
      ...this._workflowOptions.map(
        option => html`
          <option value="${option.workflowKey}" ?selected="${option.workflowKey === this._draftWorkflowKey}">
            ${option.displayName} (${option.workflowKey}${option.definitionKey !== option.workflowKey ? ` → ${option.definitionKey}` : ''})
          </option>
        `
      )
    ];
  }

  private _renderSnippet() {
    return `<prism-workflow-editor
  workflow-key="${this.workflowKey}"
  authoring-api-base="${this._resolvedAuthoringApiBase}">
</prism-workflow-editor>`;
  }

  render() {
    const editorIdentity = `${this.workflowKey}|${this._resolvedAuthoringApiBase}`;

    return html`
      <a class="skip-link" href="#workflow-editor-reference-main">Skip to editor</a>

      <div
        class="shell"
        data-prism-component="workflow-editor-shell"
        data-prism-active-workflow="${this.workflowKey}"
      >
        <header class="hero">
          <div class="hero-copy">
            <p class="eyebrow">Reference editor host</p>
            <h1>Compose the editor into your app with one element and one API base.</h1>
            <p class="intro">
              This shell stays focused on authoring: workflow selection, editor mounting, and
              authoring API wiring. Runtime cases, approvals, and business processing still belong
              to your application.
            </p>
          </div>

          <section class="launch-card" aria-labelledby="workflow-host-config-title">
            <h2 id="workflow-host-config-title">Launch the editor</h2>
            <form @submit="${this._handleLaunch}">
              <label for="workflow-key">Workflow definition</label>
              <select
                id="workflow-key"
                .value="${live(this._draftWorkflowKey)}"
                @change="${(event: Event) => {
                  this._draftWorkflowKey = (event.target as HTMLSelectElement).value;
                }}"
              >
                ${this._renderWorkflowOptions()}
              </select>

              <label for="authoring-api-base">Authoring API base</label>
              <input
                id="authoring-api-base"
                type="url"
                inputmode="url"
                .value="${this._draftApiBase}"
                @input="${(event: Event) => {
                  this._draftApiBase = (event.target as HTMLInputElement).value;
                }}"
              />

              <button class="launch-button" type="submit">Open workflow</button>
            </form>

            ${this._loadingOptions
              ? html`<p class="meta" role="status">Loading available workflows…</p>`
              : html`<p class="meta">
                  ${this._workflowOptions.length > 0
                    ? `${this._workflowOptions.length} workflow definition${this._workflowOptions.length === 1 ? '' : 's'} discovered.`
                    : 'Manual mode — type any workflow key that your authoring API serves.'}
                </p>`}

            ${this._optionsError
              ? html`
                  <p class="inline-error" role="alert">
                    Could not query the authoring API: ${this._optionsError}
                  </p>
                `
              : null}
            ${this._requestedWorkflowMissing
              ? html`
                  <p class="inline-warning">
                    The current URL requests <code>${this.workflowKey}</code>, but this authoring API
                    does not list it. The shell is staying on that key instead of switching workflows.
                  </p>
                `
              : null}
          </section>
        </header>

        <main id="workflow-editor-reference-main" class="content">
          <section class="editor-stage" aria-labelledby="editor-stage-title">
            <div class="section-heading">
              <div>
                <p class="section-kicker">Mounted editor</p>
                <h2 id="editor-stage-title">${this.workflowKey}</h2>
              </div>
              <p class="section-note">Connected to ${this._resolvedAuthoringApiBase}</p>
            </div>

            <div class="editor-frame">
              ${keyed(
                editorIdentity,
                html`
                  <prism-workflow-editor
                    workflow-key="${this.workflowKey}"
                    authoring-api-base="${this._resolvedAuthoringApiBase}"
                    approver-name="reference-shell"
                  ></prism-workflow-editor>
                `
              )}
            </div>
          </section>

          <aside class="integration-rail" aria-labelledby="integration-rail-title">
            <h2 id="integration-rail-title">Why this host stays thin</h2>
            <ol class="pattern-list">
              <li>Pick a workflow key.</li>
              <li>Point the shell at your authoring API.</li>
              <li>Let your business app own runtime workflows and domain actions.</li>
            </ol>

            <div class="snippet-card">
              <h3>Integration snippet</h3>
              <pre><code>${this._renderSnippet()}</code></pre>
            </div>
          </aside>
        </main>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      min-height: 100vh;
      color: #0b0c0c;
      background: #f3f2f1;
      font-family: "GDS Transport", arial, sans-serif;
    }

    * {
      box-sizing: border-box;
    }

    .skip-link {
      position: absolute;
      left: 1rem;
      top: -3rem;
      z-index: 10;
      background: #0b0c0c;
      color: #fff;
      padding: 0.75rem 1rem;
      text-decoration: none;
    }

    .skip-link:focus {
      top: 1rem;
    }

    .shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }

    .hero {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(280px, 420px);
      gap: 1.5rem;
      padding: 2rem;
      background: linear-gradient(135deg, #1d70b8 0%, #003078 100%);
      color: #fff;
    }

    .eyebrow,
    .section-kicker {
      margin: 0 0 0.5rem;
      font-size: 0.875rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }

    h1,
    h2,
    h3,
    p {
      margin-top: 0;
    }

    h1 {
      margin-bottom: 1rem;
      font-size: clamp(2rem, 4vw, 3rem);
      line-height: 1.1;
    }

    .intro {
      max-width: 58rem;
      margin-bottom: 0;
      font-size: 1.125rem;
      line-height: 1.6;
    }

    .launch-card,
    .integration-rail {
      background: #fff;
      color: #0b0c0c;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 8px 24px rgba(11, 12, 12, 0.12);
    }

    .launch-card form {
      display: grid;
      gap: 0.75rem;
    }

    label {
      font-weight: 700;
    }

    select,
    input {
      width: 100%;
      min-height: 2.75rem;
      padding: 0.625rem 0.75rem;
      border: 2px solid #505a5f;
      border-radius: 6px;
      font: inherit;
    }

    select:focus-visible,
    input:focus-visible,
    .launch-button:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .launch-button {
      min-height: 2.75rem;
      padding: 0.75rem 1rem;
      border: 0;
      border-radius: 999px;
      background: #00703c;
      color: #fff;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }

    .launch-button:hover {
      background: #005a30;
    }

    .meta,
    .section-note {
      color: #505a5f;
      font-size: 0.95rem;
    }

    .inline-error {
      margin-bottom: 0;
      padding: 0.75rem 1rem;
      border-left: 4px solid #d4351c;
      background: #fce8e6;
      color: #d4351c;
    }

    .inline-warning {
      margin-bottom: 0;
      padding: 0.75rem 1rem;
      border-left: 4px solid #1d70b8;
      background: #e8f1fb;
      color: #003078;
    }

    .content {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(280px, 360px);
      gap: 1.5rem;
      padding: 1.5rem 2rem 2rem;
      min-height: 0;
      flex: 1;
    }

    .editor-stage {
      min-height: 0;
    }

    .section-heading {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: end;
      margin-bottom: 1rem;
    }

    .editor-frame {
      min-height: 70vh;
      overflow: hidden;
      border: 1px solid #b1b4b6;
      border-radius: 16px;
      background: #fff;
      box-shadow: 0 8px 24px rgba(11, 12, 12, 0.08);
    }

    prism-workflow-editor {
      display: block;
      height: 70vh;
      min-height: 42rem;
    }

    .pattern-list {
      margin: 0 0 1.5rem;
      padding-left: 1.25rem;
      line-height: 1.6;
    }

    .snippet-card {
      padding: 1rem;
      border-radius: 10px;
      background: #f8f8f8;
    }

    pre {
      margin: 0;
      overflow-x: auto;
      white-space: pre-wrap;
      word-break: break-word;
      font-family: ui-monospace, SFMono-Regular, SF Mono, Consolas, monospace;
      font-size: 0.9rem;
      line-height: 1.5;
    }

    @media (max-width: 1100px) {
      .hero,
      .content {
        grid-template-columns: 1fr;
      }

      .section-heading {
        flex-direction: column;
        align-items: start;
      }
    }

    @media (max-width: 720px) {
      .hero,
      .content {
        padding: 1rem;
      }

      .editor-frame,
      prism-workflow-editor {
        min-height: 32rem;
        height: 32rem;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-editor-shell': PrismWorkflowEditorShellElement;
  }
}
