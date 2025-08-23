// Vite config (JS) to ensure Angular dev server accepts proxied host via nginx
// and HMR connects over WSS on the public domain.
import { defineConfig } from 'vite'

export default defineConfig({
  server: {
    host: true,
    // Allow specific hosts (add others as needed)
    allowedHosts: [
      'creative-systems-inc.com',
      'www.creative-systems-inc.com',
      'localhost',
      '127.0.0.1'
    ],
    hmr: {
      host: 'creative-systems-inc.com',
      protocol: 'wss',
      clientPort: 443
    }
  }
})


