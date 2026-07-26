import { UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE, UMB_CMS_SERVICE_BLUEPRINT_WORKSPACE_ALIAS } from '../entity.js';

export const manifests = [
  {
    type: 'workspace',
    kind: 'routable',
    alias: UMB_CMS_SERVICE_BLUEPRINT_WORKSPACE_ALIAS,
    name: 'CMS Service Blueprint Workspace',
    api: () => import('./cms-service-blueprint-workspace.context.js'),
    meta: {
      entityType: UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE,
    },
  },
];
