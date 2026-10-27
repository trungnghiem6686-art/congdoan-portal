'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, ApiClientError, type Paged } from '@/lib/api'
import { uploadFile } from '@/lib/upload'
import { useAdmin } from './AdminContext'
import { RichTextEditor } from './RichTextEditor'
import { Dialog, Field, Notice, Pager, Table, Title, btnGhost, btnPrimary, errorText, input } from './ui'

export type Item = Record<string, any> & { id: string }

export type FieldDef =
  | { key: string; label: string; type: 'text' | 'textarea' | 'date' | 'datetime' | 'number' | 'checkbox'; required?: boolean; hint?: string }
  | { key: string; label: string; type: 'select'; options: [string, string][]; required?: boolean; hint?: string }
  | { key: string; label: string; type: 'media'; accept: string; hint?: string; required?: boolean }
  | { key: string; label: string; type: 'richtext'; hint?: string; required?: boolean }

export type Column = { label: string; render: (i: Item) => React.ReactNode }

type Props = {
  title: string
  endpoint: string
  /** Endpoint returns a plain array instead of a paged result. */
  unpaged?: boolean
  fields: FieldDef[]
  columns: Column[]
  /** Item-level actions endpoints exist: /hide, /show, DELETE, /restore. */
  hideable?: boolean
  defaults?: Item | Record<string, unknown>
  extra?: (item: Item, reload: () => void) => React.ReactNode
}

const toLocal = (utc?: string | null) => (utc ? new Date(new Date(utc).getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16) : '')

/** Generic list + form for simple CMS resources (search, paging, trash, hide/show, optimistic concurrency). */
export function ResourcePage({ title, endpoint, unpaged, fields, columns, hideable = true, defaults = {}, extra }: Props) {
  const { setDirty } = useAdmin()
  const [q, setQ] = useState('')
  const [trash, setTrash] = useState(false)
  const [page, setPage] = useState(1)
  const [rows, setRows] = useState<Item[]>([])
  const [total, setTotal] = useState(0)
  const [editing, setEditing] = useState<Item | Record<string, unknown> | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [conflict, setConflict] = useState(false)

  const reload = useCallback(() => {
    const s = new URLSearchParams({ page: String(page), pageSize: '20', q, deleted: String(trash) })
    api<Paged<Item> | Item[]>(`${endpoint}?${s}`).then((d) => {
      if (Array.isArray(d)) {
        setRows(d)
        setTotal(d.length)
      } else {
        setRows(d.items)
        setTotal(d.total)
      }
    })
  }, [endpoint, page, q, trash])

  useEffect(() => reload(), [reload])
  useEffect(() => {
    setDirty(editing !== null)
    return () => setDirty(false)
  }, [editing, setDirty])

  async function save() {
    if (!editing) return
    const body: Record<string, unknown> = { ...editing }
    for (const f of fields) {
      if (f.type === 'datetime') body[f.key] = body[f.key] ? new Date(String(body[f.key])).toISOString() : null
      if ((f.type === 'date' || f.type === 'select' || f.type === 'media') && body[f.key] === '') body[f.key] = null
    }
    try {
      const id = (editing as Item).id
      await api(id ? `${endpoint}/${id}` : endpoint, { method: id ? 'PUT' : 'POST', body })
      setMsg({ kind: 'ok', text: 'Đã lưu' })
      setEditing(null)
      reload()
    } catch (e) {
      if (e instanceof ApiClientError && e.code === 'concurrency_conflict') setConflict(true)
      else setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function act(i: Item, action: 'hide' | 'show' | 'delete' | 'restore') {
    if (action === 'delete' && !window.confirm('Chuyển mục này vào thùng rác?')) return
    try {
      await api(`${endpoint}/${i.id}${action === 'delete' ? '' : '/' + action}`, { method: action === 'delete' ? 'DELETE' : 'POST' })
      reload()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  const startEdit = (i: Item) => {
    const copy: Item = { ...i }
    for (const f of fields) if (f.type === 'datetime') copy[f.key] = toLocal(i[f.key])
    setEditing(copy)
  }
  const set = (k: string, v: unknown) => setEditing((e) => (e ? { ...e, [k]: v } : e))

  return (
    <>
      <Title actions={<button className={btnPrimary} onClick={() => setEditing({ ...defaults })}>Thêm mới</button>}>{title}</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="mb-4 flex flex-wrap gap-2">
        {!unpaged && <input aria-label="Tìm kiếm" placeholder="Tìm…" className={`${input} max-w-xs`} value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />}
        <label className="flex items-center gap-2"><input type="checkbox" checked={trash} onChange={(e) => { setPage(1); setTrash(e.target.checked) }} /> Thùng rác</label>
      </div>
      <Table head={[...columns.map((c) => c.label), '']}>
        {rows.map((i) => (
          <tr key={i.id} className={`border-t border-slate-100 ${i.isVisible === false ? 'opacity-60' : ''}`}>
            {columns.map((c) => <td key={c.label} className="p-3">{c.render(i)}</td>)}
            <td className="p-3 text-right">
              <div className="flex flex-wrap justify-end gap-1">
                {trash ? (
                  <button className={btnGhost} onClick={() => act(i, 'restore')}>Khôi phục</button>
                ) : (
                  <>
                    <button className={btnGhost} onClick={() => startEdit(i)}>Sửa</button>
                    {hideable && 'isVisible' in i && <button className={btnGhost} onClick={() => act(i, i.isVisible ? 'hide' : 'show')}>{i.isVisible ? 'Ẩn' : 'Hiện'}</button>}
                    {extra?.(i, reload)}
                    <button className={btnGhost} onClick={() => act(i, 'delete')}>Xóa</button>
                  </>
                )}
              </div>
            </td>
          </tr>
        ))}
      </Table>
      {rows.length === 0 && <p className="mt-2 rounded bg-white p-4 text-slate-600">Chưa có dữ liệu.</p>}
      {!unpaged && <Pager page={page} pageSize={20} total={total} onPage={setPage} />}

      {editing && (
        <Dialog title={(editing as Item).id ? `Sửa: ${title.toLowerCase()}` : `Thêm: ${title.toLowerCase()}`} onClose={() => setEditing(null)}>
          <form className="max-h-[70vh] space-y-3 overflow-y-auto pr-1" onSubmit={(e) => { e.preventDefault(); void save() }}>
            {fields.map((f) => <FieldInput key={f.key} f={f} value={(editing as Item)[f.key]} onChange={(v) => set(f.key, v)} />)}
            <div className="flex justify-end gap-2 pt-2">
              <button type="button" className={btnGhost} onClick={() => setEditing(null)}>Hủy</button>
              <button type="submit" className={btnPrimary}>Lưu</button>
            </div>
          </form>
        </Dialog>
      )}
      {conflict && (
        <Dialog title="Xung đột chỉnh sửa" onClose={() => setConflict(false)}>
          <p>Mục này đã được người khác sửa. Tải lại để xem bản mới nhất rồi sửa lại.</p>
          <div className="mt-4 flex justify-end"><button className={btnPrimary} onClick={() => { setConflict(false); setEditing(null); reload() }}>Tải lại bản mới nhất</button></div>
        </Dialog>
      )}
    </>
  )
}

function FieldInput({ f, value, onChange }: { f: FieldDef; value: unknown; onChange: (v: unknown) => void }) {
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  switch (f.type) {
    case 'checkbox':
      return <label className="flex items-center gap-2"><input type="checkbox" checked={!!value} onChange={(e) => onChange(e.target.checked)} /> {f.label}</label>
    case 'textarea':
      return <Field label={f.label} hint={f.hint}><textarea className={input} rows={4} value={String(value ?? '')} onChange={(e) => onChange(e.target.value)} /></Field>
    case 'select':
      return (
        <Field label={f.label} hint={f.hint}>
          <select className={input} required={f.required} value={String(value ?? '')} onChange={(e) => onChange(e.target.value)}>
            {!f.required && <option value="">— Không chọn —</option>}
            {f.options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </Field>
      )
    case 'richtext':
      return <div><span className="mb-1 block text-sm font-medium">{f.label}</span><RichTextEditor label={f.label} initialHtml={String(value ?? '')} onChange={onChange} /></div>
    case 'media':
      return (
        <Field label={f.label} hint={f.hint}>
          {typeof value === 'string' && value && <a className="mb-1 block text-sm text-brand-blue underline" href={`/media/${value}`} target="_blank" rel="noreferrer">Tệp hiện tại</a>}
          <input type="file" accept={f.accept} disabled={busy} onChange={async (e) => {
            const file = e.target.files?.[0]
            if (!file) return
            setBusy(true)
            setErr(null)
            try { onChange((await uploadFile(file)).id) } catch (x) { setErr(errorText(x)) } finally { setBusy(false) }
          }} />
          {busy && <span className="text-sm">Đang tải…</span>}
          {err && <span role="alert" className="text-sm text-brand-red">{err}</span>}
        </Field>
      )
    default:
      return (
        <Field label={f.label} hint={f.hint}>
          <input className={input} required={f.required} type={f.type === 'datetime' ? 'datetime-local' : f.type} value={String(value ?? '')}
            onChange={(e) => onChange(f.type === 'number' ? Number(e.target.value) : e.target.value)} />
        </Field>
      )
  }
}
