import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
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

  it('triggers fetchDevices once on mount', () => {
    const fetchDevices = vi.fn()
    useDevicesStore.setState({ fetchDevices })

    render(<DevicesPage />)

    expect(fetchDevices).toHaveBeenCalledOnce()
  })

  describe('polling', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('re-fetches devices on an interval while the page is mounted', () => {
      const fetchDevices = vi.fn()
      useDevicesStore.setState({ fetchDevices })

      const { unmount } = render(<DevicesPage />)
      expect(fetchDevices).toHaveBeenCalledTimes(1)

      vi.advanceTimersByTime(15_000)
      expect(fetchDevices).toHaveBeenCalledTimes(2)

      vi.advanceTimersByTime(15_000)
      expect(fetchDevices).toHaveBeenCalledTimes(3)

      unmount()
      vi.advanceTimersByTime(30_000)
      expect(fetchDevices).toHaveBeenCalledTimes(3)
    })

    it('skips the poll tick while the tab is hidden', () => {
      const fetchDevices = vi.fn()
      useDevicesStore.setState({ fetchDevices })
      vi.spyOn(document, 'hidden', 'get').mockReturnValue(true)

      render(<DevicesPage />)
      expect(fetchDevices).toHaveBeenCalledTimes(1)

      vi.advanceTimersByTime(15_000)
      expect(fetchDevices).toHaveBeenCalledTimes(1)

      vi.restoreAllMocks()
    })
  })
})
