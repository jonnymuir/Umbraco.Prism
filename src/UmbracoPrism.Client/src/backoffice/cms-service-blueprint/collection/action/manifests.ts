import { UMB_CMS_SERVICE_BLUEPRINT_ROOT_ENTITY_TYPE, UMB_CMS_SERVICE_BLUEPRINT_COLLECTION_ALIAS } from '../../entity.js';

export const manifests = [
  {
    type: 'collectionAction',
    kind: 'create',
    alias: 'Prism.CollectionAction.CmsServiceBlueprint.Create',
    name: 'Create CMS Service Blueprint Collection Action',
    conditions: [
      {
        alias: 'Umb.Condition.CollectionAlias',
        match: UMB_CMS_SERVICE_BLUEPRINT_COLLECTION_ALIAS,
      },
    ],
  },
  {
    type: 'entityCreateOptionAction',
    alias: 'Prism.EntityCreateOptionAction.CmsServiceBlueprint',
    name: 'Create CMS Service Blueprint Option Action',
    api: () => import('./cms-service-blueprint-create-option-action.js'),
    forEntityTypes: [UMB_CMS_SERVICE_BLUEPRINT_ROOT_ENTITY_TYPE],
    meta: {
      icon: 'icon-diagram',
      label: 'New service blueprint',
      description: 'Author a new CMS Service Blueprint definition, hosted and run entirely in Umbraco.',
    },
  },
];
