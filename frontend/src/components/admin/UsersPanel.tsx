import { ShieldCheck, ShieldOff } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { setUserRole } from '@/api/admin'
import { toApiError } from '@/api/http'
import type { UserRole } from '@/api/types'
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
import { Skeleton } from '@/components/ui/skeleton'
import { getAdminErrorMessage } from '@/lib/adminErrors'
import { useAdminStore } from '@/stores/adminStore'

export function UsersPanel() {
  const users = useAdminStore((state) => state.users)
  const status = useAdminStore((state) => state.usersStatus)
  const error = useAdminStore((state) => state.usersError)
  const fetchUsers = useAdminStore((state) => state.fetchUsers)
  const allowedEmails = useAdminStore((state) => state.allowedEmails)

  const [pendingUserId, setPendingUserId] = useState<string | null>(null)

  const joinedEmails = new Set(users.map((user) => user.email.toLowerCase()))
  const pendingInvites = allowedEmails.filter(
    (entry) => !joinedEmails.has(entry.email.toLowerCase()),
  )

  const changeRole = async (
    userId: string,
    role: UserRole,
    successMessage: string,
  ) => {
    setPendingUserId(userId)
    try {
      await setUserRole(userId, role)
      toast.success(successMessage)
      await fetchUsers()
    } catch (err) {
      const { title, description } = getAdminErrorMessage(toApiError(err))
      toast.error(title, { description })
    } finally {
      setPendingUserId(null)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Users &amp; roles</CardTitle>
      </CardHeader>
      <CardContent>
        <AsyncSection
          status={status}
          error={error}
          skeleton={
            <div className="flex flex-col gap-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          }
          errorFallback={
            error && (
              <ErrorAlert
                title={getAdminErrorMessage(error).title}
                description={getAdminErrorMessage(error).description}
                onRetry={() => void fetchUsers()}
              />
            )
          }
        >
          {users.length === 0 && pendingInvites.length === 0 ? (
            <p className="text-sm text-muted-foreground">No users yet.</p>
          ) : (
            <ul className="flex flex-col divide-y divide-border text-sm">
              {pendingInvites.map((invite) => (
                <li
                  key={invite.email}
                  className="flex flex-wrap items-center justify-between gap-3 py-3"
                >
                  <span className="text-muted-foreground">{invite.email}</span>
                  <Badge variant="outline">Pending</Badge>
                </li>
              ))}
              {users.map((user) => (
                <li
                  key={user.id}
                  className="flex flex-wrap items-center justify-between gap-3 py-3"
                >
                  <div className="flex flex-col">
                    <span className="font-medium">{user.displayName}</span>
                    <span className="text-xs text-muted-foreground">
                      {user.email}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge
                      variant={
                        user.role === 'Administrator' ? 'default' : 'outline'
                      }
                    >
                      {user.role}
                    </Badge>

                    {user.isPrimaryAdministrator ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled
                        title="The founding administrator can't be demoted."
                      >
                        Protected
                      </Button>
                    ) : user.role === 'Administrator' ? (
                      <AlertDialog>
                        <AlertDialogTrigger
                          render={
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={pendingUserId === user.id}
                            >
                              <ShieldOff data-icon="inline-start" />
                              Demote
                            </Button>
                          }
                        />
                        <AlertDialogContent>
                          <AlertDialogHeader>
                            <AlertDialogTitle>
                              Remove {user.displayName}&apos;s admin rights?
                            </AlertDialogTitle>
                            <AlertDialogDescription>
                              They&apos;ll keep access to the app but won&apos;t
                              be able to manage users, the allow-list, or
                              credentials anymore.
                            </AlertDialogDescription>
                          </AlertDialogHeader>
                          <AlertDialogFooter>
                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                            <AlertDialogAction
                              onClick={() =>
                                void changeRole(
                                  user.id,
                                  'User',
                                  `${user.displayName} is no longer an administrator.`,
                                )
                              }
                            >
                              Demote
                            </AlertDialogAction>
                          </AlertDialogFooter>
                        </AlertDialogContent>
                      </AlertDialog>
                    ) : (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={pendingUserId === user.id}
                        onClick={() =>
                          void changeRole(
                            user.id,
                            'Administrator',
                            `${user.displayName} is now an administrator.`,
                          )
                        }
                      >
                        <ShieldCheck data-icon="inline-start" />
                        Promote
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </AsyncSection>
      </CardContent>
    </Card>
  )
}
