import { UMB_CMS_SERVICE_BLUEPRINT_COLLECTION_ALIAS } from '../../entity.js';

export const UMB_CMS_SERVICE_BLUEPRINT_TABLE_COLLECTION_VIEW_ALIAS = 'Prism.CollectionView.CmsServiceBlueprint.Table';

export const manifests = [
  {
    type: 'collectionView',
    alias: UMB_CMS_SERVICE_BLUEPRINT_TABLE_COLLECTION_VIEW_ALIAS,
    name: 'CMS Service Blueprint Table Collection View',
    js: () => import('./cms-service-blueprint-table-collection-view.element.js'),
    meta: {
      label: 'Table',
      icon: 'icon-table',
      pathName: 'table',
    },
    conditions: [
      {
        alias: 'Umb.Condition.CollectionAlias',
        match: UMB_CMS_SERVICE_BLUEPRINT_COLLECTION_ALIAS,
      },
    ],
  },
];
