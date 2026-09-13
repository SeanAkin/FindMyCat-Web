import { describe, expect, it } from 'vitest'
import { ApiError } from '@/api/http'
import { getDeviceErrorMessage } from '@/lib/deviceErrors'

describe('getDeviceErrorMessage', () => {
  it('maps traccar_not_configured to a setup nudge, not an error', () => {
    const message = getDeviceErrorMessage(
      new ApiError(409, 'traccar_not_configured', 'not configured'),
    )

    expect(message.variant).toBe('setup')
    expect(message.title).toBe('Location tracking is not set up yet')
  })

  it('maps traccar_credential_rejected to an admin fix-it message', () => {
    const message = getDeviceErrorMessage(
      new ApiError(409, 'traccar_credential_rejected', 'rejected'),
    )

    expect(message.variant).toBe('error')
    expect(message.description).toContain('re-enter it')
  })

  it('maps traccar_unavailable to a retry message', () => {
    const message = getDeviceErrorMessage(
      new ApiError(502, 'traccar_unavailable', 'unavailable'),
    )

    expect(message.description).toContain('Try again')
  })

  it('maps hologram_not_configured to a setup nudge, not an error', () => {
    const message = getDeviceErrorMessage(
      new ApiError(409, 'hologram_not_configured', 'not configured'),
    )

    expect(message.variant).toBe('setup')
    expect(message.title).toBe('Collar commands are not set up yet')
  })

  it('maps hologram_credential_rejected to an admin fix-it message', () => {
    const message = getDeviceErrorMessage(
      new ApiError(409, 'hologram_credential_rejected', 'rejected'),
    )

    expect(message.variant).toBe('error')
    expect(message.description).toContain('re-enter it')
  })

  it('maps hologram_device_not_found to a re-pair message', () => {
    const message = getDeviceErrorMessage(
      new ApiError(404, 'hologram_device_not_found', 'not found'),
    )

    expect(message.variant).toBe('error')
    expect(message.title).toBe('Collar not recognized')
  })

  it('maps hologram_unavailable to a retry message', () => {
    const message = getDeviceErrorMessage(
      new ApiError(502, 'hologram_unavailable', 'unavailable'),
    )

    expect(message.description).toContain('Try again')
  })

  it('passes through the backend message for range validation errors', () => {
    const message = getDeviceErrorMessage(
      new ApiError(
        400,
        'range_too_large',
        'History range must not exceed 31 days.',
      ),
    )

    expect(message.title).toBe('Invalid date range')
    expect(message.description).toBe('History range must not exceed 31 days.')
  })

  it('falls back to a generic message for unmapped or network errors', () => {
    const message = getDeviceErrorMessage(new ApiError(0, null, ''))

    expect(message.title).toBe('Something went wrong')
    expect(message.description).toBe(
      'Failed to load device data. Please try again.',
    )
  })
})
