import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { login, register } from '@/api/auth'
import { ApiError } from '@/api/http'
import { LoginPage } from '@/pages/LoginPage'
import { useAuthStore } from '@/stores/authStore'

vi.mock('@/api/auth', () => ({
  getSession: vi.fn(),
  getAuthProviders: vi.fn(),
  logout: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
}))

function renderLoginPage(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<div>Devices page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('LoginPage', () => {
  afterEach(() => {
    vi.clearAllMocks()
    useAuthStore.setState({ status: 'loading', user: null, providers: ['Password', 'Google'] })
  })

  it('shows the sign-in button by default', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login')

    expect(
      screen.getByRole('button', { name: 'Sign in with Google' }),
    ).toHaveAttribute('href', '/auth/login')
  })

  it('hides the Google sign-in button when Google auth is disabled', () => {
    useAuthStore.setState({
      status: 'unauthenticated',
      user: null,
      providers: ['Password'],
    })
    renderLoginPage('/login')

    expect(
      screen.queryByRole('button', { name: 'Sign in with Google' }),
    ).not.toBeInTheDocument()
  })

  it('shows the access-denied screen for a not-allow-listed email', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login?error=not_allow_listed')

    expect(screen.getByText('Access not granted')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Sign in with Google' }),
    ).not.toBeInTheDocument()
  })

  it('also shows the access-denied screen for the legacy access_denied code', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login?error=access_denied')

    expect(screen.getByText('Access not granted')).toBeInTheDocument()
  })

  it('shows a generic failure message alongside the sign-in button for other errors', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login?error=sign_in_failed')

    expect(
      screen.getByText('Sign-in failed. Please try again.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Sign in with Google' }),
    ).toBeInTheDocument()
  })

  it('redirects away when already authenticated', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: '1', email: 'a@b.com', displayName: 'A', role: 'User' },
    })
    renderLoginPage('/login')

    expect(screen.getByText('Devices page')).toBeInTheDocument()
  })

  it('shows a specific message when Google sign-in collides with a password account', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login?error=email_registered_with_password')

    expect(
      screen.getByText(/already has a password-based account/i),
    ).toBeInTheDocument()
  })

  it('signs in with email and password', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    vi.mocked(login).mockResolvedValue({
      id: '1',
      email: 'cat@example.com',
      displayName: 'Cat Owner',
      role: 'User',
    })
    const user = userEvent.setup()
    renderLoginPage('/login')

    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'cat@example.com',
    )
    await user.type(screen.getByPlaceholderText('••••••••'), 'Str0ng!Pass')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() =>
      expect(login).toHaveBeenCalledWith('cat@example.com', 'Str0ng!Pass'),
    )
    expect(useAuthStore.getState().status).toBe('authenticated')
  })

  it('shows a mapped error when sign-in fails', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    vi.mocked(login).mockRejectedValue(
      new ApiError(401, 'invalid_credentials', 'Incorrect email or password.'),
    )
    const user = userEvent.setup()
    renderLoginPage('/login')

    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'cat@example.com',
    )
    await user.type(screen.getByPlaceholderText('••••••••'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(
      await screen.findByText('Incorrect email or password'),
    ).toBeInTheDocument()
  })

  it('switches to the create-account form and registers', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    vi.mocked(register).mockResolvedValue({
      id: '2',
      email: 'new@example.com',
      displayName: 'New Person',
      role: 'User',
    })
    const user = userEvent.setup()
    renderLoginPage('/login')

    await user.click(
      screen.getByRole('button', { name: /don't have an account/i }),
    )
    expect(screen.getByPlaceholderText('Jane Doe')).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('Jane Doe'), 'New Person')
    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'new@example.com',
    )
    await user.type(screen.getByPlaceholderText('••••••••'), 'Str0ng!Pass')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() =>
      expect(register).toHaveBeenCalledWith(
        'new@example.com',
        'Str0ng!Pass',
        'New Person',
      ),
    )
    expect(useAuthStore.getState().status).toBe('authenticated')
  })

  it('lists every unmet password requirement separately', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    vi.mocked(register).mockRejectedValue(
      new ApiError(
        400,
        'weak_password',
        'That password does not meet the requirements.',
        {
          password: [
            'Password must be at least 8 characters long.',
            'Password must contain at least one uppercase letter.',
            'Password must contain at least one symbol.',
          ],
        },
      ),
    )
    const user = userEvent.setup()
    renderLoginPage('/login')

    await user.click(
      screen.getByRole('button', { name: /don't have an account/i }),
    )
    await user.type(screen.getByPlaceholderText('Jane Doe'), 'New Person')
    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'new@example.com',
    )
    await user.type(screen.getByPlaceholderText('••••••••'), 'weak')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(
      await screen.findByText('Password must be at least 8 characters long.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Password must contain at least one uppercase letter.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Password must contain at least one symbol.'),
    ).toBeInTheDocument()

    // The static hint gives way to the requirements actually missed.
    expect(screen.queryByText(/8-64 characters/)).not.toBeInTheDocument()
  })

  it('shows messages for several rejected fields at once', async () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    vi.mocked(register).mockRejectedValue(
      new ApiError(400, null, 'The request was not valid.', {
        displayName: ['The DisplayName field is required.'],
        password: ['Password must contain at least one symbol.'],
      }),
    )
    const user = userEvent.setup()
    renderLoginPage('/login')

    await user.click(
      screen.getByRole('button', { name: /don't have an account/i }),
    )
    await user.type(screen.getByPlaceholderText('Jane Doe'), ' ')
    await user.type(
      screen.getByPlaceholderText('name@example.com'),
      'new@example.com',
    )
    await user.type(screen.getByPlaceholderText('••••••••'), 'Str0ngPass')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(
      await screen.findByText('The DisplayName field is required.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Password must contain at least one symbol.'),
    ).toBeInTheDocument()
  })
})
