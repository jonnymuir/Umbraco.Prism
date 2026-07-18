// Native backoffice mount for the Prism workflow editor — the CMS Workflow implementation's
// entire reason for existing is this editing experience, so (unlike MockBusinessApp, a pure
// business-app host with no backoffice) the editor is mounted directly as a backoffice
// extension rather than hosted as a standalone runtime-only page. See
// src/workflow-editor/README.md for the hosting-flexibility rationale.

import { LitElement, css, html } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import '../workflow-editor/prism-workflow-editor-shell.js';
import { UmbracoBackofficeWorkflowSource } from './cms-workflow-source.js';
import type { WorkflowQueueDefinition } from '../workflow-editor/workflow-stage-assignment.js';

const QUEUES_URL = '/umbraco/management/api/v1/prism/cms-workflows/queues';

@customElement('prism-cms-workflow-editor')
export class PrismCmsWorkflowEditorElement extends UmbElementMixin(LitElement) {
  @state()
  private _availableQueues: WorkflowQueueDefinition[] = [];

  private _source?: UmbracoBackofficeWorkflowSource;

  connectedCallback(): void {
    super.connectedCallback();

    this.consumeContext(UMB_AUTH_CONTEXT, authContext => {
      if (!authContext) return;

      this._source = new UmbracoBackofficeWorkflowSource(() => authContext.getLatestToken());
      this.requestUpdate();
      void this._loadQueues(authContext);
    });
  }

  private async _loadQueues(authContext: { getLatestToken: () => Promise<string | undefined> }): Promise<void> {
    try {
      const token = await authContext.getLatestToken();
      const response = await fetch(QUEUES_URL, {
        headers: {
          Accept: 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        credentials: 'same-origin',
      });
      if (!response.ok) return;

      this._availableQueues = (await response.json()) as WorkflowQueueDefinition[];
    } catch {
      // The shell falls back gracefully with an empty queue list — not fatal.
    }
  }

  render() {
    if (!this._source) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`
      <prism-workflow-editor-shell
        .workflowSource=${this._source}
        .availableQueues=${this._availableQueues}
      ></prism-workflow-editor-shell>
    `;
  }

  static styles = css`
    :host {
      display: block;
      height: 100%;
      width: 100%;
    }
  `;
}

export default PrismCmsWorkflowEditorElement;
