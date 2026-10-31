import { create } from 'zustand'
import {
  getDeviceEvents,
  getDeviceHistory,
  getDeviceState,
  listDevices,
} from '@/api/devices'
import {
  createDevicesHubConnection,
  reconnectDelayMs,
  type DevicesHubConnection,
} from '@/api/devicesHub'
import { ApiError, toApiError } from '@/api/http'
import type {
  DeviceEventResponse,
  DeviceResponse,
  DeviceStateResponse,
  DeviceStatusResponse,
  PositionResponse,
  TraccarConnectionStatusResponse,
} from '@/api/types'
import type { TraccarProblemCode } from '@/lib/deviceErrors'

export type LoadStatus = 'idle' | 'loading' | 'success' | 'error'

export type HubStatus =
  'stopped' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export type DeviceSyncMode = 'off' | 'live' | 'polling'

export const DEVICE_POLL_INTERVAL_MS = 15_000
export const TRACCAR_OUTAGE_GRACE_MS = 10_000

export interface DevicesState {
  devices: DeviceResponse[]
  status: LoadStatus
  error: ApiError | null
  fetchDevices: () => Promise<void>

  hubStatus: HubStatus
  traccarConnection: TraccarConnectionStatusResponse | null
  isTraccarOutageOngoing: boolean
  startLiveUpdates: () => Promise<void>
  stopLiveUpdates: () => void

  selectedDeviceId: number | null
  selectDevice: (deviceId: number | null) => void

  history: PositionResponse[]
  historyStatus: LoadStatus
  historyError: ApiError | null
  fetchHistory: (deviceId: number, from: Date, to: Date) => Promise<void>

  deviceState: DeviceStateResponse
  deviceStateStatus: LoadStatus
  deviceStateError: ApiError | null
  fetchDeviceState: (deviceId: number) => Promise<void>

  deviceEvents: DeviceEventResponse[]
  deviceEventsStatus: LoadStatus
  deviceEventsError: ApiError | null
  fetchDeviceEvents: (deviceId: number) => Promise<void>

  lastSentDeviceCommand: Record<number, 'lost' | 'active'>
  setLastSentDeviceCommand: (
    deviceId: number,
    command: 'lost' | 'active',
  ) => void
}

let hubConnection: DevicesHubConnection | null = null
let pollIntervalId: number | undefined
let traccarOutageGraceTimeoutId: number | undefined
let connectRetryTimeoutId: number | undefined

function mergeDeviceStatuses(
  devices: DeviceResponse[],
  updates: DeviceStatusResponse[],
): DeviceResponse[] {
  const updatesById = new Map(updates.map((update) => [update.id, update]))
  const knownIds = new Set(devices.map((device) => device.id))
  const updatedDevices = devices.map((device) => {
    const update = updatesById.get(device.id)
    return update ? { ...update, position: device.position } : device
  })
  const newDevices = updates
    .filter((update) => !knownIds.has(update.id))
    .map((update) => ({ ...update, position: null }))
  return [...updatedDevices, ...newDevices]
}

function replacePositions(
  devices: DeviceResponse[],
  positions: PositionResponse[],
): DeviceResponse[] {
  const positionsByDeviceId = new Map(
    positions.map((position) => [position.deviceId, position]),
  )
  return devices.map((device) => {
    const position = positionsByDeviceId.get(device.id)
    return position ? { ...device, position } : device
  })
}

function clearTraccarOutageGraceTimeout() {
  window.clearTimeout(traccarOutageGraceTimeoutId)
  traccarOutageGraceTimeoutId = undefined
}

function clearConnectRetryTimeout() {
  window.clearTimeout(connectRetryTimeoutId)
  connectRetryTimeoutId = undefined
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

  hubStatus: 'stopped',
  traccarConnection: null,
  isTraccarOutageOngoing: false,
  startLiveUpdates: async () => {
    if (hubConnection) return

    const applyTraccarConnection = (
      traccarConnection: TraccarConnectionStatusResponse,
    ) => {
      const previous = get().traccarConnection
      if (traccarConnection.code === previous?.code) return

      clearTraccarOutageGraceTimeout()
      set({ traccarConnection, isTraccarOutageOngoing: false })
      if (traccarConnection.code === 'traccar_unavailable') {
        traccarOutageGraceTimeoutId = window.setTimeout(
          () => set({ isTraccarOutageOngoing: true }),
          TRACCAR_OUTAGE_GRACE_MS,
        )
      }

      const missedUpdatesWhileTraccarWasDown =
        traccarConnection.connected && previous !== null
      if (missedUpdatesWhileTraccarWasDown) void get().fetchDevices()
    }

    const connection = createDevicesHubConnection({
      onDevicesUpdated: (updates) =>
        whileCurrent(() =>
          set((state) => ({
            devices: mergeDeviceStatuses(state.devices, updates),
          })),
        ),
      onPositionsUpdated: (positions) =>
        whileCurrent(() =>
          set((state) => ({
            devices: replacePositions(state.devices, positions),
          })),
        ),
      onTraccarConnectionChanged: (traccarConnection) =>
        whileCurrent(() => applyTraccarConnection(traccarConnection)),
      onReconnecting: () =>
        whileCurrent(() => set({ hubStatus: 'reconnecting' })),
      onReconnected: () =>
        whileCurrent(() => {
          set({ hubStatus: 'connected' })
          void get().fetchDevices()
        }),
      onClose: () => whileCurrent(() => disconnectAndRetry(0)),
    })
    const whileCurrent = (handler: () => void) => {
      if (hubConnection === connection) handler()
    }

    const disconnectAndRetry = (previousRetryCount: number) => {
      set({ hubStatus: 'disconnected' })
      clearConnectRetryTimeout()
      connectRetryTimeoutId = window.setTimeout(
        () => void retryConnect(previousRetryCount + 1),
        reconnectDelayMs(previousRetryCount),
      )
    }
    const connect = async (previousRetryCount: number): Promise<boolean> => {
      try {
        await connection.start()
      } catch {
        whileCurrent(() => disconnectAndRetry(previousRetryCount))
        return false
      }
      whileCurrent(() => set({ hubStatus: 'connected' }))
      return hubConnection === connection
    }
    const retryConnect = async (previousRetryCount: number) => {
      if (await connect(previousRetryCount)) void get().fetchDevices()
    }

    hubConnection = connection
    set({ hubStatus: 'connecting' })
    await connect(0)
    if (hubConnection === connection) await get().fetchDevices()
  },
  stopLiveUpdates: () => {
    const connection = hubConnection
    hubConnection = null
    clearConnectRetryTimeout()
    clearTraccarOutageGraceTimeout()
    set({
      hubStatus: 'stopped',
      traccarConnection: null,
      isTraccarOutageOngoing: false,
    })
    void connection?.stop()
  },

  selectedDeviceId: null,
  selectDevice: (deviceId) =>
    set({
      selectedDeviceId: deviceId,
      history: [],
      historyStatus: 'idle',
      historyError: null,
      deviceState: {},
      deviceStateStatus: 'idle',
      deviceStateError: null,
      deviceEvents: [],
      deviceEventsStatus: 'idle',
      deviceEventsError: null,
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

  deviceState: {},
  deviceStateStatus: 'idle',
  deviceStateError: null,
  fetchDeviceState: async (deviceId) => {
    const isSilentRefresh = get().deviceStateStatus === 'success'
    if (!isSilentRefresh) {
      set({ deviceStateStatus: 'loading', deviceStateError: null })
    }
    const isStaleRequest = () => get().selectedDeviceId !== deviceId
    try {
      const deviceState = await getDeviceState(deviceId)
      if (isStaleRequest()) return
      set({ deviceState, deviceStateStatus: 'success', deviceStateError: null })
    } catch (error) {
      if (isStaleRequest() || isSilentRefresh) return
      set({ deviceStateStatus: 'error', deviceStateError: toApiError(error) })
    }
  },

  deviceEvents: [],
  deviceEventsStatus: 'idle',
  deviceEventsError: null,
  fetchDeviceEvents: async (deviceId) => {
    const isSilentRefresh = get().deviceEventsStatus === 'success'
    if (!isSilentRefresh) {
      set({ deviceEventsStatus: 'loading', deviceEventsError: null })
    }
    const isStaleRequest = () => get().selectedDeviceId !== deviceId
    try {
      const deviceEvents = await getDeviceEvents(deviceId)
      if (isStaleRequest()) return
      set({
        deviceEvents,
        deviceEventsStatus: 'success',
        deviceEventsError: null,
      })
    } catch (error) {
      if (isStaleRequest() || isSilentRefresh) return
      set({ deviceEventsStatus: 'error', deviceEventsError: toApiError(error) })
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

export function selectDeviceSyncMode(state: DevicesState): DeviceSyncMode {
  if (state.hubStatus === 'stopped') return 'off'
  const isLiveDataFlowing =
    state.hubStatus === 'connected' && state.traccarConnection?.connected
  return isLiveDataFlowing ? 'live' : 'polling'
}

export function selectTraccarProblem(
  state: DevicesState,
): TraccarProblemCode | null {
  if (state.hubStatus !== 'connected') return null
  const code = state.traccarConnection?.code ?? null
  if (code === 'live_updates_disabled') return null
  if (code === 'traccar_unavailable' && !state.isTraccarOutageOngoing) {
    return null
  }
  return code
}

function selectShouldPoll(state: DevicesState): boolean {
  const mode = selectDeviceSyncMode(state)
  const isRecoveringFromAFailedLoad =
    mode === 'live' && state.status === 'error'
  return mode === 'polling' || isRecoveringFromAFailedLoad
}

useDevicesStore.subscribe((state) => {
  const shouldPoll = selectShouldPoll(state)
  if (shouldPoll && pollIntervalId === undefined) {
    pollIntervalId = window.setInterval(() => {
      if (!document.hidden) void useDevicesStore.getState().fetchDevices()
    }, DEVICE_POLL_INTERVAL_MS)
  } else if (!shouldPoll && pollIntervalId !== undefined) {
    window.clearInterval(pollIntervalId)
    pollIntervalId = undefined
  }
})
