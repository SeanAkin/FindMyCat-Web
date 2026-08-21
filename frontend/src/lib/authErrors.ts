import type { ApiError } from '@/api/http'

export interface AuthErrorMessage {
  title: string
  description: string
}

export function getAuthErrorMessage(error: ApiError): AuthErrorMessage {
  switch (error.code) {
    case 'not_allow_listed':
      return {
        title: 'Access not granted',
        description:
          error.message ||
          "This email hasn't been added to the allow-list yet. Ask a household administrator to add it.",
      }
    case 'email_already_registered':
      return {
        title: 'Account already exists',
        description:
          error.message ||
          'An account with this email already exists. Try signing in instead.',
      }
    case 'email_registered_with_password':
      return {
        title: 'Sign in with your password instead',
        description:
          error.message || 'This email already has a password-based account.',
      }
    case 'weak_password':
      return {
        title: "That password won't work",
        description:
          error.message ||
          'Choose a password that meets the requirements below.',
      }
    case 'invalid_credentials':
      return {
        title: 'Incorrect email or password',
        description: error.message || 'Please try again.',
      }
    default:
      return {
        title: 'Something went wrong',
        description: error.message || 'Please try again.',
      }
  }
}
