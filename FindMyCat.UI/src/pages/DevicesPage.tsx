import { useEffect } from 'react'
import { DeviceDetailPanel } from '@/components/devices/DeviceDetailPanel'
import { DeviceList } from '@/components/devices/DeviceList'
import { LiveUpdatesBadge } from '@/components/devices/LiveUpdatesBadge'
import { TraccarStatusAlert } from '@/components/devices/TraccarStatusAlert'
import { DeviceMap } from '@/components/map/DeviceMap'
import { useDevicesStore } from '@/stores/devicesStore'

export function DevicesPage() {
  const startLiveUpdates = useDevicesStore((state) => state.startLiveUpdates)
  const stopLiveUpdates = useDevicesStore((state) => state.stopLiveUpdates)

  useEffect(() => {
    void startLiveUpdates()
    return () => stopLiveUpdates()
  }, [startLiveUpdates, stopLiveUpdates])

  return (
    <div>
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Devices</h1>
        <LiveUpdatesBadge />
      </div>
      <div className="mt-6 empty:hidden">
        <TraccarStatusAlert />
      </div>
      <div className="mt-6">
        <DeviceMap />
      </div>
      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-[minmax(280px,380px)_1fr]">
        <DeviceList />
        <DeviceDetailPanel />
      </div>
    </div>
  )
}
