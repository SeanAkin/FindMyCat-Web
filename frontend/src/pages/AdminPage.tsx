import { useEffect } from 'react'
import { AllowedEmailsPanel } from '@/components/admin/AllowedEmailsPanel'
import { CredentialVaultPanel } from '@/components/admin/CredentialVaultPanel'
import { UsersPanel } from '@/components/admin/UsersPanel'
import { useAdminStore } from '@/stores/adminStore'

export function AdminPage() {
  const fetchAllowedEmails = useAdminStore((state) => state.fetchAllowedEmails)
  const fetchUsers = useAdminStore((state) => state.fetchUsers)
  const fetchCredentialStatus = useAdminStore(
    (state) => state.fetchCredentialStatus,
  )

  useEffect(() => {
    void fetchAllowedEmails()
    void fetchUsers()
    void fetchCredentialStatus()
  }, [fetchAllowedEmails, fetchUsers, fetchCredentialStatus])

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight">Admin</h1>
      <p className="mt-2 text-muted-foreground">
        Manage who can sign in, who has admin rights, and the household's
        tracking credentials.
      </p>
      <div className="mt-6 flex flex-col gap-6">
        <AllowedEmailsPanel />
        <UsersPanel />
        <CredentialVaultPanel />
      </div>
    </div>
  )
}
