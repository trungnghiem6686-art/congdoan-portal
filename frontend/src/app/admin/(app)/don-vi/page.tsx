'use client'

import { useCallback, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { Field, Notice, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Unit = { id: string; code: string; name: string; parentId?: string | null; sortOrder: number }
type Group = { id: string; name: string; description?: string; memberCount: number }

export default function OrgUnitsPage() {
  const [units, setUnits] = useState<Unit[]>([])
  const [groups, setGroups] = useState<Group[]>([])
  const [form, setForm] = useState<Partial<Unit>>({ code: '', name: '' })
  const [groupName, setGroupName] = useState('')
  const [members, setMembers] = useState<{ group: Group; codes: string } | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const load = useCallback(() => {
    api<Unit[]>('/api/admin/org-units').then(setUnits)
    api<Group[]>('/api/admin/candidate-groups').then(setGroups).catch(() => setGroups([]))
  }, [])
  useEffect(() => load(), [load])
  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); setMsg({ kind: 'ok', text: ok }); load() } catch (e) { setMsg({ kind: 'error', text: errorText(e) }) }
  }
  const tree = (parent: string | null, depth = 0): React.ReactNode =>
    units.filter((u) => (u.parentId ?? null) === parent).map((u) => (
      <li key={u.id}>
        <div className="flex items-center gap-2 py-1" style={{ paddingLeft: depth * 20 }}>
          <span className="font-medium">{u.name}</span><code className="text-xs text-slate-500">{u.code}</code>
          <button className={btnGhost} onClick={() => setForm(u)}>Sửa</button>
          <button className={btnGhost} onClick={() => window.confirm('Xóa đơn vị?') && run(() => api(`/api/admin/org-units/${u.id}`, { method: 'DELETE' }), 'Đã xóa.')}>Xóa</button>
        </div>
        <ul>{tree(u.id, depth + 1)}</ul>
      </li>
    ))
  return (
    <>
      <Title>Đơn vị và nhóm thí sinh</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="grid gap-6 lg:grid-cols-2">
        <section className="rounded bg-white p-4 shadow-sm">
          <h2 className="mb-2 font-bold">Đơn vị</h2>
          <ul>{tree(null)}</ul>
          <form className="mt-4 grid gap-2 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); void run(() => api(form.id ? `/api/admin/org-units/${form.id}` : '/api/admin/org-units', { method: form.id ? 'PUT' : 'POST', body: { ...form, parentId: form.parentId || null } }), 'Đã lưu đơn vị.').then(() => setForm({ code: '', name: '' })) }}>
            <Field label="Mã đơn vị"><input required className={input} value={form.code ?? ''} onChange={(e) => setForm({ ...form, code: e.target.value })} /></Field>
            <Field label="Tên đơn vị"><input required className={input} value={form.name ?? ''} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
            <Field label="Đơn vị cha"><select className={input} value={form.parentId ?? ''} onChange={(e) => setForm({ ...form, parentId: e.target.value })}><option value="">— Cấp cao nhất —</option>{units.filter((u) => u.id !== form.id).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>
            <div className="flex items-end gap-2"><button className={btnPrimary}>{form.id ? 'Lưu đơn vị' : 'Thêm đơn vị'}</button>{form.id && <button type="button" className={btnGhost} onClick={() => setForm({ code: '', name: '' })}>Hủy</button>}</div>
          </form>
        </section>
        <section className="rounded bg-white p-4 shadow-sm">
          <h2 className="mb-2 font-bold">Nhóm thí sinh</h2>
          <ul className="space-y-1">{groups.map((g) => (
            <li key={g.id} className="flex items-center gap-2"><span className="font-medium">{g.name}</span><span className="text-sm text-slate-500">{g.memberCount} người</span>
              <button className={btnGhost} onClick={() => setMembers({ group: g, codes: '' })}>Thêm thành viên</button>
              <button className={btnGhost} onClick={() => run(() => api(`/api/admin/candidate-groups/${g.id}`, { method: 'DELETE' }), 'Đã xóa nhóm.')}>Xóa</button></li>
          ))}</ul>
          <form className="mt-3 flex gap-2" onSubmit={(e) => { e.preventDefault(); void run(() => api('/api/admin/candidate-groups', { method: 'POST', body: { name: groupName } }), 'Đã tạo nhóm.').then(() => setGroupName('')) }}>
            <input aria-label="Tên nhóm mới" required className={input} value={groupName} onChange={(e) => setGroupName(e.target.value)} placeholder="Tên nhóm mới" />
            <button className={btnPrimary}>Tạo nhóm</button>
          </form>
          {members && (
            <form className="mt-4 space-y-2" onSubmit={async (e) => {
              e.preventDefault()
              const ids: string[] = []
              for (const code of members.codes.split(/[\s,;]+/).filter(Boolean)) {
                const r = await api<{ items: { id: string; code: string }[] }>(`/api/admin/candidates?q=${encodeURIComponent(code)}&pageSize=5`)
                const hit = r.items.find((x) => x.code === code.toLowerCase())
                if (hit) ids.push(hit.id)
              }
              await run(() => api(`/api/admin/candidate-groups/${members.group.id}/members`, { method: 'PUT', body: { add: ids } }), `Đã thêm ${ids.length} thí sinh vào nhóm.`)
              setMembers(null)
            }}>
              <Field label={`Mã thí sinh thêm vào “${members.group.name}”`}><textarea className={input} rows={3} value={members.codes} onChange={(e) => setMembers({ ...members, codes: e.target.value })} /></Field>
              <button className={btnPrimary}>Thêm</button>
            </form>
          )}
        </section>
      </div>
    </>
  )
}
