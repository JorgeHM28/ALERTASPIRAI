import { useEffect, useState } from 'react'
import { api, ErrorApi } from '../api'

export function usePublicResource<T>(path: string | null, onSuccess?: () => void) {
  const [data, setData] = useState<T>()
  const [error, setError] = useState<number | null>(null)
  const [loading, setLoading] = useState(Boolean(path))
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    if (!path) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    api<T>(path, { signal: controller.signal })
      .then((value) => {
        if (controller.signal.aborted) return
        setData(value)
        onSuccess?.()
      })
      .catch((reason) => {
        if (!controller.signal.aborted) setError(reason instanceof ErrorApi ? reason.estado : 0)
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [path, attempt, onSuccess])
  return { data, error, loading, retry: () => setAttempt((n) => n + 1) }
}
