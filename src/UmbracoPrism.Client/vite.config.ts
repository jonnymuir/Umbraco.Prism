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
        // Generic live-form runtime: re-evaluates a service request's declarative
        // calculations client-side and updates bound components in place
        'prism-live-form': 'src/live-form/prism-live-form.ts',
        // Generic file-upload runtime: uploads a chosen file immediately with a real progress
        // bar, independent of prism-live-form (a stage can have file-upload fields with no
        // calculations block at all) — see Wayfinder.Umbraco's ServiceRequestPageViewModel.HasFileUploadField
        // for the matching server-side gate on whether this script is even included.
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