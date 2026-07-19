import { UMB_CMS_WORKFLOW_COLLECTION_ALIAS } from '../../entity.js';

export const UMB_CMS_WORKFLOW_TABLE_COLLECTION_VIEW_ALIAS = 'Prism.CollectionView.CmsWorkflow.Table';

export const manifests = [
  {
    type: 'collectionView',
    alias: UMB_CMS_WORKFLOW_TABLE_COLLECTION_VIEW_ALIAS,
    name: 'CMS Workflow Table Collection View',
    js: () => import('./cms-workflow-table-collection-view.element.js'),
    meta: {
      label: 'Table',
      icon: 'icon-table',
      pathName: 'table',
    },
    conditions: [
      {
        alias: 'Umb.Condition.CollectionAlias',
        match: UMB_CMS_WORKFLOW_COLLECTION_ALIAS,
      },
    ],
  },
];
