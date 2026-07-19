import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { cmsWorkflowFetch } from '../cms-workflow-http.js';
import { UMB_CMS_WORKFLOW_ENTITY_TYPE, type CmsWorkflowEntityModel } from '../entity.js';

type ServerSummary = { definitionKey: string; displayName: string };

/**
 * The list is expected to stay small (a handful of authored workflows, not hundreds), so
 * paging is applied client-side against the one cheap list endpoint rather than adding
 * server-side paging query params this screen doesn't need yet.
 */
export class UmbCmsWorkflowCollectionRepository extends UmbRepositoryBase {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    super(host);
    this.#host = host;
  }

  async requestCollection(filter: { skip?: number; take?: number } = {}) {
    const response = await cmsWorkflowFetch(this.#host, '');
    if (!response.ok) {
      return { error: new Error(`Failed to list CMS workflows (${response.status}).`) };
    }

    const all = (await response.json()) as ServerSummary[];
    const items: CmsWorkflowEntityModel[] = all
      .map((item) => ({
        entityType: UMB_CMS_WORKFLOW_ENTITY_TYPE as typeof UMB_CMS_WORKFLOW_ENTITY_TYPE,
        unique: item.definitionKey,
        definitionKey: item.definitionKey,
        displayName: item.displayName,
      }))
      .sort((a, b) => a.displayName.localeCompare(b.displayName));

    const skip = filter.skip ?? 0;
    const take = filter.take ?? items.length;

    return { data: { items: items.slice(skip, skip + take), total: items.length } };
  }
}

export default UmbCmsWorkflowCollectionRepository;
