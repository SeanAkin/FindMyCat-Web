import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api, onUnauthorized } from '@/api/http'

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('api http wrapper', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    onUnauthorized(null)
    vi.unstubAllGlobals()
  })

  it('parses a successful JSON response', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(200, { id: 1, name: 'Nova' }),
    )

    const result = await api.get<{ id: number; name: string }>('/api/devices')

    expect(result).toEqual({ id: 1, name: 'Nova' })
    expect(fetch).toHaveBeenCalledWith(
      '/api/devices',
      expect.objectContaining({ method: 'GET', credentials: 'include' }),
    )
  })

  it('returns undefined for a 204 No Content response', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }))

    const result = await api.post<void>('/api/devices/1/ping')

    expect(result).toBeUndefined()
  })

  it('maps a {code,message} error body onto ApiError', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(409, {
        code: 'traccar_not_configured',
        message: 'Traccar is not configured.',
      }),
    )

    const error = await api
      .get<never>('/api/devices')
      .catch((caught: unknown) => caught as ApiError)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(409)
    expect(error.code).toBe('traccar_not_configured')
    expect(error.message).toBe('Traccar is not configured.')
    expect(error.isUnauthorized).toBe(false)
  })

  it('falls back to statusText and a null code when the error body is not JSON', async () => {
    vi.mocked(fetch).mockResolvedValue(
      new Response('<html>gateway error</html>', {
        status: 502,
        statusText: 'Bad Gateway',
      }),
    )

    const error = await api
      .get<never>('/api/devices')
      .catch((caught: unknown) => caught as ApiError)

    expect(error.status).toBe(502)
    expect(error.code).toBeNull()
    expect(error.message).toBe('Bad Gateway')
  })

  it('flags a 401 distinctly and notifies the registered unauthorized handler', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(401, {}))
    const handler = vi.fn()
    onUnauthorized(handler)

    const error = await api
      .get<never>('/auth/session')
      .catch((caught: unknown) => caught as ApiError)

    expect(error.isUnauthorized).toBe(true)
    expect(handler).toHaveBeenCalledOnce()
  })

  it('sends a JSON body and content-type header for mutating requests', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }))

    await api.put('/api/credentials/traccar', { apiToken: 'secret' })

    expect(fetch).toHaveBeenCalledWith(
      '/api/credentials/traccar',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ apiToken: 'secret' }),
        headers: { 'Content-Type': 'application/json' },
      }),
    )
  })
})
