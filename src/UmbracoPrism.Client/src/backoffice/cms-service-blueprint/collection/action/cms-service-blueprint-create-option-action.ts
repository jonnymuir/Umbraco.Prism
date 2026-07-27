import { UmbEntityCreateOptionActionBase } from '@umbraco-cms/backoffice/entity-create-option-action';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';

/**
 * Backs the built-in `collectionAction kind: 'create'` button (see
 * `core/collection/action/create/collection-create-action.element.js`) — leaving `getHref()`
 * unset makes it call `execute()` on click instead of navigating, matching the "additional
 * input required" (a definitionKey the author must choose) shape this entity needs, unlike
 * Umbraco's own random-GUID-first entities.
 */
export class UmbCreateCmsServiceBlueprintOptionAction extends UmbEntityCreateOptionActionBase {
  override async execute(): Promise<void> {
    const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
    if (!modalManager) {
      throw new Error('Modal manager context not found.');
    }

    const modalHandler = modalManager.open(this, 'Prism.CreateCmsServiceBlueprintModal', {
      type: 'sidebar',
      size: 'small',
    } as never);

    await modalHandler.onSubmit().catch(() => {
      // Modal cancelled — nothing to do, the collection re-fetches on the next visit regardless.
    });
  }
}

export { UmbCreateCmsServiceBlueprintOptionAction as api };
export default UmbCreateCmsServiceBlueprintOptionAction;
