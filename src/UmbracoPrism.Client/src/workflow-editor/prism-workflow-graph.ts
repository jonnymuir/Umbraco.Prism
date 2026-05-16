import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { AuthoredWorkflow, AuthoredStage } from './types.js';

export type GraphMode = 'graph' | 'linear';

/**
 * Workflow graph canvas skeleton.
 *
 * Two view modes:
 *  - graph  — CSS-grid placeholder (V1); full SVG layout is V2.
 *  - linear — accessible ordered list of stage cards; primary AT surface.
 *
 * Emits: stage-selected CustomEvent<{ stageKey: string }>
 * Test hooks: data-prism-component, data-prism-mode, data-prism-stage
 */
@customElement('prism-workflow-graph')
export class PrismWorkflowGraphElement extends LitElement {
  @property({ attribute: false })
  workflow: AuthoredWorkflow | null = null;

  /** Controls the active view mode. Can be set externally (e.g. by the editor host title bar). */
  @property({ type: String })
  mode: GraphMode = 'graph';

  @state()
  private _selectedStageKey: string | null = null;

  @state()
  private _focusedIndex = 0;

  private _toggleMode() {
    this.mode = this.mode === 'graph' ? 'linear' : 'graph';
    this._focusedIndex = 0;
  }

  private _selectStage(stageKey: string) {
    this._selectedStageKey = stageKey;
    this.dispatchEvent(
      new CustomEvent<{ stageKey: string }>('stage-selected', {
        detail: { stageKey },
        bubbles: true,
        composed: true,
      })
    );
    this._announce(`Stage "${stageKey}" selected`);
  }

  private _announce(message: string) {
    const announcer = this.shadowRoot?.getElementById('graph-announcer');
    if (announcer) {
      announcer.textContent = '';
      requestAnimationFrame(() => {
        announcer.textContent = message;
      });
    }
  }

  private _handleGraphNodeKeydown(e: KeyboardEvent, stageKey: string) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      this._selectStage(stageKey);
    }
  }

  private _handleListKeydown(e: KeyboardEvent, index: number) {
    const stages = this.workflow?.stages ?? [];
    if (stages.length === 0) return;

    let next = index;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      next = Math.min(index + 1, stages.length - 1);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      next = Math.max(index - 1, 0);
    } else if (e.key === 'Home') {
      e.preventDefault();
      next = 0;
    } else if (e.key === 'End') {
      e.preventDefault();
      next = stages.length - 1;
    } else if (e.key === 'Enter') {
      e.preventDefault();
      this._selectStage(stages[index].stageKey);
      return;
    } else {
      return;
    }

    this._focusedIndex = next;
    const items = this.shadowRoot?.querySelectorAll<HTMLElement>('[role="option"]');
    items?.[next]?.focus();
  }

  private _renderGraph(stages: AuthoredStage[]) {
    return html`
      <div
        class="graph-canvas"
        role="application"
        tabindex="0"
        aria-label="Workflow graph canvas — ${this.workflow?.displayName ?? 'workflow'}"
        aria-roledescription="Visual workflow graph. Activate a node to select a stage."
      >
        ${stages.length === 0
          ? html`<p class="empty-state">No stages to display. Add stages to see the graph.</p>`
          : html`
              <div class="graph-grid">
                ${stages.map((stage, i) => {
                  const labelId = `node-label-${stage.stageKey}`;
                  const descId = `node-desc-${stage.stageKey}`;
                  return html`
                    <div
                      class="stage-node stage-kind-${stage.kind.toLowerCase()} ${this._selectedStageKey === stage.stageKey ? 'selected' : ''}"
                      role="button"
                      tabindex="${i === 0 ? '0' : '-1'}"
                      aria-labelledby="${labelId}"
                      aria-describedby="${descId}"
                      aria-pressed="${this._selectedStageKey === stage.stageKey}"
                      data-prism-stage="${stage.stageKey}"
                      @click=${() => this._selectStage(stage.stageKey)}
                      @keydown=${(e: KeyboardEvent) => this._handleGraphNodeKeydown(e, stage.stageKey)}
                    >
                      <span id="${labelId}" class="node-label">${stage.displayName}</span>
                      <span class="node-kind">${stage.kind}</span>
                      <span id="${descId}" class="sr-only">
                        ${stage.kind} stage.
                        ${stage.exits.length > 0
                          ? `Transitions: ${stage.exits.map(e => e.action).join(', ')}.`
                          : 'No outgoing transitions.'}
                      </span>
                    </div>
                  `;
                })}
              </div>
            `}
      </div>
    `;
  }

  private _renderLinear(stages: AuthoredStage[]) {
    return html`
      <section aria-label="Workflow stages — linear list" tabindex="0">
        ${stages.length === 0
          ? html`<p class="empty-state">No stages to display.</p>`
          : html`
              <ol
                class="stage-list"
                role="listbox"
                aria-label="Workflow stages for ${this.workflow?.displayName ?? 'workflow'}"
                aria-multiselectable="false"
              >
                ${stages.map((stage, i) => html`
                  <li
                    class="stage-card ${this._selectedStageKey === stage.stageKey ? 'selected' : ''}"
                    role="option"
                    tabindex="${i === this._focusedIndex ? '0' : '-1'}"
                    aria-selected="${this._selectedStageKey === stage.stageKey}"
                    data-prism-stage="${stage.stageKey}"
                    @click=${() => { this._focusedIndex = i; this._selectStage(stage.stageKey); }}
                    @keydown=${(e: KeyboardEvent) => this._handleListKeydown(e, i)}
                  >
                    <div class="card-header">
                      <span class="card-name">${stage.displayName}</span>
                      <span class="card-kind badge">${stage.kind}</span>
                    </div>
                    ${stage.exits.length > 0 ? html`
                      <div class="card-exits" aria-label="Transitions from this stage">
                        ${stage.exits.map(exit => html`
                          <span class="exit-tag">
                            <span aria-hidden="true">→</span>
                            <span>${exit.action}</span>
                          </span>
                        `)}
                      </div>
                    ` : nothing}
                  </li>
                `)}
              </ol>
            `}
      </section>
    `;
  }

  render() {
    const stages = this.workflow?.stages ?? [];
    const isLinear = this.mode === 'linear';

    return html`
      <div
        class="workflow-graph-root"
        data-prism-component="workflow-graph"
        data-prism-mode="${this.mode}"
      >
        <div class="toolbar">
          <span class="workflow-title">${this.workflow?.displayName ?? 'No workflow loaded'}</span>
          <button
            class="mode-toggle"
            aria-pressed="${isLinear}"
            style="${isLinear ? 'background-color:#1e3a8a;color:#ffffff;border-color:#1e3a8a;' : ''}"
            @click=${this._toggleMode}
            title="${isLinear ? 'Switch to graph view' : 'Switch to linear list view'}"
          >
            ${isLinear ? 'Graph view' : 'List view'}
          </button>
        </div>

        <div
          id="graph-announcer"
          role="status"
          aria-live="polite"
          aria-atomic="true"
          class="sr-only"
        ></div>

        ${isLinear ? this._renderLinear(stages) : this._renderGraph(stages)}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .workflow-graph-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--uui-color-surface, #f4f4f4);
      border: 1px solid var(--uui-color-border, #d1d5db);
      border-radius: var(--uui-border-radius, 6px);
      overflow: hidden;
    }

    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.75rem 1rem;
      background: var(--uui-color-surface-alt, #ffffff);
      border-bottom: 1px solid var(--uui-color-border, #d1d5db);
    }

    .workflow-title {
      font-size: 0.9375rem;
      font-weight: 600;
      color: var(--uui-color-text, #1f2937);
    }

    .mode-toggle {
      padding: 0.375rem 0.875rem;
      font-size: 0.875rem;
      font-weight: 500;
      color: #1f2937;
      background: #ffffff;
      border: 2px solid #6b7280;
      border-radius: 4px;
      cursor: pointer;
      transition: background-color 0.15s ease;
    }

    .mode-toggle:hover {
      background: #f3f4f6;
    }

    .mode-toggle:focus-visible {
      outline: 3px solid #2563eb;
      outline-offset: 2px;
    }

    .mode-toggle[aria-pressed="true"] {
      background: #1e3a8a;
      color: #ffffff;
      border-color: #1e3a8a;
    }

    /* ── Graph mode ─────────────────────────────────── */

    .graph-canvas {
      flex: 1;
      padding: 1.5rem;
      overflow: auto;
    }

    .graph-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 1rem;
      min-height: 200px;
    }

    .stage-node {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      padding: 1rem;
      background: #ffffff;
      border: 2px solid #d1d5db;
      border-radius: 6px;
      cursor: pointer;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
      min-height: 80px;
    }

    .stage-node:hover {
      border-color: #4b5563;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
    }

    .stage-node:focus-visible {
      /* GDS focus style — 3px #0b0c0c with #ffdd00 offset */
      outline: 3px solid #0b0c0c;
      outline-offset: 2px;
      box-shadow: 0 0 0 5px #ffdd00;
    }

    .stage-node.selected {
      border-color: #1d4ed8;
      box-shadow: 0 0 0 2px rgba(29, 78, 216, 0.25);
    }

    .stage-node.stage-kind-waiting {
      border-style: dashed;
    }

    .stage-node.stage-kind-confirmation {
      border-radius: 12px;
      border-color: #16a34a;
    }

    .stage-node.stage-kind-decision {
      border-left: 4px solid #d97706;
    }

    .stage-node.stage-kind-backstage {
      background: #f9fafb;
      border-color: #767676;
    }

    .node-label {
      font-size: 0.875rem;
      font-weight: 600;
      color: #111827;
      line-height: 1.3;
    }

    .node-kind {
      font-size: 0.75rem;
      color: #4b5563;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    /* ── Linear mode ─────────────────────────────────── */

    section[aria-label] {
      flex: 1;
      padding: 1rem;
      overflow: auto;
    }

    .stage-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .stage-card {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 0.875rem 1rem;
      background: #ffffff;
      border: 2px solid #d1d5db;
      border-radius: 6px;
      cursor: pointer;
      transition: border-color 0.15s ease;
    }

    .stage-card:hover {
      border-color: #4b5563;
    }

    .stage-card:focus-visible {
      outline: 3px solid #2563eb;
      outline-offset: 2px;
    }

    .stage-card.selected,
    .stage-card[aria-selected="true"] {
      border-color: #1d4ed8;
      background: #eff6ff;
    }

    .card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }

    .card-name {
      font-size: 0.9375rem;
      font-weight: 600;
      color: #111827;
    }

    .badge {
      font-size: 0.6875rem;
      font-weight: 600;
      color: #374151;
      background: #e5e7eb;
      padding: 0.125rem 0.5rem;
      border-radius: 3px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      white-space: nowrap;
    }

    .card-exits {
      display: flex;
      flex-wrap: wrap;
      gap: 0.375rem;
    }

    .exit-tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.75rem;
      color: #4b5563;
      background: #f3f4f6;
      padding: 0.125rem 0.5rem;
      border-radius: 3px;
    }

    .empty-state {
      color: #4b5563;
      font-size: 0.875rem;
      text-align: center;
      padding: 2rem;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-graph': PrismWorkflowGraphElement;
  }
}
