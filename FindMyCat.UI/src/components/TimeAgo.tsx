import { useSyncExternalStore } from 'react'
import { getClockNow, subscribeToClock } from '@/lib/clock'
import { formatTimeAgo } from '@/lib/timeAgo'

export function TimeAgo({ date }: { date: string }) {
  const now = useSyncExternalStore(subscribeToClock, getClockNow)
  return formatTimeAgo(new Date(date), new Date(now))
}
