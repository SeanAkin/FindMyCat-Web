import type { DeviceStatus } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

interface DeviceStatusBadgeProps {
  status: DeviceStatus
  disabled: boolean
}

export function DeviceStatusBadge({
  status,
  disabled,
}: DeviceStatusBadgeProps) {
  if (disabled) {
    return (
      <Badge variant="outline" className="gap-1.5">
        <span className="size-1.5 rounded-full bg-muted-foreground" />
        Disabled
      </Badge>
    )
  }

  const isOnline = status === 'online'

  return (
    <Badge variant={isOnline ? 'default' : 'outline'} className="gap-1.5">
      <span
        className={cn(
          'size-1.5 rounded-full',
          isOnline ? 'bg-primary-foreground' : 'bg-muted-foreground',
        )}
      />
      {isOnline ? 'Online' : 'Offline'}
    </Badge>
  )
}
