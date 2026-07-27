export const UMB_CMS_SERVICE_BLUEPRINT_ITEM_REPOSITORY_ALIAS = 'Prism.Repository.CmsServiceBlueprintItem';
const UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_ALIAS = 'Prism.Store.CmsServiceBlueprintItem';

export const manifests = [
  {
    type: 'repository',
    alias: UMB_CMS_SERVICE_BLUEPRINT_ITEM_REPOSITORY_ALIAS,
    name: 'CMS Service Blueprint Item Repository',
    api: () => import('./cms-service-blueprint-item.repository.js'),
  },
  {
    type: 'itemStore',
    alias: UMB_CMS_SERVICE_BLUEPRINT_ITEM_STORE_ALIAS,
    name: 'CMS Service Blueprint Item Store',
    api: () => import('./cms-service-blueprint-item.store.js'),
  },
];
