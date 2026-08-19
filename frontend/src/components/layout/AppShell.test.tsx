import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppShell } from '@/components/layout/AppShell'
import { useAuthStore } from '@/stores/authStore'

function renderAppShell() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<div>Devices content</div>} />
        </Route>
        <Route path="/login" element={<div>Login page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppShell', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(null, { status: 204 })),
    )
    useAuthStore.setState({
      status: 'authenticated',
      user: {
        id: '1',
        email: 'cat.owner@example.com',
        displayName: 'Cat Owner',
        role: 'User',
      },
    })
  })

  afterEach(() => {
    useAuthStore.setState({ status: 'loading', user: null })
    vi.unstubAllGlobals()
  })

  it('logs out and navigates to the login page', async () => {
    const user = userEvent.setup()
    renderAppShell()

    await user.click(screen.getByRole('button', { name: 'Account menu' }))
    await user.click(await screen.findByRole('menuitem', { name: /log out/i }))

    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(useAuthStore.getState().status).toBe('unauthenticated')
  })
})
