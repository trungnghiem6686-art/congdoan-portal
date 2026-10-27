import type { Metadata } from 'next'
import { ChangePasswordForm } from '@/components/admin/LoginForm'

export const metadata: Metadata = { title: 'Đổi mật khẩu', robots: { index: false } }

export default function AdminChangePassword() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-brand-bg p-4">
      <ChangePasswordForm kind="admin" />
    </main>
  )
}
