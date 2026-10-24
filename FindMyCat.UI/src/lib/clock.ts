const TICK_MS = 1_000

const listeners = new Set<() => void>()
let now = Date.now()
let intervalId: number | undefined

export function subscribeToClock(listener: () => void): () => void {
  listeners.add(listener)
  if (intervalId === undefined) {
    now = Date.now()
    intervalId = window.setInterval(() => {
      now = Date.now()
      for (const notify of listeners) notify()
    }, TICK_MS)
  }

  return () => {
    listeners.delete(listener)
    if (listeners.size === 0) {
      window.clearInterval(intervalId)
      intervalId = undefined
    }
  }
}

function catchUpIfReadBeforeAnyoneSubscribed() {
  const isTicking = intervalId !== undefined
  const isOffByAWholeTick = Math.abs(Date.now() - now) >= TICK_MS
  if (!isTicking && isOffByAWholeTick) now = Date.now()
}

export function getClockNow(): number {
  catchUpIfReadBeforeAnyoneSubscribed()
  return now
}
