import { create } from 'zustand'
import { listAllowedEmails, listUsers } from '@/api/admin'
import { getCredentialStatus } from '@/api/credentials'
import { ApiError, toApiError } from '@/api/http'
import type {
  AllowedEmailResponse,
  CredentialStatusResponse,
  UserResponse,
} from '@/api/types'

export type LoadStatus = 'idle' | 'loading' | 'success' | 'error'

interface AdminState {
  allowedEmails: AllowedEmailResponse[]
  allowedEmailsStatus: LoadStatus
  allowedEmailsError: ApiError | null
  fetchAllowedEmails: () => Promise<void>

  users: UserResponse[]
  usersStatus: LoadStatus
  usersError: ApiError | null
  fetchUsers: () => Promise<void>

  credentials: CredentialStatusResponse | null
  credentialsStatus: LoadStatus
  credentialsError: ApiError | null
  fetchCredentialStatus: () => Promise<void>
}

export const useAdminStore = create<AdminState>((set) => ({
  allowedEmails: [],
  allowedEmailsStatus: 'idle',
  allowedEmailsError: null,
  fetchAllowedEmails: async () => {
    set({ allowedEmailsStatus: 'loading', allowedEmailsError: null })
    try {
      const allowedEmails = await listAllowedEmails()
      set({ allowedEmails, allowedEmailsStatus: 'success' })
    } catch (error) {
      set({
        allowedEmailsStatus: 'error',
        allowedEmailsError: toApiError(error),
      })
    }
  },

  users: [],
  usersStatus: 'idle',
  usersError: null,
  fetchUsers: async () => {
    set({ usersStatus: 'loading', usersError: null })
    try {
      const users = await listUsers()
      set({ users, usersStatus: 'success' })
    } catch (error) {
      set({ usersStatus: 'error', usersError: toApiError(error) })
    }
  },

  credentials: null,
  credentialsStatus: 'idle',
  credentialsError: null,
  fetchCredentialStatus: async () => {
    set({ credentialsStatus: 'loading', credentialsError: null })
    try {
      const credentials = await getCredentialStatus()
      set({ credentials, credentialsStatus: 'success' })
    } catch (error) {
      set({ credentialsStatus: 'error', credentialsError: toApiError(error) })
    }
  },
}))
