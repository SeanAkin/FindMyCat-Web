const MINUTE = 60
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR
const WEEK = 7 * DAY

export function formatTimeAgo(date: Date, now: Date = new Date()): string {
  const seconds = Math.max(
    0,
    Math.round((now.getTime() - date.getTime()) / 1000),
  )

  if (seconds < 10) return 'Just now'
  if (seconds < MINUTE) return `${seconds}s ago`
  if (seconds < HOUR) return `${Math.floor(seconds / MINUTE)}m ago`
  if (seconds < DAY) return `${Math.floor(seconds / HOUR)}h ago`
  if (seconds < WEEK) return `${Math.floor(seconds / DAY)}d ago`

  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}
