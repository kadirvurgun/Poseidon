import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

function port(name: string, fallback: number): number {
  const value = process.env[name]
  if (value === undefined) return fallback
  if (!/^[0-9]+$/.test(value) || Number(value) < 1024 || Number(value) > 65535) {
    throw new Error(`Invalid ${name}`)
  }
  return Number(value)
}
const apiPort = port('POSEIDON_API_PORT', 5080)
const uiPort = port('POSEIDON_UI_PORT', 4173)
if (apiPort === uiPort) throw new Error('API and UI ports must differ')
const proxy = { '/api': `http://127.0.0.1:${apiPort}`, '/ops': { target: 'http://127.0.0.1:8765', changeOrigin: true } }

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true, proxy },
  preview: { host: '127.0.0.1', port: uiPort, strictPort: true, proxy },
  test: { environment: 'jsdom', setupFiles: ['./src/test-setup.ts'], clearMocks: true,
    reporters: ['default', './scripts/strict-test-reporter.ts'] },
})
