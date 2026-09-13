import type { ReactNode } from 'react'

interface AsyncSectionProps {
  status: 'idle' | 'loading' | 'success' | 'error'
  error: unknown
  skeleton: ReactNode
  errorFallback: ReactNode
  children: ReactNode
}

export function AsyncSection({
  status,
  error,
  skeleton,
  errorFallback,
  children,
}: AsyncSectionProps) {
  if (status === 'loading' || status === 'idle') return <>{skeleton}</>
  if (status === 'error' && error) return <>{errorFallback}</>
  return <>{children}</>
}
