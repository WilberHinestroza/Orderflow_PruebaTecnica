/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// En desarrollo (npm run dev) Vite hace de proxy igual que nginx en Docker:
//   /api/*       -> Orders API
//   /inventory/* -> Inventory Worker
// Así el frontend siempre llama rutas relativas y no hay CORS que configurar.
const ordersUrl = process.env.ORDERS_API_URL ?? 'http://localhost:5080';
const inventoryUrl = process.env.INVENTORY_URL ?? 'http://localhost:5081';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: ordersUrl, rewrite: (p) => p.replace(/^\/api/, '') },
      '/inventory': { target: inventoryUrl, rewrite: (p) => p.replace(/^\/inventory/, '') },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
  },
});
