import { RefreshCw } from 'lucide-react'
import { type ReactNode, useCallback, useEffect } from 'react'
import type {
  CommandTelemetry,
  DeviceEventResponse,
  NetworkTelemetry,
} from '@/api/types'
import { AsyncSection } from '@/components/AsyncSection'
import { ErrorAlert } from '@/components/ErrorAlert'
import { TimeAgo } from '@/components/TimeAgo'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { getDeviceErrorMessage } from '@/lib/deviceErrors'
import { useDevicesStore } from '@/stores/devicesStore'

const TELEMETRY_POLL_INTERVAL_MS = 15_000

const NETWORK_MODES: Record<number, string> = {
  0: 'None',
  7: 'LTE-M',
  9: 'NB-IoT',
}

function formatMode(mode: number | undefined): string {
  if (mode === undefined) return 'Unknown'
  return NETWORK_MODES[mode] ?? String(mode)
}

function formatSeconds(seconds: number | undefined): string {
  return seconds !== undefined ? `${seconds}s` : 'Unknown'
}

function formatText(value: string | undefined): string {
  return value && value.trim() !== '' ? value : 'Unknown'
}

function networkOf(fields: object | null | undefined): NetworkTelemetry | null {
  return (fields as NetworkTelemetry | undefined) ?? null
}

function commandOf(fields: object | null | undefined): CommandTelemetry | null {
  return (fields as CommandTelemetry | undefined) ?? null
}

interface DeviceTelemetryPanelProps {
  deviceId: number
}

export function DeviceTelemetryPanel({ deviceId }: DeviceTelemetryPanelProps) {
  const deviceState = useDevicesStore((state) => state.deviceState)
  const deviceStateStatus = useDevicesStore((state) => state.deviceStateStatus)
  const deviceStateError = useDevicesStore((state) => state.deviceStateError)
  const fetchDeviceState = useDevicesStore((state) => state.fetchDeviceState)

  const deviceEvents = useDevicesStore((state) => state.deviceEvents)
  const deviceEventsStatus = useDevicesStore(
    (state) => state.deviceEventsStatus,
  )
  const deviceEventsError = useDevicesStore((state) => state.deviceEventsError)
  const fetchDeviceEvents = useDevicesStore((state) => state.fetchDeviceEvents)

  const refresh = useCallback(() => {
    void fetchDeviceState(deviceId)
    void fetchDeviceEvents(deviceId)
  }, [deviceId, fetchDeviceState, fetchDeviceEvents])

  useEffect(() => {
    refresh()
    const intervalId = window.setInterval(() => {
      if (!document.hidden) refresh()
    }, TELEMETRY_POLL_INTERVAL_MS)
    return () => window.clearInterval(intervalId)
  }, [refresh])

  const networkEvent = deviceState.network ?? null
  const commandEvent = deviceState.command ?? null
  const network = networkOf(networkEvent)
  const command = commandOf(commandEvent)

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <CardTitle className="text-base">Collar status</CardTitle>
        <Button
          size="icon-sm"
          variant="ghost"
          aria-label="Refresh telemetry"
          onClick={refresh}
        >
          <RefreshCw />
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <AsyncSection
          status={deviceStateStatus}
          error={deviceStateError}
          skeleton={<Skeleton className="h-24 w-full" />}
          errorFallback={
            deviceStateError && (
              <ErrorAlert
                title={getDeviceErrorMessage(deviceStateError).title}
                description={
                  getDeviceErrorMessage(deviceStateError).description
                }
                onRetry={() => void fetchDeviceState(deviceId)}
                adminActionable={
                  getDeviceErrorMessage(deviceStateError).adminActionable
                }
              />
            )
          }
        >
          {Object.keys(deviceState).length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No telemetry reported yet.
            </p>
          ) : (
            <div className="flex flex-col gap-5">
              <section className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-muted-foreground">
                  Network
                </h3>
                {network ? (
                  <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
                    <Stat label="Status" value={formatText(network.status)} />
                    <Stat label="Mode" value={formatMode(network.mode)} />
                    <Stat
                      label="eDRX"
                      value={formatSeconds(network.edrx_s)}
                    />
                    {networkEvent && (
                      <Stat
                        label="Reported"
                        value={<TimeAgo date={networkEvent.receivedAt} />}
                      />
                    )}
                  </dl>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    No network status reported yet.
                  </p>
                )}
              </section>

              <section className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-muted-foreground">
                  Last command
                </h3>
                {command ? (
                  <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
                    <Stat label="Command" value={formatText(command.command)} />
                    <Stat label="Status" value={formatText(command.status)} />
                    <Stat label="Method" value={formatText(command.method)} />
                    <Stat label="Reason" value={formatText(command.reason)} />
                    {commandEvent && (
                      <Stat
                        label="When"
                        value={<TimeAgo date={commandEvent.receivedAt} />}
                      />
                    )}
                  </dl>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    No command run yet.
                  </p>
                )}
              </section>
            </div>
          )}
        </AsyncSection>

        <section className="flex flex-col gap-2">
          <h3 className="text-xs font-medium text-muted-foreground">
            Recent events
          </h3>
          <AsyncSection
            status={deviceEventsStatus}
            error={deviceEventsError}
            skeleton={
              <div className="flex flex-col gap-2">
                <Skeleton className="h-6 w-full" />
                <Skeleton className="h-6 w-full" />
                <Skeleton className="h-6 w-full" />
              </div>
            }
            errorFallback={
              deviceEventsError && (
                <ErrorAlert
                  title={getDeviceErrorMessage(deviceEventsError).title}
                  description={
                    getDeviceErrorMessage(deviceEventsError).description
                  }
                  onRetry={() => void fetchDeviceEvents(deviceId)}
                  adminActionable={
                    getDeviceErrorMessage(deviceEventsError).adminActionable
                  }
                />
              )
            }
          >
            {deviceEvents.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No events reported yet.
              </p>
            ) : (
              <ul className="flex max-h-64 flex-col divide-y divide-border overflow-y-auto text-sm">
                {deviceEvents.map((event) => (
                  <li key={event.id} className="flex flex-col gap-1 py-2">
                    <div className="flex items-center justify-between gap-3">
                      <Badge variant="outline" className="capitalize">
                        {event.kind}
                      </Badge>
                      <span className="text-muted-foreground">
                        <TimeAgo date={event.receivedAt} />
                      </span>
                    </div>
                    <EventDetail event={event} />
                  </li>
                ))}
              </ul>
            )}
          </AsyncSection>
        </section>
      </CardContent>
    </Card>
  )
}

function EventDetail({ event }: { event: DeviceEventResponse }) {
  if (event.kind === 'network') {
    const network = networkOf(event.payload)
    return (
      <span className="text-muted-foreground">
        {formatText(network?.status)} ·{' '}
        {formatMode(network?.mode)} · eDRX{' '}
        {formatSeconds(network?.edrx_s)}
      </span>
    )
  }

  if (event.kind === 'command') {
    const command = commandOf(event.payload)
    return (
      <span className="text-muted-foreground">
        {formatText(command?.command)} ·{' '}
        {formatText(command?.status)} (
        {formatText(command?.method)})
      </span>
    )
  }

  return (
    <pre className="overflow-x-auto rounded bg-muted p-2 text-xs text-muted-foreground">
      {JSON.stringify(event.payload, null, 2)}
    </pre>
  )
}

function Stat({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  )
}
