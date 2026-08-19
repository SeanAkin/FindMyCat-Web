import { Trash2 } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { toast } from 'sonner'
import { addAllowedEmail, removeAllowedEmail } from '@/api/admin'
import { toApiError } from '@/api/http'
import { AsyncSection } from '@/components/AsyncSection'
import { ErrorAlert } from '@/components/ErrorAlert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { getAdminErrorMessage } from '@/lib/adminErrors'
import { useAdminStore } from '@/stores/adminStore'

export function AllowedEmailsPanel() {
  const allowedEmails = useAdminStore((state) => state.allowedEmails)
  const status = useAdminStore((state) => state.allowedEmailsStatus)
  const error = useAdminStore((state) => state.allowedEmailsError)
  const fetchAllowedEmails = useAdminStore((state) => state.fetchAllowedEmails)

  const [emailInput, setEmailInput] = useState('')
  const [isAdding, setIsAdding] = useState(false)
  const [removingEmail, setRemovingEmail] = useState<string | null>(null)

  const handleAdd = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const email = emailInput.trim()
    if (!email) return

    setIsAdding(true)
    try {
      await addAllowedEmail(email)
      setEmailInput('')
      toast.success(`${email} can now sign in.`)
      await fetchAllowedEmails()
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setIsAdding(false)
    }
  }

  const handleRemove = async (email: string) => {
    setRemovingEmail(email)
    try {
      await removeAllowedEmail(email)
      toast.success(`${email} removed from the allow-list.`)
      await fetchAllowedEmails()
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setRemovingEmail(null)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Allow-list</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <form
          onSubmit={(event) => void handleAdd(event)}
          className="flex flex-wrap gap-2"
        >
          <Input
            type="email"
            required
            placeholder="name@example.com"
            value={emailInput}
            onChange={(event) => setEmailInput(event.target.value)}
            className="min-w-48 flex-1"
          />
          <Button type="submit" size="sm" disabled={isAdding}>
            {isAdding ? 'Adding…' : 'Add email'}
          </Button>
        </form>

        <AsyncSection
          status={status}
          error={error}
          skeleton={
            <div className="flex flex-col gap-2">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          }
          errorFallback={
            error && (
              <ErrorAlert
                title={getAdminErrorMessage(error).title}
                description={getAdminErrorMessage(error).description}
                onRetry={() => void fetchAllowedEmails()}
              />
            )
          }
        >
          {allowedEmails.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No emails on the allow-list yet.
            </p>
          ) : (
            <ul className="flex flex-col divide-y divide-border text-sm">
              {allowedEmails.map((entry) => (
                <li
                  key={entry.email}
                  className="flex items-center justify-between gap-4 py-2"
                >
                  <span>{entry.email}</span>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Remove ${entry.email}`}
                    disabled={removingEmail === entry.email}
                    onClick={() => void handleRemove(entry.email)}
                  >
                    <Trash2 />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </AsyncSection>
      </CardContent>
    </Card>
  )
}
