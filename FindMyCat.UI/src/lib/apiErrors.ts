import type { ApiError } from '@/api/http'

export interface ErrorMessage {
  title: string
  description: string
}

/**
 * Fallback copy for failures with no domain error code. The API deliberately omits a code when the
 * HTTP status already says everything there is to say, so this branches on the status instead.
 */
export function getGenericErrorMessage(
  error: ApiError,
  fallbackDescription = 'Please try again.',
): ErrorMessage {
  switch (error.status) {
    case 401:
      return {
        title: 'Your session has ended',
        description: error.message || 'Sign in again to continue.',
      }
    case 403:
      return {
        title: 'Not allowed',
        description: error.message || "You don't have permission to do that.",
      }
    case 404:
      return {
        title: 'Not found',
        description: error.message || 'That no longer exists.',
      }
    case 429:
      return {
        title: 'Too many attempts',
        description: error.message || 'Please wait a moment and try again.',
      }
    case 400:
      return {
        title: 'That request was not valid',
        description: error.message || 'Check the details and try again.',
      }
    default:
      return {
        title: 'Something went wrong',
        description: error.message || fallbackDescription,
      }
  }
}
