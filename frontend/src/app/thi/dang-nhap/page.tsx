import type { Metadata } from 'next'
import { Suspense } from 'react'
import { LoginForm } from '@/components/admin/LoginForm'

export const metadata: Metadata = { title: 'Đăng nhập thi trực tuyến', robots: { index: false } }

export default function CandidateLogin() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-brand-bg p-4">
      <Suspense>
        <LoginForm kind="thi" />
      </Suspense>
    </main>
  )
}
