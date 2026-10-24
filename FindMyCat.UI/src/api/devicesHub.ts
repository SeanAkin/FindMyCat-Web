import {
  HubConnectionBuilder,
  LogLevel,
  type IRetryPolicy,
} from '@microsoft/signalr'
import type {
  DeviceStatusResponse,
  PositionResponse,
  TraccarConnectionStatusResponse,
} from '@/api/types'

const DEVICES_HUB_PATH = '/hubs/devices'

const FIRST_RECONNECT_DELAYS_MS = [0, 2_000, 10_000]
const LONGEST_RECONNECT_DELAY_MS = 30_000

export const reconnectDelayMs = (previousRetryCount: number) =>
  FIRST_RECONNECT_DELAYS_MS[previousRetryCount] ?? LONGEST_RECONNECT_DELAY_MS

const keepRetryingAtTheLongestDelay: IRetryPolicy = {
  nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
    reconnectDelayMs(previousRetryCount),
}

export interface DevicesHubHandlers {
  onDevicesUpdated: (devices: DeviceStatusResponse[]) => void
  onPositionsUpdated: (positions: PositionResponse[]) => void
  onTraccarConnectionChanged: (status: TraccarConnectionStatusResponse) => void
  onReconnecting: () => void
  onReconnected: () => void
  onClose: () => void
}

export interface DevicesHubConnection {
  start: () => Promise<void>
  stop: () => Promise<void>
}

export function createDevicesHubConnection(
  handlers: DevicesHubHandlers,
): DevicesHubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(DEVICES_HUB_PATH)
    .withAutomaticReconnect(keepRetryingAtTheLongestDelay)
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('DevicesUpdated', handlers.onDevicesUpdated)
  connection.on('PositionsUpdated', handlers.onPositionsUpdated)
  connection.on('TraccarConnectionChanged', handlers.onTraccarConnectionChanged)
  connection.onreconnecting(() => handlers.onReconnecting())
  connection.onreconnected(() => handlers.onReconnected())
  connection.onclose(() => handlers.onClose())

  return {
    start: () => connection.start(),
    stop: () => connection.stop(),
  }
}
