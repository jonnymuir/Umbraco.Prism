// @ts-check
// Umbraco v17 — plain ESM; resolved via Umbraco's built-in import map at runtime.
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css } from '@umbraco-cms/backoffice/external/lit';

/**
 * Reads the authoring base URL from (in priority order):
 *   1. window.PrismWorkflowEditorConfig.authoringBaseUrl  (runtime override)
 *   2. The hard-coded default pointing at MockBusinessApp dev server.
 */
function getAuthoringBaseUrl() {
  return (
    /** @type {any} */ (window).PrismWorkflowEditorConfig?.authoringBaseUrl ??
    'https://localhost:7245'
  );
}

class PrismWorkflowEditorHostElement extends UmbLitElement {
  static properties = {
    _editorAvailable: { type: Boolean, state: true },
    _checking: { type: Boolean, state: true },
  };

  constructor() {
    super();
    this._editorAvailable = false;
    this._checking = true;
  }

  connectedCallback() {
    super.connectedCallback();
    this._checkEditorAvailability();
  }

  get _editorUrl() {
    return `${getAuthoringBaseUrl()}/workflow-editor.html?workflow=planning`;
  }

  async _checkEditorAvailability() {
    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 4000);
      // mode: 'no-cors' gives an opaque response when the server is up.
      // A thrown error means the server is unreachable (connection refused / timeout).
      await fetch(getAuthoringBaseUrl(), { method: 'HEAD', mode: 'no-cors', signal: controller.signal });
      clearTimeout(timeout);
      this._editorAvailable = true;
    } catch {
      this._editorAvailable = false;
    }
    this._checking = false;
  }

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      width: 100%;
    }

    iframe {
      flex: 1;
      border: none;
      width: 100%;
      height: 100%;
    }

    .prism-fallback {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 1rem;
      color: var(--uui-color-text-alt, #6b7280);
      font-family: var(--uui-font-family, sans-serif);
    }

    .prism-fallback h2 {
      margin: 0;
      font-size: 1.25rem;
      color: var(--uui-color-text, #1f2937);
    }

    .prism-fallback p {
      margin: 0;
      font-size: 0.9rem;
    }

    .prism-fallback code {
      background: var(--uui-color-surface-alt, #f3f4f6);
      padding: 0.2em 0.4em;
      border-radius: 3px;
      font-size: 0.85em;
    }

    .prism-spinner {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      color: var(--uui-color-text-alt, #6b7280);
      font-family: var(--uui-font-family, sans-serif);
    }
  `;

  render() {
    if (this._checking) {
      return html`<div class="prism-spinner">Connecting to editor…</div>`;
    }

    if (!this._editorAvailable) {
      return html`
        <div class="prism-fallback">
          <uui-icon name="icon-document" style="font-size: 3rem;"></uui-icon>
          <h2>Editor not yet built</h2>
          <p>
            The workflow editor app was not reachable at
            <code>${getAuthoringBaseUrl()}</code>.
          </p>
          <p>
            Start the MockBusinessApp (<code>dotnet run --project src/UmbracoPrism.MockBusinessApp</code>)
            or set <code>window.PrismWorkflowEditorConfig.authoringBaseUrl</code> to the correct URL.
          </p>
        </div>
      `;
    }

    return html`<iframe src="${this._editorUrl}" title="Workflow Editor — Planning Application"></iframe>`;
  }
}

customElements.define('prism-workflow-editor-host', PrismWorkflowEditorHostElement);
