import { create } from 'zustand'
import { getDeviceHistory, listDevices } from '@/api/devices'
import { ApiError, toApiError } from '@/api/http'
import type { DeviceResponse, PositionResponse } from '@/api/types'

export type LoadStatus = 'idle' | 'loading' | 'success' | 'error'

interface DevicesState {
  devices: DeviceResponse[]
  status: LoadStatus
  error: ApiError | null
  fetchDevices: () => Promise<void>

  selectedDeviceId: number | null
  selectDevice: (deviceId: number | null) => void

  history: PositionResponse[]
  historyStatus: LoadStatus
  historyError: ApiError | null
  fetchHistory: (deviceId: number, from: Date, to: Date) => Promise<void>

  lastSentDeviceCommand: Record<number, 'lost' | 'active'>
  setLastSentDeviceCommand: (
    deviceId: number,
    command: 'lost' | 'active',
  ) => void
}

export const useDevicesStore = create<DevicesState>((set, get) => ({
  devices: [],
  status: 'idle',
  error: null,
  fetchDevices: async () => {
    const isSilentPollRefresh = get().status === 'success'
    if (!isSilentPollRefresh) set({ status: 'loading', error: null })
    try {
      const devices = await listDevices()
      set({ devices, status: 'success', error: null })
    } catch (error) {
      if (!isSilentPollRefresh) {
        set({ status: 'error', error: toApiError(error) })
      }
    }
  },

  selectedDeviceId: null,
  selectDevice: (deviceId) =>
    set({
      selectedDeviceId: deviceId,
      history: [],
      historyStatus: 'idle',
      historyError: null,
    }),

  history: [],
  historyStatus: 'idle',
  historyError: null,
  fetchHistory: async (deviceId, from, to) => {
    set({ historyStatus: 'loading', historyError: null })
    const isStaleRequest = () => get().selectedDeviceId !== deviceId
    try {
      const history = await getDeviceHistory(deviceId, from, to)
      if (isStaleRequest()) return
      set({ history, historyStatus: 'success' })
    } catch (error) {
      if (isStaleRequest()) return
      set({ historyStatus: 'error', historyError: toApiError(error) })
    }
  },

  lastSentDeviceCommand: {},
  setLastSentDeviceCommand: (deviceId, command) =>
    set((state) => ({
      lastSentDeviceCommand: {
        ...state.lastSentDeviceCommand,
        [deviceId]: command,
      },
    })),
}))
