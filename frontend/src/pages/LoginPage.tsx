import { Navigate, useSearchParams } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/authStore'

export function LoginPage() {
  const status = useAuthStore((state) => state.status)
  const [searchParams] = useSearchParams()
  const error = searchParams.get('error')

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  if (error === 'access_denied') {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-background px-4 text-center text-foreground">
        <Logo size={40} />
        <h1 className="text-xl font-semibold tracking-tight">
          Access not granted
        </h1>
        <p className="max-w-sm text-sm text-muted-foreground">
          Your Google account isn&apos;t on the FindMyCat allow-list yet. Ask a
          household administrator to add your email.
        </p>
      </div>
    )
  }

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-background px-4 text-center text-foreground">
      <Logo size={40} />
      <h1 className="text-xl font-semibold tracking-tight">
        Sign in to FindMyCat
      </h1>
      {error && (
        <p className="max-w-sm text-sm text-destructive">
          Sign-in failed. Please try again.
        </p>
      )}
      <Button nativeButton={false} render={<a href="/auth/login" />}>
        Sign in with Google
      </Button>
    </div>
  )
}
