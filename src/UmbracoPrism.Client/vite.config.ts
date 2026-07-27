import { defineConfig } from 'vite';

export default defineConfig({
  resolve: {
    dedupe: ['react', 'react-dom'],
  },
  build: {
    // This sends compiled JS directly to the Core package static web assets
    outDir: '../UmbracoPrism.Core/wwwroot/dist',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      input: {
        // Umbraco backoffice extension (dashboard + modals)
        'prism-dashboard': 'src/backoffice/index.ts',
        // CMS Service Blueprint's native backoffice screen — a Collection + entity-actions + Workspace
        // (Umbraco 17's own idiomatic shape for a flat, non-hierarchical entity; see the
        // Webhook management package for the pattern this mirrors), registered as a single
        // "bundle" extension in umbraco-package.json. NOT built here — see
        // vite.cms-service-blueprint-manifests.config.ts for why it needs its own build step.
        // The Prism section's "CMS Service Blueprint" tab — renders <umb-collection> from the bundle
        // above (unlike MockBusinessApp's runtime-only build, this compiles directly into
        // Core's bundle, since CMS Service Blueprint's entire point is this native backoffice mount).
        'prism-cms-service-blueprint-tab': 'src/backoffice/cms-service-blueprint/prism-cms-service-blueprint-tab.element.ts',
        // Standalone frontend web component — no Umbraco dependencies,
        // safe to load in the public-facing test site shell
        'prism-mobile-nav': 'src/mobile/prism-mobile-nav.ts',
        // Generic live-form runtime: re-evaluates a service request's declarative
        // calculations client-side and updates bound components in place
        'prism-live-form': 'src/live-form/prism-live-form.ts',
        // Generic file-upload runtime: uploads a chosen file immediately with a real progress
        // bar, independent of prism-live-form (a stage can have file-upload fields with no
        // calculations block at all) — see PrismServiceRequestViewModel.HasFileUploadField for the
        // matching server-side gate on whether this script is even included.
        'prism-file-upload': 'src/file-upload/prism-file-upload.ts',
      },
      output: {
        format: 'es',
        entryFileNames: '[name].js',
        chunkFileNames: '[name]-[hash].js',
      },
      // Tell Vite: "Don't bundle Umbraco's code, it will be there at runtime"
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});