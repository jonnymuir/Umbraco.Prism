import { UMB_CMS_WORKFLOW_ROOT_ENTITY_TYPE, UMB_CMS_WORKFLOW_COLLECTION_ALIAS } from '../../entity.js';

export const manifests = [
  {
    type: 'collectionAction',
    kind: 'create',
    alias: 'Prism.CollectionAction.CmsWorkflow.Create',
    name: 'Create CMS Workflow Collection Action',
    conditions: [
      {
        alias: 'Umb.Condition.CollectionAlias',
        match: UMB_CMS_WORKFLOW_COLLECTION_ALIAS,
      },
    ],
  },
  {
    type: 'entityCreateOptionAction',
    alias: 'Prism.EntityCreateOptionAction.CmsWorkflow',
    name: 'Create CMS Workflow Option Action',
    api: () => import('./cms-workflow-create-option-action.js'),
    forEntityTypes: [UMB_CMS_WORKFLOW_ROOT_ENTITY_TYPE],
    meta: {
      icon: 'icon-diagram',
      label: 'New CMS workflow',
      description: 'Author a new CMS Workflow definition, hosted and run entirely in Umbraco.',
    },
  },
];
