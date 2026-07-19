// Entity/workspace identity constants for the CMS Workflow backoffice screen — a flat
// (non-hierarchical) entity, so this mirrors Umbraco 17's own Webhook management package
// (Collection + entity-actions + Workspace) rather than a custom Tree, which is the idiomatic
// shape for a flat list in Umbraco 17.

export const UMB_CMS_WORKFLOW_ENTITY_TYPE = 'prism-cms-workflow';
export const UMB_CMS_WORKFLOW_ROOT_ENTITY_TYPE = 'prism-cms-workflow-root';

export const UMB_CMS_WORKFLOW_WORKSPACE_ALIAS = 'Prism.Workspace.CmsWorkflow';

export const UMB_CMS_WORKFLOW_COLLECTION_ALIAS = 'Prism.Collection.CmsWorkflow';

export const UMB_CMS_WORKFLOW_EDIT_PATH_PREFIX = 'section/prism/workspace/prism-cms-workflow/edit/';

/**
 * The shape this backoffice screen works with — deliberately NOT the full
 * `WorkflowDefinitionFile`/`AuthoredWorkflow` JSON. The actual workflow editing surface
 * (`<prism-workflow-editor>`) manages its own rich local state and calls
 * `UmbracoBackofficeWorkflowSource` directly for load/save — this model exists only so the
 * generic Umbraco collection/entity-action/workspace-routing machinery has something to list,
 * identify, and delete.
 */
export interface CmsWorkflowEntityModel {
  entityType: typeof UMB_CMS_WORKFLOW_ENTITY_TYPE;
  unique: string;
  definitionKey: string;
  displayName: string;
}
