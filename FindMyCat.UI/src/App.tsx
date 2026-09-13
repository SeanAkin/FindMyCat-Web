import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { RequireAdmin } from '@/components/auth/RequireAdmin'
import { RequireAuth } from '@/components/auth/RequireAuth'
import { AppShell } from '@/components/layout/AppShell'
import { Toaster } from '@/components/ui/sonner'
import { AdminPage } from '@/pages/AdminPage'
import { DevicesPage } from '@/pages/DevicesPage'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth />}>
          <Route element={<AppShell />}>
            <Route path="/" element={<DevicesPage />} />
            <Route element={<RequireAdmin />}>
              <Route path="/admin" element={<AdminPage />} />
            </Route>
          </Route>
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
      <Toaster />
    </BrowserRouter>
  )
}

export default App
