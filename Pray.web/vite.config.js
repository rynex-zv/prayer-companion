import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [react(), tailwindcss()],
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
