import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbItemRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { UmbCmsServiceBlueprintItemServerDataSource } from './cms-service-blueprint-item.server.data-source.js';
import { UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_CONTEXT } from './cms-service-blueprint-item.store.js';
import type { CmsServiceBlueprintEntityModel } from '../../entity.js';

export class UmbCmsServiceBlueprintItemRepository extends UmbItemRepositoryBase<CmsServiceBlueprintEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UmbCmsServiceBlueprintItemServerDataSource, UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_CONTEXT);
  }
}

export default UmbCmsServiceBlueprintItemRepository;
