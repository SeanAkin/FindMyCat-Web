import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DeviceList } from '@/components/devices/DeviceList'
import { ApiError } from '@/api/http'
import { useDevicesStore } from '@/stores/devicesStore'
import type { DeviceResponse } from '@/api/types'

const device = {
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
} satisfies DeviceResponse

describe('DeviceList', () => {
  afterEach(() => {
    useDevicesStore.setState({
      devices: [],
      status: 'idle',
      error: null,
      selectedDeviceId: null,
      history: [],
      historyStatus: 'idle',
      historyError: null,
    })
  })

  it('renders skeletons while loading', () => {
    useDevicesStore.setState({ status: 'loading' })
    render(<DeviceList />)

    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('renders an empty state when there are no devices', () => {
    useDevicesStore.setState({ status: 'success', devices: [] })
    render(<DeviceList />)

    expect(screen.getByText('No devices yet')).toBeInTheDocument()
  })

  it('renders a setup nudge for traccar_not_configured without a retry button', () => {
    useDevicesStore.setState({
      status: 'error',
      error: new ApiError(409, 'traccar_not_configured', 'x'),
    })
    render(<DeviceList />)

    expect(
      screen.getByText('Location tracking is not set up yet'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Try again' }),
    ).not.toBeInTheDocument()
  })

  it('renders a retry button for a real error', () => {
    useDevicesStore.setState({
      status: 'error',
      error: new ApiError(502, 'traccar_unavailable', 'x'),
    })
    render(<DeviceList />)

    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })

  it('retry calls fetchDevices again', async () => {
    const fetchDevices = vi.fn()
    useDevicesStore.setState({
      status: 'error',
      error: new ApiError(502, 'traccar_unavailable', 'x'),
      fetchDevices,
    })
    const user = userEvent.setup()
    render(<DeviceList />)

    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(fetchDevices).toHaveBeenCalledOnce()
  })

  it('renders device cards and selecting one calls selectDevice', async () => {
    const selectDevice = vi.fn()
    useDevicesStore.setState({
      status: 'success',
      devices: [device],
      selectDevice,
    })
    const user = userEvent.setup()
    render(<DeviceList />)

    expect(screen.getByText('Nova')).toBeInTheDocument()
    expect(screen.getByText('Online')).toBeInTheDocument()
    expect(screen.getByText('80%')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /nova/i }))

    expect(selectDevice).toHaveBeenCalledWith(1)
  })
})
