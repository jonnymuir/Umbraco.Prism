import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import { UmbItemStoreBase } from '@umbraco-cms/backoffice/store';
import type { CmsWorkflowEntityModel } from '../../entity.js';

export class UmbCmsWorkflowItemStore extends UmbItemStoreBase<CmsWorkflowEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UMB_CMS_WORKFLOW_ITEM_STORE_CONTEXT.toString());
  }
}

export default UmbCmsWorkflowItemStore;

export const UMB_CMS_WORKFLOW_ITEM_STORE_CONTEXT = new UmbContextToken<UmbCmsWorkflowItemStore>(
  'UmbCmsWorkflowItemStore',
);
