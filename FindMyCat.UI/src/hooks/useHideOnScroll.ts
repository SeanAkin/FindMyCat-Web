import { useEffect, useRef, useState } from 'react'

const SCROLL_DELTA_THRESHOLD = 8

export function useHideOnScroll(alwaysVisibleWithinPx: number): boolean {
  const [hidden, setHidden] = useState(false)
  const lastYRef = useRef(0)

  useEffect(() => {
    lastYRef.current = Math.max(window.scrollY, 0)

    const handleScroll = () => {
      const y = Math.max(window.scrollY, 0)

      if (y <= alwaysVisibleWithinPx) {
        setHidden(false)
        lastYRef.current = y
        return
      }

      const delta = y - lastYRef.current
      if (Math.abs(delta) < SCROLL_DELTA_THRESHOLD) return

      setHidden(delta > 0)
      lastYRef.current = y
    }

    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [alwaysVisibleWithinPx])

  return hidden
}
