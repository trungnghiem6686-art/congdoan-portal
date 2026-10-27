'use client'

import { useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { api, ApiClientError, resetCsrf } from '@/lib/api'
import { btnPrimary, input } from './ui'

type Kind = 'admin' | 'thi'
const apiBase = (k: Kind) => (k === 'admin' ? '/api/admin/auth' : '/api/thi/auth')
const home = (k: Kind) => (k === 'admin' ? '/admin' : '/thi')

/** Only same-area relative paths are accepted as return URLs (no open redirect). */
function safeNext(next: string | null, k: Kind) {
  return next && next.startsWith(home(k)) && !next.startsWith('//') ? next : home(k)
}

export function LoginForm({ kind }: { kind: Kind }) {
  const router = useRouter()
  const params = useSearchParams()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    setBusy(true)
    setError(null)
    try {
      const me = await api<{ mustChangePassword: boolean }>(`${apiBase(kind)}/login`, {
        method: 'POST', body: { userName: f.get('userName'), password: f.get('password') },
      })
      resetCsrf()
      router.replace(me.mustChangePassword ? `${home(kind)}/doi-mat-khau` : safeNext(params.get('next'), kind))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Không đăng nhập được.')
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded bg-white p-6 shadow">
      <h1 className="text-xl font-bold text-brand-navy">{kind === 'admin' ? 'Đăng nhập quản trị' : 'Đăng nhập thi trực tuyến'}</h1>
      {error && <p role="alert" className="rounded bg-red-50 p-3 text-brand-red">{error}</p>}
      <label className="block">
        <span className="mb-1 block font-medium">{kind === 'admin' ? 'Tên đăng nhập' : 'Mã thí sinh'}</span>
        <input name="userName" required autoComplete="username" className={input} />
      </label>
      <label className="block">
        <span className="mb-1 block font-medium">Mật khẩu</span>
        <input name="password" type="password" required autoComplete="current-password" className={input} />
      </label>
      <button type="submit" disabled={busy} className={`${btnPrimary} w-full justify-center`}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </form>
  )
}

export function ChangePasswordForm({ kind }: { kind: Kind }) {
  const router = useRouter()
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (f.get('newPassword') !== f.get('confirm')) return setError('Mật khẩu nhập lại không khớp.')
    try {
      await api(`${apiBase(kind)}/change-password`, { method: 'POST', body: { currentPassword: f.get('currentPassword'), newPassword: f.get('newPassword') } })
      router.replace(home(kind))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Không đổi được mật khẩu.')
    }
  }

  return (
    <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded bg-white p-6 shadow">
      <h1 className="text-xl font-bold text-brand-navy">Đổi mật khẩu</h1>
      <p className="text-sm text-slate-600">Vì lý do an toàn, bạn cần đặt mật khẩu mới (ít nhất 8 ký tự, gồm chữ và số).</p>
      {error && <p role="alert" className="rounded bg-red-50 p-3 text-brand-red">{error}</p>}
      <label className="block"><span className="mb-1 block font-medium">Mật khẩu hiện tại</span><input name="currentPassword" type="password" required autoComplete="current-password" className={input} /></label>
      <label className="block"><span className="mb-1 block font-medium">Mật khẩu mới</span><input name="newPassword" type="password" required minLength={8} autoComplete="new-password" className={input} /></label>
      <label className="block"><span className="mb-1 block font-medium">Nhập lại mật khẩu mới</span><input name="confirm" type="password" required autoComplete="new-password" className={input} /></label>
      <button type="submit" className={`${btnPrimary} w-full justify-center`}>Lưu mật khẩu</button>
    </form>
  )
}
