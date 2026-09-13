import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-background px-4 text-foreground">
      <h1 className="text-xl font-semibold tracking-tight">Page not found</h1>
      <Button nativeButton={false} render={<Link to="/" />}>
        Back to devices
      </Button>
    </div>
  )
}
