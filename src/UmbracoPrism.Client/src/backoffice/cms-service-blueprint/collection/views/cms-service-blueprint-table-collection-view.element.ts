import { css, html } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import { UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX, type CmsServiceBlueprintEntityModel } from '../../entity.js';

interface TableColumn {
  name: string;
  alias: string;
  align?: string;
}

interface TableItem {
  id: string;
  icon: string;
  data: Array<{ columnAlias: string; value: unknown }>;
}

@customElement('prism-cms-service-blueprint-table-collection-view')
export class PrismCmsServiceBlueprintTableCollectionViewElement extends UmbLitElement {
  @state() private _tableColumns: TableColumn[] = [
    { name: 'Name', alias: 'name' },
    { name: 'Definition key', alias: 'definitionKey' },
    { name: '', alias: 'entityActions', align: 'right' },
  ];

  @state() private _tableItems: TableItem[] = [];

  constructor() {
    super();
    this.consumeContext(UMB_COLLECTION_CONTEXT, (context) => {
      this.observe(context?.items, (items) => this.#createTableItems((items ?? []) as CmsServiceBlueprintEntityModel[]), 'prismCmsServiceBlueprintCollectionItems');
    });
  }

  #createTableItems(serviceBlueprints: CmsServiceBlueprintEntityModel[]) {
    this._tableItems = serviceBlueprints.map((serviceBlueprint) => ({
      id: serviceBlueprint.unique,
      icon: 'icon-diagram',
      data: [
        {
          columnAlias: 'name',
          value: html`<a href="${UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX}${encodeURIComponent(serviceBlueprint.unique)}">${serviceBlueprint.displayName || serviceBlueprint.definitionKey}</a>`,
        },
        {
          columnAlias: 'definitionKey',
          value: serviceBlueprint.definitionKey,
        },
        {
          columnAlias: 'entityActions',
          value: html`<umb-entity-actions-table-column-view
            .value=${{ entityType: serviceBlueprint.entityType, unique: serviceBlueprint.unique, name: serviceBlueprint.displayName }}
          ></umb-entity-actions-table-column-view>`,
        },
      ],
    }));
  }

  render() {
    return html`<umb-table .columns=${this._tableColumns} .items=${this._tableItems}></umb-table>`;
  }

  static styles = css`
    :host {
      display: flex;
      flex-direction: column;
    }
  `;
}

export default PrismCmsServiceBlueprintTableCollectionViewElement;
