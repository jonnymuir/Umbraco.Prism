import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbDetailRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { UmbCmsWorkflowDetailServerDataSource } from './cms-workflow-detail.server.data-source.js';
import { UMB_CMS_WORKFLOW_DETAIL_STORE_CONTEXT } from './cms-workflow-detail.store.js';
import type { CmsWorkflowEntityModel } from '../../entity.js';

export class UmbCmsWorkflowDetailRepository extends UmbDetailRepositoryBase<CmsWorkflowEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UmbCmsWorkflowDetailServerDataSource, UMB_CMS_WORKFLOW_DETAIL_STORE_CONTEXT);
  }

  /** CMS Workflow definitions are not nested under a parent — always create at the root. */
  async create(model: CmsWorkflowEntityModel) {
    return super.create(model, null);
  }
}

export default UmbCmsWorkflowDetailRepository;
