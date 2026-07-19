import { UMB_CMS_WORKFLOW_ENTITY_TYPE } from '../entity.js';
import { UMB_CMS_WORKFLOW_DETAIL_REPOSITORY_ALIAS } from '../repository/detail/manifests.js';
import { UMB_CMS_WORKFLOW_ITEM_REPOSITORY_ALIAS } from '../repository/item/manifests.js';

export const manifests = [
  {
    type: 'entityAction',
    kind: 'delete',
    alias: 'Prism.EntityAction.CmsWorkflow.Delete',
    name: 'Delete CMS Workflow Entity Action',
    forEntityTypes: [UMB_CMS_WORKFLOW_ENTITY_TYPE],
    meta: {
      detailRepositoryAlias: UMB_CMS_WORKFLOW_DETAIL_REPOSITORY_ALIAS,
      itemRepositoryAlias: UMB_CMS_WORKFLOW_ITEM_REPOSITORY_ALIAS,
    },
  },
];
