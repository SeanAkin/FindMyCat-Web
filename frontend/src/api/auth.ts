import { api } from '@/api/http'
import type { SessionResponse } from '@/api/types'

export const getSession = (signal?: AbortSignal) =>
  api.get<SessionResponse>('/auth/session', signal)

export const logout = (signal?: AbortSignal) =>
  api.post<void>('/auth/logout', undefined, signal)

export const register = (
  email: string,
  password: string,
  displayName: string,
  signal?: AbortSignal,
) =>
  api.post<SessionResponse>(
    '/auth/register',
    { email, password, displayName },
    signal,
  )

export const login = (email: string, password: string, signal?: AbortSignal) =>
  api.post<SessionResponse>('/auth/login', { email, password }, signal)
