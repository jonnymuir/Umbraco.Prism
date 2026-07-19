import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import { UmbDetailStoreBase } from '@umbraco-cms/backoffice/store';
import type { CmsWorkflowEntityModel } from '../../entity.js';

export class UmbCmsWorkflowDetailStore extends UmbDetailStoreBase<CmsWorkflowEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UMB_CMS_WORKFLOW_DETAIL_STORE_CONTEXT.toString());
  }
}

export default UmbCmsWorkflowDetailStore;

export const UMB_CMS_WORKFLOW_DETAIL_STORE_CONTEXT = new UmbContextToken<UmbCmsWorkflowDetailStore>(
  'UmbCmsWorkflowDetailStore',
);
