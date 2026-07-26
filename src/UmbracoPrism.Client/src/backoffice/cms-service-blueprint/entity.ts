// Entity/workspace identity constants for the CMS Service Blueprint backoffice screen — a flat
// (non-hierarchical) entity, so this mirrors Umbraco 17's own Webhook management package
// (Collection + entity-actions + Workspace) rather than a custom Tree, which is the idiomatic
// shape for a flat list in Umbraco 17.

export const UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE = 'prism-cms-service-blueprint';
export const UMB_CMS_SERVICE_BLUEPRINT_ROOT_ENTITY_TYPE = 'prism-cms-service-blueprint-root';

export const UMB_CMS_SERVICE_BLUEPRINT_WORKSPACE_ALIAS = 'Prism.Workspace.CmsServiceBlueprint';

export const UMB_CMS_SERVICE_BLUEPRINT_COLLECTION_ALIAS = 'Prism.Collection.CmsServiceBlueprint';

export const UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX = 'section/prism/workspace/prism-cms-service-blueprint/edit/';

/**
 * The shape this backoffice screen works with — deliberately NOT the full
 * `ServiceBlueprintDefinitionFile`/`AuthoredServiceBlueprint` JSON. The actual serviceBlueprint editing surface
 * (`<prism-service-blueprint-editor>`) manages its own rich local state and calls
 * `UmbracoBackofficeServiceBlueprintSource` directly for load/save — this model exists only so the
 * generic Umbraco collection/entity-action/workspace-routing machinery has something to list,
 * identify, and delete.
 */
export interface CmsServiceBlueprintEntityModel {
  entityType: typeof UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE;
  unique: string;
  definitionKey: string;
  displayName: string;
}
