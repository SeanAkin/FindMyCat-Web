import { describe, expect, it } from 'vitest'
import { validateHistoryRange } from '@/lib/historyRange'

describe('validateHistoryRange', () => {
  it('accepts a range exactly at the 31-day cap', () => {
    const from = new Date('2026-07-16T00:00:00.000Z')
    const to = new Date('2026-08-16T00:00:00.000Z')

    expect(validateHistoryRange(from, to)).toBeNull()
  })

  it('rejects a range one millisecond over the 31-day cap', () => {
    const from = new Date('2026-07-15T23:59:59.999Z')
    const to = new Date('2026-08-16T00:00:00.000Z')

    expect(validateHistoryRange(from, to)).toBe(
      'History range must not exceed 31 days.',
    )
  })

  it('rejects a range where from is after to', () => {
    const from = new Date('2026-08-16T00:00:00.000Z')
    const to = new Date('2026-08-15T00:00:00.000Z')

    expect(validateHistoryRange(from, to)).toBe(
      'The start date must be earlier than the end date.',
    )
  })

  it('rejects an equal from and to, matching the backend >= check', () => {
    const same = new Date('2026-08-16T00:00:00.000Z')

    expect(validateHistoryRange(same, same)).toBe(
      'The start date must be earlier than the end date.',
    )
  })

  it('accepts a normal short range', () => {
    const from = new Date('2026-08-15T00:00:00.000Z')
    const to = new Date('2026-08-16T00:00:00.000Z')

    expect(validateHistoryRange(from, to)).toBeNull()
  })
})
