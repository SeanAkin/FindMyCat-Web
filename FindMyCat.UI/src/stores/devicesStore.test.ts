import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DevicesHubHandlers } from '@/api/devicesHub'
import type {
  DeviceResponse,
  DeviceStatusResponse,
  PositionResponse,
  TraccarConnectionStatusResponse,
} from '@/api/types'

const hub = vi.hoisted(() => ({
  handlers: null as DevicesHubHandlers | null,
  start: vi.fn<() => Promise<void>>(),
  stop: vi.fn<() => Promise<void>>(),
}))

vi.mock('@/api/devicesHub', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/devicesHub')>()),
  createDevicesHubConnection: (handlers: DevicesHubHandlers) => {
    hub.handlers = handlers
    return { start: hub.start, stop: hub.stop }
  },
}))

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const device = {
  id: 1,
  name: 'Nova',
  uniqueId: 'unique-1',
  status: 'online',
  lastUpdate: '2026-08-16T11:00:00.000Z',
  disabled: false,
  position: null,
} satisfies DeviceResponse

describe('devicesStore', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchDevices sets success status and the device list', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(200, [device]))
    const { useDevicesStore } = await import('./devicesStore')

    await useDevicesStore.getState().fetchDevices()

    expect(useDevicesStore.getState().status).toBe('success')
    expect(useDevicesStore.getState().devices).toEqual([device])
  })

  it('fetchDevices sets error status and an ApiError on failure', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(409, {
        code: 'traccar_not_configured',
        message: 'not configured',
      }),
    )
    const { useDevicesStore } = await import('./devicesStore')

    await useDevicesStore.getState().fetchDevices()

    expect(useDevicesStore.getState().status).toBe('error')
    expect(useDevicesStore.getState().error?.code).toBe(
      'traccar_not_configured',
    )
  })

  it('fetchDevices does not flip status to loading on a background poll refresh', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(200, [device]))
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.setState({ status: 'success', devices: [device] })

    const fetchPromise = useDevicesStore.getState().fetchDevices()
    expect(useDevicesStore.getState().status).toBe('success')

    await fetchPromise
    expect(useDevicesStore.getState().status).toBe('success')
  })

  it('fetchDevices keeps the last-good devices when a background poll refresh fails', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(200, [device]))
    const { useDevicesStore } = await import('./devicesStore')
    await useDevicesStore.getState().fetchDevices()
    expect(useDevicesStore.getState().status).toBe('success')

    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))
    await useDevicesStore.getState().fetchDevices()

    expect(useDevicesStore.getState().status).toBe('success')
    expect(useDevicesStore.getState().error).toBeNull()
    expect(useDevicesStore.getState().devices).toEqual([device])
  })

  it('fetchDevices wraps a raw network failure as an ApiError instead of throwing', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))
    const { useDevicesStore } = await import('./devicesStore')

    await useDevicesStore.getState().fetchDevices()

    expect(useDevicesStore.getState().status).toBe('error')
    expect(useDevicesStore.getState().error).toBeInstanceOf(Error)
  })

  it('selectDevice sets the selected id and resets history state', async () => {
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.setState({
      history: [{ deviceId: 1 } as never],
      historyStatus: 'success',
    })

    useDevicesStore.getState().selectDevice(1)

    expect(useDevicesStore.getState().selectedDeviceId).toBe(1)
    expect(useDevicesStore.getState().history).toEqual([])
    expect(useDevicesStore.getState().historyStatus).toBe('idle')
  })

  it('selectDevice(null) clears the selection', async () => {
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.setState({ selectedDeviceId: 1 })

    useDevicesStore.getState().selectDevice(null)

    expect(useDevicesStore.getState().selectedDeviceId).toBeNull()
  })

  it('fetchHistory sets success status and the position list', async () => {
    const position = {
      deviceId: 1,
      fixTime: '2026-08-16T11:00:00.000Z',
      latitude: 1,
      longitude: 2,
    }
    vi.mocked(fetch).mockResolvedValue(jsonResponse(200, [position]))
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.getState().selectDevice(1)

    await useDevicesStore
      .getState()
      .fetchHistory(1, new Date('2026-08-15'), new Date('2026-08-16'))

    expect(useDevicesStore.getState().historyStatus).toBe('success')
    expect(useDevicesStore.getState().history).toEqual([position])
  })

  it('fetchHistory sets error status on failure', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(400, { code: 'range_too_large', message: 'too large' }),
    )
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.getState().selectDevice(1)

    await useDevicesStore
      .getState()
      .fetchHistory(1, new Date('2026-01-01'), new Date('2026-08-16'))

    expect(useDevicesStore.getState().historyStatus).toBe('error')
    expect(useDevicesStore.getState().historyError?.code).toBe(
      'range_too_large',
    )
  })

  it('ignores a history response for a device that is no longer selected', async () => {
    const position = {
      deviceId: 1,
      fixTime: '2026-08-16T11:00:00.000Z',
      latitude: 1,
      longitude: 2,
    }
    let resolveFetch: (response: Response) => void = () => {}
    vi.mocked(fetch).mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve
      }),
    )
    const { useDevicesStore } = await import('./devicesStore')
    useDevicesStore.getState().selectDevice(1)

    const fetchPromise = useDevicesStore
      .getState()
      .fetchHistory(1, new Date('2026-08-15'), new Date('2026-08-16'))

    useDevicesStore.getState().selectDevice(2)
    resolveFetch(jsonResponse(200, [position]))
    await fetchPromise

    expect(useDevicesStore.getState().historyStatus).toBe('idle')
    expect(useDevicesStore.getState().history).toEqual([])
  })
})

function fakePosition(deviceId: number, latitude: number): PositionResponse {
  return {
    deviceId,
    fixTime: '2026-08-16T11:00:00.000Z',
    deviceTime: '2026-08-16T11:00:00.000Z',
    serverTime: '2026-08-16T11:00:00.000Z',
    latitude,
    longitude: 2,
    altitude: 0,
    speedKnots: 0,
    course: 0,
    accuracy: 5,
    valid: true,
    batteryLevel: 80,
    satellites: 8,
  }
}

const live = {
  connected: true,
  code: null,
} satisfies TraccarConnectionStatusResponse
const liveUpdatesDisabled = {
  connected: false,
  code: 'live_updates_disabled',
} satisfies TraccarConnectionStatusResponse
const traccarUnavailable = {
  connected: false,
  code: 'traccar_unavailable',
} satisfies TraccarConnectionStatusResponse
const credentialRejected = {
  connected: false,
  code: 'traccar_credential_rejected',
} satisfies TraccarConnectionStatusResponse

describe('devicesStore live updates', () => {
  type DevicesStoreModule = typeof import('./devicesStore')
  let devicesStoreModule: DevicesStoreModule | null = null

  function hubHandlers(): DevicesHubHandlers {
    if (!hub.handlers) throw new Error('The hub connection was never created.')
    return hub.handlers
  }

  const fetchCount = () => vi.mocked(fetch).mock.calls.length

  async function importStore(): Promise<DevicesStoreModule> {
    devicesStoreModule = await import('./devicesStore')
    return devicesStoreModule
  }

  async function startLiveUpdates(): Promise<DevicesStoreModule> {
    const storeModule = await importStore()
    await storeModule.useDevicesStore.getState().startLiveUpdates()
    return storeModule
  }

  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('fetch', vi.fn())
    vi.useFakeTimers()
    hub.handlers = null
    hub.start.mockReset().mockResolvedValue(undefined)
    hub.stop.mockReset().mockResolvedValue(undefined)
    vi.mocked(fetch).mockImplementation(() =>
      Promise.resolve(jsonResponse(200, [device])),
    )
  })

  afterEach(() => {
    devicesStoreModule?.useDevicesStore.getState().stopLiveUpdates()
    devicesStoreModule = null
    vi.restoreAllMocks()
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('connects to the hub before the first REST fetch', async () => {
    let finishStarting = () => {}
    hub.start.mockReturnValue(
      new Promise((resolve) => {
        finishStarting = () => resolve()
      }),
    )
    const { useDevicesStore } = await importStore()

    const starting = useDevicesStore.getState().startLiveUpdates()
    expect(useDevicesStore.getState().hubStatus).toBe('connecting')
    expect(fetch).not.toHaveBeenCalled()

    finishStarting()
    await starting

    expect(useDevicesStore.getState().hubStatus).toBe('connected')
    expect(fetch).toHaveBeenCalledOnce()
    expect(fetch).toHaveBeenCalledWith('/api/devices', expect.anything())
    expect(useDevicesStore.getState().devices).toEqual([device])
  })

  it('still fetches from REST when the hub cannot connect', async () => {
    hub.start.mockRejectedValue(new Error('negotiation failed'))

    const { useDevicesStore } = await startLiveUpdates()

    expect(useDevicesStore.getState().hubStatus).toBe('disconnected')
    expect(fetch).toHaveBeenCalledOnce()
  })

  it('retries a failed first connect, then fetches once connected', async () => {
    hub.start
      .mockRejectedValueOnce(new Error('negotiation failed'))
      .mockRejectedValueOnce(new Error('negotiation failed'))
    const { useDevicesStore, selectDeviceSyncMode } = await startLiveUpdates()
    const mode = () => selectDeviceSyncMode(useDevicesStore.getState())
    expect(useDevicesStore.getState().hubStatus).toBe('disconnected')
    expect(mode()).toBe('polling')

    await vi.advanceTimersByTimeAsync(0)
    expect(hub.start).toHaveBeenCalledTimes(2)
    expect(useDevicesStore.getState().hubStatus).toBe('disconnected')

    const fetchesBeforeConnecting = fetchCount()
    await vi.advanceTimersByTimeAsync(2_000)
    expect(hub.start).toHaveBeenCalledTimes(3)
    expect(useDevicesStore.getState().hubStatus).toBe('connected')
    expect(fetchCount()).toBe(fetchesBeforeConnecting + 1)
  })

  it('stopLiveUpdates cancels a pending connect retry', async () => {
    hub.start.mockRejectedValue(new Error('negotiation failed'))
    const { useDevicesStore } = await startLiveUpdates()
    await vi.advanceTimersByTimeAsync(0)
    const startsBeforeStop = hub.start.mock.calls.length

    useDevicesStore.getState().stopLiveUpdates()
    await vi.advanceTimersByTimeAsync(60_000)

    expect(hub.start).toHaveBeenCalledTimes(startsBeforeStop)
    expect(useDevicesStore.getState().hubStatus).toBe('stopped')
  })

  it('does not open a second connection when already started', async () => {
    const { useDevicesStore } = await startLiveUpdates()

    await useDevicesStore.getState().startLiveUpdates()

    expect(hub.start).toHaveBeenCalledOnce()
  })

  it('a device status update keeps the device position', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    const position = fakePosition(1, 1)
    useDevicesStore.setState({ devices: [{ ...device, position }] })
    const update = {
      ...device,
      name: 'Nova Renamed',
      status: 'offline',
    } satisfies DeviceStatusResponse

    hubHandlers().onDevicesUpdated([update])

    expect(useDevicesStore.getState().devices).toEqual([
      { ...update, position },
    ])
  })

  it('a device status update for an unknown device adds it without a position', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    const newDevice = {
      id: 2,
      name: 'Pixel',
      uniqueId: 'unique-2',
      status: 'online',
      lastUpdate: null,
      disabled: false,
    } satisfies DeviceStatusResponse

    hubHandlers().onDevicesUpdated([newDevice])

    expect(useDevicesStore.getState().devices).toEqual([
      device,
      { ...newDevice, position: null },
    ])
  })

  it('a position update replaces the device position', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    useDevicesStore.setState({
      devices: [{ ...device, position: fakePosition(1, 1) }],
    })
    const newerPosition = fakePosition(1, 3)

    hubHandlers().onPositionsUpdated([newerPosition, fakePosition(99, 4)])

    expect(useDevicesStore.getState().devices).toEqual([
      { ...device, position: newerPosition },
    ])
  })

  it('a reconnect refetches devices from REST', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(live)
    const fetchesBeforeReconnect = fetchCount()

    hubHandlers().onReconnecting()
    expect(useDevicesStore.getState().hubStatus).toBe('reconnecting')
    hubHandlers().onReconnected()

    expect(useDevicesStore.getState().hubStatus).toBe('connected')
    expect(fetchCount()).toBe(fetchesBeforeReconnect + 1)
  })

  it('switches between polling and live as the status and connection change, with one poll timer', async () => {
    const { useDevicesStore, selectDeviceSyncMode } = await startLiveUpdates()
    const mode = () => selectDeviceSyncMode(useDevicesStore.getState())
    let fetches = fetchCount()
    const expectOnePollPerInterval = () => {
      fetches = fetchCount()
      vi.advanceTimersByTime(15_000)
      expect(fetchCount()).toBe(++fetches)
      vi.advanceTimersByTime(15_000)
      expect(fetchCount()).toBe(++fetches)
    }
    const expectNoPolling = () => {
      fetches = fetchCount()
      vi.advanceTimersByTime(45_000)
      expect(fetchCount()).toBe(fetches)
    }

    hubHandlers().onTraccarConnectionChanged(liveUpdatesDisabled)
    expect(mode()).toBe('polling')
    expectOnePollPerInterval()

    hubHandlers().onTraccarConnectionChanged(live)
    expect(mode()).toBe('live')
    expectNoPolling()

    hubHandlers().onTraccarConnectionChanged(traccarUnavailable)
    expect(mode()).toBe('polling')
    expectOnePollPerInterval()

    hubHandlers().onTraccarConnectionChanged(live)
    expect(mode()).toBe('live')
    expectNoPolling()

    hubHandlers().onReconnecting()
    expect(mode()).toBe('polling')
    expectOnePollPerInterval()

    hubHandlers().onReconnected()
    hubHandlers().onTraccarConnectionChanged(live)
    expect(mode()).toBe('live')
    expectNoPolling()
  })

  it('polls as a fallback while the hub is disconnected', async () => {
    hub.start.mockRejectedValue(new Error('negotiation failed'))
    await startLiveUpdates()
    const fetchesAfterStart = fetchCount()

    vi.advanceTimersByTime(15_000)

    expect(fetchCount()).toBe(fetchesAfterStart + 1)
  })

  it('keeps retrying a failed REST load while live, then stops once it succeeds', async () => {
    vi.mocked(fetch).mockImplementationOnce(() =>
      Promise.resolve(jsonResponse(502, { code: 'traccar_unavailable' })),
    )
    const { useDevicesStore, selectDeviceSyncMode } = await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(live)
    expect(selectDeviceSyncMode(useDevicesStore.getState())).toBe('live')
    expect(useDevicesStore.getState().status).toBe('error')

    await vi.advanceTimersByTimeAsync(15_000)
    expect(fetchCount()).toBe(2)
    expect(useDevicesStore.getState().status).toBe('success')
    expect(useDevicesStore.getState().devices).toEqual([device])

    await vi.advanceTimersByTimeAsync(45_000)
    expect(fetchCount()).toBe(2)
  })

  it('reconnects and refetches after the server closes the connection', async () => {
    const { useDevicesStore, selectDeviceSyncMode } = await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(live)
    const fetchesBeforeClose = fetchCount()

    hubHandlers().onClose()
    expect(useDevicesStore.getState().hubStatus).toBe('disconnected')
    expect(selectDeviceSyncMode(useDevicesStore.getState())).toBe('polling')

    await vi.advanceTimersByTimeAsync(0)
    expect(hub.start).toHaveBeenCalledTimes(2)
    expect(useDevicesStore.getState().hubStatus).toBe('connected')
    expect(fetchCount()).toBe(fetchesBeforeClose + 1)
  })

  it('skips the poll tick while the tab is hidden', async () => {
    await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(liveUpdatesDisabled)
    const fetchesAfterStart = fetchCount()
    vi.spyOn(document, 'hidden', 'get').mockReturnValue(true)

    vi.advanceTimersByTime(15_000)

    expect(fetchCount()).toBe(fetchesAfterStart)
  })

  it('refetches from REST when Traccar comes back after being down', async () => {
    await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(traccarUnavailable)
    const fetchesWhileDown = fetchCount()

    hubHandlers().onTraccarConnectionChanged(live)

    expect(fetchCount()).toBe(fetchesWhileDown + 1)
  })

  it('does not refetch for the first status after connecting', async () => {
    await startLiveUpdates()
    const fetchesAfterStart = fetchCount()

    hubHandlers().onTraccarConnectionChanged(live)

    expect(fetchCount()).toBe(fetchesAfterStart)
  })

  it('only reports traccar_unavailable as a problem after the grace period', async () => {
    const { useDevicesStore, selectTraccarProblem } = await startLiveUpdates()
    const problem = () => selectTraccarProblem(useDevicesStore.getState())

    hubHandlers().onTraccarConnectionChanged(traccarUnavailable)
    expect(problem()).toBeNull()

    vi.advanceTimersByTime(10_000)
    expect(problem()).toBe('traccar_unavailable')

    hubHandlers().onTraccarConnectionChanged(live)
    expect(problem()).toBeNull()
  })

  it('a repeated status does not restart the outage grace period', async () => {
    const { useDevicesStore, selectTraccarProblem } = await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(traccarUnavailable)
    vi.advanceTimersByTime(10_000)

    hubHandlers().onTraccarConnectionChanged(traccarUnavailable)

    expect(selectTraccarProblem(useDevicesStore.getState())).toBe(
      'traccar_unavailable',
    )
  })

  it('reports a rejected credential as a problem straight away', async () => {
    const { useDevicesStore, selectTraccarProblem } = await startLiveUpdates()

    hubHandlers().onTraccarConnectionChanged(credentialRejected)

    expect(selectTraccarProblem(useDevicesStore.getState())).toBe(
      'traccar_credential_rejected',
    )
  })

  it('does not report live_updates_disabled as a problem', async () => {
    const { useDevicesStore, selectTraccarProblem } = await startLiveUpdates()

    hubHandlers().onTraccarConnectionChanged(liveUpdatesDisabled)

    expect(selectTraccarProblem(useDevicesStore.getState())).toBeNull()
  })

  it('stopLiveUpdates stops the hub connection and polling', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    hubHandlers().onTraccarConnectionChanged(liveUpdatesDisabled)

    useDevicesStore.getState().stopLiveUpdates()
    const fetchesAfterStop = fetchCount()
    vi.advanceTimersByTime(45_000)

    expect(hub.stop).toHaveBeenCalledOnce()
    expect(useDevicesStore.getState().hubStatus).toBe('stopped')
    expect(useDevicesStore.getState().traccarConnection).toBeNull()
    expect(fetchCount()).toBe(fetchesAfterStop)
  })

  it('ignores hub events from a connection that was stopped', async () => {
    const { useDevicesStore } = await startLiveUpdates()
    const stoppedConnectionHandlers = hubHandlers()
    useDevicesStore.getState().stopLiveUpdates()

    stoppedConnectionHandlers.onClose()
    stoppedConnectionHandlers.onTraccarConnectionChanged(live)

    expect(useDevicesStore.getState().hubStatus).toBe('stopped')
    expect(useDevicesStore.getState().traccarConnection).toBeNull()
  })
})
