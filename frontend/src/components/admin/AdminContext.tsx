'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import { api, ApiClientError, resetCsrf } from '@/lib/api'

export type Me = { id: string; userName: string; displayName: string; mustChangePassword: boolean }

type Ctx = {
  me: Me
  permissions: Set<string>
  can: (code: string) => boolean
  setDirty: (dirty: boolean) => void
  logout: () => Promise<void>
}

const AdminCtx = createContext<Ctx | null>(null)

export function useAdmin() {
  const c = useContext(AdminCtx)
  if (!c) throw new Error('useAdmin must be used inside AdminProvider')
  return c
}

/** One admin app per tab: a module-level flag is enough and stays out of React rendering. */
const guard = { dirty: false }

export const UNSAVED_MESSAGE = 'Bạn có thay đổi chưa lưu. Rời trang và bỏ thay đổi?'

/** Loads the signed-in admin; sends anonymous visitors to the login page with a return URL. */
export function AdminProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const [state, setState] = useState<{ me: Me; permissions: string[] } | null>(null)

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        const me = await api<Me>('/api/admin/auth/me', { cache: 'no-store' })
        if (me.mustChangePassword) return router.replace('/admin/doi-mat-khau')
        const permissions = await api<string[]>('/api/admin/auth/permissions', { cache: 'no-store' })
        if (alive) setState({ me, permissions })
      } catch (e) {
        if (e instanceof ApiClientError && (e.status === 401 || e.status === 403))
          router.replace(`/admin/dang-nhap?next=${encodeURIComponent(pathname)}`)
      }
    })()
    return () => {
      alive = false
    }
  }, [router, pathname])

  // Unsaved-changes guard: browser close/reload and in-app link clicks.
  useEffect(() => {
    const beforeUnload = (e: BeforeUnloadEvent) => {
      if (!guard.dirty) return
      e.preventDefault()
      e.returnValue = ''
    }
    const click = (e: MouseEvent) => {
      if (!guard.dirty) return
      const a = (e.target as HTMLElement | null)?.closest('a[href]') as HTMLAnchorElement | null
      if (!a || a.target === '_blank' || e.defaultPrevented) return
      if (!window.confirm(UNSAVED_MESSAGE)) {
        e.preventDefault()
        e.stopPropagation()
      } else guard.dirty = false
    }
    window.addEventListener('beforeunload', beforeUnload)
    document.addEventListener('click', click, true)
    return () => {
      window.removeEventListener('beforeunload', beforeUnload)
      document.removeEventListener('click', click, true)
    }
  }, [])

  const setDirty = useCallback((d: boolean) => {
    guard.dirty = d
  }, [])

  const logout = useCallback(async () => {
    await api('/api/admin/auth/logout', { method: 'POST' }).catch(() => undefined)
    resetCsrf()
    router.replace('/admin/dang-nhap')
  }, [router])

  const value = useMemo<Ctx | null>(() => {
    if (!state) return null
    const set = new Set(state.permissions)
    return { me: state.me, permissions: set, can: (c) => set.has(c), setDirty, logout }
  }, [state, setDirty, logout])

  if (!value) return <p className="p-8 text-slate-600" role="status">Đang tải…</p>
  return <AdminCtx.Provider value={value}>{children}</AdminCtx.Provider>
}
