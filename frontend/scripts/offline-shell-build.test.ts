import { readFileSync } from 'node:fs'
import { webcrypto } from 'node:crypto'
import { runInNewContext } from 'node:vm'
import { describe, expect, it, vi } from 'vitest'
import { versionOfflineShell, type StaticArtifact } from './offline-shell-build'

const template = readFileSync(new URL('../public/offline-shell.js', import.meta.url), 'utf8')
const fixtures: StaticArtifact[] = [
  { fileName: 'index.html', bytes: '<!doctype html><div id="root"></div><script src="/assets/app-a1.js"></script>' },
  { fileName: 'assets/app-a1.js', bytes: 'export const publicBuild = 1' },
  { fileName: 'assets/style-b2.css', bytes: 'body { color: black }' },
]
const source = (artifacts = fixtures) => versionOfflineShell(template, artifacts)
const cacheName = (worker: string) => `poseidon.offline.static.${JSON.parse(worker.match(/const build = ("[a-f0-9]+")/)![1])}`
type WorkerEvent = { request?: { url: string; method: string; mode: string }; waitUntil: (value: Promise<unknown>) => void; respondWith: (value: Promise<Response>) => void }

function harness(worker = source(), stores = new Map<string, Map<string, Response>>()) {
  const listeners = new Map<string, (event: WorkerEvent) => void>()
  const network = new Map(fixtures.map(x => [`/${x.fileName}`, new Response(typeof x.bytes === 'string' ? x.bytes : Uint8Array.from(x.bytes).buffer)]))
  const fetcher = vi.fn(async (input: string | { url: string }, options?: unknown) => {
    void options
    const path = typeof input === 'string' ? input : new URL(input.url).pathname
    const response = network.get(path)
    if (!response) throw new Error('network_unavailable')
    return response.clone()
  })
  const cacheStorage = {
    open: vi.fn(async (name: string) => {
      if (!stores.has(name)) stores.set(name, new Map())
      const store = stores.get(name)!
      return { put: async (path: string, response: Response) => { store.set(path, response.clone()) },
        match: async (path: string) => store.get(path)?.clone() }
    }),
    keys: vi.fn(async () => [...stores.keys()]),
    delete: vi.fn(async (name: string) => stores.delete(name)),
  }
  const skipWaiting = vi.fn(), claim = vi.fn(), reload = vi.fn()
  runInNewContext(worker, {
    self: { location: { origin: 'https://poseidon.test' }, skipWaiting, clients: { claim, reload },
      addEventListener: (name: string, callback: (event: WorkerEvent) => void) => listeners.set(name, callback) },
    caches: cacheStorage, fetch: fetcher, URL, crypto: webcrypto, Uint8Array, Response,
  })
  function lifecycle(name: string) {
    let pending: Promise<unknown> | undefined
    listeners.get(name)!({ waitUntil: value => { pending = value }, respondWith: () => { throw new Error('unexpected_response') } })
    if (!pending) throw new Error('missing_lifecycle_promise')
    return pending
  }
  function request(path: string, method = 'GET', mode = 'navigate') {
    let response: Promise<Response> | undefined
    listeners.get('fetch')!({ request: { url: new URL(path, 'https://poseidon.test').href, method, mode },
      respondWith: value => { response = value }, waitUntil: () => { throw new Error('unexpected_wait') } })
    return response
  }
  return { stores, network, fetcher, cacheStorage, lifecycle, request, skipWaiting, claim, reload }
}

describe('public offline shell build and safe update lifecycle', () => {
  it('versions the actual public bytes deterministically, independent of bundle enumeration', () => {
    expect(source([...fixtures].reverse())).toBe(source())
    expect(cacheName(source([{ ...fixtures[0], bytes: '<!doctype html>new shell' }, ...fixtures.slice(1)]))).not.toBe(cacheName(source()))
    expect(cacheName(source([fixtures[0], { ...fixtures[1], bytes: 'changed code' }, fixtures[2]]))).not.toBe(cacheName(source()))
    expect(source()).not.toContain('__POSEIDON_STATIC_')
  })
  it('excludes source maps, API/private artifacts, malformed paths and unrelated public files', () => {
    expect(source([...fixtures, { fileName: 'assets/app.js.map', bytes: 'private source' },
      { fileName: 'api/profile.json', bytes: 'private user' }, { fileName: 'assets/../auth.js', bytes: 'bad' }])).toBe(source())
    expect(() => source([fixtures[0], fixtures[0], fixtures[1]])).toThrow('offline_shell_inventory_invalid')
    expect(() => source([fixtures[0]])).toThrow('offline_shell_inventory_invalid')
    expect(() => versionOfflineShell(template.replace("'__POSEIDON_STATIC_BUILD__'", "'bad'"), fixtures)).toThrow('offline_shell_template_invalid')
    expect(() => source([fixtures[0], { fileName: 'assets/large.js', bytes: new Uint8Array(8 * 1024 * 1024 + 1) }])).toThrow('offline_shell_inventory_invalid')
  })
  it('precaches only integrity-checked public bytes without credentials or forcing current tabs', async () => {
    const h = harness(); await h.lifecycle('install')
    expect([...h.stores.get(cacheName(source()))!.keys()].sort()).toEqual([...fixtures.map(x => `/${x.fileName}`), '/__poseidon_offline_public_builds__'].sort())
    for (const call of h.fetcher.mock.calls) expect(call[1]).toEqual({ credentials: 'omit', cache: 'no-cache', redirect: 'error' })
    expect(h.skipWaiting).not.toHaveBeenCalled(); expect(h.claim).not.toHaveBeenCalled(); expect(h.reload).not.toHaveBeenCalled()
  })
  it('rejects deployment races and sign-in HTML before replacing an existing valid shell', async () => {
    const h = harness(); h.stores.set('poseidon.offline.static.v1', new Map([['/index.html', new Response('old public shell')]]))
    h.network.set('/assets/app-a1.js', new Response('<html>private sign-in response</html>'))
    await expect(h.lifecycle('install')).rejects.toThrow('static_shell_integrity')
    expect(h.cacheStorage.open).not.toHaveBeenCalled()
    expect(await h.stores.get('poseidon.offline.static.v1')!.get('/index.html')!.text()).toBe('old public shell')
  })
  it('cleans only obsolete owned public versions on natural activation, retaining unrelated caches', async () => {
    const h = harness()
    const old = `poseidon.offline.static.${'a'.repeat(64)}`
    for (const name of [old, 'poseidon.offline.static.v1', 'another-app', 'poseidon.offline.static.private-records']) h.stores.set(name, new Map())
    await h.lifecycle('install')
    const future = `poseidon.offline.static.${'b'.repeat(64)}`
    h.stores.set(future, new Map()) // A later install completed while this worker waited.
    const restarted = harness(source(), h.stores) // Waiting worker process was terminated.
    await restarted.lifecycle('activate')
    expect(restarted.cacheStorage.delete.mock.calls.flat().sort()).toEqual([old, 'poseidon.offline.static.v1'].sort())
    expect(h.stores.has(future)).toBe(true)
    expect(h.stores.has(cacheName(source()))).toBe(true)
    expect(h.stores.has('another-app')).toBe(true); expect(h.stores.has('poseidon.offline.static.private-records')).toBe(true)
    expect(h.claim).not.toHaveBeenCalled(); expect(h.reload).not.toHaveBeenCalled()
  })
  it('does not intercept API, evidence, auth, query, cross-origin, malformed context or mutation requests', () => {
    const h = harness()
    for (const path of ['/api/session', '/api/private/file', '/auth/callback', '/assets/app-a1.js?token=private',
      'https://other.test/assets/app-a1.js', '/app/crew-roster/------------------------------------', '/index.html', '/assets/unknown.js'])
      expect(h.request(path)).toBeUndefined()
    expect(h.request('/assets/app-a1.js', 'POST', 'cors')).toBeUndefined()
    expect(h.fetcher).not.toHaveBeenCalled(); expect(h.cacheStorage.open).not.toHaveBeenCalled()
  })
  it('boots only the cached public shell disconnected and never stores a signed navigation response', async () => {
    const h = harness(); await h.lifecycle('install')
    const path = '/app/crew-roster/30000000-0000-0000-0000-000000000001'
    expect(await (await h.request(path))!.text()).toBe(fixtures[0].bytes)
    h.network.set(path, new Response('signed personal navigation'))
    expect(await (await h.request(path))!.text()).toBe('signed personal navigation')
    expect(await h.stores.get(cacheName(source()))!.get('/index.html')!.clone().text()).toBe(fixtures[0].bytes)
    expect(h.stores.get(cacheName(source()))!.has(path)).toBe(false)
  })
  it('refuses modified assets on cache miss rather than persisting unexpected bytes', async () => {
    const h = harness(); await h.lifecycle('install')
    const cache = h.stores.get(cacheName(source()))!; cache.delete('/assets/app-a1.js')
    h.network.set('/assets/app-a1.js', new Response('unexpected payload'))
    await expect(h.request('/assets/app-a1.js', 'GET', 'cors')).rejects.toThrow('static_shell_integrity')
    expect(cache.has('/assets/app-a1.js')).toBe(false); expect(cache.has('/index.html')).toBe(true)
  })
})
