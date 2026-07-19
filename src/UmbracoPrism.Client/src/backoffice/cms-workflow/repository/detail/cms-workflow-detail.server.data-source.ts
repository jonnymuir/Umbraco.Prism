import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbDetailDataSource } from '@umbraco-cms/backoffice/repository';
import { cmsWorkflowFetch } from '../../cms-workflow-http.js';
import { UMB_CMS_WORKFLOW_ENTITY_TYPE, type CmsWorkflowEntityModel } from '../../entity.js';

/**
 * Talks to CmsWorkflowAuthoringController's REST surface directly (list/read/save/delete) —
 * NOT the generated Management API client, matching UmbracoBackofficeWorkflowSource's own
 * approach (this endpoint isn't part of Umbraco's OpenAPI-generated surface).
 *
 * Deliberately thin: this backoffice screen (collection + entity actions + workspace routing)
 * only ever needs `definitionKey`/`displayName` to identify and list a workflow. The actual
 * authored JSON is read/written entirely by `<prism-workflow-editor>` via its own
 * `UmbracoBackofficeWorkflowSource` — this data source's `update()` is never called by anything
 * (the workspace registers no generic Save action; the editor's own Save button is the only one),
 * and its `create()` posts the minimal valid definition the editor needs to then load and build
 * out from a blank slate.
 */
export class UmbCmsWorkflowDetailServerDataSource implements UmbDetailDataSource<CmsWorkflowEntityModel> {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  async createScaffold(preset: Partial<CmsWorkflowEntityModel> = {}) {
    const data: CmsWorkflowEntityModel = {
      entityType: UMB_CMS_WORKFLOW_ENTITY_TYPE,
      unique: '',
      definitionKey: '',
      displayName: '',
      ...preset,
    };
    return { data };
  }

  async read(unique: string) {
    const response = await cmsWorkflowFetch(this.#host, `/${encodeURIComponent(unique)}`);
    if (!response.ok) {
      return { error: new Error(`Failed to load CMS workflow '${unique}' (${response.status}).`) };
    }
    const payload = (await response.json()) as { definitionKey: string; displayName: string };
    return {
      data: {
        entityType: UMB_CMS_WORKFLOW_ENTITY_TYPE,
        unique: payload.definitionKey,
        definitionKey: payload.definitionKey,
        displayName: payload.displayName,
      } satisfies CmsWorkflowEntityModel,
    };
  }

  /**
   * Creates the workflow with the minimum valid shape `<prism-workflow-editor>` and the CMS
   * Workflow runtime both expect: a single `eligibility` initial state and the one well-known
   * `cms-visitor` queue (see `CmsWorkflowQueue` on the server — `CmsWorkflowSingleQueueValidator`
   * rejects anything else). The author fills in the real content once the editor opens.
   */
  async create(model: CmsWorkflowEntityModel) {
    const body = {
      definitionKey: model.definitionKey,
      displayName: model.displayName,
      version: 0,
      schemaVersion: '1.0',
      initialState: 'start',
      instancePolicy: 'single',
      queues: [{ key: 'cms-visitor', displayName: 'Site visitor' }],
      states: [
        {
          stateKey: 'start',
          displayName: model.displayName || model.definitionKey,
          stageType: 'Question',
          queueKey: 'cms-visitor',
          components: [],
          routes: [],
        },
      ],
    };

    const response = await cmsWorkflowFetch(this.#host, `/${encodeURIComponent(model.definitionKey)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const detail = await response.text().catch(() => '');
      return { error: new Error(`Failed to create CMS workflow '${model.definitionKey}' (${response.status}). ${detail}`) };
    }

    return this.read(model.definitionKey);
  }

  /**
   * Deliberately unsupported — never wired to any UI (see the class doc comment). Throws loudly
   * instead of silently no-op "succeeding", so if some future generic workspace chrome ever
   * calls this path unexpectedly, it fails visibly rather than quietly discarding the author's
   * actual edits (which live in `<prism-workflow-editor>`'s own state, not this thin model).
   */
  async update(_model: CmsWorkflowEntityModel): Promise<never> {
    throw new Error(
      'UmbCmsWorkflowDetailServerDataSource.update() is not supported — CMS Workflow content is saved via ' +
        "<prism-workflow-editor>'s own Save button (UmbracoBackofficeWorkflowSource), not this generic workspace path.",
    );
  }

  async delete(unique: string) {
    const response = await cmsWorkflowFetch(this.#host, `/${encodeURIComponent(unique)}`, { method: 'DELETE' });
    if (!response.ok && response.status !== 404) {
      return { error: new Error(`Failed to delete CMS workflow '${unique}' (${response.status}).`) };
    }
    return {};
  }
}

export default UmbCmsWorkflowDetailServerDataSource;
