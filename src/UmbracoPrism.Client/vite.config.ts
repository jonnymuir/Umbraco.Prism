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
        // CMS Workflow's native backoffice mount — the workflow editor itself, compiled
        // directly into Core's bundle (unlike vite.workflow-editor.config.ts's runtime-only
        // build for hosts like MockBusinessApp with no backoffice of their own)
        'prism-cms-workflow-editor': 'src/backoffice/prism-cms-workflow-editor.ts',
        // Standalone frontend web component — no Umbraco dependencies,
        // safe to load in the public-facing test site shell
        'prism-mobile-nav': 'src/mobile/prism-mobile-nav.ts',
        // Generic live-form runtime: re-evaluates a workflow's declarative
        // calculations client-side and updates bound components in place
        'prism-live-form': 'src/live-form/prism-live-form.ts',
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