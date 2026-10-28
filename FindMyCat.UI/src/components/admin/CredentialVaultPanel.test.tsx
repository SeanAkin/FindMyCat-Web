import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { CredentialVaultPanel } from '@/components/admin/CredentialVaultPanel'
import {
  deleteHologramKey,
  deleteIngestKey,
  deleteTraccarToken,
  getCredentialStatus,
  rotateIngestKey,
  setTraccarToken,
} from '@/api/credentials'
import { useAdminStore } from '@/stores/adminStore'

vi.mock('@/api/credentials', () => ({
  getCredentialStatus: vi.fn(),
  setTraccarToken: vi.fn(),
  setHologramKey: vi.fn(),
  deleteTraccarToken: vi.fn(),
  deleteHologramKey: vi.fn(),
  rotateIngestKey: vi.fn(),
  deleteIngestKey: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

const realFetchCredentialStatus =
  useAdminStore.getState().fetchCredentialStatus

describe('CredentialVaultPanel', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useAdminStore.setState({
      credentials: null,
      credentialsStatus: 'idle',
      credentialsError: null,
      fetchCredentialStatus: realFetchCredentialStatus,
    })
  })

  it('never renders a stored secret, only configured status', () => {
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: true,
        hologramConfigured: false,
        ingestConfigured: false,
      },
    })
    render(<CredentialVaultPanel />)

    expect(screen.getAllByText('Configured')).toHaveLength(1)
    expect(screen.getAllByText('Not configured')).toHaveLength(2)
    const traccarInput = screen.getByPlaceholderText('Traccar API token')
    const hologramInput = screen.getByPlaceholderText('Hologram API key')
    expect(traccarInput).toHaveAttribute('type', 'password')
    expect(traccarInput).toHaveValue('')
    expect(hologramInput).toHaveValue('')
  })

  it('shows a Set button when not configured and a Rotate button once configured', () => {
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: true,
        ingestConfigured: false,
      },
    })
    render(<CredentialVaultPanel />)

    expect(screen.getByRole('button', { name: 'Set' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Rotate' })).toBeInTheDocument()
  })

  it('sets a Traccar token, clears the input, and refreshes status', async () => {
    vi.mocked(setTraccarToken).mockResolvedValue(undefined)
    const fetchCredentialStatus = vi.fn()
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: false,
        ingestConfigured: false,
      },
      fetchCredentialStatus,
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    const traccarInput = screen.getByPlaceholderText('Traccar API token')
    await user.type(traccarInput, 'secret-token')
    await user.click(
      within(traccarInput.closest('form')!).getByRole('button', {
        name: 'Set',
      }),
    )

    await waitFor(() =>
      expect(setTraccarToken).toHaveBeenCalledWith('secret-token'),
    )
    expect(fetchCredentialStatus).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith('Traccar API token saved.')
    expect(traccarInput).toHaveValue('')
  })

  it('clears a configured credential only after confirming', async () => {
    vi.mocked(deleteHologramKey).mockResolvedValue(undefined)
    const fetchCredentialStatus = vi.fn()
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: true,
        ingestConfigured: false,
      },
      fetchCredentialStatus,
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Clear' }))
    expect(deleteHologramKey).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Clear' }))

    await waitFor(() => expect(deleteHologramKey).toHaveBeenCalled())
    expect(fetchCredentialStatus).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith('Hologram API key cleared.')
  })

  it('cancelling the clear dialog does not delete the credential', async () => {
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: true,
        hologramConfigured: false,
        ingestConfigured: false,
      },
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Clear' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(deleteTraccarToken).not.toHaveBeenCalled()
  })

  it('generates an ingest key, reveals it once, and refreshes status', async () => {
    vi.mocked(rotateIngestKey).mockResolvedValue({ apiKey: 'a'.repeat(64) })
    const fetchCredentialStatus = vi.fn()
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: false,
        ingestConfigured: false,
      },
      fetchCredentialStatus,
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Generate key' }))

    await waitFor(() => expect(rotateIngestKey).toHaveBeenCalled())
    expect(fetchCredentialStatus).toHaveBeenCalled()
    const keyField = screen.getByDisplayValue('a'.repeat(64))
    expect(keyField).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Done' }))
    expect(screen.queryByDisplayValue('a'.repeat(64))).not.toBeInTheDocument()
  })

  it('rotates an existing ingest key only after confirming', async () => {
    vi.mocked(rotateIngestKey).mockResolvedValue({ apiKey: 'b'.repeat(64) })
    const fetchCredentialStatus = vi.fn()
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: false,
        ingestConfigured: true,
      },
      fetchCredentialStatus,
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Rotate key' }))
    expect(rotateIngestKey).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Rotate key' }))

    await waitFor(() => expect(rotateIngestKey).toHaveBeenCalled())
    expect(fetchCredentialStatus).toHaveBeenCalled()
    expect(screen.getByDisplayValue('b'.repeat(64))).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
    )
  })

  it('disables a configured ingest key only after confirming', async () => {
    vi.mocked(deleteIngestKey).mockResolvedValue(undefined)
    const fetchCredentialStatus = vi.fn()
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: false,
        ingestConfigured: true,
      },
      fetchCredentialStatus,
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Disable' }))
    expect(deleteIngestKey).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Disable' }))

    await waitFor(() => expect(deleteIngestKey).toHaveBeenCalled())
    expect(fetchCredentialStatus).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith('Ingest API key disabled.')
  })

  it('keeps the revealed key visible while the status refresh runs', async () => {
    vi.mocked(rotateIngestKey).mockResolvedValue({ apiKey: 'c'.repeat(64) })
    vi.mocked(getCredentialStatus).mockResolvedValue({
      traccarConfigured: false,
      hologramConfigured: false,
      ingestConfigured: true,
    })
    useAdminStore.setState({
      credentialsStatus: 'success',
      credentials: {
        traccarConfigured: false,
        hologramConfigured: false,
        ingestConfigured: false,
      },
    })
    const user = userEvent.setup()
    render(<CredentialVaultPanel />)

    await user.click(screen.getByRole('button', { name: 'Generate key' }))

    await waitFor(() => expect(getCredentialStatus).toHaveBeenCalled())
    await waitFor(() =>
      expect(useAdminStore.getState().credentials?.ingestConfigured).toBe(true),
    )
    expect(screen.getByDisplayValue('c'.repeat(64))).toBeInTheDocument()
  })
})
