import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbDetailDataSource } from '@umbraco-cms/backoffice/repository';
import { cmsServiceBlueprintFetch } from '../../cms-service-blueprint-http.js';
import { UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE, type CmsServiceBlueprintEntityModel } from '../../entity.js';

/**
 * Talks to CmsServiceBlueprintAuthoringController's REST surface directly (list/read/save/delete) —
 * NOT the generated Management API client, matching UmbracoBackofficeServiceBlueprintSource's own
 * approach (this endpoint isn't part of Umbraco's OpenAPI-generated surface).
 *
 * Deliberately thin: this backoffice screen (collection + entity actions + workspace routing)
 * only ever needs `definitionKey`/`displayName` to identify and list a service blueprint. The actual
 * authored JSON is read/written entirely by `<prism-service-blueprint-editor>` via its own
 * `UmbracoBackofficeServiceBlueprintSource` — this data source's `update()` is never called by anything
 * (the workspace registers no generic Save action; the editor's own Save button is the only one),
 * and its `create()` posts the minimal valid definition the editor needs to then load and build
 * out from a blank slate.
 */
export class UmbCmsServiceBlueprintDetailServerDataSource implements UmbDetailDataSource<CmsServiceBlueprintEntityModel> {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  async createScaffold(preset: Partial<CmsServiceBlueprintEntityModel> = {}) {
    const data: CmsServiceBlueprintEntityModel = {
      entityType: UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE,
      unique: '',
      definitionKey: '',
      displayName: '',
      ...preset,
    };
    return { data };
  }

  async read(unique: string) {
    const response = await cmsServiceBlueprintFetch(this.#host, `/${encodeURIComponent(unique)}`);
    if (!response.ok) {
      return { error: new Error(`Failed to load CMS serviceBlueprint '${unique}' (${response.status}).`) };
    }
    const payload = (await response.json()) as { definitionKey: string; displayName: string };
    return {
      data: {
        entityType: UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE,
        unique: payload.definitionKey,
        definitionKey: payload.definitionKey,
        displayName: payload.displayName,
      } satisfies CmsServiceBlueprintEntityModel,
    };
  }

  /**
   * Creates the service blueprint with the minimum valid shape `<prism-service-blueprint-editor>` and the CMS
   * ServiceBlueprint runtime both expect: a single `eligibility` initial state and the one well-known
   * `cms-visitor` queue (see `CmsServiceBlueprintQueue` on the server — `CmsServiceBlueprintSingleQueueValidator`
   * rejects anything else). The author fills in the real content once the editor opens.
   */
  async create(model: CmsServiceBlueprintEntityModel) {
    const body = {
      definitionKey: model.definitionKey,
      displayName: model.displayName,
      version: 0,
      schemaVersion: '1.0',
      initialStage: 'start',
      requestPolicy: 'single',
      queues: [{ key: 'cms-visitor', displayName: 'Site visitor' }],
      stages: [
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

    const response = await cmsServiceBlueprintFetch(this.#host, `/${encodeURIComponent(model.definitionKey)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const detail = await response.text().catch(() => '');
      return { error: new Error(`Failed to create CMS serviceBlueprint '${model.definitionKey}' (${response.status}). ${detail}`) };
    }

    return this.read(model.definitionKey);
  }

  /**
   * Deliberately unsupported — never wired to any UI (see the class doc comment). Throws loudly
   * instead of silently no-op "succeeding", so if some future generic workspace chrome ever
   * calls this path unexpectedly, it fails visibly rather than quietly discarding the author's
   * actual edits (which live in `<prism-service-blueprint-editor>`'s own state, not this thin model).
   */
  async update(_model: CmsServiceBlueprintEntityModel): Promise<never> {
    throw new Error(
      'UmbCmsServiceBlueprintDetailServerDataSource.update() is not supported — CMS Service Blueprint content is saved via ' +
        "<prism-service-blueprint-editor>'s own Save button (UmbracoBackofficeServiceBlueprintSource), not this generic workspace path.",
    );
  }

  async delete(unique: string) {
    const response = await cmsServiceBlueprintFetch(this.#host, `/${encodeURIComponent(unique)}`, { method: 'DELETE' });
    if (!response.ok && response.status !== 404) {
      return { error: new Error(`Failed to delete CMS serviceBlueprint '${unique}' (${response.status}).`) };
    }
    return {};
  }
}

export default UmbCmsServiceBlueprintDetailServerDataSource;
