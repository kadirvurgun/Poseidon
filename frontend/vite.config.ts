import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

const proxy = { '/api': 'http://127.0.0.1:5080' }

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true, proxy },
  preview: { port: 4173, strictPort: true, proxy },
  test: { environment: 'jsdom', setupFiles: ['./src/test-setup.ts'], clearMocks: true },
})
