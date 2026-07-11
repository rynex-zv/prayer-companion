import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [
    tanstackRouter({
      target: 'react',
      autoCodeSplitting: false,
      routesDirectory: './routes',
      generatedRouteTree: './routeTree.gen.ts',
    }),
    react(),
    tailwindcss(),
  ],
  root: 'src',
  base: process.env.PRAY_WEB_TARGET === 'phone' ? './' : '/',
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    outDir: process.env.PRAY_WEB_OUTDIR ?? '../dist',
    emptyOutDir: true
  }
});
