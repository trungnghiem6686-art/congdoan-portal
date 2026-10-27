import type { Metadata } from 'next'
import { Suspense } from 'react'
import { LoginForm } from '@/components/admin/LoginForm'

export const metadata: Metadata = { title: 'Đăng nhập quản trị', robots: { index: false } }

export default function AdminLogin() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-brand-bg p-4">
      <Suspense>
        <LoginForm kind="admin" />
      </Suspense>
    </main>
  )
}
