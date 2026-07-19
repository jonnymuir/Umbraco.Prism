import { manifests as viewManifests } from './views/manifests.js';
import { manifests as actionManifests } from './action/manifests.js';
import { UMB_CMS_WORKFLOW_COLLECTION_ALIAS } from '../entity.js';

export const UMB_CMS_WORKFLOW_COLLECTION_REPOSITORY_ALIAS = 'Prism.Repository.CmsWorkflowCollection';

export const manifests = [
  {
    type: 'collection',
    kind: 'default',
    alias: UMB_CMS_WORKFLOW_COLLECTION_ALIAS,
    name: 'CMS Workflow Collection',
    meta: {
      repositoryAlias: UMB_CMS_WORKFLOW_COLLECTION_REPOSITORY_ALIAS,
    },
  },
  {
    type: 'repository',
    alias: UMB_CMS_WORKFLOW_COLLECTION_REPOSITORY_ALIAS,
    name: 'CMS Workflow Collection Repository',
    api: () => import('./cms-workflow-collection.repository.js'),
  },
  ...viewManifests,
  ...actionManifests,
];
