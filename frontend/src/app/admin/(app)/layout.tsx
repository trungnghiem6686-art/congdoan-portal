import type { Metadata } from 'next'
import { AdminProvider } from '@/components/admin/AdminContext'
import { AdminShell } from '@/components/admin/AdminShell'

export const metadata: Metadata = { title: 'Quản trị', robots: { index: false, follow: false } }

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <AdminProvider>
      <AdminShell>{children}</AdminShell>
    </AdminProvider>
  )
}
