import { PawPrint } from 'lucide-react'
import { AsyncSection } from '@/components/AsyncSection'
import { DeviceCard } from '@/components/devices/DeviceCard'
import { ErrorAlert } from '@/components/ErrorAlert'
import { Skeleton } from '@/components/ui/skeleton'
import { getDeviceErrorMessage } from '@/lib/deviceErrors'
import { useDevicesStore } from '@/stores/devicesStore'

export function DeviceList() {
  const devices = useDevicesStore((state) => state.devices)
  const status = useDevicesStore((state) => state.status)
  const error = useDevicesStore((state) => state.error)
  const selectedDeviceId = useDevicesStore((state) => state.selectedDeviceId)
  const selectDevice = useDevicesStore((state) => state.selectDevice)
  const fetchDevices = useDevicesStore((state) => state.fetchDevices)

  return (
    <AsyncSection
      status={status}
      error={error}
      skeleton={
        <div className="flex flex-col gap-3">
          <Skeleton className="h-28 w-full rounded-xl" />
          <Skeleton className="h-28 w-full rounded-xl" />
          <Skeleton className="h-28 w-full rounded-xl" />
        </div>
      }
      errorFallback={
        error &&
        (() => {
          const message = getDeviceErrorMessage(error)
          return (
            <ErrorAlert
              title={message.title}
              description={message.description}
              variant={message.variant === 'error' ? 'destructive' : 'default'}
              onRetry={
                message.variant === 'error'
                  ? () => void fetchDevices()
                  : undefined
              }
              adminActionable={message.adminActionable}
            />
          )
        })()
      }
    >
      {devices.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border py-12 text-center">
          <PawPrint className="size-6 text-muted-foreground" />
          <p className="font-medium">No devices yet</p>
          <p className="max-w-xs text-sm text-muted-foreground">
            Your household&apos;s collars will appear here once they check in.
          </p>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {devices.map((device) => (
            <DeviceCard
              key={device.id}
              device={device}
              selected={device.id === selectedDeviceId}
              onSelect={() => selectDevice(device.id)}
            />
          ))}
        </div>
      )}
    </AsyncSection>
  )
}
