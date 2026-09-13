import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DeviceHistoryPanel } from '@/components/devices/DeviceHistoryPanel'
import { useDevicesStore } from '@/stores/devicesStore'

describe('DeviceHistoryPanel', () => {
  afterEach(() => {
    useDevicesStore.setState({
      history: [],
      historyStatus: 'idle',
      historyError: null,
    })
  })

  it('shows a validation message and does not call fetchHistory when the range exceeds 31 days', async () => {
    const fetchHistory = vi.fn()
    useDevicesStore.setState({ fetchHistory })
    const user = userEvent.setup()
    render(<DeviceHistoryPanel deviceId={1} />)

    const fromInput = screen.getByLabelText('From')
    await user.clear(fromInput)
    await user.type(fromInput, '2026-01-01')

    await user.click(screen.getByRole('button', { name: 'Load history' }))

    expect(
      screen.getByText('History range must not exceed 31 days.'),
    ).toBeInTheDocument()
    expect(fetchHistory).not.toHaveBeenCalled()
  })

  it('calls fetchHistory with the chosen range when valid', async () => {
    const fetchHistory = vi.fn()
    useDevicesStore.setState({ fetchHistory })
    const user = userEvent.setup()
    render(<DeviceHistoryPanel deviceId={7} />)

    await user.click(screen.getByRole('button', { name: 'Load history' }))

    expect(fetchHistory).toHaveBeenCalledOnce()
    expect(fetchHistory.mock.calls[0]?.[0]).toBe(7)
  })

  it('renders fetched fixes', () => {
    useDevicesStore.setState({
      historyStatus: 'success',
      history: [
        {
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
      ],
    })
    render(<DeviceHistoryPanel deviceId={1} />)

    expect(screen.getByText('51.5074, -0.1278')).toBeInTheDocument()
  })

  it('renders an empty message when the window has no fixes', () => {
    useDevicesStore.setState({ historyStatus: 'success', history: [] })
    render(<DeviceHistoryPanel deviceId={1} />)

    expect(
      screen.getByText('No fixes recorded in this window.'),
    ).toBeInTheDocument()
  })
})
