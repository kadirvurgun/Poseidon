/* global self, caches, fetch, URL, Request */
/* Static application shell only. Never cache API responses, signed HTML, query
 * strings, user data, files, provider traffic or authentication state. */
const shellCache = 'poseidon.offline.static.v1'
const asset = path => /^\/assets\/[a-zA-Z0-9_.-]+\.(js|css|woff2)$/.test(path)
const eligible = url => url.origin === self.location.origin && !url.search && asset(url.pathname)
async function storeStatic(cache, url) {
  const response = await fetch(url, { credentials: 'omit', cache: 'no-cache' })
  if (!response.ok || response.redirected) throw new Error('static_shell_unavailable')
  await cache.put(url, response.clone()); return response
}
self.addEventListener('install', event => event.waitUntil((async () => {
  const cache = await caches.open(shellCache)
  const shell = await storeStatic(cache, '/index.html')
  if (!(shell.headers.get('content-type') || '').includes('text/html')) throw new Error('static_shell_unavailable')
  const html = await shell.text()
  // The build-owned shell has no interpolated user state. Only exact static asset URLs are followed.
  const paths = [...html.matchAll(/(?:src|href)=["'](\/assets\/[a-zA-Z0-9_.-]+\.(?:js|css))["']/g)].map(x => x[1])
  if (!paths.length || paths.length > 32) throw new Error('static_shell_unavailable')
  await Promise.all([...new Set(paths)].map(path => storeStatic(cache, path)))
  await self.skipWaiting()
})()))
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()))
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url)
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return
  // Controlled Crew workspace navigation may boot the static shell disconnected.
  // It still requires explicit online server authorization before decrypting/capturing after restart.
  const crew = event.request.mode === 'navigate' && !url.search && /^\/app\/crew-roster\/[0-9a-f-]{36}(?:\/crew-profile\/[0-9a-f-]{36})?$/.test(url.pathname)
  if (!crew && !eligible(url)) return
  event.respondWith((async () => {
    const cache = await caches.open(shellCache)
    if (crew) {
      try { return await fetch(event.request) }
      catch { const response = await cache.match('/index.html'); if (response) return response; throw new Error('static_shell_unavailable') }
    }
    // Bounded same-build static inventory; never evict unsent application records.
    const existing = await cache.match(url.href)
    if (existing) return existing
    const response = await fetch(new Request(event.request, { credentials: 'omit' }))
    if (response.ok && !response.redirected && (await cache.keys()).length < 128) await cache.put(url.href, response.clone())
    return response
  })())
})
