import { type FormEvent, useState } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { login, register } from '@/api/auth'
import { toApiError } from '@/api/http'
import { Logo } from '@/components/Logo'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { type AuthErrorMessage, getAuthErrorMessage } from '@/lib/authErrors'
import { useAuthStore } from '@/stores/authStore'

type Mode = 'signin' | 'register'

export function LoginPage() {
  const status = useAuthStore((state) => state.status)
  const signIn = useAuthStore((state) => state.signIn)
  const [searchParams] = useSearchParams()
  const redirectError = searchParams.get('error')

  const [mode, setMode] = useState<Mode>('signin')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState<AuthErrorMessage | null>(null)

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  if (
    redirectError === 'access_denied' ||
    redirectError === 'not_allow_listed'
  ) {
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

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormError(null)
    setIsSubmitting(true)
    try {
      const session =
        mode === 'signin'
          ? await login(email, password)
          : await register(email, password, displayName)
      signIn(session)
    } catch (err) {
      setFormError(getAuthErrorMessage(toApiError(err)))
    } finally {
      setIsSubmitting(false)
    }
  }

  const switchMode = (nextMode: Mode) => {
    setMode(nextMode)
    setFormError(null)
  }

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-background px-4 text-center text-foreground">
      <Logo size={40} />
      <h1 className="text-xl font-semibold tracking-tight">
        {mode === 'signin'
          ? 'Sign in to FindMyCat'
          : 'Create your FindMyCat account'}
      </h1>

      {redirectError === 'email_registered_with_password' && (
        <p className="max-w-sm text-sm text-destructive">
          This email already has a password-based account. Sign in with your
          email and password below.
        </p>
      )}
      {redirectError && redirectError !== 'email_registered_with_password' && (
        <p className="max-w-sm text-sm text-destructive">
          Sign-in failed. Please try again.
        </p>
      )}

      {formError && (
        <Alert variant="destructive" className="max-w-sm text-left">
          <AlertTitle>{formError.title}</AlertTitle>
          <AlertDescription>{formError.description}</AlertDescription>
        </Alert>
      )}

      <form
        onSubmit={(event) => void handleSubmit(event)}
        className="flex w-full max-w-sm flex-col gap-2 text-left"
      >
        {mode === 'register' && (
          <label className="flex flex-col gap-1 text-sm">
            Name
            <Input
              required
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              placeholder="Jane Doe"
            />
          </label>
        )}
        <label className="flex flex-col gap-1 text-sm">
          Email
          <Input
            type="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="name@example.com"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Password
          <Input
            type="password"
            required
            maxLength={64}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="••••••••"
          />
        </label>
        {mode === 'register' && (
          <p className="text-xs text-muted-foreground">
            8-64 characters, with one uppercase letter and one symbol.
          </p>
        )}
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting
            ? mode === 'signin'
              ? 'Signing in…'
              : 'Creating account…'
            : mode === 'signin'
              ? 'Sign in'
              : 'Create account'}
        </Button>
      </form>

      <button
        type="button"
        className="text-sm text-muted-foreground underline underline-offset-2 hover:text-foreground"
        onClick={() => switchMode(mode === 'signin' ? 'register' : 'signin')}
      >
        {mode === 'signin'
          ? "Don't have an account? Create one"
          : 'Already have an account? Sign in'}
      </button>

      <div className="flex w-full max-w-sm items-center gap-2 text-xs text-muted-foreground">
        <span className="h-px flex-1 bg-border" />
        or
        <span className="h-px flex-1 bg-border" />
      </div>

      <Button nativeButton={false} render={<a href="/auth/login" />}>
        Sign in with Google
      </Button>
    </div>
  )
}
