import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import { UmbDetailStoreBase } from '@umbraco-cms/backoffice/store';
import type { CmsServiceBlueprintEntityModel } from '../../entity.js';

export class UmbCmsServiceBlueprintDetailStore extends UmbDetailStoreBase<CmsServiceBlueprintEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_CONTEXT.toString());
  }
}

export default UmbCmsServiceBlueprintDetailStore;

export const UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_CONTEXT = new UmbContextToken<UmbCmsServiceBlueprintDetailStore>(
  'UmbCmsServiceBlueprintDetailStore',
);
