/* global self, caches, fetch, URL, crypto, Response */
/* Public build artifacts only. No API/auth/user/evidence response is cached. */
const build = '__POSEIDON_STATIC_BUILD__'
const manifest = '__POSEIDON_STATIC_MANIFEST__'
const shellCache = `poseidon.offline.static.${build}`
const publicMetadata = '/__poseidon_offline_public_builds__'
const ownedCache = name => /^poseidon\.offline\.static\.(?:v1|[a-f0-9]{64})$/.test(name)
const inventory = Array.isArray(manifest) ? new Map(manifest.map(x => [x.path, x])) : new Map()
const uuid = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
const crewPath = new RegExp(`^/app/crew-roster/${uuid}(?:/crew-profile/${uuid})?$`)
const tugPath = new RegExp(`^/app/tug-job-detail/${uuid}$`)
const dailyPath = new RegExp(`^/app/(?:noon-report|deck-daily-log|engine-daily-log)/${uuid}/?$`)
const vesselId = new RegExp(`^${uuid}$`)
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
  // Snapshot predecessors before this install. A newer concurrently installing
  // build must not be mistaken for an obsolete cache during later activation.
  const predecessors = (await caches.keys()).filter(name => name !== shellCache && ownedCache(name))
  if (predecessors.length > 256) throw new Error('static_shell_cache_inventory_unavailable')
  // Validate the entire exact public build before writing any cache entry.
  // A deployment race or sign-in fallback must never replace a valid shell.
  const responses = await Promise.all([...inventory.values()].map(async entry => [entry.path, await publicArtifact(entry)]))
  const cache = await caches.open(shellCache)
  await Promise.all(responses.map(([path, response]) => cache.put(path, response)))
  // Public cache names only; persist across worker termination while waiting.
  await cache.put(publicMetadata, new Response(JSON.stringify(predecessors), { headers: { 'Content-Type': 'application/json' } }))
  // Default browser lifecycle waits for old controlled tabs to close. Do not
  // skipWaiting, claim existing tabs, post a reload, or touch IndexedDB/keys.
})()))
self.addEventListener('activate', event => event.waitUntil((async () => {
  // Browser activation follows old controlled clients closing naturally.
  // Delete this application's obsolete public builds only, never other caches.
  const cache = await caches.open(shellCache)
  const metadata = await cache.match(publicMetadata)
  let predecessors
  try { predecessors = metadata ? await metadata.json() : [] } catch { return }
  if (!Array.isArray(predecessors) || predecessors.length > 256 || predecessors.some(name => typeof name !== 'string' || name === shellCache || !ownedCache(name))) return
  // Cache collection is optional maintenance and cannot break a valid upgrade.
  await Promise.all(predecessors.map(name => caches.delete(name).catch(() => false)))
})()))
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return
  // Scoped navigation may carry only its closed, noncredential context IDs.
  // Query values never become cache keys; the fallback is the public index only.
  const tug = event.request.mode === 'navigate' && tugPath.test(url.pathname) &&
    [...url.searchParams.keys()].length === 1 && url.searchParams.has('vessel') && vesselId.test(url.searchParams.get('vessel'))
  const dailyKeys = [...url.searchParams.keys()]
  const daily = event.request.mode === 'navigate' && dailyPath.test(url.pathname) &&
    (dailyKeys.length === 0 || dailyKeys.length === 1 && ['id', 'draft'].includes(dailyKeys[0]) &&
      vesselId.test(url.searchParams.get(dailyKeys[0])))
  if (url.search && !tug && !daily) return
  const crew = event.request.mode === 'navigate' && crewPath.test(url.pathname)
  const entry = inventory.get(url.pathname)
  if (!crew && !tug && !daily && (!entry || url.pathname === '/index.html')) return
  event.respondWith((async () => {
    const cache = await caches.open(shellCache)
    if (crew || tug || daily) {
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
