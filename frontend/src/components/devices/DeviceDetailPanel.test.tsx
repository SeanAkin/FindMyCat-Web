import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { DeviceDetailPanel } from '@/components/devices/DeviceDetailPanel'
import { useDevicesStore } from '@/stores/devicesStore'

function renderDeviceDetailPanel() {
  return render(
    <MemoryRouter>
      <DeviceDetailPanel />
    </MemoryRouter>,
  )
}

const deviceWithPosition = {
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
    latitude: 51.5074,
    longitude: -0.1278,
    altitude: 10,
    speedKnots: 1.2,
    course: 90,
    accuracy: 5,
    valid: true,
    batteryLevel: 80,
    satellites: 8,
  },
}

const deviceWithoutPosition = {
  ...deviceWithPosition,
  id: 2,
  name: 'No Fix Yet',
  position: null,
}

describe('DeviceDetailPanel', () => {
  afterEach(() => {
    useDevicesStore.setState({ devices: [], selectedDeviceId: null })
    vi.unstubAllGlobals()
  })

  it('shows a placeholder when no device is selected', () => {
    useDevicesStore.setState({
      devices: [deviceWithPosition],
      selectedDeviceId: null,
    })
    renderDeviceDetailPanel()

    expect(
      screen.getByText('Select a device to see details'),
    ).toBeInTheDocument()
  })

  it('shows full position stats for a selected device with a fix', () => {
    vi.stubGlobal('fetch', vi.fn())
    useDevicesStore.setState({
      devices: [deviceWithPosition],
      selectedDeviceId: 1,
    })
    renderDeviceDetailPanel()

    expect(screen.getByText('Nova')).toBeInTheDocument()
    expect(screen.getByText('1.2 kn')).toBeInTheDocument()
    expect(screen.getByText('8')).toBeInTheDocument()
    expect(screen.getByText('5 m')).toBeInTheDocument()
    expect(screen.getByText('80%')).toBeInTheDocument()
    expect(screen.getByText('51.50740, -0.12780')).toBeInTheDocument()
  })

  it('shows a no-fix message when the device has never reported a position', () => {
    vi.stubGlobal('fetch', vi.fn())
    useDevicesStore.setState({
      devices: [deviceWithoutPosition],
      selectedDeviceId: 2,
    })
    renderDeviceDetailPanel()

    expect(
      screen.getByText('This collar has not reported a location yet.'),
    ).toBeInTheDocument()
  })

  it('shows a lost-mode badge once a Mark Lost command has been sent', () => {
    vi.stubGlobal('fetch', vi.fn())
    useDevicesStore.setState({
      devices: [deviceWithPosition],
      selectedDeviceId: 1,
      lastSentDeviceCommand: { 1: 'lost' },
    })
    renderDeviceDetailPanel()

    expect(screen.getByText('Lost mode command sent')).toBeInTheDocument()
  })
})

