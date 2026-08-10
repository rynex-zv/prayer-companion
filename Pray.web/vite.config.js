import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import { fileURLToPath, URL } from 'node:url';
import { readFile } from 'node:fs/promises';
import { extname, isAbsolute, relative, resolve } from 'node:path';

const wasmRoot = fileURLToPath(new URL('./wasm', import.meta.url));

function serveWasmDuringDevelopment() {
  return {
    name: 'serve-pray-wasm',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/wasm', async (request, response, next) => {
        try {
          const relativePath = decodeURIComponent((request.url ?? '').split('?')[0]).replace(/^\/+/, '');
          const filePath = resolve(wasmRoot, relativePath);
          const pathFromRoot = relative(wasmRoot, filePath);
          if (pathFromRoot.startsWith('..') || isAbsolute(pathFromRoot)) {
            next();
            return;
          }

          const contentTypes = {
            '.js': 'text/javascript',
            '.json': 'application/json',
            '.wasm': 'application/wasm',
            '.dat': 'application/octet-stream',
          };
          response.setHeader('Content-Type', contentTypes[extname(filePath)] ?? 'application/octet-stream');
          response.end(await readFile(filePath));
        } catch {
          next();
        }
      });
    },
  };
}

export default defineConfig({
  plugins: [
    serveWasmDuringDevelopment(),
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
    emptyOutDir: true,
    // MAUI evaluates MauiAsset items before invoking the frontend target. Stable
    // phone names keep that evaluated list valid when Vite replaces the bundle.
    rollupOptions: process.env.PRAY_WEB_TARGET === 'phone' ? {
      output: {
        entryFileNames: 'assets/app.js',
        chunkFileNames: 'assets/chunk-[name].js',
        assetFileNames: 'assets/[name][extname]',
      },
    } : undefined,
  }
});
