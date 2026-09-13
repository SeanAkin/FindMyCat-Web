import { api } from '@/api/http'
import type { DeviceResponse, PositionResponse } from '@/api/types'

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

export const pingDevice = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/ping`, undefined, signal)

export const markDeviceLost = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/lost`, undefined, signal)

export const markDeviceActive = (deviceId: number, signal?: AbortSignal) =>
  api.post<void>(`/api/devices/${deviceId}/active`, undefined, signal)
