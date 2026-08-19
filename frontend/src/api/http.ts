import type { ApiErrorCode } from '@/api/types'

export class ApiError extends Error {
  readonly status: number
  readonly code: ApiErrorCode | null

  constructor(status: number, code: ApiErrorCode | null, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }

  get isUnauthorized(): boolean {
    return this.status === 401
  }
}

export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError
    ? error
    : new ApiError(0, null, 'Could not reach the server.')
}

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

export function onUnauthorized(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const response = await fetch(path, {
    method: options.method ?? 'GET',
    credentials: 'include',
    signal: options.signal,
    headers:
      options.body === undefined
        ? undefined
        : { 'Content-Type': 'application/json' },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (response.status === 401) {
    unauthorizedHandler?.()
  }

  if (!response.ok) {
    throw await parseErrorResponse(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

async function parseErrorResponse(response: Response): Promise<ApiError> {
  const body = (await response.json().catch(() => null)) as {
    code?: string
    message?: string
  } | null
  return new ApiError(
    response.status,
    (body?.code as ApiErrorCode | undefined) ?? null,
    body?.message ?? response.statusText,
  )
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  post: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>(path, { method: 'POST', body, signal }),
  put: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>(path, { method: 'PUT', body, signal }),
  delete: <T>(path: string, signal?: AbortSignal) =>
    request<T>(path, { method: 'DELETE', signal }),
}
