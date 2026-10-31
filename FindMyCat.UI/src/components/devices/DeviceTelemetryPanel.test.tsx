import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import type { DeviceEventResponse, DeviceStateEntry } from '@/api/types'
import { DeviceTelemetryPanel } from '@/components/devices/DeviceTelemetryPanel'
import { useDevicesStore } from '@/stores/devicesStore'

const networkFields = { status: 'registered', mode: 7, edrx_s: 81.92 }

const commandFields = {
  command: 'ping',
  status: 'ok',
  method: 'gnss',
  satellites: 8,
  elapsed_s: 42,
  battery: 90,
  uptime_s: 3600,
}

const networkEvent: DeviceEventResponse = {
  id: 2,
  receivedAt: '2026-08-16T11:00:00Z',
  kind: 'network',
  payload: networkFields,
}

const commandEvent: DeviceEventResponse = {
  id: 1,
  receivedAt: '2026-08-16T10:00:00Z',
  kind: 'command',
  payload: commandFields,
}

const networkState: DeviceStateEntry = { id: 2, receivedAt: '2026-08-16T11:00:00Z', ...networkFields }

const commandState: DeviceStateEntry = { id: 1, receivedAt: '2026-08-16T10:00:00Z', ...commandFields }

describe('DeviceTelemetryPanel', () => {
  afterEach(() => {
    useDevicesStore.setState({
      deviceState: {},
      deviceStateStatus: 'idle',
      deviceStateError: null,
      deviceEvents: [],
      deviceEventsStatus: 'idle',
      deviceEventsError: null,
    })
  })

  it('triggers the telemetry fetches on mount', () => {
    const fetchDeviceState = vi.fn()
    const fetchDeviceEvents = vi.fn()
    useDevicesStore.setState({ fetchDeviceState, fetchDeviceEvents })

    render(<DeviceTelemetryPanel deviceId={5} />)

    expect(fetchDeviceState).toHaveBeenCalledWith(5)
    expect(fetchDeviceEvents).toHaveBeenCalledWith(5)
  })

  it('polls the telemetry every 15 seconds while mounted', () => {
    vi.useFakeTimers()
    try {
      const fetchDeviceState = vi.fn()
      const fetchDeviceEvents = vi.fn()
      useDevicesStore.setState({ fetchDeviceState, fetchDeviceEvents })

      render(<DeviceTelemetryPanel deviceId={5} />)
      expect(fetchDeviceState).toHaveBeenCalledTimes(1)

      act(() => {
        vi.advanceTimersByTime(30_000)
      })

      expect(fetchDeviceState).toHaveBeenCalledTimes(3)
      expect(fetchDeviceEvents).toHaveBeenCalledTimes(3)
    } finally {
      vi.useRealTimers()
    }
  })

  it('renders the network and command summaries from state', () => {
    useDevicesStore.setState({
      deviceStateStatus: 'success',
      deviceState: { network: networkState, command: commandState },
    })

    render(<DeviceTelemetryPanel deviceId={1} />)

    expect(screen.getByText('registered')).toBeInTheDocument()
    expect(screen.getByText('LTE-M')).toBeInTheDocument()
    expect(screen.getByText('ping')).toBeInTheDocument()
    expect(screen.getByText('gnss')).toBeInTheDocument()
  })

  it('says quietly when a kind is missing', () => {
    useDevicesStore.setState({
      deviceStateStatus: 'success',
      deviceState: { network: networkState },
    })

    render(<DeviceTelemetryPanel deviceId={1} />)

    expect(screen.getByText('No command run yet.')).toBeInTheDocument()
  })

  it('renders the empty state when no telemetry has arrived', () => {
    useDevicesStore.setState({
      deviceStateStatus: 'success',
      deviceState: {},
    })

    render(<DeviceTelemetryPanel deviceId={1} />)

    expect(screen.getByText('No telemetry reported yet.')).toBeInTheDocument()
  })

  it('renders the recent events list newest first', () => {
    useDevicesStore.setState({
      deviceEventsStatus: 'success',
      deviceEvents: [networkEvent, commandEvent],
    })

    render(<DeviceTelemetryPanel deviceId={1} />)

    expect(screen.getAllByText('network').length).toBeGreaterThan(0)
    expect(screen.getAllByText('command').length).toBeGreaterThan(0)
  })

  it('falls back to a raw render for an unrecognised kind', () => {
    useDevicesStore.setState({
      deviceEventsStatus: 'success',
      deviceEvents: [
        {
          id: 9,
          receivedAt: '2026-08-16T12:00:00.000Z',
          kind: 'firmware',
          payload: { version: '1.2.3' },
        },
      ],
    })

    render(<DeviceTelemetryPanel deviceId={1} />)

    expect(screen.getByText(/1\.2\.3/)).toBeInTheDocument()
  })
})
