import { useCallback, useEffect, useState } from 'react'
import { parseStatus } from './contract'
import type { OpsStatus } from './contract'

export function useOpsStatus() {
  const [data, setData] = useState<OpsStatus | null>(null)
  const [error, setError] = useState(false)
  const [loading, setLoading] = useState(true)
  const [receivedAt, setReceivedAt] = useState<number | null>(null)
  const [attempt, setAttempt] = useState(0)
  const retry = useCallback(() => setAttempt(n => n + 1), [])
  useEffect(() => {
    let mounted = true
    let next: ReturnType<typeof setTimeout> | undefined
    let timeout: ReturnType<typeof setTimeout> | undefined
    let controller: AbortController
    async function poll() {
      controller = new AbortController()
      setLoading(true)
      try {
        const timedOut = new Promise<never>((_, reject) => {
          timeout = setTimeout(() => { controller.abort(); reject(new Error('Timeout')) }, 8_000)
        })
        const result = await Promise.race([
          fetch('/ops/status', { signal: controller.signal, cache: 'no-store' }).then(async response => {
            if (!response.ok) throw new Error('HTTP failure')
            return parseStatus(await response.json())
          }), timedOut,
        ])
        if (mounted) { setData(result); setReceivedAt(Date.now()); setError(false) }
      } catch { if (mounted) setError(true) }
      finally {
        clearTimeout(timeout)
        if (mounted) { setLoading(false); next = setTimeout(() => void poll(), 5_000) }
      }
    }
    void poll()
    return () => { mounted = false; clearTimeout(next); clearTimeout(timeout); controller?.abort() }
  }, [attempt])
  return { data, error, loading, receivedAt, retry }
}
