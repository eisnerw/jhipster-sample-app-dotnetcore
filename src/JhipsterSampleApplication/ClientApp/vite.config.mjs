import { defineConfig } from 'vite';

// ESM Vite config so Angular's Vite dev server reliably picks it up
export default defineConfig({
  server: {
    host: true,
    allowedHosts: ['creative-systems-inc.com', 'www.creative-systems-inc.com', 'localhost', '127.0.0.1'],
    hmr: {
      host: 'creative-systems-inc.com',
      protocol: 'wss',
      clientPort: 443,
    },
  },
});
