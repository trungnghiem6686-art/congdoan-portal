'use client'

import { useCallback, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { Notice, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Role = { id: string; code: string; name: string; isSystem: boolean; permissions: string[] }
type Perm = { code: string; module: string; name: string }

export default function RolesPage() {
  const [roles, setRoles] = useState<Role[]>([])
  const [perms, setPerms] = useState<Perm[]>([])
  const [edit, setEdit] = useState<Partial<Role> | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const load = useCallback(() => { api<Role[]>('/api/admin/roles').then(setRoles) }, [])
  useEffect(() => { load(); api<Perm[]>('/api/admin/permissions').then(setPerms) }, [load])
  const modules = [...new Set(perms.map((p) => p.module))]
  async function save() {
    if (!edit) return
    try {
      await api(edit.id ? `/api/admin/roles/${edit.id}` : '/api/admin/roles', { method: edit.id ? 'PUT' : 'POST', body: edit })
      setMsg({ kind: 'ok', text: 'Đã lưu vai trò. Quyền mới có hiệu lực ngay ở yêu cầu tiếp theo.' })
      setEdit(null)
      load()
    } catch (e) { setMsg({ kind: 'error', text: errorText(e) }) }
  }
  return (
    <>
      <Title actions={<button className={btnPrimary} onClick={() => setEdit({ code: '', name: '', permissions: [] })}>Thêm vai trò</button>}>Vai trò và quyền</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <ul className="space-y-1">{roles.map((r) => (
          <li key={r.id}><button className={`${edit?.id === r.id ? btnPrimary : btnGhost} w-full justify-between`} onClick={() => setEdit(r)}>{r.name}<span className="text-xs">{r.permissions.length} quyền</span></button></li>
        ))}</ul>
        {edit && (
          <form className="space-y-3 rounded bg-white p-4 shadow-sm" onSubmit={(e) => { e.preventDefault(); void save() }}>
            {!edit.id && <input aria-label="Mã vai trò" required placeholder="Mã (vd: bien_tap_don_vi)" className={input} value={edit.code} onChange={(e) => setEdit({ ...edit, code: e.target.value })} />}
            <input aria-label="Tên vai trò" required className={input} value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} />
            {edit.code === 'system_admin' && <Notice kind="info">Quản trị hệ thống luôn có mọi quyền.</Notice>}
            {modules.map((m) => (
              <fieldset key={m} className="rounded border border-slate-200 p-3">
                <legend className="px-1 text-sm font-semibold uppercase">{m}</legend>
                <div className="grid gap-1 sm:grid-cols-2">{perms.filter((p) => p.module === m).map((p) => (
                  <label key={p.code} className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={edit.code === 'system_admin'} checked={edit.permissions!.includes(p.code)}
                    onChange={(e) => setEdit({ ...edit, permissions: e.target.checked ? [...edit.permissions!, p.code] : edit.permissions!.filter((x) => x !== p.code) })} /> {p.name}</label>
                ))}</div>
              </fieldset>
            ))}
            <div className="flex gap-2">
              {edit.code !== 'system_admin' && <button className={btnPrimary}>Lưu vai trò</button>}
              {edit.id && !edit.isSystem && <button type="button" className={btnGhost} onClick={async () => { try { await api(`/api/admin/roles/${edit.id}`, { method: 'DELETE' }); setEdit(null); load() } catch (x) { setMsg({ kind: 'error', text: errorText(x) }) } }}>Xóa vai trò</button>}
            </div>
          </form>
        )}
      </div>
    </>
  )
}
