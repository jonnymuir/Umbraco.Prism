import { defineConfig } from 'vite';

// A dedicated build step for prism-cms-workflow-manifests.js only. This entry's sole content
// is a pure-data `export const manifests = [...]` — no side-effecting code (no @customElement
// decorators, nothing that registers itself globally) — read by Umbraco's own "bundle"
// extension loader via `Object.keys(importedModule)`, which cares about the *exported values*,
// not the compiled export *names*.
//
// Building this alongside every other entry in the shared vite.config.ts (which has no
// `preserveEntrySignatures` set) let Rollup rename/reassign the entry's export arbitrarily —
// confirmed live: the built module exported a single-string constant from deep inside the
// dependency graph instead of the `manifests` array, so umbraco-package.json's "bundle"
// extension loaded a file that looked fine (right size, right chunks) but silently registered
// nothing. `preserveEntrySignatures: 'strict'` fixes it — but set globally in vite.config.ts,
// it re-chunks *every* entry (confirmed: broke prism-dashboard.js's `provideContext` at
// runtime), because the other entries (dashboard, mobile-nav, live-form, tab) all rely on
// custom-element side effects, not read-my-named-export semantics. Splitting this one entry
// into its own build step scopes the fix to exactly the entry that needs it.
export default defineConfig({
  build: {
    outDir: '../UmbracoPrism.Core/wwwroot/dist',
    // Never wipe the directory here — vite.config.ts's own build already populated it with
    // every other entry; this step only adds to it.
    emptyOutDir: false,
    sourcemap: true,
    rollupOptions: {
      input: {
        'prism-cms-workflow-manifests': 'src/backoffice/cms-workflow/manifests.ts',
      },
      output: {
        format: 'es',
        entryFileNames: '[name].js',
        chunkFileNames: '[name]-[hash].js',
      },
      external: [/^@umbraco-cms\/backoffice/],
      preserveEntrySignatures: 'strict',
    },
  },
});
