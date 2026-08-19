import { describe, expect, it } from 'vitest'
import { formatTimeAgo } from '@/lib/timeAgo'

const now = new Date('2026-08-16T12:00:00.000Z')

describe('formatTimeAgo', () => {
  it('shows "Just now" for the first few seconds', () => {
    expect(formatTimeAgo(new Date('2026-08-16T11:59:55.000Z'), now)).toBe(
      'Just now',
    )
  })

  it('shows whole seconds under a minute', () => {
    expect(formatTimeAgo(new Date('2026-08-16T11:59:30.000Z'), now)).toBe(
      '30s ago',
    )
  })

  it('shows whole minutes under an hour', () => {
    expect(formatTimeAgo(new Date('2026-08-16T11:45:00.000Z'), now)).toBe(
      '15m ago',
    )
  })

  it('shows whole hours under a day', () => {
    expect(formatTimeAgo(new Date('2026-08-16T08:00:00.000Z'), now)).toBe(
      '4h ago',
    )
  })

  it('shows whole days under a week', () => {
    expect(formatTimeAgo(new Date('2026-08-13T12:00:00.000Z'), now)).toBe(
      '3d ago',
    )
  })

  it('falls back to a calendar date at a week or older', () => {
    const date = new Date('2026-08-01T12:00:00.000Z')
    const expected = date.toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    })

    expect(formatTimeAgo(date, now)).toBe(expected)
  })

  it('clamps a future date (clock skew) to "Just now" instead of going negative', () => {
    expect(formatTimeAgo(new Date('2026-08-16T12:05:00.000Z'), now)).toBe(
      'Just now',
    )
  })
})
