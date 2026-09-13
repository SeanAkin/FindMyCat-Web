import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { AllowedEmailsPanel } from '@/components/admin/AllowedEmailsPanel'
import { addAllowedEmail, removeAllowedEmail } from '@/api/admin'
import { ApiError } from '@/api/http'
import { useAdminStore } from '@/stores/adminStore'

vi.mock('@/api/admin', () => ({
  addAllowedEmail: vi.fn(),
  removeAllowedEmail: vi.fn(),
  listAllowedEmails: vi.fn(),
  listUsers: vi.fn(),
  setUserRole: vi.fn(),
}))

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}))

describe('AllowedEmailsPanel', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useAdminStore.setState({
      allowedEmails: [],
      allowedEmailsStatus: 'idle',
      allowedEmailsError: null,
      users: [],
    })
  })

  it('renders the allow-list once loaded', () => {
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'nova@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
    })
    render(<AllowedEmailsPanel />)

    expect(screen.getByText('nova@example.com')).toBeInTheDocument()
  })

  it('adds an email and refreshes the list', async () => {
    vi.mocked(addAllowedEmail).mockResolvedValue({
      email: 'new@example.com',
      addedAt: '2026-08-17T00:00:00.000Z',
    })
    const fetchAllowedEmails = vi.fn()
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [],
      fetchAllowedEmails,
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'new@example.com',
    )
    await user.click(screen.getByRole('button', { name: 'Add email' }))

    await waitFor(() =>
      expect(addAllowedEmail).toHaveBeenCalledWith('new@example.com'),
    )
    expect(fetchAllowedEmails).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith(
      'new@example.com can now sign in.',
    )
    expect(screen.getByPlaceholderText('name@example.com')).toHaveValue('')
  })

  it('requires confirmation before removing an email, then refreshes both the allow-list and the users table', async () => {
    vi.mocked(removeAllowedEmail).mockResolvedValue(undefined)
    const fetchAllowedEmails = vi.fn()
    const fetchUsers = vi.fn()
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'gone@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
      fetchAllowedEmails,
      fetchUsers,
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.click(
      screen.getByRole('button', { name: 'Remove gone@example.com' }),
    )
    expect(removeAllowedEmail).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Remove' }))

    await waitFor(() =>
      expect(removeAllowedEmail).toHaveBeenCalledWith('gone@example.com'),
    )
    expect(fetchAllowedEmails).toHaveBeenCalled()
    expect(fetchUsers).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith(
      'gone@example.com removed from the allow-list.',
    )
  })

  it('cancelling the removal dialog does not call removeAllowedEmail', async () => {
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'gone@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.click(
      screen.getByRole('button', { name: 'Remove gone@example.com' }),
    )
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(removeAllowedEmail).not.toHaveBeenCalled()
  })

  it('warns that removal deletes the account when the invite has already been joined', async () => {
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'joined@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
      users: [
        {
          id: 'user-1',
          email: 'joined@example.com',
          displayName: 'Joined Person',
          role: 'User',
          isPrimaryAdministrator: false,
          createdAt: '2026-08-01T00:00:00.000Z',
          lastLoginAt: '2026-08-01T00:00:00.000Z',
        },
      ],
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.click(
      screen.getByRole('button', { name: 'Remove joined@example.com' }),
    )

    const dialog = await screen.findByRole('alertdialog')
    expect(
      within(dialog).getByText(/their account will be deleted/i),
    ).toBeInTheDocument()
  })

  it('shows a mapped error toast when removal is blocked for the primary administrator', async () => {
    vi.mocked(removeAllowedEmail).mockRejectedValue(
      new ApiError(
        409,
        'primary_administrator_protected',
        "The original administrator account's email cannot be removed from the allow-list.",
      ),
    )
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'founder@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.click(
      screen.getByRole('button', { name: 'Remove founder@example.com' }),
    )
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Remove' }))

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('That account is protected', {
        description:
          "The original administrator account's email cannot be removed from the allow-list.",
      }),
    )
  })
})
