'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Dialog, Field, Notice, Pager, Table, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Grant = { roleCode: string; orgUnitId?: string | null }
type User = { id: string; userName: string; displayName: string; email?: string; isLocked: boolean; isDeleted: boolean; mustChangePassword: boolean; lastLoginUtc?: string; roles: Grant[] }
type Role = { code: string; name: string }
type Unit = { id: string; name: string }

export default function AdminUsersPage() {
  const [q, setQ] = useState('')
  const [trash, setTrash] = useState(false)
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<User> | null>(null)
  const [roles, setRoles] = useState<Role[]>([])
  const [units, setUnits] = useState<Unit[]>([])
  const [edit, setEdit] = useState<(Partial<User> & { password?: string }) | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  const load = useCallback(() => {
    api<Paged<User>>(`/api/admin/users?page=${page}&pageSize=20&q=${encodeURIComponent(q)}&deleted=${trash}`).then(setData)
  }, [page, q, trash])
  useEffect(() => load(), [load])
  useEffect(() => {
    api<Role[]>('/api/admin/roles').then(setRoles).catch(() => setRoles([]))
    api<Unit[]>('/api/admin/org-units').then(setUnits)
  }, [])

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); setMsg({ kind: 'ok', text: ok }); load() } catch (e) { setMsg({ kind: 'error', text: errorText(e) }) }
  }

  async function save() {
    if (!edit) return
    try {
      if (edit.id) {
        await api(`/api/admin/users/${edit.id}`, { method: 'PUT', body: { displayName: edit.displayName, email: edit.email } })
        await api(`/api/admin/users/${edit.id}/roles`, { method: 'PUT', body: { roles: edit.roles } })
        setMsg({ kind: 'ok', text: 'Đã lưu tài khoản.' })
      } else {
        const r = await api<{ initialPassword?: string }>('/api/admin/users', { method: 'POST', body: edit })
        setMsg({ kind: 'ok', text: r.initialPassword ? `Đã tạo. Mật khẩu ban đầu (chỉ hiện một lần): ${r.initialPassword}` : 'Đã tạo tài khoản.' })
      }
      setEdit(null)
      load()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  const roleName = (c: string) => roles.find((r) => r.code === c)?.name ?? c
  return (
    <>
      <Title actions={<button className={btnPrimary} onClick={() => setEdit({ userName: '', displayName: '', roles: [] })}>Thêm tài khoản</button>}>Tài khoản quản trị</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="mb-4 flex gap-2">
        <input aria-label="Tìm tài khoản" className={`${input} max-w-xs`} placeholder="Tìm…" value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
        <label className="flex items-center gap-2"><input type="checkbox" checked={trash} onChange={(e) => setTrash(e.target.checked)} /> Đã xóa</label>
      </div>
      <Table head={['Tên đăng nhập', 'Họ tên', 'Vai trò', 'Đăng nhập gần nhất', 'Tình trạng', '']}>
        {data?.items.map((u) => (
          <tr key={u.id} className="border-t">
            <td className="p-2">{u.userName}</td><td className="p-2">{u.displayName}</td>
            <td className="p-2">{u.roles.map((r) => roleName(r.roleCode) + (r.orgUnitId ? ` (${units.find((x) => x.id === r.orgUnitId)?.name ?? 'đơn vị'})` : '')).join(', ')}</td>
            <td className="p-2">{formatDateTime(u.lastLoginUtc)}</td><td className="p-2">{u.isLocked ? 'Đã khóa' : 'Hoạt động'}</td>
            <td className="p-2"><div className="flex flex-wrap gap-1">
              {trash ? <button className={btnGhost} onClick={() => run(() => api(`/api/admin/users/${u.id}/restore`, { method: 'POST' }), 'Đã khôi phục.')}>Khôi phục</button> : <>
                <button className={btnGhost} onClick={() => setEdit(u)}>Sửa</button>
                <button className={btnGhost} onClick={() => run(() => api(`/api/admin/users/${u.id}/${u.isLocked ? 'unlock' : 'lock'}`, { method: 'POST' }), u.isLocked ? 'Đã mở khóa.' : 'Đã khóa và đăng xuất mọi phiên.')}>{u.isLocked ? 'Mở khóa' : 'Khóa'}</button>
                <button className={btnGhost} onClick={() => run(async () => { const r = await api<{ password: string }>(`/api/admin/users/${u.id}/reset-password`, { method: 'POST' }); window.alert(`Mật khẩu mới (bắt buộc đổi khi đăng nhập): ${r.password}`) }, 'Đã đặt lại mật khẩu.')}>Đặt lại MK</button>
                <button className={btnGhost} onClick={() => window.confirm('Xóa tài khoản?') && run(() => api(`/api/admin/users/${u.id}`, { method: 'DELETE' }), 'Đã xóa.')}>Xóa</button>
              </>}
            </div></td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
      {edit && (
        <Dialog title={edit.id ? 'Sửa tài khoản' : 'Thêm tài khoản'} onClose={() => setEdit(null)}>
          <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); void save() }}>
            {!edit.id && <Field label="Tên đăng nhập"><input required className={input} value={edit.userName} onChange={(e) => setEdit({ ...edit, userName: e.target.value })} /></Field>}
            <Field label="Họ tên"><input required className={input} value={edit.displayName} onChange={(e) => setEdit({ ...edit, displayName: e.target.value })} /></Field>
            <Field label="Email"><input type="email" className={input} value={edit.email ?? ''} onChange={(e) => setEdit({ ...edit, email: e.target.value })} /></Field>
            {!edit.id && <Field label="Mật khẩu ban đầu" hint="Để trống để hệ thống tạo ngẫu nhiên. Người dùng phải đổi khi đăng nhập lần đầu."><input className={input} value={edit.password ?? ''} onChange={(e) => setEdit({ ...edit, password: e.target.value })} /></Field>}
            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">Vai trò</legend>
              {(edit.roles ?? []).map((g, i) => (
                <div key={i} className="flex gap-2">
                  <select aria-label="Vai trò" className={input} value={g.roleCode} onChange={(e) => setEdit({ ...edit, roles: edit.roles!.map((x, j) => (j === i ? { ...x, roleCode: e.target.value } : x)) })}>{roles.map((r) => <option key={r.code} value={r.code}>{r.name}</option>)}</select>
                  <select aria-label="Phạm vi" className={input} value={g.orgUnitId ?? ''} onChange={(e) => setEdit({ ...edit, roles: edit.roles!.map((x, j) => (j === i ? { ...x, orgUnitId: e.target.value || null } : x)) })}><option value="">Toàn hệ thống</option>{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
                  <button type="button" className={btnGhost} onClick={() => setEdit({ ...edit, roles: edit.roles!.filter((_, j) => j !== i) })}>×</button>
                </div>
              ))}
              <button type="button" className={btnGhost} onClick={() => setEdit({ ...edit, roles: [...(edit.roles ?? []), { roleCode: roles[0]?.code ?? 'editor', orgUnitId: null }] })}>+ Vai trò</button>
            </fieldset>
            <button className={btnPrimary}>Lưu</button>
          </form>
        </Dialog>
      )}
    </>
  )
}
