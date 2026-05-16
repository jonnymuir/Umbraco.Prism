import { LitElement, html, css, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { ProposalEnvelope } from './types.js';
import './prism-proposal-diff.js';

interface ConversationMessage {
  id: string;
  role: 'user' | 'agent';
  text: string;
  timestamp: string;
}

/**
 * Agent conversation surface skeleton.
 *
 * Scrollable ARIA live region for messages, textarea + submit button,
 * and a proposal diff area that appears when a `proposal` property is set.
 *
 * Emits: nl-request CustomEvent<{ text: string }>
 * Test hooks: data-prism-component, data-prism-conversation-input
 */
@customElement('prism-conversation-pane')
export class PrismConversationPaneElement extends LitElement {
  @property({ attribute: false })
  proposal: ProposalEnvelope | null = null;

  @state()
  private _messages: ConversationMessage[] = [];

  @state()
  private _inputValue = '';

  @state()
  private _busy = false;

  /**
   * Push an agent message into the conversation log from outside the component.
   * Also clears the busy state so the input is re-enabled.
   * Used by the editor host to surface API results and friendly error messages.
   */
  pushAgentMessage(text: string): void {
    this._busy = false;
    this._messages = [
      ...this._messages,
      {
        id: `msg-agent-${Date.now()}`,
        role: 'agent',
        text,
        timestamp: new Date().toISOString(),
      },
    ];
  }

  private _handleInput(e: Event) {
    this._inputValue = (e.target as HTMLTextAreaElement).value;
  }

  private _handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      this._handleSubmit();
    }
  }

  private _handleSubmit() {
    const text = this._inputValue.trim();
    if (!text || this._busy) return;

    this._messages = [
      ...this._messages,
      {
        id: `msg-${Date.now()}`,
        role: 'user',
        text,
        timestamp: new Date().toISOString(),
      },
    ];
    this._inputValue = '';
    this._busy = true;

    this.dispatchEvent(
      new CustomEvent<{ text: string }>('nl-request', {
        detail: { text },
        bubbles: true,
        composed: true,
      })
    );

    // Simulate agent acknowledgment (stub — no real API call)
    setTimeout(() => {
      this._messages = [
        ...this._messages,
        {
          id: `msg-ack-${Date.now()}`,
          role: 'agent',
          text: 'Request received. Processing…',
          timestamp: new Date().toISOString(),
        },
      ];
      this._busy = false;
    }, 600);
  }

  private _handleProposalAccept(e: Event) {
    e.stopPropagation();
    this._messages = [
      ...this._messages,
      {
        id: `msg-accept-${Date.now()}`,
        role: 'agent',
        text: 'Proposal accepted.',
        timestamp: new Date().toISOString(),
      },
    ];
  }

  private _handleProposalReject(e: Event) {
    e.stopPropagation();
    this._messages = [
      ...this._messages,
      {
        id: `msg-reject-${Date.now()}`,
        role: 'agent',
        text: 'Proposal rejected.',
        timestamp: new Date().toISOString(),
      },
    ];
  }

  render() {
    const canSubmit = this._inputValue.trim().length > 0 && !this._busy;

    return html`
      <div
        class="conversation-root"
        data-prism-component="conversation-pane"
      >
        <header class="pane-header">
          <h2 class="pane-title">
            <span aria-hidden="true">💬</span>
            <span>Conversation</span>
          </h2>
        </header>

        <!-- Scrollable message list — ARIA live region -->
        <div
          class="message-list"
          role="log"
          aria-label="Conversation messages"
          aria-live="polite"
          aria-atomic="false"
          aria-relevant="additions"
        >
          ${this._messages.length === 0 ? html`
            <p class="empty-conversation">
              Type a message to start a conversation with the workflow agent.
            </p>
          ` : this._messages.map(msg => html`
            <article
              class="message message-${msg.role}"
              aria-label="${msg.role === 'user' ? 'You' : 'Agent'}: ${msg.text}"
            >
              <span class="message-role" aria-hidden="true">
                ${msg.role === 'user' ? 'You' : 'Agent'}
              </span>
              <p class="message-text">${msg.text}</p>
            </article>
          `)}
          ${this._busy ? html`
            <div class="message message-agent loading" aria-live="polite" aria-busy="true">
              <span class="sr-only">Agent is responding…</span>
              <span class="dots" aria-hidden="true">···</span>
            </div>
          ` : nothing}
        </div>

        <!-- Proposal diff area -->
        ${this.proposal ? html`
          <div class="proposal-area" aria-label="Agent proposal">
            <prism-proposal-diff
              .proposal="${this.proposal}"
              @proposal-accept="${this._handleProposalAccept}"
              @proposal-reject="${this._handleProposalReject}"
            ></prism-proposal-diff>
          </div>
        ` : nothing}

        <!-- Input area -->
        <div class="input-area">
          <label for="conversation-input" class="sr-only">
            Type your message to the workflow agent
          </label>
          <textarea
            id="conversation-input"
            class="conversation-input"
            data-prism-conversation-input
            placeholder="Type a message… (Ctrl+Enter to send)"
            rows="3"
            .value="${this._inputValue}"
            ?disabled="${this._busy}"
            aria-disabled="${this._busy}"
            @input="${this._handleInput}"
            @keydown="${this._handleKeydown}"
          ></textarea>
          <button
            class="submit-btn"
            ?disabled="${!canSubmit}"
            aria-disabled="${!canSubmit}"
            @click="${this._handleSubmit}"
          >
            <span>Send</span>
            <span aria-hidden="true">↵</span>
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

    .conversation-root {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--uui-color-surface-alt, #ffffff);
      border: 1px solid var(--uui-color-border, #d1d5db);
      border-radius: var(--uui-border-radius, 6px);
      overflow: hidden;
    }

    .pane-header {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #e5e7eb;
      background: #f9fafb;
    }

    .pane-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      font-size: 0.9375rem;
      font-weight: 700;
      color: #111827;
    }

    /* Message list */
    .message-list {
      flex: 1;
      overflow-y: auto;
      padding: 0.875rem;
      display: flex;
      flex-direction: column;
      gap: 0.625rem;
    }

    .empty-conversation {
      margin: auto;
      text-align: center;
      color: #6b7280;
      font-size: 0.875rem;
      padding: 1.5rem;
    }

    .message {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      max-width: 85%;
    }

    .message-user {
      align-self: flex-end;
    }

    .message-agent {
      align-self: flex-start;
    }

    .message-role {
      font-size: 0.6875rem;
      font-weight: 700;
      color: #6b7280;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .message-user .message-role {
      text-align: right;
    }

    .message-text {
      margin: 0;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .message-user .message-text {
      background: #1d4ed8;
      color: #ffffff;
      border-bottom-right-radius: 2px;
    }

    .message-agent .message-text {
      background: #f3f4f6;
      color: #111827;
      border-bottom-left-radius: 2px;
    }

    .loading .dots {
      display: inline-block;
      padding: 0.5rem 0.75rem;
      background: #f3f4f6;
      border-radius: 6px;
      font-size: 1.25rem;
      letter-spacing: 0.25em;
      color: #6b7280;
    }

    /* Proposal area */
    .proposal-area {
      padding: 0 0.875rem 0.875rem;
      border-top: 1px solid #e5e7eb;
    }

    /* Input area */
    .input-area {
      display: flex;
      gap: 0.5rem;
      align-items: flex-end;
      padding: 0.75rem;
      border-top: 1px solid #e5e7eb;
      background: #f9fafb;
    }

    .conversation-input {
      flex: 1;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      font-family: inherit;
      color: #111827;
      background: #ffffff;
      border: 2px solid #d1d5db;
      border-radius: 6px;
      resize: none;
      line-height: 1.5;
      transition: border-color 0.15s ease;
    }

    .conversation-input:focus {
      outline: none;
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.15);
    }

    .conversation-input:disabled {
      background: #f3f4f6;
      color: #6b7280;
      cursor: not-allowed;
    }

    .conversation-input::placeholder {
      color: #9ca3af;
    }

    .submit-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.5rem 1rem;
      font-size: 0.875rem;
      font-weight: 600;
      color: #ffffff;
      background: #1d4ed8;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      transition: background-color 0.15s ease, opacity 0.15s ease;
      white-space: nowrap;
    }

    .submit-btn:hover:not(:disabled) {
      background: #1e40af;
    }

    .submit-btn:focus-visible {
      outline: 3px solid #2563eb;
      outline-offset: 2px;
    }

    .submit-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-conversation-pane': PrismConversationPaneElement;
  }
}
