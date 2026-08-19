import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { LoginPage } from '@/pages/LoginPage'
import { useAuthStore } from '@/stores/authStore'

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
    useAuthStore.setState({ status: 'loading', user: null })
  })

  it('shows the sign-in button by default', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login')

    expect(
      screen.getByRole('button', { name: 'Sign in with Google' }),
    ).toHaveAttribute('href', '/auth/login')
  })

  it('shows the access-denied screen for a not-allow-listed email', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderLoginPage('/login?error=access_denied')

    expect(screen.getByText('Access not granted')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Sign in with Google' }),
    ).not.toBeInTheDocument()
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
})
