// The "CMS Workflow" tab in the Prism section — renders the generic <umb-collection> element
// directly (the same one Umbraco's own `workspaceView kind: "collection"` uses internally,
// see core/collection/workspace-view/collection-workspace-view.element.js), instead of routing
// through a "root workspace" URL. That was the original design, but stock Umbraco only ever
// reaches a root workspace via a `menuItem` click (which seeds UMB_ENTITY_CONTEXT before
// navigating) — Prism.Section has no menu system, and a raw window.location.href redirect
// skips that context-seeding entirely, leaving the collection waiting forever for an entity
// context that never arrives (confirmed live: zero requests to the collection's own endpoint
// ever fired). Providing UmbEntityContext ourselves and rendering <umb-collection> straight
// inside this dashboard tab sidesteps the whole workspace-routing question for the list.

import { LitElement, css, html } from 'lit';
import { customElement } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UmbEntityContext } from '@umbraco-cms/backoffice/entity';
import { UMB_CMS_WORKFLOW_COLLECTION_ALIAS, UMB_CMS_WORKFLOW_ROOT_ENTITY_TYPE } from './entity.js';

@customElement('prism-cms-workflow-tab')
export class PrismCmsWorkflowTabElement extends UmbElementMixin(LitElement) {
  constructor() {
    super();
    // The "Create" option-action (see collection/action/manifests.ts) discovers itself via
    // UMB_ENTITY_CONTEXT.entityType — a workspace would provide this automatically; a bare
    // dashboard has to do it itself.
    const entityContext = new UmbEntityContext(this);
    entityContext.setEntityType(UMB_CMS_WORKFLOW_ROOT_ENTITY_TYPE);
    entityContext.setUnique(null);
  }

  render() {
    return html`
      <umb-collection data-mark="collection:cms-workflow" alias=${UMB_CMS_WORKFLOW_COLLECTION_ALIAS}></umb-collection>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }
  `;
}

export default PrismCmsWorkflowTabElement;
