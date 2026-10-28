import { Check, Copy, KeyRound, TriangleAlert } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { toast } from 'sonner'
import {
  deleteHologramKey,
  deleteIngestKey,
  deleteTraccarToken,
  rotateIngestKey,
  setHologramKey,
  setTraccarToken,
} from '@/api/credentials'
import { toApiError } from '@/api/http'
import { AsyncSection } from '@/components/AsyncSection'
import { ErrorAlert } from '@/components/ErrorAlert'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { getAdminErrorMessage } from '@/lib/adminErrors'
import { useAdminStore } from '@/stores/adminStore'

const INGEST_KEY_WARNING =
  "This key is shown only once. Copy it now and paste it into EMQX's x-api-key forward header. If you lose it you must rotate, and rotating means updating the key in EMQX too."

interface CredentialFieldProps {
  label: string
  configured: boolean
  placeholder: string
  onSet: (value: string) => Promise<void>
  onClear: () => Promise<void>
  clearWarning: string
}

function CredentialField({
  label,
  configured,
  placeholder,
  onSet,
  onClear,
  clearWarning,
}: CredentialFieldProps) {
  const [value, setValue] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [isClearing, setIsClearing] = useState(false)

  const handleSet = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = value.trim()
    if (!trimmed) return

    setIsSaving(true)
    try {
      await onSet(trimmed)
      setValue('')
      toast.success(`${label} saved.`)
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setIsSaving(false)
    }
  }

  const handleClear = async () => {
    setIsClearing(true)
    try {
      await onClear()
      toast.success(`${label} cleared.`)
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setIsClearing(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <KeyRound className="size-3.5 text-muted-foreground" />
          <span className="font-medium">{label}</span>
        </div>
        <Badge variant={configured ? 'default' : 'outline'}>
          {configured ? 'Configured' : 'Not configured'}
        </Badge>
      </div>

      <form
        onSubmit={(event) => void handleSet(event)}
        className="flex flex-wrap gap-2"
      >
        <Input
          type="password"
          autoComplete="off"
          placeholder={placeholder}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          className="min-w-48 flex-1"
        />
        <Button type="submit" size="sm" disabled={isSaving || !value.trim()}>
          {isSaving ? 'Saving…' : configured ? 'Rotate' : 'Set'}
        </Button>
        {configured && (
          <AlertDialog>
            <AlertDialogTrigger
              render={
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={isClearing}
                >
                  Clear
                </Button>
              }
            />
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Clear the {label}?</AlertDialogTitle>
                <AlertDialogDescription>{clearWarning}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={() => void handleClear()}>
                  Clear
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </form>
    </div>
  )
}

interface IngestKeyFieldProps {
  configured: boolean
  onChanged: () => Promise<void>
}

function IngestKeyField({ configured, onChanged }: IngestKeyFieldProps) {
  const [revealedKey, setRevealedKey] = useState<string | null>(null)
  const [isRotating, setIsRotating] = useState(false)
  const [isDisabling, setIsDisabling] = useState(false)
  const [copied, setCopied] = useState(false)

  const dismissReveal = () => {
    setRevealedKey(null)
    setCopied(false)
  }

  const handleRotate = async () => {
    setIsRotating(true)
    try {
      const { apiKey } = await rotateIngestKey()
      setRevealedKey(apiKey)
      setCopied(false)
      await onChanged()
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setIsRotating(false)
    }
  }

  const handleDisable = async () => {
    setIsDisabling(true)
    try {
      await deleteIngestKey()
      dismissReveal()
      toast.success('Ingest API key disabled.')
      await onChanged()
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setIsDisabling(false)
    }
  }

  const handleCopy = async () => {
    if (!revealedKey) return
    try {
      await navigator.clipboard.writeText(revealedKey)
      setCopied(true)
      toast.success('Key copied to clipboard.')
    } catch {
      toast.error('Could not copy the key', {
        description: 'Select it in the field and copy it manually.',
      })
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <KeyRound className="size-3.5 text-muted-foreground" />
          <span className="font-medium">Ingest API key</span>
        </div>
        <Badge variant={configured ? 'default' : 'outline'}>
          {configured ? 'Configured' : 'Not configured'}
        </Badge>
      </div>

      <p className="text-sm text-muted-foreground">
        EMQX sends this as the <code>x-api-key</code> header when forwarding
        collar telemetry. Generate it here, then paste it into EMQX.
      </p>

      <div className="flex flex-wrap gap-2">
        {configured ? (
          <AlertDialog>
            <Tooltip>
              <TooltipTrigger
                render={
                  <AlertDialogTrigger
                    render={
                      <Button type="button" size="sm" disabled={isRotating}>
                        {isRotating ? 'Rotating…' : 'Rotate key'}
                      </Button>
                    }
                  />
                }
              />
              <TooltipContent>{INGEST_KEY_WARNING}</TooltipContent>
            </Tooltip>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Rotate the ingest API key?</AlertDialogTitle>
                <AlertDialogDescription>
                  {INGEST_KEY_WARNING} The current key stops working the moment
                  you rotate.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={() => void handleRotate()}>
                  Rotate key
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        ) : (
          <Tooltip>
            <TooltipTrigger
              render={
                <Button
                  type="button"
                  size="sm"
                  disabled={isRotating}
                  onClick={() => void handleRotate()}
                >
                  {isRotating ? 'Generating…' : 'Generate key'}
                </Button>
              }
            />
            <TooltipContent>{INGEST_KEY_WARNING}</TooltipContent>
          </Tooltip>
        )}

        {configured && (
          <AlertDialog>
            <AlertDialogTrigger
              render={
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={isDisabling}
                >
                  Disable
                </Button>
              }
            />
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Disable the ingest API key?</AlertDialogTitle>
                <AlertDialogDescription>
                  Telemetry forwarded by EMQX will be rejected until a new key
                  is generated and set in EMQX.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={() => void handleDisable()}>
                  Disable
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </div>

      {revealedKey && (
        <Alert variant="destructive">
          <TriangleAlert />
          <AlertTitle>Copy this key now</AlertTitle>
          <AlertDescription>
            <p>{INGEST_KEY_WARNING}</p>
            <div className="flex w-full flex-wrap gap-2">
              <Input
                readOnly
                value={revealedKey}
                onFocus={(event) => event.target.select()}
                className="min-w-48 flex-1 font-mono"
              />
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => void handleCopy()}
              >
                {copied ? (
                  <Check className="size-3.5" />
                ) : (
                  <Copy className="size-3.5" />
                )}
                {copied ? 'Copied' : 'Copy'}
              </Button>
              <Button type="button" size="sm" onClick={dismissReveal}>
                Done
              </Button>
            </div>
          </AlertDescription>
        </Alert>
      )}
    </div>
  )
}

export function CredentialVaultPanel() {
  const credentials = useAdminStore((state) => state.credentials)
  const status = useAdminStore((state) => state.credentialsStatus)
  const error = useAdminStore((state) => state.credentialsError)
  const fetchCredentialStatus = useAdminStore(
    (state) => state.fetchCredentialStatus,
  )

  return (
    <Card>
      <CardHeader>
        <CardTitle>Credential vault</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <AsyncSection
          status={status}
          error={error}
          skeleton={
            <div className="flex flex-col gap-2">
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
            </div>
          }
          errorFallback={
            error && (
              <ErrorAlert
                title={getAdminErrorMessage(error).title}
                description={getAdminErrorMessage(error).description}
                onRetry={() => void fetchCredentialStatus()}
              />
            )
          }
        >
          {credentials && (
            <>
              <CredentialField
                label="Traccar API token"
                configured={credentials.traccarConfigured}
                placeholder="Traccar API token"
                onSet={async (value) => {
                  await setTraccarToken(value)
                  await fetchCredentialStatus()
                }}
                onClear={async () => {
                  await deleteTraccarToken()
                  await fetchCredentialStatus()
                }}
                clearWarning="Location tracking will stop working for everyone in the household until a new token is set."
              />
              <CredentialField
                label="Hologram API key"
                configured={credentials.hologramConfigured}
                placeholder="Hologram API key"
                onSet={async (value) => {
                  await setHologramKey(value)
                  await fetchCredentialStatus()
                }}
                onClear={async () => {
                  await deleteHologramKey()
                  await fetchCredentialStatus()
                }}
                clearWarning="Collar commands (ping, mark lost/active) will stop working until a new key is set."
              />
              <IngestKeyField
                configured={credentials.ingestConfigured}
                onChanged={fetchCredentialStatus}
              />
            </>
          )}
        </AsyncSection>
      </CardContent>
    </Card>
  )
}
