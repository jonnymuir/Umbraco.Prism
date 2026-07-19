export const UMB_CMS_WORKFLOW_DETAIL_REPOSITORY_ALIAS = 'Prism.Repository.CmsWorkflowDetail';
const UMB_CMS_WORKFLOW_DETAIL_STORE_ALIAS = 'Prism.Store.CmsWorkflowDetail';

export const manifests = [
  {
    type: 'repository',
    alias: UMB_CMS_WORKFLOW_DETAIL_REPOSITORY_ALIAS,
    name: 'CMS Workflow Detail Repository',
    api: () => import('./cms-workflow-detail.repository.js'),
  },
  {
    type: 'store',
    alias: UMB_CMS_WORKFLOW_DETAIL_STORE_ALIAS,
    name: 'CMS Workflow Detail Store',
    api: () => import('./cms-workflow-detail.store.js'),
  },
];
