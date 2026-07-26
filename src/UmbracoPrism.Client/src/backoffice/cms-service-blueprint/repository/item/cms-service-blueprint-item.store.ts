import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import { UmbItemStoreBase } from '@umbraco-cms/backoffice/store';
import type { CmsServiceBlueprintEntityModel } from '../../entity.js';

export class UmbCmsServiceBlueprintItemStore extends UmbItemStoreBase<CmsServiceBlueprintEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_CONTEXT.toString());
  }
}

export default UmbCmsServiceBlueprintItemStore;

export const UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_CONTEXT = new UmbContextToken<UmbCmsServiceBlueprintItemStore>(
  'UmbCmsServiceBlueprintItemStore',
);
