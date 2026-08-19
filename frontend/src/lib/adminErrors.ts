import type { ApiError } from '@/api/http'

export interface AdminErrorMessage {
  title: string
  description: string
}

export function getAdminErrorMessage(error: ApiError): AdminErrorMessage {
  switch (error.code) {
    case 'primary_administrator_protected':
      return {
        title: 'That account is protected',
        description:
          error.message ||
          "The founding administrator's access can't be changed here.",
      }
    default:
      return {
        title: 'Something went wrong',
        description: error.message || 'Please try again.',
      }
  }
}
