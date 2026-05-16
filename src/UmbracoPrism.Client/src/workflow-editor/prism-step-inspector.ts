import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { AuthoredWorkflow, AuthoredStage } from './types.js';

/**
 * Step inspector skeleton — right-panel read-only summary of a selected stage.
 *
 * Heading hierarchy: one h2 (stage title), h3 for each subsection.
 * Editing affordances are V2; V1 renders read-only fields/exits/role gates.
 *
 * Test hooks: data-prism-component, data-prism-stage-detail
 */
@customElement('prism-step-inspector')
export class PrismStepInspectorElement extends LitElement {
  @property({ attribute: false })
  workflow: AuthoredWorkflow | null = null;

  @property({ type: String, attribute: 'selected-stage-key' })
  selectedStageKey: string | null = null;

  private get _selectedStage(): AuthoredStage | null {
    if (!this.workflow || !this.selectedStageKey) return null;
    return this.workflow.stages.find(s => s.stageKey === this.selectedStageKey) ?? null;
  }

  private _renderEmpty() {
    return html`
      <div class="empty-state" role="status">
        <p>Select a stage from the graph to inspect its properties.</p>
      </div>
    `;
  }

  private _renderStage(stage: AuthoredStage) {
    const fields = this.workflow?.fields.filter(f =>
      stage.views.some(v => v.fields.some(fRef => fRef.fieldKey === f.fieldKey))
    ) ?? [];

    const roleLabels = stage.roleGates.map(key => {
      const role = this.workflow?.roles.find(r => r.roleKey === key);
      return role?.displayName ?? key;
    });

    return html`
      <article
        class="inspector-panel"
        data-prism-stage-detail="${stage.stageKey}"
        aria-labelledby="inspector-stage-title"
      >
        <div class="inspector-header">
          <h2 id="inspector-stage-title" class="stage-title">${stage.displayName}</h2>
          <span class="stage-kind-badge">${stage.kind}</span>
        </div>

        <!-- Fields subsection -->
        <section class="inspector-section" aria-labelledby="section-fields-${stage.stageKey}">
          <h3 id="section-fields-${stage.stageKey}" class="section-heading">
            Fields
          </h3>
          ${fields.length === 0
            ? html`<p class="section-empty">No fields defined for this stage.</p>`
            : html`
                <ul class="field-list">
                  ${fields.map(f => html`
                    <li class="field-item">
                      <span class="field-label">${f.label}</span>
                      <span class="field-meta">${f.kind}${f.required ? ' · required' : ''}</span>
                    </li>
                  `)}
                </ul>
              `}
        </section>

        <!-- Role gating subsection -->
        <section class="inspector-section" aria-labelledby="section-roles-${stage.stageKey}">
          <h3 id="section-roles-${stage.stageKey}" class="section-heading">
            Role gating
          </h3>
          ${roleLabels.length === 0
            ? html`<p class="section-empty">Accessible by any authenticated user.</p>`
            : html`
                <ul class="role-list">
                  ${roleLabels.map(label => html`<li class="role-tag">${label}</li>`)}
                </ul>
              `}
        </section>

        <!-- Exits (transitions) subsection -->
        <section class="inspector-section" aria-labelledby="section-exits-${stage.stageKey}">
          <h3 id="section-exits-${stage.stageKey}" class="section-heading">
            Transitions (outgoing)
          </h3>
          ${stage.exits.length === 0
            ? html`<p class="section-empty">No outgoing transitions. This is a terminal stage.</p>`
            : html`
                <ul class="exit-list">
                  ${stage.exits.map(exit => html`
                    <li class="exit-item">
                      <span class="exit-action">${exit.action}</span>
                      <span aria-hidden="true" class="exit-arrow">→</span>
                      <span class="exit-target">${exit.toStageKey}</span>
                      ${exit.requiresRole
                        ? html`<span class="exit-role" title="Required role">(${exit.requiresRole})</span>`
                        : nothing}
                    </li>
                  `)}
                </ul>
              `}
        </section>

        <!-- Waiting metadata subsection (only for Waiting stages) -->
        ${stage.kind === 'Waiting' && stage.waiting ? html`
          <section class="inspector-section" aria-labelledby="section-waiting-${stage.stageKey}">
            <h3 id="section-waiting-${stage.stageKey}" class="section-heading">
              Waiting configuration
            </h3>
            <dl class="waiting-meta">
              ${stage.waiting.content ? html`
                <div class="meta-row">
                  <dt>Message</dt>
                  <dd>${stage.waiting.content}</dd>
                </div>
              ` : nothing}
              ${stage.waiting.expectedWaitSeconds ? html`
                <div class="meta-row">
                  <dt>Expected wait</dt>
                  <dd>${Math.round(stage.waiting.expectedWaitSeconds / 3600)} hours</dd>
                </div>
              ` : nothing}
              <div class="meta-row">
                <dt>Allow defer</dt>
                <dd>${stage.waiting.allowDefer ? 'Yes' : 'No'}</dd>
              </div>
            </dl>
          </section>
        ` : nothing}
      </article>
    `;
  }

  render() {
    const stage = this._selectedStage;
    return html`
      <div
        class="step-inspector-root"
        data-prism-component="step-inspector"
        tabindex="0"
      >
        ${stage ? this._renderStage(stage) : this._renderEmpty()}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
    }

    .step-inspector-root {
      height: 100%;
      overflow-y: auto;
      background: var(--uui-color-surface-alt, #ffffff);
      border: 1px solid var(--uui-color-border, #d1d5db);
      border-radius: var(--uui-border-radius, 6px);
    }

    .empty-state {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      min-height: 120px;
      padding: 2rem;
      color: #4b5563;
      font-size: 0.875rem;
      text-align: center;
    }

    .empty-state p {
      margin: 0;
    }

    .inspector-panel {
      display: flex;
      flex-direction: column;
      gap: 0;
    }

    .inspector-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 1rem 1.25rem 0.875rem;
      border-bottom: 1px solid #e5e7eb;
    }

    .stage-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #111827;
      line-height: 1.3;
    }

    .stage-kind-badge {
      flex-shrink: 0;
      font-size: 0.6875rem;
      font-weight: 600;
      color: #374151;
      background: #e5e7eb;
      padding: 0.125rem 0.5rem;
      border-radius: 3px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .inspector-section {
      padding: 0.875rem 1.25rem;
      border-bottom: 1px solid #f3f4f6;
    }

    .inspector-section:last-child {
      border-bottom: none;
    }

    .section-heading {
      margin: 0 0 0.625rem;
      font-size: 0.8125rem;
      font-weight: 700;
      color: #374151;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .section-empty {
      margin: 0;
      font-size: 0.875rem;
      color: #595959;
      font-style: italic;
    }

    /* Field list */
    .field-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .field-item {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.375rem 0.5rem;
      background: #f9fafb;
      border-radius: 4px;
    }

    .field-label {
      font-size: 0.875rem;
      font-weight: 500;
      color: #111827;
    }

    .field-meta {
      font-size: 0.75rem;
      color: #4b5563;
      white-space: nowrap;
    }

    /* Role list */
    .role-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-wrap: wrap;
      gap: 0.375rem;
    }

    .role-tag {
      font-size: 0.75rem;
      font-weight: 500;
      color: #1e40af;
      background: #dbeafe;
      padding: 0.25rem 0.625rem;
      border-radius: 3px;
    }

    /* Exit list */
    .exit-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .exit-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      color: #111827;
    }

    .exit-action {
      font-weight: 600;
      color: #1d4ed8;
    }

    .exit-arrow {
      color: #595959;
    }

    .exit-target {
      font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, monospace;
      font-size: 0.8125rem;
      color: #374151;
    }

    .exit-role {
      font-size: 0.75rem;
      color: #4b5563;
      font-style: italic;
    }

    /* Waiting metadata */
    .waiting-meta {
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .meta-row {
      display: flex;
      gap: 0.5rem;
      font-size: 0.875rem;
    }

    .meta-row dt {
      font-weight: 600;
      color: #374151;
      min-width: 120px;
    }

    .meta-row dd {
      margin: 0;
      color: #111827;
    }

    /* Focus indicators */
    :focus-visible {
      outline: 3px solid #2563eb;
      outline-offset: 2px;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-step-inspector': PrismStepInspectorElement;
  }
}
