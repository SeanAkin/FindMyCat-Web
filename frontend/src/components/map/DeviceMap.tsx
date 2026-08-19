import { useEffect, useRef } from 'react'
import * as L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import '@/components/map/leaflet-theme.css'
import { PawPrint } from 'lucide-react'
import type { DeviceResponse } from '@/api/types'
import { Skeleton } from '@/components/ui/skeleton'
import { formatTimeAgo } from '@/lib/timeAgo'
import { useDevicesStore } from '@/stores/devicesStore'
import { useThemeStore } from '@/stores/themeStore'
import { cn } from '@/lib/utils'

const TILE_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const TILE_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
const DEFAULT_CENTER: L.LatLngExpression = [20, 0]
const DEFAULT_ZOOM = 2
const SINGLE_DEVICE_ZOOM = 14
const FIT_BOUNDS_PADDING: L.PointExpression = [32, 32]

type DeviceWithPosition = DeviceResponse & {
  position: NonNullable<DeviceResponse['position']>
}

function hasValidPosition(device: DeviceResponse): device is DeviceWithPosition {
  return device.position !== null && device.position.valid
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

function createDeviceIcon(online: boolean, selected: boolean): L.DivIcon {
  const size = selected ? 22 : 16
  return L.divIcon({
    className: cn(
      'device-marker',
      online && 'device-marker--online',
      selected && 'device-marker--selected',
    ),
    html: '<span class="device-marker-dot"></span>',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    popupAnchor: [0, -size / 2],
  })
}

function popupHtml(device: DeviceWithPosition): string {
  const battery =
    device.position.batteryLevel !== null
      ? `${device.position.batteryLevel}%`
      : 'Unknown'
  const lastUpdate = device.lastUpdate
    ? formatTimeAgo(new Date(device.lastUpdate))
    : 'Never reported'

  return `<div class="device-popup">
    <p class="device-popup-title">${escapeHtml(device.name)}</p>
    <dl>
      <div><dt>Battery</dt><dd>${escapeHtml(battery)}</dd></div>
      <div><dt>Last update</dt><dd>${escapeHtml(lastUpdate)}</dd></div>
    </dl>
  </div>`
}

export function DeviceMap() {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)
  const tileLayerRef = useRef<L.TileLayer | null>(null)
  const markersRef = useRef(new Map<number, L.Marker>())
  const routeLineRef = useRef<L.Polyline | null>(null)
  const hasFitInitialBoundsRef = useRef(false)

  const devices = useDevicesStore((state) => state.devices)
  const status = useDevicesStore((state) => state.status)
  const selectedDeviceId = useDevicesStore((state) => state.selectedDeviceId)
  const selectDevice = useDevicesStore((state) => state.selectDevice)
  const history = useDevicesStore((state) => state.history)
  const resolvedTheme = useThemeStore((state) => state.resolvedTheme)

  const devicesWithPosition = devices.filter(hasValidPosition)

  // Mount the Leaflet instance into the container div once; tear it down on unmount.
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const map = L.map(container, {
      center: DEFAULT_CENTER,
      zoom: DEFAULT_ZOOM,
    })
    mapRef.current = map

    return () => {
      map.remove()
      mapRef.current = null
      markersRef.current.clear()
      routeLineRef.current = null
      hasFitInitialBoundsRef.current = false
    }
  }, [])

  // Chromium can leave a filtered layer blank if toggled after painting, so recreate it instead.
  useEffect(() => {
    const map = mapRef.current
    if (!map) return

    const tileLayer = L.tileLayer(TILE_URL, {
      attribution: TILE_ATTRIBUTION,
      maxZoom: 19,
      className: resolvedTheme === 'dark' ? 'device-map-tiles--dark' : '',
    }).addTo(map)
    tileLayerRef.current = tileLayer

    return () => {
      tileLayer.remove()
    }
  }, [resolvedTheme])

  // Sync one marker per device with a valid position; fit the viewport once.
  useEffect(() => {
    const map = mapRef.current
    if (!map) return

    const seenIds = new Set<number>()

    for (const device of devicesWithPosition) {
      seenIds.add(device.id)
      const latLng: L.LatLngExpression = [
        device.position.latitude,
        device.position.longitude,
      ]
      const icon = createDeviceIcon(
        device.status === 'online',
        device.id === selectedDeviceId,
      )

      let marker = markersRef.current.get(device.id)
      if (!marker) {
        marker = L.marker(latLng, { icon }).addTo(map).on('click', () => {
          selectDevice(device.id)
        })
        markersRef.current.set(device.id, marker)
      } else {
        marker.setLatLng(latLng)
        marker.setIcon(icon)
      }
      marker.bindPopup(popupHtml(device))
    }

    for (const [id, marker] of markersRef.current) {
      if (!seenIds.has(id)) {
        marker.remove()
        markersRef.current.delete(id)
      }
    }

    if (!hasFitInitialBoundsRef.current && devicesWithPosition.length > 0) {
      hasFitInitialBoundsRef.current = true
      const singleDevice =
        devicesWithPosition.length === 1 ? devicesWithPosition[0] : undefined
      if (singleDevice) {
        map.setView(
          [singleDevice.position.latitude, singleDevice.position.longitude],
          SINGLE_DEVICE_ZOOM,
        )
      } else {
        const bounds = L.latLngBounds(
          devicesWithPosition.map(
            (device): L.LatLngExpression => [
              device.position.latitude,
              device.position.longitude,
            ],
          ),
        )
        map.fitBounds(bounds, { padding: FIT_BOUNDS_PADDING })
      }
    }
  }, [devicesWithPosition, selectedDeviceId, selectDevice])

  // Selecting a device (from the list or a marker click) focuses its marker.
  useEffect(() => {
    const map = mapRef.current
    if (!map || selectedDeviceId === null) return

    const marker = markersRef.current.get(selectedDeviceId)
    if (!marker) return

    map.flyTo(marker.getLatLng(), Math.max(map.getZoom(), SINGLE_DEVICE_ZOOM))
    marker.openPopup()
  }, [selectedDeviceId])

  // Draw the fetched history window for the selected device as a route.
  useEffect(() => {
    const map = mapRef.current
    if (!map) return

    routeLineRef.current?.remove()
    routeLineRef.current = null

    if (selectedDeviceId === null || history.length === 0) return

    const points: L.LatLngExpression[] = [...history]
      .sort(
        (a, b) => new Date(a.fixTime).getTime() - new Date(b.fixTime).getTime(),
      )
      .map((position) => [position.latitude, position.longitude])

    const line = L.polyline(points, {
      color: 'var(--color-primary)',
      weight: 3,
    }).addTo(map)
    routeLineRef.current = line
    map.fitBounds(line.getBounds(), { padding: FIT_BOUNDS_PADDING })
  }, [history, selectedDeviceId])

  if (status === 'error') return null

  const isLoading = status === 'loading' || status === 'idle'
  const isEmpty = status === 'success' && devicesWithPosition.length === 0

  return (
    <div className="relative h-[420px] w-full overflow-hidden rounded-xl border border-border">
      <div ref={containerRef} className="device-map h-full w-full" />
      {isLoading && (
        <Skeleton className="absolute inset-0 h-full w-full rounded-xl" />
      )}
      {isEmpty && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-card text-center text-muted-foreground">
          <PawPrint className="size-6" />
          <p className="font-medium text-foreground">No positions yet</p>
          <p className="max-w-xs text-sm">
            Nothing has reported a location yet. The map will populate once a
            fix comes in.
          </p>
        </div>
      )}
    </div>
  )
}
