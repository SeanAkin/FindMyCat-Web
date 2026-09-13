import { cn } from '@/lib/utils'
import logoSrc from '@/assets/logo.png'

interface LogoProps {
  size?: number
  className?: string
}

export function Logo({ size = 32, className }: LogoProps) {
  return (
    <img
      src={logoSrc}
      alt="FindMyCat"
      width={size}
      height={size}
      className={cn('rounded-md dark:invert', className)}
    />
  )
}
