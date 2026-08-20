import { create } from 'zustand'
import { getSession, logout as logoutRequest } from '@/api/auth'
import { ApiError, onUnauthorized } from '@/api/http'
import type { SessionResponse } from '@/api/types'

export type AuthStatus =
  'loading' | 'authenticated' | 'unauthenticated' | 'error'

interface AuthState {
  status: AuthStatus
  user: SessionResponse | null
  checkSession: () => Promise<void>
  signIn: (user: SessionResponse) => void
  logout: () => Promise<void>
}

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  checkSession: async () => {
    try {
      const user = await getSession()
      set({ status: 'authenticated', user })
    } catch (error) {
      const isUnauthorized = error instanceof ApiError && error.isUnauthorized
      set({ status: isUnauthorized ? 'unauthenticated' : 'error', user: null })
    }
  },
  signIn: (user) => set({ status: 'authenticated', user }),
  logout: async () => {
    await logoutRequest().catch(() => {})
    set({ status: 'unauthenticated', user: null })
  },
}))

onUnauthorized(() => {
  useAuthStore.setState({ status: 'unauthenticated', user: null })
})

export function selectIsAdmin(state: AuthState): boolean {
  return state.user?.role === 'Administrator'
}
