import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { LiveUpdatesBadge } from '@/components/devices/LiveUpdatesBadge'
import { useDevicesStore } from '@/stores/devicesStore'

describe('LiveUpdatesBadge', () => {
  afterEach(() => {
    useDevicesStore.setState({
      hubStatus: 'stopped',
      traccarConnection: null,
      isTraccarOutageOngoing: false,
    })
  })

  it('renders nothing while live updates are stopped', () => {
    const { container } = render(<LiveUpdatesBadge />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows Live while Traccar is connected', () => {
    useDevicesStore.setState({
      hubStatus: 'connected',
      traccarConnection: { connected: true, code: null },
    })

    render(<LiveUpdatesBadge />)

    expect(screen.getByText('Live')).toBeInTheDocument()
  })

  it('shows Connecting while the hub reconnects', () => {
    useDevicesStore.setState({
      hubStatus: 'reconnecting',
      traccarConnection: { connected: true, code: null },
    })

    render(<LiveUpdatesBadge />)

    expect(screen.getByText('Connecting')).toBeInTheDocument()
  })

  it('shows Connecting while the hub waits for its first Traccar status', () => {
    useDevicesStore.setState({
      hubStatus: 'connected',
      traccarConnection: null,
    })

    render(<LiveUpdatesBadge />)

    expect(screen.getByText('Connecting')).toBeInTheDocument()
  })

  it('shows Connecting during the traccar_unavailable grace period', () => {
    useDevicesStore.setState({
      hubStatus: 'connected',
      traccarConnection: { connected: false, code: 'traccar_unavailable' },
      isTraccarOutageOngoing: false,
    })

    render(<LiveUpdatesBadge />)

    expect(screen.getByText('Connecting')).toBeInTheDocument()
  })

  it('shows the polling interval when live updates are disabled', () => {
    useDevicesStore.setState({
      hubStatus: 'connected',
      traccarConnection: { connected: false, code: 'live_updates_disabled' },
    })

    render(<LiveUpdatesBadge />)

    expect(screen.getByText('Refreshes every 15s')).toBeInTheDocument()
  })
})
