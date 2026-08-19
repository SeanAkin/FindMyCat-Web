import { useState } from 'react'
import { ErrorAlert } from '@/components/ErrorAlert'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { getDeviceErrorMessage } from '@/lib/deviceErrors'
import { validateHistoryRange } from '@/lib/historyRange'
import { useDevicesStore } from '@/stores/devicesStore'

function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10)
}

const DEFAULT_WINDOW_DAYS = 1

function defaultRange(): { from: string; to: string } {
  const now = new Date()
  const from = new Date(
    now.getTime() - DEFAULT_WINDOW_DAYS * 24 * 60 * 60 * 1000,
  )
  return { from: toDateInputValue(from), to: toDateInputValue(now) }
}

interface DeviceHistoryPanelProps {
  deviceId: number
}

export function DeviceHistoryPanel({ deviceId }: DeviceHistoryPanelProps) {
  const [initialRange] = useState(defaultRange)
  const [fromInput, setFromInput] = useState(initialRange.from)
  const [toInput, setToInput] = useState(initialRange.to)
  const [validationError, setValidationError] = useState<string | null>(null)

  const history = useDevicesStore((state) => state.history)
  const historyStatus = useDevicesStore((state) => state.historyStatus)
  const historyError = useDevicesStore((state) => state.historyError)
  const fetchHistory = useDevicesStore((state) => state.fetchHistory)
  const historyErrorMessage = historyError
    ? getDeviceErrorMessage(historyError)
    : null

  const handleLoad = () => {
    const from = new Date(`${fromInput}T00:00:00`)
    const to = new Date(`${toInput}T23:59:59.999`)
    const rangeError = validateHistoryRange(from, to)

    if (rangeError) {
      setValidationError(rangeError)
      return
    }

    setValidationError(null)
    void fetchHistory(deviceId, from, to)
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">History</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-xs text-muted-foreground">From</span>
            <Input
              type="date"
              value={fromInput}
              max={toInput}
              onChange={(event) => setFromInput(event.target.value)}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-xs text-muted-foreground">To</span>
            <Input
              type="date"
              value={toInput}
              min={fromInput}
              onChange={(event) => setToInput(event.target.value)}
            />
          </label>
          <Button size="sm" onClick={handleLoad}>
            Load history
          </Button>
        </div>

        {validationError && (
          <Alert variant="destructive">
            <AlertDescription>{validationError}</AlertDescription>
          </Alert>
        )}

        {historyStatus === 'loading' && (
          <div className="flex flex-col gap-2">
            <Skeleton className="h-6 w-full" />
            <Skeleton className="h-6 w-full" />
            <Skeleton className="h-6 w-full" />
          </div>
        )}

        {historyStatus === 'error' && historyErrorMessage && (
          <ErrorAlert
            title={historyErrorMessage.title}
            description={historyErrorMessage.description}
            adminActionable={historyErrorMessage.adminActionable}
          />
        )}

        {historyStatus === 'success' && history.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No fixes recorded in this window.
          </p>
        )}

        {historyStatus === 'success' && history.length > 0 && (
          <ul className="flex max-h-64 flex-col divide-y divide-border overflow-y-auto text-sm">
            {history.map((position) => (
              <li
                key={position.fixTime}
                className="flex items-center justify-between gap-4 py-2"
              >
                <span className="text-muted-foreground">
                  {new Date(position.fixTime).toLocaleString(undefined)}
                </span>
                <span className="tabular-nums">
                  {position.latitude.toFixed(4)},{' '}
                  {position.longitude.toFixed(4)}
                </span>
                <span className="text-muted-foreground tabular-nums">
                  {position.speedKnots.toFixed(1)} kn
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
