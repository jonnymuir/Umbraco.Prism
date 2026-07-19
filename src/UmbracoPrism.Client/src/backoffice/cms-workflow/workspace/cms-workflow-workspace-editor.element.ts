// Mounted by the workspace's own routing (see cms-workflow-workspace.context.ts's `edit/:unique`
// route) once the workspace context has loaded a specific definitionKey. Mounts
// <prism-workflow-editor> directly — not the shell (<prism-workflow-editor-shell>), which owns
// its own internal workflow list/switcher that would be entirely redundant here: the collection
// + workspace routing this file lives inside of already IS that list/switcher, at the Umbraco
// section level rather than nested inside the editor itself.

import { LitElement, css, html } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import '../../../workflow-editor/prism-workflow-editor.js';
import { UmbracoBackofficeWorkflowSource } from '../../cms-workflow-source.js';
import type { WorkflowQueueDefinition } from '../../../workflow-editor/workflow-stage-assignment.js';
import type { UmbCmsWorkflowWorkspaceContext } from './cms-workflow-workspace.context.js';
import { UMB_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/workspace';

const QUEUES_URL = '/umbraco/management/api/v1/prism/cms-workflows/queues';

@customElement('prism-cms-workflow-workspace-editor')
export class PrismCmsWorkflowWorkspaceEditorElement extends UmbElementMixin(LitElement) {
  @state() private _availableQueues: WorkflowQueueDefinition[] = [];
  @state() private _definitionKey: string | null = null;

  private _source?: UmbracoBackofficeWorkflowSource;

  connectedCallback(): void {
    super.connectedCallback();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;

      this._source = new UmbracoBackofficeWorkflowSource(() => authContext.getLatestToken());
      this.requestUpdate();
      void this._loadQueues(authContext);
    });

    this.consumeContext(UMB_WORKSPACE_CONTEXT, (workspaceContext) => {
      const context = workspaceContext as UmbCmsWorkflowWorkspaceContext | undefined;
      if (!context) return;

      this.observe(context.data, (data) => {
        this._definitionKey = data?.definitionKey ?? null;
      });
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
      // The editor falls back gracefully with an empty queue list — not fatal.
    }
  }

  render() {
    if (!this._source || !this._definitionKey) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`
      <prism-workflow-editor
        workflow-key=${this._definitionKey}
        .workflowSource=${this._source}
        .availableQueues=${this._availableQueues}
      ></prism-workflow-editor>
    `;
  }

  static styles = css`
    :host {
      display: block;
      width: 100%;
      min-height: 70vh;
    }

    prism-workflow-editor {
      display: block;
      min-height: 70vh;
    }
  `;
}

export default PrismCmsWorkflowWorkspaceEditorElement;
