import { defineConfig } from 'vite';

export default defineConfig({
  resolve: {
    // A second React copy (e.g. via Storybook's transitive deps) breaks hooks —
    // force a single instance into the bundle.
    dedupe: ['react', 'react-dom'],
  },
  build: {
    // This sends workflow-editor assets to the WorkflowEditor package static web assets
    outDir: '../UmbracoPrism.WorkflowEditor/wwwroot/dist',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      input: {
        // Standalone workflow editor host page (V1 planning walkthrough)
        'workflow-editor': 'workflow-editor.html',
      },
      output: {
        format: 'es',
        entryFileNames: '[name].js',
        chunkFileNames: '[name]-[hash].js',
      },
    },
  },
});
