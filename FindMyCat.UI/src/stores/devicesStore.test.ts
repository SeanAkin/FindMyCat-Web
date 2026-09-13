import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DeviceResponse } from '@/api/types'

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
