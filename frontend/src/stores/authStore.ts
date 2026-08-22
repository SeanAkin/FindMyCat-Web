import { create } from 'zustand'
import { getAuthProviders, getSession, logout as logoutRequest } from '@/api/auth'
import { ApiError, onUnauthorized } from '@/api/http'
import type { AuthProviderName, SessionResponse } from '@/api/types'

export type AuthStatus =
  'loading' | 'authenticated' | 'unauthenticated' | 'error'

const DEFAULT_PROVIDERS: AuthProviderName[] = ['Password', 'Google']

interface AuthState {
  status: AuthStatus
  user: SessionResponse | null
  providers: AuthProviderName[]
  checkSession: () => Promise<void>
  loadAuthProviders: () => Promise<void>
  signIn: (user: SessionResponse) => void
  logout: () => Promise<void>
}

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  providers: DEFAULT_PROVIDERS,
  checkSession: async () => {
    try {
      const user = await getSession()
      set({ status: 'authenticated', user })
    } catch (error) {
      const isUnauthorized = error instanceof ApiError && error.isUnauthorized
      set({ status: isUnauthorized ? 'unauthenticated' : 'error', user: null })
    }
  },
  loadAuthProviders: async () => {
    const { providers } = await getAuthProviders().catch(() => ({ providers: DEFAULT_PROVIDERS }))
    set({ providers })
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
