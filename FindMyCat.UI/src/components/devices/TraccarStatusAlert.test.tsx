import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { TraccarStatusAlert } from '@/components/devices/TraccarStatusAlert'
import { useAuthStore } from '@/stores/authStore'
import { useDevicesStore } from '@/stores/devicesStore'

function renderAlert() {
  return render(
    <MemoryRouter>
      <TraccarStatusAlert />
    </MemoryRouter>,
  )
}

describe('TraccarStatusAlert', () => {
  afterEach(() => {
    useDevicesStore.setState({
      status: 'idle',
      hubStatus: 'stopped',
      traccarConnection: null,
      isTraccarOutageOngoing: false,
    })
    useAuthStore.setState({ status: 'loading', user: null })
  })

  it('points an administrator at re-entering a rejected credential', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: {
        id: '1',
        email: 'admin@example.com',
        displayName: 'Admin',
        role: 'Administrator',
      },
    })
    useDevicesStore.setState({
      status: 'success',
      hubStatus: 'connected',
      traccarConnection: {
        connected: false,
        code: 'traccar_credential_rejected',
      },
    })

    renderAlert()

    expect(
      screen.getByText('Tracking credential was rejected'),
    ).toBeInTheDocument()
    expect(screen.getByText('Go to Admin')).toHaveAttribute('href', '/admin')
  })

  it('leaves the error to the device list when the REST fetch failed', () => {
    useDevicesStore.setState({
      status: 'error',
      hubStatus: 'connected',
      traccarConnection: {
        connected: false,
        code: 'traccar_credential_rejected',
      },
    })

    const { container } = renderAlert()

    expect(container).toBeEmptyDOMElement()
  })
})
