import { css, html } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import { UMB_CMS_WORKFLOW_EDIT_PATH_PREFIX, type CmsWorkflowEntityModel } from '../../entity.js';

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

@customElement('prism-cms-workflow-table-collection-view')
export class PrismCmsWorkflowTableCollectionViewElement extends UmbLitElement {
  @state() private _tableColumns: TableColumn[] = [
    { name: 'Name', alias: 'name' },
    { name: 'Definition key', alias: 'definitionKey' },
    { name: '', alias: 'entityActions', align: 'right' },
  ];

  @state() private _tableItems: TableItem[] = [];

  constructor() {
    super();
    this.consumeContext(UMB_COLLECTION_CONTEXT, (context) => {
      this.observe(context?.items, (items) => this.#createTableItems((items ?? []) as CmsWorkflowEntityModel[]), 'prismCmsWorkflowCollectionItems');
    });
  }

  #createTableItems(workflows: CmsWorkflowEntityModel[]) {
    this._tableItems = workflows.map((workflow) => ({
      id: workflow.unique,
      icon: 'icon-diagram',
      data: [
        {
          columnAlias: 'name',
          value: html`<a href="${UMB_CMS_WORKFLOW_EDIT_PATH_PREFIX}${encodeURIComponent(workflow.unique)}">${workflow.displayName || workflow.definitionKey}</a>`,
        },
        {
          columnAlias: 'definitionKey',
          value: workflow.definitionKey,
        },
        {
          columnAlias: 'entityActions',
          value: html`<umb-entity-actions-table-column-view
            .value=${{ entityType: workflow.entityType, unique: workflow.unique, name: workflow.displayName }}
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

export default PrismCmsWorkflowTableCollectionViewElement;
