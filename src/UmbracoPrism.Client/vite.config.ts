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
        // Standalone frontend web component — no Umbraco dependencies,
        // safe to load in the public-facing test site shell
        'prism-mobile-nav': 'src/mobile/prism-mobile-nav.ts',
        // Generic file-upload runtime: uploads a chosen file immediately with a real progress
        // bar — a genuine host extension point, unlike the live-form runtime, which this package
        // no longer ships its own copy of (see Wayfinder.Umbraco's ServiceRequestPageViewModel.HasFileUploadField
        // for the matching server-side gate on whether this script is even included). Blueprint
        // stages now load Wayfinder.Rendering.GovUk's own wayfinder-live-form.js directly for
        // declarative-calculation recalculation, instead of this package's former
        // prism-live-form.ts (deleted — it duplicated that script's logic, drifting from
        // Wayfinder's own fixes until this).
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