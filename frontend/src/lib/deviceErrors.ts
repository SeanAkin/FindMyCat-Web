import type { ApiError } from '@/api/http'

export interface DeviceErrorMessage {
  title: string
  description: string
  /** "setup" is a first-time-configuration nudge, not a failure; renders less alarming. */
  variant: 'setup' | 'error'
  adminActionable: boolean
}

export function getDeviceErrorMessage(error: ApiError): DeviceErrorMessage {
  switch (error.code) {
    case 'traccar_not_configured':
      return {
        variant: 'setup',
        adminActionable: true,
        title: 'Location tracking is not set up yet',
        description:
          'Ask a household administrator to add the Traccar connection in Admin settings.',
      }
    case 'traccar_credential_rejected':
      return {
        variant: 'error',
        adminActionable: true,
        title: 'Tracking credential was rejected',
        description:
          'The stored Traccar token no longer works. An administrator needs to re-enter it.',
      }
    case 'traccar_unavailable':
      return {
        variant: 'error',
        adminActionable: false,
        title: 'Tracking service unavailable',
        description:
          'Could not reach the tracking service. Try again in a moment.',
      }
    case 'hologram_not_configured':
      return {
        variant: 'setup',
        adminActionable: true,
        title: 'Collar commands are not set up yet',
        description:
          'Ask a household administrator to add the Hologram connection in Admin settings.',
      }
    case 'hologram_credential_rejected':
      return {
        variant: 'error',
        adminActionable: true,
        title: 'Collar command credential was rejected',
        description:
          'The stored Hologram key no longer works. An administrator needs to re-enter it.',
      }
    case 'hologram_device_not_found':
      return {
        variant: 'error',
        adminActionable: false,
        title: 'Collar not recognized',
        description:
          "Hologram doesn't recognize this collar. It may need to be re-paired.",
      }
    case 'hologram_unavailable':
      return {
        variant: 'error',
        adminActionable: false,
        title: 'Collar command service unavailable',
        description:
          'Could not reach the collar command service. Try again in a moment.',
      }
    case 'invalid_range':
    case 'range_too_large':
      return {
        variant: 'error',
        adminActionable: false,
        title: 'Invalid date range',
        description: error.message,
      }
    default:
      return {
        variant: 'error',
        adminActionable: false,
        title: 'Something went wrong',
        description:
          error.message || 'Failed to load device data. Please try again.',
      }
  }
}
