import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbItemRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { UmbCmsWorkflowItemServerDataSource } from './cms-workflow-item.server.data-source.js';
import { UMB_CMS_WORKFLOW_ITEM_STORE_CONTEXT } from './cms-workflow-item.store.js';
import type { CmsWorkflowEntityModel } from '../../entity.js';

export class UmbCmsWorkflowItemRepository extends UmbItemRepositoryBase<CmsWorkflowEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UmbCmsWorkflowItemServerDataSource, UMB_CMS_WORKFLOW_ITEM_STORE_CONTEXT);
  }
}

export default UmbCmsWorkflowItemRepository;
