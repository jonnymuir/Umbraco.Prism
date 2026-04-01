import { defineConfig } from 'vite';

export default defineConfig({
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