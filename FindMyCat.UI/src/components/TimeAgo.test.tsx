import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import { TimeAgo } from '@/components/TimeAgo'

describe('TimeAgo', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-08-16T12:00:00.000Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('keeps counting up while nothing else re-renders', () => {
    render(<TimeAgo date="2026-08-16T11:59:30.000Z" />)
    expect(screen.getByText('30s ago')).toBeInTheDocument()

    act(() => vi.advanceTimersByTime(15_000))

    expect(screen.getByText('45s ago')).toBeInTheDocument()
  })

  it('stops ticking once nothing shows a time', () => {
    const { unmount } = render(<TimeAgo date="2026-08-16T11:59:30.000Z" />)
    expect(vi.getTimerCount()).toBe(1)

    unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
