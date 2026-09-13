import { Link } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { selectIsAdmin, useAuthStore } from '@/stores/authStore'

interface ErrorAlertProps {
  title: string
  description: string
  variant?: 'destructive' | 'default'
  onRetry?: () => void
  adminActionable?: boolean
}

export function ErrorAlert({
  title,
  description,
  variant = 'destructive',
  onRetry,
  adminActionable = false,
}: ErrorAlertProps) {
  const isAdmin = useAuthStore(selectIsAdmin)
  const showAdminLink = adminActionable && isAdmin

  return (
    <Alert variant={variant}>
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{description}</p>
        {(onRetry || showAdminLink) && (
          <div className="mt-2 flex flex-wrap gap-2">
            {onRetry && (
              <Button size="sm" variant="outline" onClick={onRetry}>
                Try again
              </Button>
            )}
            {showAdminLink && (
              <Button
                size="sm"
                variant="outline"
                nativeButton={false}
                render={<Link to="/admin" />}
              >
                Go to Admin
              </Button>
            )}
          </div>
        )}
      </AlertDescription>
    </Alert>
  )
}
