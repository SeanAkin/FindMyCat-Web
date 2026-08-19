import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { toast } from 'sonner'
import { DeviceCommands } from '@/components/devices/DeviceCommands'
import { markDeviceActive, markDeviceLost, pingDevice } from '@/api/devices'
import { ApiError } from '@/api/http'
import { useDevicesStore } from '@/stores/devicesStore'

vi.mock('@/api/devices', () => ({
  pingDevice: vi.fn(),
  markDeviceLost: vi.fn(),
  markDeviceActive: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

function renderDeviceCommands() {
  return render(
    <MemoryRouter>
      <DeviceCommands deviceId={1} deviceName="Nova" />
    </MemoryRouter>,
  )
}

describe('DeviceCommands', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useDevicesStore.setState({ lastSentDeviceCommand: {} })
  })

  it('sends a ping without a confirmation dialog', async () => {
    vi.mocked(pingDevice).mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderDeviceCommands()

    await user.click(screen.getByRole('button', { name: /ping/i }))

    await waitFor(() => expect(pingDevice).toHaveBeenCalledWith(1))
    expect(toast.success).toHaveBeenCalledWith('Ping sent to Nova.')
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
  })

  it('requires confirmation before sending Mark Lost, then records the intent', async () => {
    vi.mocked(markDeviceLost).mockResolvedValue(undefined)
    const user = userEvent.setup()
    renderDeviceCommands()

    await user.click(screen.getByRole('button', { name: 'Mark Lost' }))
    expect(markDeviceLost).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Mark Lost' }))

    await waitFor(() => expect(markDeviceLost).toHaveBeenCalledWith(1))
    expect(toast.success).toHaveBeenCalledWith(
      'Lost mode command sent to Nova.',
    )
    expect(useDevicesStore.getState().lastSentDeviceCommand[1]).toBe('lost')
  })

  it('cancelling the confirmation dialog does not send Mark Active', async () => {
    const user = userEvent.setup()
    renderDeviceCommands()

    await user.click(screen.getByRole('button', { name: 'Mark Active' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(markDeviceActive).not.toHaveBeenCalled()
  })

  it('maps a hologram error to a toast with title and description', async () => {
    vi.mocked(pingDevice).mockRejectedValue(
      new ApiError(409, 'hologram_not_configured', 'not configured'),
    )
    const user = userEvent.setup()
    renderDeviceCommands()

    await user.click(screen.getByRole('button', { name: /ping/i }))

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'Collar commands are not set up yet',
        {
          description:
            'Ask a household administrator to add the Hologram connection in Admin settings.',
        },
      ),
    )
  })
})
