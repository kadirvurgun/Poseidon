/* global self, caches, fetch, URL, crypto */
/* Public build artifacts only. No API/auth/user/evidence response is cached. */
const build = '__POSEIDON_STATIC_BUILD__'
const manifest = '__POSEIDON_STATIC_MANIFEST__'
const shellCache = `poseidon.offline.static.${build}`
const ownedCache = name => /^poseidon\.offline\.static\.(?:v1|[a-f0-9]{64})$/.test(name)
const inventory = Array.isArray(manifest) ? new Map(manifest.map(x => [x.path, x])) : new Map()
const uuid = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
const crewPath = new RegExp(`^/app/crew-roster/${uuid}(?:/crew-profile/${uuid})?$`)
async function publicArtifact(entry) {
  const response = await fetch(entry.path, { credentials: 'omit', cache: 'no-cache', redirect: 'error' })
  if (!response.ok || response.redirected) throw new Error('static_shell_unavailable')
  const bytes = await response.clone().arrayBuffer()
  if (bytes.byteLength !== entry.size) throw new Error('static_shell_integrity')
  const sha = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), x => x.toString(16).padStart(2, '0')).join('')
  if (sha !== entry.sha256) throw new Error('static_shell_integrity')
  return response
}
self.addEventListener('install', event => event.waitUntil((async () => {
  if (!/^[a-f0-9]{64}$/.test(build) || inventory.size < 2 || inventory.size > 128 || !inventory.has('/index.html'))
    throw new Error('static_shell_build_required')
  // Validate the entire exact public build before writing any cache entry.
  // A deployment race or sign-in fallback must never replace a valid shell.
  const responses = await Promise.all([...inventory.values()].map(async entry => [entry.path, await publicArtifact(entry)]))
  const cache = await caches.open(shellCache)
  await Promise.all(responses.map(([path, response]) => cache.put(path, response)))
  // Default browser lifecycle waits for old controlled tabs to close. Do not
  // skipWaiting, claim existing tabs, post a reload, or touch IndexedDB/keys.
})()))
self.addEventListener('activate', event => event.waitUntil((async () => {
  // Browser activation follows old controlled clients closing naturally.
  // Delete this application's obsolete public builds only, never other caches.
  const names = await caches.keys()
  await Promise.all(names.filter(name => name !== shellCache && ownedCache(name)).map(name => caches.delete(name)))
})()))
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)
  if (event.request.method !== 'GET' || url.origin !== self.location.origin || url.search) return
  const crew = event.request.mode === 'navigate' && crewPath.test(url.pathname)
  const entry = inventory.get(url.pathname)
  if (!crew && (!entry || url.pathname === '/index.html')) return
  event.respondWith((async () => {
    const cache = await caches.open(shellCache)
    if (crew) {
      try { return await fetch(event.request) }
      catch { const response = await cache.match('/index.html'); if (response) return response; throw new Error('static_shell_unavailable') }
    }
    const existing = await cache.match(url.pathname)
    if (existing) return existing
    const response = await publicArtifact(entry)
    await cache.put(url.pathname, response.clone())
    return response
  })())
})
