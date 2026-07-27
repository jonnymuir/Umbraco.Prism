import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbDetailRepositoryBase } from '@umbraco-cms/backoffice/repository';
import { UmbCmsServiceBlueprintDetailServerDataSource } from './cms-service-blueprint-detail.server.data-source.js';
import { UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_CONTEXT } from './cms-service-blueprint-detail.store.js';
import type { CmsServiceBlueprintEntityModel } from '../../entity.js';

export class UmbCmsServiceBlueprintDetailRepository extends UmbDetailRepositoryBase<CmsServiceBlueprintEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, UmbCmsServiceBlueprintDetailServerDataSource, UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_CONTEXT);
  }

  /** CMS Service Blueprint definitions are not nested under a parent — always create at the root. */
  async create(model: CmsServiceBlueprintEntityModel) {
    return super.create(model, null);
  }
}

export default UmbCmsServiceBlueprintDetailRepository;
