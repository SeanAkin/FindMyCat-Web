import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { RequireAdmin } from '@/components/auth/RequireAdmin'
import { useAuthStore } from '@/stores/authStore'

function renderAdminRoute() {
  return render(
    <MemoryRouter initialEntries={['/admin']}>
      <Routes>
        <Route element={<RequireAdmin />}>
          <Route path="/admin" element={<div>Admin content</div>} />
        </Route>
        <Route path="/" element={<div>Devices page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAdmin', () => {
  afterEach(() => {
    useAuthStore.setState({ status: 'loading', user: null })
  })

  it('redirects non-admin users to /', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: '1', email: 'a@b.com', displayName: 'A', role: 'User' },
    })
    renderAdminRoute()

    expect(screen.getByText('Devices page')).toBeInTheDocument()
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument()
  })

  it('renders the admin route for administrators', () => {
    useAuthStore.setState({
      status: 'authenticated',
      user: {
        id: '1',
        email: 'a@b.com',
        displayName: 'A',
        role: 'Administrator',
      },
    })
    renderAdminRoute()

    expect(screen.getByText('Admin content')).toBeInTheDocument()
  })
})
