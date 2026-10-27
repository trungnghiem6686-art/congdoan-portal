'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from '@/components/admin/AdminContext'
import { Dialog, Field, Notice, Pager, Table, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Cand = { id: string; code: string; displayName: string; email?: string; phone?: string; orgUnitId?: string | null; isLocked: boolean; lastLoginUtc?: string }
type Unit = { id: string; name: string; code: string }

export default function CandidatesPage() {
  const { can } = useAdmin()
  const manage = can('identity.candidate.manage')
  const [q, setQ] = useState('')
  const [unit, setUnit] = useState('')
  const [trash, setTrash] = useState(false)
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Cand> | null>(null)
  const [units, setUnits] = useState<Unit[]>([])
  const [edit, setEdit] = useState<Partial<Cand> & { password?: string } | null>(null)
  const [importing, setImporting] = useState(false)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [importErrors, setImportErrors] = useState<{ row: number; message: string }[]>([])

  const load = useCallback(() => {
    api<Paged<Cand>>(`/api/admin/candidates?${new URLSearchParams({ page: String(page), pageSize: '20', q, deleted: String(trash), ...(unit ? { orgUnitId: unit } : {}) })}`).then(setData)
  }, [page, q, unit, trash])
  useEffect(() => load(), [load])
  useEffect(() => { api<Unit[]>('/api/admin/org-units').then(setUnits) }, [])

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); setMsg({ kind: 'ok', text: ok }); load() } catch (e) { setMsg({ kind: 'error', text: errorText(e) }) }
  }

  async function save() {
    if (!edit) return
    const body = { code: edit.code, displayName: edit.displayName, email: edit.email || null, phone: edit.phone || null, orgUnitId: edit.orgUnitId || null, password: edit.password || null }
    try {
      if (edit.id) await api(`/api/admin/candidates/${edit.id}`, { method: 'PUT', body })
      else {
        const r = await api<{ initialPassword?: string }>('/api/admin/candidates', { method: 'POST', body })
        if (r.initialPassword) window.alert(`Mật khẩu ban đầu của ${edit.code}: ${r.initialPassword}`)
      }
      setEdit(null)
      setMsg({ kind: 'ok', text: 'Đã lưu thí sinh.' })
      load()
    } catch (e) { setMsg({ kind: 'error', text: errorText(e) }) }
  }

  async function runImport(file: File) {
    setImportErrors([])
    try {
      const r = await api<{ created: number; credentials: { code: string; password: string }[] }>('/api/admin/candidates/import', { method: 'POST', body: { csv: await file.text() } })
      const csv = 'ma,mat_khau\n' + r.credentials.map((c) => `${c.code},${c.password}`).join('\n')
      const a = document.createElement('a')
      a.href = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv' }))
      a.download = 'mat-khau-thi-sinh.csv'
      a.click()
      setMsg({ kind: 'ok', text: `Đã tạo ${r.created} thí sinh. Tệp mật khẩu ban đầu đã được tải về – hãy bảo mật và phát cho thí sinh.` })
      setImporting(false)
      load()
    } catch (e) {
      const data = (e as { data?: { errors?: { row: number; message: string }[] } }).data
      setImportErrors(data?.errors ?? [])
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <>
      <Title actions={manage && <>
        <button className={btnGhost} onClick={() => setImporting(true)}>Nhập danh sách</button>
        <button className={btnPrimary} onClick={() => setEdit({ code: '', displayName: '' })}>Thêm thí sinh</button>
      </>}>Thí sinh</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="mb-4 flex flex-wrap gap-2">
        <input aria-label="Tìm thí sinh" className={`${input} max-w-xs`} placeholder="Mã, họ tên, email…" value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
        <select aria-label="Đơn vị" className={`${input} max-w-xs`} value={unit} onChange={(e) => { setPage(1); setUnit(e.target.value) }}><option value="">Mọi đơn vị</option>{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
        <label className="flex items-center gap-2"><input type="checkbox" checked={trash} onChange={(e) => setTrash(e.target.checked)} /> Đã xóa</label>
      </div>
      <Table head={['Mã', 'Họ tên', 'Đơn vị', 'Đăng nhập gần nhất', 'Tình trạng', '']}>
        {data?.items.map((c) => (
          <tr key={c.id} className="border-t">
            <td className="p-2">{c.code}</td><td className="p-2">{c.displayName}</td><td className="p-2">{units.find((u) => u.id === c.orgUnitId)?.name}</td>
            <td className="p-2">{formatDateTime(c.lastLoginUtc)}</td><td className="p-2">{c.isLocked ? 'Đã khóa' : 'Hoạt động'}</td>
            <td className="p-2">{manage && <div className="flex flex-wrap gap-1">
              {trash ? <button className={btnGhost} onClick={() => run(() => api(`/api/admin/candidates/${c.id}/restore`, { method: 'POST' }), 'Đã khôi phục.')}>Khôi phục</button> : <>
                <button className={btnGhost} onClick={() => setEdit(c)}>Sửa</button>
                <button className={btnGhost} onClick={() => run(() => api(`/api/admin/candidates/${c.id}/${c.isLocked ? 'unlock' : 'lock'}`, { method: 'POST' }), 'Đã cập nhật.')}>{c.isLocked ? 'Mở khóa' : 'Khóa'}</button>
                <button className={btnGhost} onClick={() => run(async () => { const r = await api<{ password: string }>(`/api/admin/candidates/${c.id}/reset-password`, { method: 'POST' }); window.alert(`Mật khẩu mới của ${c.code}: ${r.password}`) }, 'Đã đặt lại mật khẩu.')}>Đặt lại MK</button>
                <button className={btnGhost} onClick={() => window.confirm('Xóa thí sinh?') && run(() => api(`/api/admin/candidates/${c.id}`, { method: 'DELETE' }), 'Đã xóa.')}>Xóa</button>
              </>}
            </div>}</td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
      {edit && (
        <Dialog title={edit.id ? 'Sửa thí sinh' : 'Thêm thí sinh'} onClose={() => setEdit(null)}>
          <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); void save() }}>
            {!edit.id && <Field label="Mã thí sinh (tên đăng nhập)"><input required className={input} value={edit.code} onChange={(e) => setEdit({ ...edit, code: e.target.value })} /></Field>}
            <Field label="Họ tên"><input required className={input} value={edit.displayName} onChange={(e) => setEdit({ ...edit, displayName: e.target.value })} /></Field>
            <Field label="Email"><input type="email" className={input} value={edit.email ?? ''} onChange={(e) => setEdit({ ...edit, email: e.target.value })} /></Field>
            <Field label="Điện thoại"><input className={input} value={edit.phone ?? ''} onChange={(e) => setEdit({ ...edit, phone: e.target.value })} /></Field>
            <Field label="Đơn vị"><select className={input} value={edit.orgUnitId ?? ''} onChange={(e) => setEdit({ ...edit, orgUnitId: e.target.value })}><option value="">— Không —</option>{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>
            {!edit.id && <Field label="Mật khẩu ban đầu" hint="Để trống để tạo ngẫu nhiên."><input className={input} value={edit.password ?? ''} onChange={(e) => setEdit({ ...edit, password: e.target.value })} /></Field>}
            <button className={btnPrimary}>Lưu</button>
          </form>
        </Dialog>
      )}
      {importing && (
        <Dialog title="Nhập danh sách thí sinh (CSV)" onClose={() => setImporting(false)}>
          <p className="mb-2 text-sm">Cột: <code>ma, ho_ten, email, so_dien_thoai, ma_don_vi, mat_khau</code>. Mọi dòng phải hợp lệ thì mới ghi; mật khẩu để trống sẽ được tạo ngẫu nhiên.</p>
          <input type="file" accept=".csv" aria-label="Tệp CSV thí sinh" onChange={(e) => e.target.files?.[0] && void runImport(e.target.files[0])} />
          {importErrors.length > 0 && <ul className="mt-3 list-disc pl-5 text-sm text-brand-red">{importErrors.map((x) => <li key={x.row}>Dòng {x.row}: {x.message}</li>)}</ul>}
        </Dialog>
      )}
    </>
  )
}
