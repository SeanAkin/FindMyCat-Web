import { useEffect } from 'react'
import { DeviceDetailPanel } from '@/components/devices/DeviceDetailPanel'
import { DeviceList } from '@/components/devices/DeviceList'
import { DeviceMap } from '@/components/map/DeviceMap'
import { useDevicesStore } from '@/stores/devicesStore'

const DEVICE_POLL_INTERVAL_MS = 15_000

export function DevicesPage() {
  const fetchDevices = useDevicesStore((state) => state.fetchDevices)

  useEffect(() => {
    void fetchDevices()
    const intervalId = window.setInterval(() => {
      if (!document.hidden) void fetchDevices()
    }, DEVICE_POLL_INTERVAL_MS)
    return () => window.clearInterval(intervalId)
  }, [fetchDevices])

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight">Devices</h1>
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
