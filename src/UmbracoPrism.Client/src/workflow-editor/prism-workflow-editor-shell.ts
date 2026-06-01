import { LitElement, css, html, nothing } from 'lit';
import { keyed } from 'lit/directives/keyed.js';
import { live } from 'lit/directives/live.js';
import { customElement, property, state } from 'lit/decorators.js';
import './prism-workflow-editor.js';
import type { WorkflowSource, WorkflowSummary } from './workflow-source.js';
import type { WorkflowActionCatalog } from './workflow-action-catalog.js';
import type { WorkflowAuthorContext } from './workflow-author-context.js';
import type { WorkflowQueueDefinition } from './workflow-stage-assignment.js';

@customElement('prism-workflow-editor-shell')
export class PrismWorkflowEditorShellElement extends LitElement {
  @property({ type: String, attribute: 'workflow-key' })
  workflowKey = 'planning';

  /**
   * Host-supplied source of authored workflows. The shell lists workflows
   * via `source.list()` and forwards the selected workflow to
   * `<prism-workflow-editor>`.
   */
  @property({ attribute: false })
  workflowSource?: WorkflowSource;

  /** Optional host-supplied action catalog forwarded to the editor. */
  @property({ attribute: false })
  actionCatalog?: WorkflowActionCatalog;

  /** Optional host-supplied UX hints forwarded to the editor. */
  @property({ attribute: false })
  authorContext?: WorkflowAuthorContext;

  /** Optional host-supplied queue catalog forwarded to the editor. */
  @property({ attribute: false })
  availableQueues: WorkflowQueueDefinition[] = [];

  @state() private _draftWorkflowKey = 'planning';
  @state() private _workflowOptions: WorkflowSummary[] = [];
  @state() private _sourceError: string | null = null;

  protected updated(changed: Map<string, unknown>): void {
    if (changed.has('workflowKey')) {
      this._draftWorkflowKey = this.workflowKey;
      this._syncUrlToWorkflow();
    }
    if (changed.has('workflowSource')) {
      void this._loadWorkflowOptions();
    }
  }

  connectedCallback(): void {
    super.connectedCallback();

    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('workflow');
      if (keyParam) {
        this.workflowKey = keyParam;
      }
    }

    this._draftWorkflowKey = this.workflowKey;
    void this._loadWorkflowOptions();
  }

  private async _loadWorkflowOptions(): Promise<void> {
    if (!this.workflowSource) {
      this._workflowOptions = [];
      this._sourceError = null;
      return;
    }

    try {
      const options = await this.workflowSource.list();
      this._workflowOptions = options;
      this._sourceError = null;

      if (!this._draftWorkflowKey.trim() && options.length > 0) {
        this._draftWorkflowKey = options[0].workflowKey;
        this.workflowKey = this._draftWorkflowKey;
      }
    } catch (error) {
      this._workflowOptions = [];
      this._sourceError = error instanceof Error ? error.message : String(error);
    }
  }

  private _syncUrlToWorkflow(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set('workflow', this.workflowKey);
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

    return this._workflowOptions.map(
      option => html`
        <option value="${option.workflowKey}" ?selected="${option.workflowKey === this._draftWorkflowKey}">
          ${option.displayName} (${option.workflowKey}${option.definitionKey !== option.workflowKey ? ` → ${option.definitionKey}` : ''})
        </option>
      `
    );
  }

  private _renderEditorOrPlaceholder() {
    if (!this.workflowSource) {
      // Developer affordance — fail loudly when a host forgot to wire a source.
      // Storybook stories that drive `<prism-workflow-editor>` directly via
      // `initialWorkflow` should not be using the shell.
      return html`
        <div class="empty-state" role="status" data-prism-shell-empty="no-source">
          <h2>No workflow source configured</h2>
          <p>
            Set <code>element.workflowSource</code> on
            <code>&lt;prism-workflow-editor-shell&gt;</code> to a
            <code>WorkflowSource</code> implementation. The in-memory reference
            implementation lives in <code>in-memory-workflow-source.ts</code>.
          </p>
        </div>
      `;
    }

    if (this._sourceError) {
      return html`
        <div class="empty-state" role="alert" data-prism-shell-empty="source-error">
          <h2>Workflow source unavailable</h2>
          <p>${this._sourceError}</p>
        </div>
      `;
    }

    return keyed(
      this.workflowKey,
      html`
        <prism-workflow-editor
          workflow-key="${this.workflowKey}"
          .workflowSource=${this.workflowSource}
          .actionCatalog=${this.actionCatalog}
          .authorContext=${this.authorContext}
          .availableQueues=${this.availableQueues}
        ></prism-workflow-editor>
      `
    );
  }

  render() {
    return html`
      <a class="skip-link" href="#workflow-editor-reference-main">Skip to editor</a>

      <div
        class="shell"
        data-prism-component="workflow-editor-shell"
        data-prism-active-workflow="${this.workflowKey}"
      >
        <header class="topbar">
          <div class="topbar-content">
            <h1>Workflow Editor</h1>
            ${this._workflowOptions.length > 0
             ? html`
                 <select
                   class="workflow-selector"
                   .value="${live(this._draftWorkflowKey)}"
                   @change="${(event: Event) => {
                     this._draftWorkflowKey = (event.target as HTMLSelectElement).value;
                     this.workflowKey = this._draftWorkflowKey;
                   }}"
                   aria-label="Select workflow"
                 >
                   ${this._renderWorkflowOptions()}
                 </select>
               `
             : this.workflowSource
               ? html`<p class="workflow-label">${this.workflowKey}</p>`
               : nothing}
          </div>
        </header>

        <main id="workflow-editor-reference-main" class="content">
          <div class="editor-frame">
            ${this._renderEditorOrPlaceholder()}
          </div>
        </main>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      height: 100vh;
      min-height: 100vh;
      overflow: hidden;
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
      height: 100%;
      min-height: 0;
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

    .workflow-selector {
      min-width: 250px;
      padding: 0.625rem 0.75rem;
      border: 2px solid #505a5f;
      border-radius: 6px;
      font: inherit;
      background: #fff;
      color: #0b0c0c;
      cursor: pointer;
    }

    .workflow-selector:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .workflow-label {
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

    prism-workflow-editor {
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

      .workflow-selector {
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
    'prism-workflow-editor-shell': PrismWorkflowEditorShellElement;
  }
}
