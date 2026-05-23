import { LitElement, html, css } from 'lit';
import { customElement } from 'lit/decorators.js';
import { WORKFLOW_SHORTCUT_GROUPS } from './workflow-shortcuts.js';

@customElement('prism-help-panel')
export class PrismHelpPanel extends LitElement {
  render() {
    return html`
      <div class="help-root">
        <div class="help-header">
          <h2 class="help-title">Workflow editor help</h2>
          <p class="help-intro">
            Quick reference for keyboard shortcuts and common authoring tasks.
          </p>
        </div>

        <section class="help-section">
          <h3 class="help-section-title">Keyboard shortcuts</h3>
          
          ${WORKFLOW_SHORTCUT_GROUPS.map(group => html`
            <div class="shortcut-group">
              <h4 class="shortcut-group-title">${group.title}</h4>
              <dl class="shortcut-list">
                ${group.shortcuts.map(shortcut => html`
                  <div class="shortcut-item">
                    <dt class="shortcut-command">${shortcut.command}</dt>
                    <dd class="shortcut-keys">${shortcut.labels.join(' + ')}</dd>
                  </div>
                `)}
              </dl>
            </div>
          `)}
        </section>

        <section class="help-section">
          <h3 class="help-section-title">Quick tips</h3>
          <ul class="tip-list">
            <li>Use the <strong>Outline</strong> panel on the left to navigate between stages quickly.</li>
            <li>Switch between <strong>Graph view</strong> and <strong>List view</strong> for different editing perspectives.</li>
            <li><strong>Validation</strong> errors are shown inline and in this panel — fix critical errors before saving.</li>
            <li>Use <strong>Preview</strong> to see how a stage will look at runtime across different surfaces.</li>
            <li><strong>Simulation</strong> lets you walk through the workflow to test transitions and catch dead ends.</li>
            <li>All structural changes support <strong>Undo/Redo</strong> — experiment safely.</li>
          </ul>
        </section>

        <section class="help-section">
          <h3 class="help-section-title">Getting started</h3>
          <ol class="guide-list">
            <li>Create your first stage using the <strong>+ Add stage</strong> button in the graph.</li>
            <li>Select the stage and use the <strong>Inspector</strong> panel (right side) to edit its details.</li>
            <li>Add transitions between stages to define the workflow path.</li>
            <li>Configure actions on stages to specify what happens at each step.</li>
            <li>Use <strong>Validation</strong> to check for issues before saving.</li>
            <li>Save your workflow when ready — changes will be published to the runtime.</li>
          </ol>
        </section>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      height: 100%;
      overflow-y: auto;
      font-family: "GDS Transport", arial, sans-serif;
    }

    .help-root {
      padding: 1.5rem;
      max-width: 64rem;
    }

    .help-header {
      margin-bottom: 2rem;
      padding-bottom: 1rem;
      border-bottom: 2px solid #b1b4b6;
    }

    .help-title {
      margin: 0 0 0.5rem;
      font-size: 1.5rem;
      font-weight: 700;
      color: #0b0c0c;
      line-height: 1.25;
    }

    .help-intro {
      margin: 0;
      font-size: 1rem;
      color: #505a5f;
      line-height: 1.5;
    }

    .help-section {
      margin-bottom: 2rem;
    }

    .help-section-title {
      margin: 0 0 1rem;
      font-size: 1.25rem;
      font-weight: 700;
      color: #0b0c0c;
      line-height: 1.3;
    }

    .shortcut-group {
      margin-bottom: 1.5rem;
      padding: 1rem;
      border: 1px solid #d8dde3;
      border-radius: 8px;
      background: #f8f8f8;
    }

    .shortcut-group-title {
      margin: 0 0 0.875rem;
      font-size: 1rem;
      font-weight: 700;
      color: #0b0c0c;
    }

    .shortcut-list {
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
    }

    .shortcut-item {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(auto, 1fr);
      gap: 1rem;
      align-items: center;
      padding: 0.75rem;
      border-radius: 6px;
      background: #ffffff;
      border: 1px solid #e5e7eb;
    }

    .shortcut-command {
      margin: 0;
      font-weight: 600;
      color: #0b0c0c;
      font-size: 0.9375rem;
    }

    .shortcut-keys {
      margin: 0;
      text-align: right;
      font-size: 0.875rem;
      font-weight: 600;
      color: #505a5f;
      font-family: ui-monospace, "SF Mono", "Monaco", "Cascadia Mono", "Consolas", monospace;
    }

    .tip-list,
    .guide-list {
      margin: 0;
      padding-left: 1.5rem;
      color: #0b0c0c;
      line-height: 1.6;
    }

    .tip-list li,
    .guide-list li {
      margin-bottom: 0.75rem;
      font-size: 0.9375rem;
    }

    .tip-list li:last-child,
    .guide-list li:last-child {
      margin-bottom: 0;
    }

    strong {
      font-weight: 700;
      color: #0b0c0c;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-help-panel': PrismHelpPanel;
  }
}
