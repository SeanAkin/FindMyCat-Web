import { CalendarClock, Gauge, MapPin, Satellite } from 'lucide-react'
import type { ReactNode } from 'react'
import { DeviceCommands } from '@/components/devices/DeviceCommands'
import { DeviceHistoryPanel } from '@/components/devices/DeviceHistoryPanel'
import { DeviceStatusBadge } from '@/components/devices/DeviceStatusBadge'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatTimeAgo } from '@/lib/timeAgo'
import { useDevicesStore } from '@/stores/devicesStore'

export function DeviceDetailPanel() {
  const devices = useDevicesStore((state) => state.devices)
  const selectedDeviceId = useDevicesStore((state) => state.selectedDeviceId)
  const lastSentDeviceCommand = useDevicesStore(
    (state) => state.lastSentDeviceCommand,
  )
  const device =
    devices.find((candidate) => candidate.id === selectedDeviceId) ?? null

  if (!device) {
    return (
      <div className="flex h-full min-h-48 items-center justify-center rounded-xl border border-dashed border-border text-center text-muted-foreground">
        <p>Select a device to see details</p>
      </div>
    )
  }

  const position = device.position

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">{device.name}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <DeviceStatusBadge
              status={device.status}
              disabled={device.disabled}
            />
            {lastSentDeviceCommand[device.id] === 'lost' && (
              <Badge
                variant="outline"
                className="gap-1.5 text-amber-600 dark:text-amber-400"
              >
                <span className="size-1.5 rounded-full bg-current" />
                Lost mode command sent
              </Badge>
            )}
          </div>

          <DeviceCommands deviceId={device.id} deviceName={device.name} />

          {position ? (
            <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <Stat
                icon={<Gauge className="size-3.5" />}
                label="Speed"
                value={`${position.speedKnots.toFixed(1)} kn`}
              />
              <Stat
                icon={<Satellite className="size-3.5" />}
                label="Satellites"
                value={
                  position.satellites !== null
                    ? String(position.satellites)
                    : 'Unknown'
                }
              />
              <Stat
                icon={<MapPin className="size-3.5" />}
                label="Accuracy"
                value={`${position.accuracy.toFixed(0)} m`}
              />
              <Stat
                icon={<CalendarClock className="size-3.5" />}
                label="Last fix"
                value={formatTimeAgo(new Date(position.fixTime))}
              />
              <Stat
                label="Battery"
                value={
                  position.batteryLevel !== null
                    ? `${position.batteryLevel}%`
                    : 'Unknown'
                }
              />
              <Stat
                label="Coordinates"
                value={`${position.latitude.toFixed(5)}, ${position.longitude.toFixed(5)}`}
              />
            </dl>
          ) : (
            <p className="text-sm text-muted-foreground">
              This collar has not reported a location yet.
            </p>
          )}
        </CardContent>
      </Card>

      <DeviceHistoryPanel deviceId={device.id} />
    </div>
  )
}

function Stat({
  icon,
  label,
  value,
}: {
  icon?: ReactNode
  label: string
  value: string
}) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="flex items-center gap-1.5 text-xs text-muted-foreground">
        {icon}
        {label}
      </dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  )
}
