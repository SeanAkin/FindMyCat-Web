import { afterEach, describe, expect, it } from 'vitest'
import { act, fireEvent, renderHook } from '@testing-library/react'
import { useHideOnScroll } from '@/hooks/useHideOnScroll'

const OFFSET = 64

function scrollTo(y: number) {
  act(() => {
    Object.defineProperty(window, 'scrollY', { value: y, configurable: true })
    fireEvent.scroll(window)
  })
}

describe('useHideOnScroll', () => {
  afterEach(() => {
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true })
  })

  it('starts visible', () => {
    const { result } = renderHook(() => useHideOnScroll(OFFSET))
    expect(result.current).toBe(false)
  })

  it('hides when scrolling down past the offset and shows again on scroll up', () => {
    const { result } = renderHook(() => useHideOnScroll(OFFSET))

    scrollTo(300)
    expect(result.current).toBe(true)

    scrollTo(200)
    expect(result.current).toBe(false)
  })

  it('stays visible while within the offset of the top', () => {
    const { result } = renderHook(() => useHideOnScroll(OFFSET))

    scrollTo(OFFSET)
    expect(result.current).toBe(false)
  })

  it('ignores scroll deltas below the jitter threshold', () => {
    const { result } = renderHook(() => useHideOnScroll(OFFSET))

    scrollTo(300)
    expect(result.current).toBe(true)

    scrollTo(296)
    expect(result.current).toBe(true)
  })

  it('reveals when scrolling back to the top', () => {
    const { result } = renderHook(() => useHideOnScroll(OFFSET))

    scrollTo(300)
    scrollTo(0)
    expect(result.current).toBe(false)
  })
})
