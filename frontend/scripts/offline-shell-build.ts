import { createHash } from 'node:crypto'
import { readFile, writeFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import type { Plugin, ResolvedConfig } from 'vite'

export type StaticArtifact = { fileName: string; bytes: string | Uint8Array }
const manifestMarker = "'__POSEIDON_STATIC_MANIFEST__'"
const versionMarker = "'__POSEIDON_STATIC_BUILD__'"
const ownedAsset = /^assets\/[a-zA-Z0-9_.-]+\.(?:js|css|woff2)$/
const digest = (bytes: string | Uint8Array) => createHash('sha256').update(bytes).digest('hex')

// Only public bundler output enters this manifest. Never inspect environment,
// source maps, API data, auth state, generated credentials or runtime storage.
export function versionOfflineShell(template: string, artifacts: StaticArtifact[]): string {
  if (template.split(manifestMarker).length !== 2 || template.split(versionMarker).length !== 2)
    throw new Error('offline_shell_template_invalid')
  const entries = artifacts.filter(x => x.fileName === 'index.html' || ownedAsset.test(x.fileName))
    .map(x => ({ path: `/${x.fileName}`, sha256: digest(x.bytes), size: Buffer.byteLength(x.bytes) }))
    .sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0)
  if (!entries.some(x => x.path === '/index.html') || !entries.some(x => x.path.endsWith('.js')) ||
      entries.length > 128 || new Set(entries.map(x => x.path)).size !== entries.length ||
      entries.some(x => x.size > 8 * 1024 * 1024) || entries.reduce((sum, x) => sum + x.size, 0) > 32 * 1024 * 1024)
    throw new Error('offline_shell_inventory_invalid')
  const manifest = JSON.stringify(entries)
  const version = digest(template + '\n' + manifest)
  return template.replace(manifestMarker, manifest).replace(versionMarker, JSON.stringify(version))
}

export function offlineShellBuild(): Plugin {
  let config: ResolvedConfig
  return {
    name: 'poseidon-public-offline-shell', apply: 'build', enforce: 'post',
    configResolved(value) { config = value },
    async writeBundle(options, bundle) {
      if (!options.dir) throw new Error('offline_shell_output_directory_required')
      const artifacts: StaticArtifact[] = Object.values(bundle).map(item => ({
        fileName: item.fileName, bytes: item.type === 'chunk' ? item.code : item.source,
      }))
      const template = await readFile(resolve(config.root, 'public/offline-shell.js'), 'utf8')
      // Public copy and all bundle emission have finished before writeBundle.
      // The fixed worker URL changes bytes with every actual public build.
      await writeFile(resolve(config.root, options.dir, 'offline-shell.js'), versionOfflineShell(template, artifacts), 'utf8')
    },
  }
}
