import { LitElement, html, css, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { ProposalEnvelope, ProposalOp } from './types.js';

/**
 * Proposal diff dialog skeleton.
 *
 * Renders a ProposalEnvelope's rationale and ordered list of ops.
 * Accept / Reject buttons emit proposal-accept / proposal-reject CustomEvents.
 * Implements role="dialog" with aria-labelledby, aria-modal, and a focus trap.
 * Pressing Escape triggers rejection.
 *
 * Test hooks: data-prism-component, data-prism-op-index
 */
@customElement('prism-proposal-diff')
export class PrismProposalDiffElement extends LitElement {
  @property({ attribute: false })
  proposal: ProposalEnvelope | null = null;

  connectedCallback() {
    super.connectedCallback();
    this.addEventListener('keydown', this._handleHostKeydown.bind(this));
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.removeEventListener('keydown', this._handleHostKeydown.bind(this));
  }

  updated(changed: Map<string, unknown>) {
    if (changed.has('proposal') && this.proposal) {
      // Move focus to the accept button when a proposal arrives
      requestAnimationFrame(() => {
        this.shadowRoot?.querySelector<HTMLButtonElement>('.btn-accept')?.focus();
      });
    }
  }

  private _handleHostKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      e.stopPropagation();
      this._reject();
    }
  }

  private _handleDialogKeydown(e: KeyboardEvent) {
    if (e.key !== 'Tab') return;

    const focusable = Array.from(
      this.shadowRoot?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [tabindex="0"]:not([disabled])'
      ) ?? []
    );

    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = this.shadowRoot?.activeElement;

    if (e.shiftKey && active === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && active === last) {
      e.preventDefault();
      first.focus();
    }
  }

  private _accept() {
    this.dispatchEvent(
      new CustomEvent('proposal-accept', {
        detail: { proposalId: this.proposal?.id },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _reject() {
    this.dispatchEvent(
      new CustomEvent('proposal-reject', {
        detail: { proposalId: this.proposal?.id },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _renderOp(op: ProposalOp, index: number) {
    const opLabels: Record<ProposalOp['op'], string> = {
      'insert-stage': 'Insert stage',
      'remove-stage': 'Remove stage',
      'update-stage': 'Update stage',
      'insert-handoff': 'Insert handoff',
      'update-transition': 'Update transition',
    };

    return html`
      <li
        class="op-item op-${op.op}"
        data-prism-op-index="${index}"
        aria-label="Operation ${index + 1}: ${opLabels[op.op]} at ${op.path}"
      >
        <span class="op-badge" aria-hidden="true">${this._opSymbol(op.op)}</span>
        <div class="op-content">
          <span class="op-type">${opLabels[op.op]}</span>
          <code class="op-path">${op.path}</code>
          ${op.before ? html`<span class="op-placement">before <code>${op.before}</code></span>` : nothing}
          ${op.after ? html`<span class="op-placement">after <code>${op.after}</code></span>` : nothing}
        </div>
      </li>
    `;
  }

  private _opSymbol(op: ProposalOp['op']): string {
    switch (op) {
      case 'insert-stage':
      case 'insert-handoff':
        return '+';
      case 'remove-stage':
        return '−';
      case 'update-stage':
      case 'update-transition':
        return '~';
      default:
        return '·';
    }
  }

  render() {
    if (!this.proposal) return nothing;

    const { proposal } = this;
    const headingId = `diff-heading-${proposal.id.slice(0, 8)}`;

    return html`
      <div
        class="diff-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="${headingId}"
        data-prism-component="proposal-diff"
        @keydown="${this._handleDialogKeydown}"
      >
        <div class="diff-header">
          <h2 id="${headingId}" class="diff-title">Agent Proposal</h2>
          <div class="diff-meta">
            <span class="agent-badge">
              ${proposal.agent.kind} · ${proposal.agent.identity}
            </span>
            ${proposal.validationResult.status === 'pass' ? html`
              <span class="validation-badge pass" aria-label="Validation passed">✓ Valid</span>
            ` : proposal.validationResult.status === 'fail' ? html`
              <span class="validation-badge fail" role="alert" aria-label="Validation failed">✕ Invalid</span>
            ` : nothing}
          </div>
        </div>

        <div class="diff-body" tabindex="0">
          <!-- Rationale -->
          <section class="rationale-section" aria-labelledby="rationale-heading-${proposal.id.slice(0, 8)}">
            <h3 id="rationale-heading-${proposal.id.slice(0, 8)}" class="subsection-heading">
              Rationale
            </h3>
            <p class="rationale-text">${proposal.rationale}</p>
          </section>

          <!-- Ops list -->
          <section class="ops-section" aria-labelledby="ops-heading-${proposal.id.slice(0, 8)}">
            <h3 id="ops-heading-${proposal.id.slice(0, 8)}" class="subsection-heading">
              Changes (${proposal.ops.length})
            </h3>
            ${proposal.ops.length === 0
              ? html`<p class="ops-empty">No operations in this proposal.</p>`
              : html`
                  <ol class="ops-list" aria-label="Proposed changes">
                    ${proposal.ops.map((op, i) => this._renderOp(op, i))}
                  </ol>
                `}
          </section>

          <!-- Validation errors -->
          ${proposal.validationResult.status === 'fail' && proposal.validationResult.errors.length > 0 ? html`
            <section class="errors-section" aria-labelledby="errors-heading-${proposal.id.slice(0, 8)}" aria-live="polite">
              <h3 id="errors-heading-${proposal.id.slice(0, 8)}" class="subsection-heading error-heading">
                Validation errors
              </h3>
              <div role="alert" aria-label="Validation errors">
                <ul class="error-list">
                  ${proposal.validationResult.errors.map(err => html`<li>${err}</li>`)}
                </ul>
              </div>
            </section>
          ` : nothing}
        </div>

        <!-- Actions -->
        <div class="diff-actions">
          <button
            class="btn btn-accept"
            @click="${this._accept}"
            aria-label="Accept all proposed changes"
          >
            Accept all
          </button>
          <button
            class="btn btn-reject"
            @click="${this._reject}"
            aria-label="Reject all proposed changes"
          >
            Reject
          </button>
        </div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: var(--uui-font-family, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif);
    }

    .diff-dialog {
      display: flex;
      flex-direction: column;
      background: #ffffff;
      border: 2px solid #1d4ed8;
      border-radius: 8px;
      overflow: hidden;
    }

    /* Header */
    .diff-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.875rem 1rem 0.75rem;
      background: #eff6ff;
      border-bottom: 1px solid #bfdbfe;
    }

    .diff-title {
      margin: 0;
      font-size: 1rem;
      font-weight: 700;
      color: #1e3a8a;
    }

    .diff-meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.375rem;
    }

    .agent-badge {
      font-size: 0.6875rem;
      color: #1e40af;
      background: #dbeafe;
      padding: 0.125rem 0.5rem;
      border-radius: 3px;
      white-space: nowrap;
    }

    .validation-badge {
      font-size: 0.6875rem;
      font-weight: 700;
      padding: 0.125rem 0.5rem;
      border-radius: 3px;
      white-space: nowrap;
    }

    .validation-badge.pass {
      color: #166534;
      background: #dcfce7;
    }

    .validation-badge.fail {
      color: #991b1b;
      background: #fee2e2;
    }

    /* Body */
    .diff-body {
      flex: 1;
      overflow-y: auto;
      max-height: 360px;
      display: flex;
      flex-direction: column;
      gap: 0;
    }

    .rationale-section,
    .ops-section,
    .errors-section {
      padding: 0.875rem 1rem;
      border-bottom: 1px solid #f3f4f6;
    }

    .rationale-section:last-child,
    .ops-section:last-child,
    .errors-section:last-child {
      border-bottom: none;
    }

    .subsection-heading {
      margin: 0 0 0.5rem;
      font-size: 0.75rem;
      font-weight: 700;
      color: #374151;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .error-heading {
      color: #991b1b;
    }

    .rationale-text {
      margin: 0;
      font-size: 0.875rem;
      color: #111827;
      line-height: 1.6;
    }

    /* Ops list */
    .ops-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .ops-empty {
      margin: 0;
      font-size: 0.875rem;
      color: #595959;
    }

    .op-item {
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
      padding: 0.5rem 0.625rem;
      border-radius: 4px;
      font-size: 0.8125rem;
    }

    .op-item.op-insert-stage,
    .op-item.op-insert-handoff {
      background: #f0fdf4;
      border-left: 3px solid #16a34a;
    }

    .op-item.op-remove-stage {
      background: #fff1f2;
      border-left: 3px solid #e11d48;
    }

    .op-item.op-update-stage,
    .op-item.op-update-transition {
      background: #fefce8;
      border-left: 3px solid #ca8a04;
    }

    .op-badge {
      flex-shrink: 0;
      width: 1.25rem;
      height: 1.25rem;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 3px;
      font-size: 1rem;
      font-weight: 700;
      color: #374151;
    }

    .op-insert-stage .op-badge,
    .op-insert-handoff .op-badge {
      color: #166534;
    }

    .op-remove-stage .op-badge {
      color: #9f1239;
    }

    .op-update-stage .op-badge,
    .op-update-transition .op-badge {
      color: #92400e;
    }

    .op-content {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      min-width: 0;
    }

    .op-type {
      font-weight: 600;
      color: #111827;
    }

    .op-path {
      font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, monospace;
      font-size: 0.75rem;
      color: #374151;
      background: rgba(0, 0, 0, 0.05);
      padding: 0.0625rem 0.25rem;
      border-radius: 2px;
      word-break: break-all;
    }

    .op-placement {
      font-size: 0.75rem;
      color: #4b5563;
    }

    .op-placement code {
      font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, monospace;
      color: #374151;
    }

    /* Errors */
    .error-list {
      margin: 0;
      padding: 0 0 0 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.875rem;
      color: #991b1b;
    }

    /* Actions */
    .diff-actions {
      display: flex;
      gap: 0.625rem;
      padding: 0.75rem 1rem;
      border-top: 1px solid #e5e7eb;
      background: #f9fafb;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
      font-weight: 600;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      transition: opacity 0.15s ease, transform 0.1s ease;
    }

    .btn:focus-visible {
      outline: 3px solid #2563eb;
      outline-offset: 2px;
    }

    .btn:hover:not(:disabled) {
      opacity: 0.88;
    }

    .btn:active:not(:disabled) {
      transform: scale(0.98);
    }

    .btn-accept {
      background: #166534;
      color: #ffffff;
    }

    .btn-reject {
      background: #e5e7eb;
      color: #111827;
    }

    .btn-reject:hover:not(:disabled) {
      background: #d1d5db;
      opacity: 1;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-proposal-diff': PrismProposalDiffElement;
  }
}
