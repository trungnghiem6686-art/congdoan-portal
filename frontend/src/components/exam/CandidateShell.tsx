'use client'

import Link from 'next/link'
import { createContext, useContext, useEffect, useState } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import { api, ApiClientError, resetCsrf } from '@/lib/api'

type Me = { id: string; userName: string; displayName: string; mustChangePassword: boolean }
const Ctx = createContext<Me | null>(null)
export const useCandidate = () => useContext(Ctx)!

/** Candidate area: sign-in check (separate cookie from admin), compact header. */
export function CandidateShell({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const [me, setMe] = useState<Me | null>(null)

  useEffect(() => {
    api<Me>('/api/thi/auth/me', { cache: 'no-store' })
      .then((m) => (m.mustChangePassword ? router.replace('/thi/doi-mat-khau') : setMe(m)))
      .catch((e) => {
        if (e instanceof ApiClientError && (e.status === 401 || e.status === 403)) router.replace(`/thi/dang-nhap?next=${encodeURIComponent(pathname)}`)
      })
  }, [router, pathname])

  if (!me) return <p className="p-6" role="status">Đang tải…</p>
  return (
    <Ctx.Provider value={me}>
      <div className="min-h-screen bg-brand-bg">
        <header className="bg-brand-navy text-white">
          <div className="container-site flex flex-wrap items-center justify-between gap-2 py-3">
            <Link href="/thi" className="font-bold">Thi trực tuyến</Link>
            <div className="flex items-center gap-3 text-sm">
              <span>{me.displayName}</span>
              <button className="rounded border border-white/40 px-2 py-1" onClick={async () => {
                await api('/api/thi/auth/logout', { method: 'POST' }).catch(() => undefined)
                resetCsrf()
                router.replace('/thi/dang-nhap')
              }}>Đăng xuất</button>
            </div>
          </div>
        </header>
        {children}
      </div>
    </Ctx.Provider>
  )
}
