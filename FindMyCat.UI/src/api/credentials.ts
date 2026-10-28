import { api } from '@/api/http'
import type { CredentialStatusResponse, IngestKeyResponse } from '@/api/types'

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

export const rotateIngestKey = (signal?: AbortSignal) =>
  api.post<IngestKeyResponse>('/api/credentials/ingest-key', undefined, signal)

export const deleteIngestKey = (signal?: AbortSignal) =>
  api.delete<void>('/api/credentials/ingest-key', signal)
