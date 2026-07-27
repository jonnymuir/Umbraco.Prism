import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { Diagnostic } from '@codemirror/lint';

/**
 * @internal Composition detail of <prism-service-blueprint-editor> — the JSON twin-pane
 * Definition tab editor. Loads CodeMirror 6 lazily on first connection so the
 * main editor bundle stays slim for authors who never open this tab.
 *
 * Inputs:
 *   value (property)        — the canonical JSON text to display.
 *   readOnly (attribute)    — disables editing.
 *   diagnostics (property)  — schema/lint issues from the host. Each rendered
 *                             as a CodeMirror gutter marker on its line.
 *
 * Output events:
 *   `definition-input` { value: string }  — fires after every user-visible
 *     change. The host owns debounce + parse + apply.
 */
@customElement('prism-definition-editor')
export class PrismDefinitionEditorElement extends LitElement {
  /** The canonical JSON text shown in the editor. */
  @property({ type: String })
  value = '';

  /** When true, the editor is read-only. */
  @property({ type: Boolean, attribute: 'read-only', reflect: true })
  readOnly = false;

  /** Inline diagnostics surfaced by the host (parse + schema/lint). */
  @property({ attribute: false })
  diagnostics: Array<{ line: number; severity: 'error' | 'warning'; message: string }> = [];

  @state() private _ready = false;
  @state() private _loadError: string | null = null;

  private _view: import('@codemirror/view').EditorView | null = null;
  private _modules: typeof import('./prism-definition-editor-codemirror.js') | null = null;
  private _suppressInputEvent = false;

  connectedCallback() {
    super.connectedCallback();
    void this._loadEditor();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._view?.destroy();
    this._view = null;
  }

  private async _loadEditor() {
    if (this._modules || this._loadError) {
      return;
    }
    try {
      this._modules = await import('./prism-definition-editor-codemirror.js');
      this._ready = true;
      // Wait one frame so the host element is available, then mount.
      await this.updateComplete;
      this._mountView();
    } catch (err) {
      this._loadError = err instanceof Error ? err.message : String(err);
    }
  }

  private _hostDiv(): HTMLDivElement | null {
    return this.renderRoot?.querySelector?.('[data-prism-definition-editor-host]') as HTMLDivElement | null;
  }

  private _mountView() {
    const host = this._hostDiv();
    if (!this._modules || !host || this._view) {
      return;
    }
    const onChange = (next: string) => {
      if (this._suppressInputEvent) {
        return;
      }
      this.value = next;
      this.dispatchEvent(
        new CustomEvent('definition-input', {
          detail: { value: next },
          bubbles: true,
          composed: true,
        })
      );
    };
    this._view = this._modules.createDefinitionView({
      parent: host,
      doc: this.value,
      readOnly: this.readOnly,
      onChange,
    });
    this._applyDiagnostics();
  }

  updated(changed: Map<string, unknown>) {
    super.updated(changed);
    if (changed.has('value') && this._view) {
      const current = this._view.state.doc.toString();
      if (current !== this.value) {
        this._suppressInputEvent = true;
        try {
          this._view.dispatch({
            changes: { from: 0, to: current.length, insert: this.value },
          });
        } finally {
          this._suppressInputEvent = false;
        }
      }
    }
    if (changed.has('readOnly') && this._view && this._modules) {
      this._modules.setReadOnlyDispatch(this._view, this.readOnly);
    }
    if (changed.has('diagnostics')) {
      this._applyDiagnostics();
    }
  }

  /** Imperative method used by host tests / focus management. */
  focus(options?: FocusOptions) {
    if (this._view) {
      this._view.focus();
      return;
    }
    super.focus(options);
  }

  private _applyDiagnostics() {
    if (!this._view || !this._modules) {
      return;
    }
    const doc = this._view.state.doc;
    const cmDiagnostics: Diagnostic[] = this.diagnostics.map(diag => {
      const safeLine = Math.min(Math.max(diag.line, 1), doc.lines);
      const lineInfo = doc.line(safeLine);
      return {
        from: lineInfo.from,
        to: lineInfo.to,
        severity: diag.severity,
        message: diag.message,
      };
    });
    this._view.dispatch({ effects: this._modules.setDiagnosticsEffect.of(cmDiagnostics) });
  }

  render() {
    return html`
      <div
        class="editor-host"
        data-prism-definition-editor-host
      ></div>
      ${this._loadError
        ? html`<p class="load-error" role="alert" data-prism-definition-load-error>
            Couldn't load the JSON editor: ${this._loadError}
          </p>`
        : !this._ready
          ? html`<p class="loading" role="status" data-prism-definition-loading>
              Loading the JSON editor…
            </p>`
          : ''}
    `;
  }

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 0;
      background: #ffffff;
      font-family: "GDS Transport", arial, sans-serif;
    }

    .editor-host {
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
    }

    .editor-host:focus-within {
      outline: 3px solid #ffdd00;
      outline-offset: -3px;
    }

    /* CodeMirror's default container should fill the host and establish a flex column. */
    :host ::slotted(.cm-editor),
    .cm-editor {
      flex: 1 1 0%;
      min-height: 0;
      display: flex !important;
      flex-direction: column !important;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
      font-size: 0.875rem;
      color: #0b0c0c;
    }

    /* The scroller is the actual scrolling container. */
    :host ::slotted(.cm-scroller),
    .cm-scroller {
      flex: 1 1 0% !important;
      min-height: 0 !important;
      overflow: auto !important;
    }

    .editor-host .cm-gutters {
      background: #f3f2f1;
      color: #505a5f;
      border-right: 1px solid #b1b4b6;
    }

    /* Diagnostic colours that meet 4.5:1 against #ffffff. */
    .editor-host .cm-diagnostic-error {
      border-left: 3px solid #b10e1e;
      background: #fbeaec;
      color: #0b0c0c;
    }

    .editor-host .cm-diagnostic-warning {
      border-left: 3px solid #594d00;
      background: #fff4d3;
      color: #0b0c0c;
    }

    .loading,
    .load-error {
      margin: 0;
      padding: 0.75rem 1rem;
      font-size: 0.9375rem;
      color: #0b0c0c;
    }

    .load-error {
      background: #fbeaec;
      color: #b10e1e;
    }

    :host([read-only]) .editor-host {
      background: #f3f2f1;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-definition-editor': PrismDefinitionEditorElement;
  }
}
