import { defineConfig } from 'vite';

export default defineConfig({
  resolve: {
    // A second React copy (e.g. via Storybook's transitive deps) breaks hooks —
    // force a single instance into the bundle.
    dedupe: ['react', 'react-dom'],
  },
  build: {
    // This sends service-blueprint-editor assets to the ServiceBlueprintEditor package static web assets
    outDir: '../UmbracoPrism.ServiceBlueprintEditor/wwwroot/dist',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      input: {
        // Standalone service blueprint editor host page (V1 planning walkthrough)
        'service-blueprint-editor': 'service-blueprint-editor.html',
      },
      output: {
        format: 'es',
        entryFileNames: '[name].js',
        chunkFileNames: '[name]-[hash].js',
      },
    },
  },
});
