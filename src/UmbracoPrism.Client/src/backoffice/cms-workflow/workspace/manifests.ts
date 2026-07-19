import { UMB_CMS_WORKFLOW_ENTITY_TYPE, UMB_CMS_WORKFLOW_WORKSPACE_ALIAS } from '../entity.js';

export const manifests = [
  {
    type: 'workspace',
    kind: 'routable',
    alias: UMB_CMS_WORKFLOW_WORKSPACE_ALIAS,
    name: 'CMS Workflow Workspace',
    api: () => import('./cms-workflow-workspace.context.js'),
    meta: {
      entityType: UMB_CMS_WORKFLOW_ENTITY_TYPE,
    },
  },
];
