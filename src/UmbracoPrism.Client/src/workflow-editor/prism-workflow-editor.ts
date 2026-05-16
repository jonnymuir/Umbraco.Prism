import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { AuthoredWorkflow, ProposalEnvelope } from './types.js';
import { fetchWorkflow, previewProposal, applyProposal } from './workflow-authoring-client.js';
import { draftProposal, V1_UNRECOGNISED_MESSAGE } from './workflow-authoring-mock-drafter.js';
import './prism-workflow-graph.js';
import './prism-step-inspector.js';
import './prism-conversation-pane.js';
import './prism-proposal-diff.js';
import type { PrismConversationPaneElement } from './prism-conversation-pane.js';

/**
 * Top-level editor host page composing the four V1 workflow editor components.
 *
 * Layout:
 *   Left  — prism-workflow-graph (with title bar + mode toggle)
 *   Right — prism-step-inspector (top) + prism-conversation-pane (bottom)
 *   Modal — prism-proposal-diff (overlay when a proposal is active)
 *
 * URL param: ?workflow=<key>  (default: "planning")
 * Prop: initialWorkflow — set directly for Storybook / offline use; skips API fetch.
 *
 * Test hooks:
 *   data-prism-component="workflow-editor"
 *   data-prism-workflow-loaded="{key}"
 *   data-prism-toast  (on the toast confirmation banner)
 */
@customElement('prism-workflow-editor')
export class PrismWorkflowEditorElement extends LitElement {
  /** Workflow key — read from ?workflow= URL param or set directly. */
  @property({ type: String, attribute: 'workflow-key' })
  workflowKey = 'planning';

  /**
   * If set, the component uses this workflow directly instead of fetching from
   * the API.  Designed for Storybook stories and offline walkthrough fixtures.
   */
  @property({ attribute: false })
  initialWorkflow: AuthoredWorkflow | null = null;

  @state() private _workflow: AuthoredWorkflow | null = null;
  @state() private _selectedStageKey: string | null = null;
  @state() private _proposal: ProposalEnvelope | null = null;
  @state() private _modalOpen = false;
  @state() private _toastMessage: string | null = null;
  @state() private _loading = false;
  @state() private _error: string | null = null;
  @state() private _graphMode: 'graph' | 'linear' = 'graph';

  connectedCallback() {
    super.connectedCallback();

    // Honour ?workflow= URL param when running as a standalone page
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const keyParam = params.get('workflow');
      if (keyParam) this.workflowKey = keyParam;
    }

    if (this.initialWorkflow) {
      this._workflow = this.initialWorkflow;
    } else {
      this._loadWorkflow();
    }
  }

  private async _loadWorkflow() {
    this._loading = true;
    this._error = null;
    try {
      this._workflow = await fetchWorkflow(this.workflowKey);
    } catch (err) {
      this._error = err instanceof Error ? err.message : String(err);
    } finally {
      this._loading = false;
    }
  }

  // ---------------------------------------------------------------------------
  // Event handlers
  // ---------------------------------------------------------------------------

  private _handleStageSelected(e: CustomEvent<{ stageKey: string }>) {
    this._selectedStageKey = e.detail.stageKey;
  }

  private async _handleNlRequest(e: CustomEvent<{ text: string }>) {
    const pane = this.shadowRoot?.querySelector<PrismConversationPaneElement>(
      'prism-conversation-pane'
    );
    if (!this._workflow) return;

    const localProposal = draftProposal(e.detail.text, this._workflow);

    if (!localProposal) {
      pane?.pushAgentMessage(V1_UNRECOGNISED_MESSAGE);
      return;
    }

    // Send to Blathers' preview endpoint; fall back to local proposal if API unavailable
    let proposal = localProposal;
    try {
      proposal = await previewProposal(this.workflowKey, localProposal);
    } catch {
      // Preview API not yet available — use locally drafted proposal for walkthrough
    }

    this._proposal = proposal;
    this._modalOpen = true;
    pane?.pushAgentMessage(
      `Proposal ready: "${proposal.rationale}" — review the diff to accept or reject.`
    );
  }

  private async _handleProposalAccept() {
    if (!this._proposal) return;
    try {
      await applyProposal(this.workflowKey, this._proposal);
    } catch {
      // Apply endpoint may not be live in V1 walkthrough — apply locally
      this._applyProposalLocally(this._proposal);
    }

    const pane = this.shadowRoot?.querySelector<PrismConversationPaneElement>(
      'prism-conversation-pane'
    );
    pane?.pushAgentMessage('Proposal accepted. Workflow updated.');

    this._closeModal();
    this._showToast('Workflow updated successfully.');

    // Re-fetch unless we are running with an injected fixture
    if (!this.initialWorkflow) {
      await this._loadWorkflow();
    }
  }

  private _applyProposalLocally(proposal: ProposalEnvelope) {
    if (!this._workflow) return;
    // V1: find insert-stage ops and splice them into the local workflow
    let stages = [...this._workflow.stages];
    for (const op of proposal.ops) {
      if (op.op === 'insert-stage' && op.value && op.before) {
        const idx = stages.findIndex(s => s.stageKey === op.before);
        const stage = op.value as typeof stages[number];
        if (idx >= 0) {
          stages = [...stages.slice(0, idx), stage, ...stages.slice(idx)];
        } else {
          stages = [...stages, stage];
        }
      }
    }
    this._workflow = { ...this._workflow, stages };
  }

  private _handleProposalReject() {
    const pane = this.shadowRoot?.querySelector<PrismConversationPaneElement>(
      'prism-conversation-pane'
    );
    pane?.pushAgentMessage('Proposal rejected.');
    this._closeModal();
  }

  private _closeModal() {
    this._modalOpen = false;
    this._proposal = null;
    // Return focus to conversation input after modal closes
    requestAnimationFrame(() => {
      this.shadowRoot
        ?.querySelector<HTMLElement>('[data-prism-conversation-input]')
        ?.focus();
    });
  }

  private _showToast(message: string) {
    this._toastMessage = message;
    setTimeout(() => {
      this._toastMessage = null;
    }, 5000);
  }

  private _toggleGraphMode() {
    this._graphMode = this._graphMode === 'graph' ? 'linear' : 'graph';
  }

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  render() {
    return html`
      <div
        data-prism-component="workflow-editor"
        data-prism-workflow-loaded="${this._workflow?.definitionKey ?? ''}"
        class="editor-root"
      >
        ${this._renderToast()}
        ${this._loading ? html`<div class="loading-banner" role="status">Loading workflow…</div>` : nothing}
        ${this._error ? html`<div class="error-banner" role="alert">${this._error}</div>` : nothing}

        <div class="editor-shell">
          <!-- Left: graph + title bar -->
          <div class="editor-left">
            <header class="editor-header">
              <h1 class="editor-title">
                ${this._workflow?.displayName ?? 'Workflow Editor'}
              </h1>
              <button
                class="mode-toggle-btn govuk-button govuk-button--secondary"
                @click="${this._toggleGraphMode}"
                aria-label="${this._graphMode === 'graph' ? 'Switch to list view' : 'Switch to graph view'}"
              >
                ${this._graphMode === 'graph' ? 'List view' : 'Graph view'}
              </button>
            </header>

            <prism-workflow-graph
              class="graph-panel"
              .workflow="${this._workflow}"
              .mode="${this._graphMode}"
              @stage-selected="${this._handleStageSelected}"
            ></prism-workflow-graph>
          </div>

          <!-- Right: inspector + conversation -->
          <div class="editor-right">
            <prism-step-inspector
              class="inspector-panel"
              .workflow="${this._workflow}"
              selected-stage-key="${this._selectedStageKey ?? ''}"
            ></prism-step-inspector>

            <prism-conversation-pane
              class="conversation-panel"
              @nl-request="${this._handleNlRequest}"
            ></prism-conversation-pane>
          </div>
        </div>

        <!-- Modal overlay for proposal diff -->
        ${this._modalOpen && this._proposal
          ? html`
              <div
                class="modal-backdrop"
                role="presentation"
                @click="${(e: MouseEvent) => {
                  if (e.target === e.currentTarget) this._handleProposalReject();
                }}"
              >
                <prism-proposal-diff
                  .proposal="${this._proposal}"
                  @proposal-accept="${this._handleProposalAccept}"
                  @proposal-reject="${this._handleProposalReject}"
                ></prism-proposal-diff>
              </div>
            `
          : nothing}
      </div>
    `;
  }

  private _renderToast() {
    if (!this._toastMessage) return nothing;
    return html`
      <div
        class="toast-banner"
        role="status"
        aria-live="assertive"
        data-prism-toast
      >
        ${this._toastMessage}
      </div>
    `;
  }

  // ---------------------------------------------------------------------------
  // Styles
  // ---------------------------------------------------------------------------

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
      font-family: "GDS Transport", arial, sans-serif;
      font-size: 1rem;
      color: #0b0c0c;
      background: #f3f2f1;
    }

    .editor-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      position: relative;
    }

    /* ---- Banners ---- */

    .loading-banner,
    .error-banner {
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
    }

    .loading-banner {
      background: #f0f4f9;
      color: #1d70b8;
    }

    .error-banner {
      background: #fce8e6;
      color: #d4351c;
    }

    .toast-banner {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 200;
      background: #00703c;
      color: #fff;
      padding: 0.75rem 1.25rem;
      border-radius: 4px;
      font-size: 1rem;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
    }

    /* ---- Shell ---- */

    .editor-shell {
      display: flex;
      flex: 1;
      overflow: hidden;
    }

    /* ---- Left panel ---- */

    .editor-left {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
      overflow: hidden;
    }

    .editor-header {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0.75rem 1rem;
      background: #1d70b8;
      color: #fff;
      flex-shrink: 0;
    }

    .editor-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      flex: 1;
    }

    .mode-toggle-btn {
      font-size: 0.875rem;
      padding: 0.4rem 0.75rem;
      background: #fff;
      color: #1d70b8;
      border: 2px solid #fff;
      border-radius: 4px;
      cursor: pointer;
      font-weight: 600;
      white-space: nowrap;
    }

    .mode-toggle-btn:hover {
      background: #e8f0fb;
    }

    .mode-toggle-btn:focus-visible {
      outline: 3px solid #ffdd00;
      outline-offset: 2px;
    }

    .graph-panel {
      flex: 1;
      overflow: hidden;
    }

    /* ---- Right panel ---- */

    .editor-right {
      width: 380px;
      flex-shrink: 0;
      display: flex;
      flex-direction: column;
      border-left: 2px solid #b1b4b6;
      background: #fff;
      overflow: hidden;
    }

    .inspector-panel {
      flex: 1;
      overflow-y: auto;
      min-height: 0;
    }

    .conversation-panel {
      flex: 1;
      min-height: 0;
      border-top: 2px solid #b1b4b6;
    }

    /* ---- Modal overlay ---- */

    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(11, 12, 12, 0.65);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
      padding: 1rem;
    }

    prism-proposal-diff {
      max-width: 720px;
      width: 100%;
      max-height: 90vh;
      overflow-y: auto;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-editor': PrismWorkflowEditorElement;
  }
}
