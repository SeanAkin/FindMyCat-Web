import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import {
  selectDeviceSyncMode,
  useDevicesStore,
  type DevicesState,
} from '@/stores/devicesStore'

type LiveUpdatesState = 'live' | 'connecting' | 'polling'

function selectLiveUpdatesState(state: DevicesState): LiveUpdatesState | null {
  const mode = selectDeviceSyncMode(state)
  if (mode === 'off') return null
  if (mode === 'live') return 'live'

  const isHubStillConnecting =
    state.hubStatus === 'connecting' || state.hubStatus === 'reconnecting'
  const isWaitingForFirstStatus =
    state.hubStatus === 'connected' && state.traccarConnection === null
  const isInTraccarStartupGrace =
    state.traccarConnection?.code === 'traccar_unavailable' &&
    !state.isTraccarOutageOngoing
  return isHubStillConnecting ||
    isWaitingForFirstStatus ||
    isInTraccarStartupGrace
    ? 'connecting'
    : 'polling'
}

const labels: Record<LiveUpdatesState, string> = {
  live: 'Live',
  connecting: 'Connecting',
  polling: 'Refreshes every 15s',
}

export function LiveUpdatesBadge() {
  const liveUpdatesState = useDevicesStore(selectLiveUpdatesState)
  if (!liveUpdatesState) return null

  const isLive = liveUpdatesState === 'live'

  return (
    <Badge variant={isLive ? 'default' : 'outline'} className="gap-1.5">
      <span
        className={cn(
          'size-1.5 rounded-full',
          isLive ? 'bg-primary-foreground' : 'bg-muted-foreground',
          liveUpdatesState === 'connecting' &&
            'animate-pulse motion-reduce:animate-none',
        )}
      />
      {labels[liveUpdatesState]}
    </Badge>
  )
}
