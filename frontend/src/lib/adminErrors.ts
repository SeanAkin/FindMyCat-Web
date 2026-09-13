import type { ApiError } from '@/api/http'
import { getGenericErrorMessage, type ErrorMessage } from '@/lib/apiErrors'

export type AdminErrorMessage = ErrorMessage

export function getAdminErrorMessage(error: ApiError): AdminErrorMessage {
  switch (error.code) {
    case 'primary_administrator_protected':
      return {
        title: 'That account is protected',
        description:
          error.message ||
          "The founding administrator's access can't be changed here.",
      }
    case 'allowed_email_not_found':
      return {
        title: 'That email is not on the allow-list',
        description:
          error.message ||
          'It may have already been removed. Refresh to check.',
      }
    case 'user_not_found':
      return {
        title: 'That user no longer exists',
        description:
          error.message || 'Their account may have already been removed.',
      }
    case 'credential_not_configured':
      return {
        title: 'Nothing to remove',
        description:
          error.message || 'That connection is not currently configured.',
      }
    default:
      return getGenericErrorMessage(error)
  }
}
