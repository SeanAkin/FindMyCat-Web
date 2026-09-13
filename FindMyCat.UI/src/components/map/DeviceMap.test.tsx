import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render } from '@testing-library/react'
import * as L from 'leaflet'
import { DeviceMap } from '@/components/map/DeviceMap'
import type { DeviceResponse } from '@/api/types'
import { useDevicesStore } from '@/stores/devicesStore'
import { useThemeStore } from '@/stores/themeStore'

vi.mock('leaflet', () => {
  function mockMap() {
    return {
      remove: vi.fn(),
      setView: vi.fn(),
      getZoom: vi.fn(() => 3),
      flyTo: vi.fn(),
      fitBounds: vi.fn(),
      invalidateSize: vi.fn(),
    }
  }

  function mockTileLayer() {
    const tileLayer: Record<string, unknown> = {}
    tileLayer.addTo = vi.fn(() => tileLayer)
    tileLayer.redraw = vi.fn(() => tileLayer)
    tileLayer.remove = vi.fn(() => tileLayer)
    return tileLayer
  }

  function mockMarker() {
    const marker: Record<string, unknown> = {}
    marker.addTo = vi.fn(() => marker)
    marker.on = vi.fn(() => marker)
    marker.setLatLng = vi.fn(() => marker)
    marker.setIcon = vi.fn(() => marker)
    marker.bindPopup = vi.fn(() => marker)
    marker.openPopup = vi.fn(() => marker)
    marker.getLatLng = vi.fn(() => ({ lat: 1, lng: 2 }))
    marker.remove = vi.fn()
    return marker
  }

  function mockPolyline() {
    const line: Record<string, unknown> = {}
    line.addTo = vi.fn(() => line)
    line.getBounds = vi.fn(() => ({}))
    line.remove = vi.fn()
    return line
  }

  return {
    map: vi.fn(mockMap),
    tileLayer: vi.fn(mockTileLayer),
    divIcon: vi.fn((options: unknown) => options),
    marker: vi.fn(mockMarker),
    polyline: vi.fn(mockPolyline),
    latLngBounds: vi.fn(() => ({
      getCenter: vi.fn(() => ({ lat: 0, lng: 0 })),
    })),
  }
})

function device(overrides: Partial<DeviceResponse> = {}): DeviceResponse {
  return {
    id: 1,
    name: 'Nova',
    uniqueId: 'unique-1',
    status: 'online',
    lastUpdate: '2026-08-16T11:00:00.000Z',
    disabled: false,
    position: {
      deviceId: 1,
      fixTime: '2026-08-16T11:00:00.000Z',
      deviceTime: '2026-08-16T11:00:00.000Z',
      serverTime: '2026-08-16T11:00:00.000Z',
      latitude: 51.5,
      longitude: -0.12,
      altitude: 10,
      speedKnots: 1.2,
      course: 90,
      accuracy: 5,
      valid: true,
      batteryLevel: 80,
      satellites: 8,
    },
    ...overrides,
  }
}

describe('DeviceMap', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useDevicesStore.setState({
      devices: [],
      status: 'idle',
      error: null,
      selectedDeviceId: null,
      history: [],
      historyStatus: 'idle',
      historyError: null,
    })
    useThemeStore.setState({ resolvedTheme: 'light' })
  })

  it('shows a loading skeleton while devices are loading', () => {
    useDevicesStore.setState({ status: 'loading' })

    const { container } = render(<DeviceMap />)

    expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull()
  })

  it('recreates the tile layer with a dark className when the resolved theme changes', () => {
    useThemeStore.setState({ resolvedTheme: 'light' })
    useDevicesStore.setState({ status: 'success', devices: [] })

    render(<DeviceMap />)
    const firstTileLayer = vi.mocked(L.tileLayer).mock.results[0]?.value as {
      remove: ReturnType<typeof vi.fn>
    }
    expect(vi.mocked(L.tileLayer)).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ className: '' }),
    )

    act(() => {
      useThemeStore.setState({ resolvedTheme: 'dark' })
    })

    expect(firstTileLayer.remove).toHaveBeenCalled()
    expect(vi.mocked(L.tileLayer)).toHaveBeenLastCalledWith(
      expect.anything(),
      expect.objectContaining({ className: 'device-map-tiles--dark' }),
    )
  })

  it('renders nothing when devices failed to load', () => {
    useDevicesStore.setState({ status: 'error' })

    const { container } = render(<DeviceMap />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows a "no positions yet" message when no device has a valid position', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ position: null })],
    })

    const { getByText } = render(<DeviceMap />)

    expect(getByText('No positions yet')).toBeInTheDocument()
  })

  it('places a marker for each device with a valid position', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ id: 1 }), device({ id: 2, name: 'Whiskers' })],
    })

    render(<DeviceMap />)

    expect(L.marker).toHaveBeenCalledTimes(2)
    expect(L.marker).toHaveBeenCalledWith([51.5, -0.12], expect.anything())
  })

  it('selecting a device calls flyTo and opens its marker popup', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ id: 1 })],
    })

    render(<DeviceMap />)
    const marker = vi.mocked(L.marker).mock.results[0]?.value as {
      flyTo?: unknown
      openPopup: ReturnType<typeof vi.fn>
    }
    const map = vi.mocked(L.map).mock.results[0]?.value as {
      flyTo: ReturnType<typeof vi.fn>
    }

    act(() => {
      useDevicesStore.setState({ selectedDeviceId: 1 })
    })

    expect(map.flyTo).toHaveBeenCalled()
    expect(marker.openPopup).toHaveBeenCalled()
  })

  it('clicking a marker selects the device', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ id: 1 })],
    })

    render(<DeviceMap />)
    const onCalls = vi.mocked(L.marker).mock.results[0]?.value as {
      on: ReturnType<typeof vi.fn>
    }
    const clickHandler = onCalls.on.mock.calls.find(
      (call: unknown[]) => call[0] === 'click',
    )?.[1] as () => void

    clickHandler()

    expect(useDevicesStore.getState().selectedDeviceId).toBe(1)
  })

  it('draws the selected device history as a polyline ordered by fix time', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ id: 1 })],
      selectedDeviceId: 1,
      history: [
        {
          deviceId: 1,
          fixTime: '2026-08-16T12:00:00.000Z',
          deviceTime: '2026-08-16T12:00:00.000Z',
          serverTime: '2026-08-16T12:00:00.000Z',
          latitude: 51.51,
          longitude: -0.13,
          altitude: 0,
          speedKnots: 0,
          course: 0,
          accuracy: 5,
          valid: true,
          batteryLevel: null,
          satellites: null,
        },
        {
          deviceId: 1,
          fixTime: '2026-08-16T11:00:00.000Z',
          deviceTime: '2026-08-16T11:00:00.000Z',
          serverTime: '2026-08-16T11:00:00.000Z',
          latitude: 51.5,
          longitude: -0.12,
          altitude: 0,
          speedKnots: 0,
          course: 0,
          accuracy: 5,
          valid: true,
          batteryLevel: null,
          satellites: null,
        },
      ],
      historyStatus: 'success',
    })

    render(<DeviceMap />)

    expect(L.polyline).toHaveBeenCalledWith(
      [
        [51.5, -0.12],
        [51.51, -0.13],
      ],
      expect.anything(),
    )
  })

  it('clears the route when the selected device changes', () => {
    useDevicesStore.setState({
      status: 'success',
      devices: [device({ id: 1 })],
      selectedDeviceId: 1,
      history: [
        {
          deviceId: 1,
          fixTime: '2026-08-16T12:00:00.000Z',
          deviceTime: '2026-08-16T12:00:00.000Z',
          serverTime: '2026-08-16T12:00:00.000Z',
          latitude: 51.51,
          longitude: -0.13,
          altitude: 0,
          speedKnots: 0,
          course: 0,
          accuracy: 5,
          valid: true,
          batteryLevel: null,
          satellites: null,
        },
      ],
      historyStatus: 'success',
    })

    render(<DeviceMap />)
    const line = vi.mocked(L.polyline).mock.results[0]?.value as {
      remove: ReturnType<typeof vi.fn>
    }

    act(() => {
      useDevicesStore.setState({ selectedDeviceId: null, history: [] })
    })

    expect(line.remove).toHaveBeenCalled()
  })
})
