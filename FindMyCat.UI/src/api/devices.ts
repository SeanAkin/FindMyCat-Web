import { api } from '@/api/http'
import type {
  DeviceEventResponse,
  DeviceResponse,
  PositionResponse,
} from '@/api/types'

export const listDevices = (signal?: AbortSignal) =>
  api.get<DeviceResponse[]>('/api/devices', signal)

export const getDeviceHistory = (
  deviceId: number,
  from: Date,
  to: Date,
  signal?: AbortSignal,
) => {
  const query = new URLSearchParams({
    from: from.toISOString(),
    to: to.toISOString(),
  })
  return api.get<PositionResponse[]>(
    `/api/devices/${deviceId}/history?${query.toString()}`,
    signal,
  )
}

export const getDeviceState = (deviceId: number, signal?: AbortSignal) =>
  api.get<DeviceEventResponse[]>(`/api/devices/${deviceId}/state`, signal)

export const getDeviceEvents = (
  deviceId: number,
  options: { after?: number; kind?: string; limit?: number } = {},
  signal?: AbortSignal,
) => {
  const query = new URLSearchParams()
  if (options.after !== undefined) query.set('after', String(options.after))
  if (options.kind) query.set('kind', options.kind)
  if (options.limit !== undefined) query.set('limit', String(options.limit))
  const suffix = query.toString()
  return api.get<DeviceEventResponse[]>(
    `/api/devices/${deviceId}/events${suffix ? `?${suffix}` : ''}`,
    signal,
  )
}

export const pingDevice = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/ping`, undefined, signal)

export const markDeviceLost = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/lost`, undefined, signal)

export const markDeviceActive = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/active`, undefined, signal)
