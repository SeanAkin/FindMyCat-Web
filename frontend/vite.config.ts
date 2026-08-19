import path from 'node:path'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const dotnetBackendDevOrigin = 'http://localhost:5120'
const sameOriginProxiedPrefixes = ['/auth', '/api', '/public']

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    proxy: Object.fromEntries(
      sameOriginProxiedPrefixes.map((prefix) => [
        prefix,
        dotnetBackendDevOrigin,
      ]),
    ),
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
