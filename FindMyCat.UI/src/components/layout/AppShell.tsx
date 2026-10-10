import { useState } from 'react'
import { LogOut, Settings, UserRound } from 'lucide-react'
import { Link, Outlet, useNavigate } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useHideOnScroll } from '@/hooks/useHideOnScroll'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/authStore'

const HEADER_HEIGHT_PX = 64

export function AppShell() {
  const user = useAuthStore((state) => state.user)
  const logout = useAuthStore((state) => state.logout)
  const navigate = useNavigate()
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false)
  const isScrolledAway = useHideOnScroll(HEADER_HEIGHT_PX)
  const isHeaderHidden = isScrolledAway && !isAccountMenuOpen

  if (!user) {
    return null
  }

  const handleLogout = async () => {
    await logout()
    navigate('/login')
  }

  return (
    <div className="min-h-dvh bg-background text-foreground">
      <header
        className={cn(
          'sticky top-0 z-10 border-b border-border bg-background/95 backdrop-blur transition-transform duration-200 focus-within:translate-y-0 motion-reduce:transition-none supports-backdrop-filter:bg-background/80',
          isHeaderHidden && '-translate-y-full',
        )}
      >
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-4">
          <Link to="/" className="flex items-center gap-2.5">
            <Logo size={28} />
            <span className="text-base font-semibold tracking-tight">
              FindMyCat
            </span>
          </Link>
          <div className="flex items-center gap-1">
            {user.role === 'Administrator' && (
              <Button
                variant="ghost"
                size="icon"
                aria-label="Admin settings"
                nativeButton={false}
                render={<Link to="/admin" />}
              >
                <Settings className="size-4.5" />
              </Button>
            )}
            <ThemeToggle />
            <DropdownMenu onOpenChange={(open) => setIsAccountMenuOpen(open)}>
              <DropdownMenuTrigger
                render={
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label="Account menu"
                  />
                }
              >
                <UserRound className="size-4.5" />
              </DropdownMenuTrigger>
              <DropdownMenuContent
                align="end"
                className="w-auto min-w-48 max-w-[calc(100vw-2rem)]"
              >
                <div className="px-1.5 py-1">
                  <p className="font-medium text-foreground wrap-anywhere">
                    {user.displayName}
                  </p>
                  <p className="text-xs text-muted-foreground wrap-anywhere">
                    {user.email}
                  </p>
                </div>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={handleLogout}>
                  <LogOut className="size-4" />
                  Log out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
