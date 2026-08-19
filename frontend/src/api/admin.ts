import { api } from '@/api/http'
import type { AllowedEmailResponse, UserResponse, UserRole } from '@/api/types'

export const listAllowedEmails = (signal?: AbortSignal) =>
  api.get<AllowedEmailResponse[]>('/api/admin/allowed-emails', signal)

export const addAllowedEmail = (email: string, signal?: AbortSignal) =>
  api.post<AllowedEmailResponse>('/api/admin/allowed-emails', { email }, signal)

export const removeAllowedEmail = (email: string, signal?: AbortSignal) =>
  api.delete<void>(
    `/api/admin/allowed-emails/${encodeURIComponent(email)}`,
    signal,
  )

export const listUsers = (signal?: AbortSignal) =>
  api.get<UserResponse[]>('/api/admin/users', signal)

export const setUserRole = (
  userId: string,
  role: UserRole,
  signal?: AbortSignal,
) => api.put<void>(`/api/admin/users/${userId}/role`, { role }, signal)
