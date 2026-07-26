import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbEntityDetailWorkspaceContextBase } from '@umbraco-cms/backoffice/workspace';
import { UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE, UMB_CMS_SERVICE_BLUEPRINT_WORKSPACE_ALIAS, type CmsServiceBlueprintEntityModel } from '../entity.js';
import { UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS } from '../repository/detail/manifests.js';

/**
 * Deliberately minimal — only enough to resolve "which definitionKey is this route editing" for
 * the details view to hand to `<prism-service-blueprint-editor>`. There is no "create" route (creation
 * happens through a dedicated modal collecting the definitionKey upfront — see
 * `create-modal/` — since unlike Umbraco's own entities this one's identity is a human-chosen
 * slug, not a random GUID minted after the fact) and no generic Save workspaceAction is
 * registered: the editor owns its own save flow via `UmbracoBackofficeServiceBlueprintSource` entirely
 * independently of this context's `data`/`submit()` machinery.
 */
export class UmbCmsServiceBlueprintWorkspaceContext extends UmbEntityDetailWorkspaceContextBase<CmsServiceBlueprintEntityModel> {
  constructor(host: UmbControllerHost) {
    super(host, {
      entityType: UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE,
      workspaceAlias: UMB_CMS_SERVICE_BLUEPRINT_WORKSPACE_ALIAS,
      detailRepositoryAlias: UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS,
    });

    this.routes.setRoutes([
      {
        path: 'edit/:unique',
        component: () => import('./cms-service-blueprint-workspace-editor.element.js'),
        setup: (_component, info) => {
          this.load(info.match.params.unique);
        },
      },
    ]);
  }

  /** Definition key of the serviceBlueprint currently loaded into this workspace, if any. */
  getDefinitionKey(): string | undefined {
    return this.getData()?.definitionKey;
  }
}

export { UmbCmsServiceBlueprintWorkspaceContext as api };
export default UmbCmsServiceBlueprintWorkspaceContext;
