/**
 * ServiceBlueprintActionCatalog — host-extensible source for the action types the
 * editor knows how to render. Prism ships a built-in catalog covering generic
 * actions; hosts compose their own catalog if they ship extra action types.
 */

import type { ActionCatalogEntry } from './types.js';
import { STUB_ACTION_CATALOG } from './types.js';

export interface ServiceBlueprintActionCatalog {
  entries(): Promise<ActionCatalogEntry[]>;
}

/**
 * Returns the generic action types Prism ships out-of-the-box.
 *
 * The catalog is hand-mirrored from the C# `BuiltInActionCatalogProvider` (see
 * `STUB_ACTION_CATALOG` in `types.ts`). Drift between the C# and TS catalogs
 * shows up in MockBusinessApp's reference smoke tests.
 */
export class BuiltInServiceBlueprintActionCatalog implements ServiceBlueprintActionCatalog {
  async entries(): Promise<ActionCatalogEntry[]> {
    return STUB_ACTION_CATALOG.map(entry => JSON.parse(JSON.stringify(entry)) as ActionCatalogEntry);
  }
}
