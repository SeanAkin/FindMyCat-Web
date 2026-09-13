import { Navigate, Outlet } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/authStore'

export function RequireAuth() {
  const status = useAuthStore((state) => state.status)
  const checkSession = useAuthStore((state) => state.checkSession)

  if (status === 'loading') {
    return <div className="min-h-dvh bg-background" />
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />
  }

  if (status === 'error') {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-background p-4">
        <Alert variant="destructive" className="max-w-sm">
          <AlertTitle>Couldn&apos;t reach the server</AlertTitle>
          <AlertDescription>
            <p>Check your connection and try again.</p>
            <Button
              size="sm"
              variant="outline"
              className="mt-2"
              onClick={() => void checkSession()}
            >
              Try again
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    )
  }

  return <Outlet />
}
