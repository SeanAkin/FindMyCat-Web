import { Navigate, Outlet } from 'react-router-dom'
import { selectIsAdmin, useAuthStore } from '@/stores/authStore'

export function RequireAdmin() {
  const isAdmin = useAuthStore(selectIsAdmin)

  if (!isAdmin) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
