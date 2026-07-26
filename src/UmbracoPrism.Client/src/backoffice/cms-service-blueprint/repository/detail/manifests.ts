export const UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS = 'Prism.Repository.CmsServiceBlueprintDetail';
const UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_ALIAS = 'Prism.Store.CmsServiceBlueprintDetail';

export const manifests = [
  {
    type: 'repository',
    alias: UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS,
    name: 'CMS Service Blueprint Detail Repository',
    api: () => import('./cms-service-blueprint-detail.repository.js'),
  },
  {
    type: 'store',
    alias: UMB_CMS_SERVICE_BLUEPRINT_DETAIL_STORE_ALIAS,
    name: 'CMS Service Blueprint Detail Store',
    api: () => import('./cms-service-blueprint-detail.store.js'),
  },
];
