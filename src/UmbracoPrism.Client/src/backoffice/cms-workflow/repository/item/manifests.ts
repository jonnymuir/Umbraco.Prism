export const UMB_CMS_WORKFLOW_ITEM_REPOSITORY_ALIAS = 'Prism.Repository.CmsWorkflowItem';
const UMB_CMS_WORKFLOW_ITEM_STORE_ALIAS = 'Prism.Store.CmsWorkflowItem';

export const manifests = [
  {
    type: 'repository',
    alias: UMB_CMS_WORKFLOW_ITEM_REPOSITORY_ALIAS,
    name: 'CMS Workflow Item Repository',
    api: () => import('./cms-workflow-item.repository.js'),
  },
  {
    type: 'itemStore',
    alias: UMB_CMS_WORKFLOW_ITEM_STORE_ALIAS,
    name: 'CMS Workflow Item Store',
    api: () => import('./cms-workflow-item.store.js'),
  },
];
