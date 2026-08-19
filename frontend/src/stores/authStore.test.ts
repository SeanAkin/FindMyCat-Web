import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('authStore', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('checkSession sets an authenticated status and user on success', async () => {
    const session = {
      id: '1',
      email: 'cat.owner@example.com',
      displayName: 'Cat Owner',
      role: 'Administrator',
    }
    vi.mocked(fetch).mockResolvedValue(jsonResponse(200, session))
    const { useAuthStore } = await import('./authStore')

    await useAuthStore.getState().checkSession()

    expect(useAuthStore.getState().status).toBe('authenticated')
    expect(useAuthStore.getState().user).toEqual(session)
  })

  it('checkSession sets an unauthenticated status on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(401, {}))
    const { useAuthStore } = await import('./authStore')

    await useAuthStore.getState().checkSession()

    expect(useAuthStore.getState().status).toBe('unauthenticated')
    expect(useAuthStore.getState().user).toBeNull()
  })

  it('checkSession sets an error status on a network failure', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))
    const { useAuthStore } = await import('./authStore')

    await useAuthStore.getState().checkSession()

    expect(useAuthStore.getState().status).toBe('error')
    expect(useAuthStore.getState().user).toBeNull()
  })

  it('logout clears the store even if the request fails', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 500 }))
    const { useAuthStore } = await import('./authStore')
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: '1', email: 'a@b.com', displayName: 'A', role: 'User' },
    })

    await useAuthStore.getState().logout()

    expect(useAuthStore.getState().status).toBe('unauthenticated')
    expect(useAuthStore.getState().user).toBeNull()
  })

  it('a 401 from any api call flips the store to unauthenticated', async () => {
    const { useAuthStore } = await import('./authStore')
    const { api } = await import('@/api/http')
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: '1', email: 'a@b.com', displayName: 'A', role: 'User' },
    })
    vi.mocked(fetch).mockResolvedValue(jsonResponse(401, {}))

    await api.get('/api/devices').catch(() => {})

    expect(useAuthStore.getState().status).toBe('unauthenticated')
    expect(useAuthStore.getState().user).toBeNull()
  })
})
