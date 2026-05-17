import { defineConfig } from 'vite';

export default defineConfig({
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
