import { api } from '@/api/http'
import type { CredentialStatusResponse } from '@/api/types'

export const getCredentialStatus = (signal?: AbortSignal) =>
  api.get<CredentialStatusResponse>('/api/credentials', signal)

export const setTraccarToken = (apiToken: string, signal?: AbortSignal) =>
  api.put<void>('/api/credentials/traccar', { apiToken }, signal)

export const setHologramKey = (apiKey: string, signal?: AbortSignal) =>
  api.put<void>('/api/credentials/hologram', { apiKey }, signal)

export const deleteTraccarToken = (signal?: AbortSignal) =>
  api.delete<void>('/api/credentials/traccar', signal)

export const deleteHologramKey = (signal?: AbortSignal) =>
  api.delete<void>('/api/credentials/hologram', signal)
