import { KeyRound } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { toast } from 'sonner'
import {
  deleteHologramKey,
  deleteTraccarToken,
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
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { getAdminErrorMessage } from '@/lib/adminErrors'
import { useAdminStore } from '@/stores/adminStore'

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
            </>
          )}
        </AsyncSection>
      </CardContent>
    </Card>
  )
}
