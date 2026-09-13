import { BatteryLow, BatteryMedium, MapPin } from 'lucide-react'
import { DeviceStatusBadge } from '@/components/devices/DeviceStatusBadge'
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import type { DeviceResponse } from '@/api/types'
import { formatTimeAgo } from '@/lib/timeAgo'
import { cn } from '@/lib/utils'

interface DeviceCardProps {
  device: DeviceResponse
  selected: boolean
  onSelect: () => void
}

export function DeviceCard({ device, selected, onSelect }: DeviceCardProps) {
  const battery = device.position?.batteryLevel ?? null
  const BatteryIcon =
    battery !== null && battery <= 20 ? BatteryLow : BatteryMedium

  return (
    <Card
      render={<button type="button" />}
      onClick={onSelect}
      className={cn(
        'w-full cursor-pointer text-left transition-shadow hover:ring-1 hover:ring-primary/40',
        selected && 'ring-2 ring-primary',
      )}
    >
      <CardHeader>
        <CardTitle>{device.name}</CardTitle>
        <CardAction>
          <DeviceStatusBadge
            status={device.status}
            disabled={device.disabled}
          />
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-1.5 text-sm text-muted-foreground">
        <div className="flex items-center gap-1.5">
          <BatteryIcon className="size-3.5" />
          {battery !== null ? `${battery}%` : 'Battery unknown'}
        </div>
        <div className="flex items-center gap-1.5">
          <MapPin className="size-3.5" />
          {device.position
            ? `${device.position.latitude.toFixed(4)}, ${device.position.longitude.toFixed(4)}`
            : 'No fix yet'}
        </div>
        <div>
          {device.lastUpdate
            ? formatTimeAgo(new Date(device.lastUpdate))
            : 'Never reported'}
        </div>
      </CardContent>
    </Card>
  )
}
