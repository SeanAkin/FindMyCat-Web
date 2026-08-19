import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { UsersPanel } from '@/components/admin/UsersPanel'
import { setUserRole } from '@/api/admin'
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

const primaryAdmin = {
  id: 'primary-1',
  email: 'founder@example.com',
  displayName: 'Founder',
  role: 'Administrator' as const,
  isPrimaryAdministrator: true,
  createdAt: '2026-01-01T00:00:00.000Z',
  lastLoginAt: '2026-08-01T00:00:00.000Z',
}

const regularAdmin = {
  id: 'admin-2',
  email: 'admin@example.com',
  displayName: 'Household Admin',
  role: 'Administrator' as const,
  isPrimaryAdministrator: false,
  createdAt: '2026-01-02T00:00:00.000Z',
  lastLoginAt: '2026-08-01T00:00:00.000Z',
}

const regularUser = {
  id: 'user-3',
  email: 'member@example.com',
  displayName: 'Household Member',
  role: 'User' as const,
  isPrimaryAdministrator: false,
  createdAt: '2026-01-03T00:00:00.000Z',
  lastLoginAt: '2026-08-01T00:00:00.000Z',
}

describe('UsersPanel', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useAdminStore.setState({ users: [], usersStatus: 'idle', usersError: null })
  })

  it('disables the demote control for the primary administrator', () => {
    useAdminStore.setState({
      usersStatus: 'success',
      users: [primaryAdmin],
    })
    render(<UsersPanel />)

    expect(
      screen.getByRole('button', { name: 'Protected' }),
    ).toBeDisabled()
    expect(
      screen.queryByRole('button', { name: /demote/i }),
    ).not.toBeInTheDocument()
  })

  it('promotes a regular user without a confirmation dialog', async () => {
    vi.mocked(setUserRole).mockResolvedValue(undefined)
    const fetchUsers = vi.fn()
    useAdminStore.setState({
      usersStatus: 'success',
      users: [regularUser],
      fetchUsers,
    })
    const user = userEvent.setup()
    render(<UsersPanel />)

    await user.click(screen.getByRole('button', { name: /promote/i }))

    await waitFor(() =>
      expect(setUserRole).toHaveBeenCalledWith('user-3', 'Administrator'),
    )
    expect(fetchUsers).toHaveBeenCalled()
    expect(toast.success).toHaveBeenCalledWith(
      'Household Member is now an administrator.',
    )
  })

  it('requires confirmation before demoting a non-primary administrator', async () => {
    vi.mocked(setUserRole).mockResolvedValue(undefined)
    const fetchUsers = vi.fn()
    useAdminStore.setState({
      usersStatus: 'success',
      users: [regularAdmin],
      fetchUsers,
    })
    const user = userEvent.setup()
    render(<UsersPanel />)

    await user.click(screen.getByRole('button', { name: /demote/i }))
    expect(setUserRole).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Demote' }))

    await waitFor(() =>
      expect(setUserRole).toHaveBeenCalledWith('admin-2', 'User'),
    )
    expect(fetchUsers).toHaveBeenCalled()
  })

  it('cancelling the demote dialog does not call setUserRole', async () => {
    useAdminStore.setState({
      usersStatus: 'success',
      users: [regularAdmin],
    })
    const user = userEvent.setup()
    render(<UsersPanel />)

    await user.click(screen.getByRole('button', { name: /demote/i }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(setUserRole).not.toHaveBeenCalled()
  })
})
