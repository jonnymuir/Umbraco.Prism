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
    this._draftApiBase = this._resolvedAuthoringApiBase;
    void this._loadWorkflowOptions();
  }

  private get _resolvedAuthoringApiBase(): string {
    return normaliseAuthoringApiBase(this.authoringApiBase || defaultAuthoringApiBase());
  }

  private async _loadWorkflowOptions(): Promise<void> {
    this._loadingOptions = true;
    this._optionsError = null;

    try {
      const options = await listWorkflows(this._resolvedAuthoringApiBase);
      this._workflowOptions = options;

      if (!this._draftWorkflowKey.trim() && options.length > 0) {
        this._draftWorkflowKey = options[0].workflowKey;
        this.workflowKey = this._draftWorkflowKey;
      }
    } catch (error) {
      this._workflowOptions = [];
      this._optionsError = error instanceof Error ? error.message : String(error);
    } finally {
      this._loadingOptions = false;
    }
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

  render() {
    const editorIdentity = `${this.workflowKey}|${this._resolvedAuthoringApiBase}`;

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
             : html`<p class="workflow-label">${this.workflowKey}</p>`}
          </div>
        </header>

        <main id="workflow-editor-reference-main" class="content">
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
