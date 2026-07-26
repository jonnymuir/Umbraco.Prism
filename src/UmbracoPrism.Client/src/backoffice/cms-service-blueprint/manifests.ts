// Aggregates every extension manifest for the CMS Service Blueprint backoffice screen. Registered via a
// single `type: "bundle"` entry in umbraco-package.json — Umbraco's own convention for a package
// registering many related extensions from one compiled entry point (see e.g.
// packages/documents/umbraco-package.js in @umbraco-cms/backoffice). Each `api`/`element`
// dynamic import below becomes its own code-split chunk.

import { manifests as itemManifests } from './repository/item/manifests.js';
import { manifests as detailManifests } from './repository/detail/manifests.js';
import { manifests as collectionManifests } from './collection/manifests.js';
import { manifests as entityActionManifests } from './entity-actions/manifests.js';
import { manifests as workspaceManifests } from './workspace/manifests.js';
import { manifests as createModalManifests } from './create-modal/manifests.js';

// This file is built by its own dedicated vite.cms-service-blueprint-manifests.config.ts, with
// `preserveEntrySignatures: 'strict'` — without it, Rollup is free to rename/reassign this
// entry's exports arbitrarily (confirmed live: the built module exported an unrelated string
// constant from deep in the graph instead of this array), since nothing *inside* this build
// statically imports `manifests` — it's read only by Umbraco's own "bundle" extension loader
// at runtime. Building it standalone, instead of alongside the other entries in the shared
// vite.config.ts, keeps that fix scoped to the one entry that needs it (a build-wide
// `preserveEntrySignatures` change re-chunks every entry — confirmed it broke
// prism-dashboard.js's `provideContext` at runtime).
export const manifests = [
  ...itemManifests,
  ...detailManifests,
  ...collectionManifests,
  ...entityActionManifests,
  ...workspaceManifests,
  ...createModalManifests,
];
