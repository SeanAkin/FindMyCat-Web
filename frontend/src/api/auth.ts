import { api } from '@/api/http'
import type { SessionResponse } from '@/api/types'

export const getSession = (signal?: AbortSignal) =>
  api.get<SessionResponse>('/auth/session', signal)

export const logout = (signal?: AbortSignal) =>
  api.post<void>('/auth/logout', undefined, signal)
