import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
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

  it('removes an email and refreshes the list', async () => {
    vi.mocked(removeAllowedEmail).mockResolvedValue(undefined)
    const fetchAllowedEmails = vi.fn()
    useAdminStore.setState({
      allowedEmailsStatus: 'success',
      allowedEmails: [
        { email: 'gone@example.com', addedAt: '2026-08-01T00:00:00.000Z' },
      ],
      fetchAllowedEmails,
    })
    const user = userEvent.setup()
    render(<AllowedEmailsPanel />)

    await user.click(
      screen.getByRole('button', { name: 'Remove gone@example.com' }),
    )

    await waitFor(() =>
      expect(removeAllowedEmail).toHaveBeenCalledWith('gone@example.com'),
    )
    expect(fetchAllowedEmails).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith(
      'gone@example.com removed from the allow-list.',
    )
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

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'That account is protected',
        {
          description:
            "The original administrator account's email cannot be removed from the allow-list.",
        },
      ),
    )
  })
})
