import { afterEach, describe, expect, it, vi } from 'vitest'
import { render } from '@testing-library/react'
import { DevicesPage } from '@/pages/DevicesPage'
import { useDevicesStore } from '@/stores/devicesStore'

vi.mock('leaflet', () => {
  function chainable() {
    const obj: Record<string, unknown> = {}
    obj.addTo = vi.fn(() => obj)
    return obj
  }

  function mockTileLayer() {
    const tileLayer: Record<string, unknown> = {}
    tileLayer.addTo = vi.fn(() => tileLayer)
    tileLayer.redraw = vi.fn(() => tileLayer)
    tileLayer.remove = vi.fn(() => tileLayer)
    return tileLayer
  }

  return {
    map: vi.fn(() => ({
      remove: vi.fn(),
      setView: vi.fn(),
      getZoom: vi.fn(() => 3),
      flyTo: vi.fn(),
      fitBounds: vi.fn(),
      invalidateSize: vi.fn(),
    })),
    tileLayer: vi.fn(mockTileLayer),
    divIcon: vi.fn((options: unknown) => options),
    marker: vi.fn(chainable),
    polyline: vi.fn(chainable),
    latLngBounds: vi.fn(() => ({ getCenter: vi.fn() })),
  }
})

describe('DevicesPage', () => {
  afterEach(() => {
    useDevicesStore.setState({
      devices: [],
      status: 'idle',
      error: null,
      selectedDeviceId: null,
    })
  })

  it('starts live updates on mount and stops them on unmount', () => {
    const startLiveUpdates = vi.fn()
    const stopLiveUpdates = vi.fn()
    useDevicesStore.setState({ startLiveUpdates, stopLiveUpdates })

    const { unmount } = render(<DevicesPage />)
    expect(startLiveUpdates).toHaveBeenCalledOnce()
    expect(stopLiveUpdates).not.toHaveBeenCalled()

    unmount()
    expect(stopLiveUpdates).toHaveBeenCalledOnce()
  })
})
