import { useEffect, useState } from 'react'

// No persistent cache. Focus/visibility/network transitions withdraw old projections
// before a fresh server read. Disconnected revocation cannot be known immediately.
export function useWorkspaceRevalidation() {
  const [epoch, setEpoch] = useState(0)
  useEffect(() => {
    const invalidate = () => setEpoch(x => x + 1)
    window.addEventListener('focus', invalidate)
    window.addEventListener('online', invalidate)
    window.addEventListener('offline', invalidate)
    document.addEventListener('visibilitychange', invalidate)
    const timer = window.setInterval(invalidate, 60000)
    return () => {
      window.removeEventListener('focus', invalidate); window.removeEventListener('online', invalidate); window.removeEventListener('offline', invalidate)
      document.removeEventListener('visibilitychange', invalidate); window.clearInterval(timer)
    }
  }, [])
  return epoch
}
