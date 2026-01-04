import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      // This is the entry point we created in the previous step
      entry: 'src/index.ts', 
      formats: ['es'],
      fileName: 'prism-dashboard',
    },
    // This sends the compiled JS directly to your TestSite's App_Plugins
    outDir: '../UmbracoPrism.TestSite/wwwroot/App_Plugins/UmbracoPrism/dist',
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      // Tell Vite: "Don't bundle Umbraco's code, it will be there at runtime"
      external: [/^@umbraco-cms\/backoffice/],
    },
  },
});