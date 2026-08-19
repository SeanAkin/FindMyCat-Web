import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/components/auth/RequireAuth'
import { useAuthStore } from '@/stores/authStore'

function renderProtectedRoute() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<RequireAuth />}>
          <Route path="/" element={<div>Protected content</div>} />
        </Route>
        <Route path="/login" element={<div>Login page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAuth', () => {
  afterEach(() => {
    useAuthStore.setState({ status: 'loading', user: null })
  })

  it('renders nothing but the login page while the session check is loading', () => {
    useAuthStore.setState({ status: 'loading', user: null })
    renderProtectedRoute()

    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
    expect(screen.queryByText('Login page')).not.toBeInTheDocument()
  })

  it('redirects to /login when unauthenticated', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    renderProtectedRoute()

    expect(screen.getByText('Login page')).toBeInTheDocument()
  })

  it('renders the protected route when authenticated', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: '1', email: 'a@b.com', displayName: 'A', role: 'User' },
    })
    renderProtectedRoute()

    expect(screen.getByText('Protected content')).toBeInTheDocument()
  })
})
