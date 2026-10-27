'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowDown, ArrowUp, GripVertical, Monitor, Plus, Smartphone, Tablet, Trash2 } from 'lucide-react'
import { api } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from './AdminContext'
import { Field, Notice, btnDanger, btnGhost, btnPrimary, errorText, input } from './ui'

type Config = Record<string, any>
type Section = { id?: string; type: string; title?: string | null; visible: boolean; startsUtc?: string | null; endsUtc?: string | null; config: Config }
type Revision = { id: string; version: number; status: string; sections: Section[]; publishedUtc?: string | null }
type History = { id: string; version: number; status: string; publishedUtc?: string | null; note?: string | null }
type State = { draft: Revision | null; published: Revision | null; history: History[] }
type BlockType = { type: string; label: string; layouts: string[] }

const defaults: Record<string, Config> = {
  featured_news: { limit: 5 }, news_list: { limit: 6 }, news_grid: { limit: 4, columns: 4 }, banner: { position: 'home_top' },
  quick_links: { items: [{ label: 'Thi trực tuyến', url: '/thi' }] }, documents: { limit: 5 }, gallery: { limit: 6 },
  exam_notice: { limit: 3 }, intro_text: { html: '<p>Nội dung giới thiệu</p>' }, links: { items: [{ label: 'Liên kết', url: 'https://' }] },
}

const viewports = [
  { label: 'Máy tính', width: 1280, Icon: Monitor },
  { label: 'Máy tính bảng', width: 768, Icon: Tablet },
  { label: 'Điện thoại', width: 390, Icon: Smartphone },
] as const

let keySeq = 0
type Row = Section & { key: string }
const withKeys = (s: Section[]): Row[] => s.map((x) => ({ ...x, key: x.id ?? `new-${++keySeq}` }))
const toUtc = (local: string) => (local ? new Date(local).toISOString() : null)
const toLocal = (utc?: string | null) => (utc ? new Date(new Date(utc).getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16) : '')

/**
 * Draft/publish block editor shared by the homepage and static pages. `base` is the admin API root
 * (e.g. /api/admin/homepage or /api/admin/pages/{id}/layout); `previewSource` feeds the preview page.
 */
export function BlockBuilder({ base, previewSource, title }: { base: string; previewSource: string; title: string }) {
  const { setDirty } = useAdmin()
  const [types, setTypes] = useState<BlockType[]>([])
  const [state, setState] = useState<State | null>(null)
  const [rows, setRows] = useState<Row[]>([])
  const [saved, setSaved] = useState('')
  const [open, setOpen] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [viewport, setViewport] = useState<number>(1280)
  const [previewKey, setPreviewKey] = useState(0)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [dragIndex, setDragIndex] = useState<number | null>(null)

  const apply = useCallback((s: State) => {
    setState(s)
    const r = withKeys((s.draft ?? s.published)?.sections ?? [])
    setRows(r)
    setSaved(JSON.stringify(r.map(({ key: _k, ...x }) => x)))
  }, [])

  useEffect(() => {
    api<State>(base).then(apply)
    api<BlockType[]>('/api/admin/homepage/block-types').then(setTypes).catch(() => setTypes([]))
  }, [base, apply])

  const payload = useMemo(() => rows.map(({ key: _k, ...x }) => x), [rows])
  const dirty = JSON.stringify(payload) !== saved
  useEffect(() => {
    setDirty(dirty)
    return () => setDirty(false)
  }, [dirty, setDirty])

  const label = (t: string) => types.find((x) => x.type === t)?.label ?? t
  const nameOf = (r: Row) => r.title?.trim() || label(r.type)
  const update = (key: string, patch: Partial<Section>) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, ...patch } : r)))
  const updateConfig = (key: string, patch: Config) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, config: { ...r.config, ...patch } } : r)))
  const move = (from: number, to: number) =>
    setRows((rs) => {
      if (to < 0 || to >= rs.length || from === to) return rs
      const next = [...rs]
      const [it] = next.splice(from, 1)
      next.splice(to, 0, it)
      return next
    })

  async function saveDraft(): Promise<Revision | null> {
    try {
      const d = await api<Revision>(`${base}/draft`, { method: 'PUT', body: { sections: payload } })
      const s = await api<State>(base)
      apply(s)
      setPreviewKey((k) => k + 1)
      setMsg({ kind: 'ok', text: 'Đã lưu bản nháp' })
      return d
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
      return null
    }
  }

  async function publish() {
    const draft = dirty || !state?.draft ? await saveDraft() : state.draft
    if (!draft) return
    try {
      const p = await api<Revision>(`${base}/publish`, { method: 'POST', body: { revisionId: draft.id } })
      apply(await api<State>(base))
      setMsg({ kind: 'ok', text: `Đã xuất bản phiên bản ${p.version}. Trang công khai cập nhật ngay, không cần triển khai lại.` })
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function rollback(version: number) {
    if (!window.confirm(`Khôi phục phiên bản ${version}? Bố cục đó sẽ được xuất bản thành phiên bản mới.`)) return
    try {
      const p = await api<Revision>(`${base}/rollback`, { method: 'POST', body: { version } })
      apply(await api<State>(base))
      setPreviewKey((k) => k + 1)
      setMsg({ kind: 'ok', text: `Đã khôi phục phiên bản ${version} thành phiên bản ${p.version}.` })
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  function add(type: string) {
    const key = `new-${++keySeq}`
    setRows((rs) => [...rs, { key, type, title: label(type), visible: true, config: { ...defaults[type] } }])
    setOpen(key)
    setAdding(false)
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-brand-navy">{title}</h1>
        <div className="flex flex-wrap gap-2">
          <button className={btnGhost} onClick={() => void saveDraft()}>Lưu bản nháp</button>
          <button className={btnDanger} onClick={() => void publish()}>Xuất bản {title.toLowerCase()}</button>
        </div>
      </div>
      <p className="mb-3 text-sm text-slate-600">
        {state?.published ? `Đang hiển thị phiên bản ${state.published.version} (xuất bản ${formatDateTime(state.published.publishedUtc)}).` : 'Chưa xuất bản phiên bản nào.'}
        {state?.draft && ' Có bản nháp chưa xuất bản.'}
        {dirty && <span className="ml-1 text-brand-red">Có thay đổi chưa lưu.</span>}
      </p>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}

      <div className="grid gap-6 2xl:grid-cols-[520px_1fr]">
        <div className="min-w-0">
          <ol className="space-y-2">
            {rows.map((r, i) => (
              <li
                key={r.key}
                data-block-item
                draggable
                onDragStart={(e) => { setDragIndex(i); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', String(i)) }}
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => { e.preventDefault(); const from = dragIndex ?? Number(e.dataTransfer.getData('text/plain')); move(from, i); setDragIndex(null) }}
                className={`rounded border bg-white shadow-sm ${r.visible ? 'border-slate-200' : 'border-dashed border-slate-300 opacity-70'}`}
              >
                <div className="flex items-center gap-2 p-2">
                  <GripVertical aria-hidden className="h-4 w-4 cursor-grab text-slate-400" />
                  <button type="button" className="flex-1 text-left" aria-expanded={open === r.key} onClick={() => setOpen(open === r.key ? null : r.key)}>
                    <span data-block-title className="font-semibold">{nameOf(r)}</span>
                    <span className="ml-2 text-xs text-slate-500">{label(r.type)}{!r.visible && ' · đang ẩn'}</span>
                  </button>
                  <button type="button" aria-label={`Chuyển lên: ${nameOf(r)}`} className={btnGhost} disabled={i === 0} onClick={() => move(i, i - 1)}><ArrowUp className="h-4 w-4" /></button>
                  <button type="button" aria-label={`Chuyển xuống: ${nameOf(r)}`} className={btnGhost} disabled={i === rows.length - 1} onClick={() => move(i, i + 1)}><ArrowDown className="h-4 w-4" /></button>
                  <button type="button" aria-label={`Xóa khối: ${nameOf(r)}`} className={btnGhost} onClick={() => setRows((rs) => rs.filter((x) => x.key !== r.key))}><Trash2 className="h-4 w-4" /></button>
                </div>
                {open === r.key && (
                  <div className="space-y-3 border-t border-slate-100 p-3">
                    <Field label="Tiêu đề khối"><input className={input} value={r.title ?? ''} onChange={(e) => update(r.key, { title: e.target.value })} /></Field>
                    <label className="flex items-center gap-2"><input type="checkbox" checked={r.visible} onChange={(e) => update(r.key, { visible: e.target.checked })} /> Hiển thị</label>
                    <div className="grid gap-3 sm:grid-cols-2">
                      <Field label="Hiện từ"><input type="datetime-local" className={input} value={toLocal(r.startsUtc)} onChange={(e) => update(r.key, { startsUtc: toUtc(e.target.value) })} /></Field>
                      <Field label="Ẩn sau"><input type="datetime-local" className={input} value={toLocal(r.endsUtc)} onChange={(e) => update(r.key, { endsUtc: toUtc(e.target.value) })} /></Field>
                    </div>
                    <Field label="Kiểu trình bày">
                      <select className={input} value={r.config.layout ?? ''} onChange={(e) => updateConfig(r.key, { layout: e.target.value })}>
                        {(types.find((t) => t.type === r.type)?.layouts ?? []).map((l) => <option key={l}>{l}</option>)}
                      </select>
                    </Field>
                    <ConfigEditor row={r} onChange={(patch) => updateConfig(r.key, patch)} />
                  </div>
                )}
              </li>
            ))}
          </ol>
          <div className="relative mt-3">
            <button type="button" className={btnPrimary} aria-haspopup="menu" aria-expanded={adding} onClick={() => setAdding((a) => !a)}>
              <Plus aria-hidden className="h-4 w-4" /> Thêm khối
            </button>
            {adding && (
              <ul role="menu" className="absolute z-10 mt-1 w-64 rounded border border-slate-200 bg-white py-1 shadow-lg">
                {types.map((t) => (
                  <li key={t.type} role="none">
                    <button role="menuitem" type="button" className="block w-full px-3 py-2 text-left hover:bg-slate-100" onClick={() => add(t.type)}>{t.label}</button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <section aria-label="Lịch sử xuất bản" className="mt-6 rounded bg-white p-3 shadow-sm">
            <h2 className="mb-2 font-bold text-brand-navy">Lịch sử xuất bản</h2>
            <ul className="space-y-1 text-sm">
              {state?.history.map((h) => (
                <li key={h.id} data-version={h.version} data-status={h.status} className="flex items-center justify-between gap-2">
                  <span>
                    Phiên bản {h.version} · {formatDateTime(h.publishedUtc)} {h.status === 'Published' && <strong className="text-green-700">(đang dùng)</strong>}
                    {h.note && <span className="text-slate-500"> – {h.note}</span>}
                  </span>
                  {h.status !== 'Published' && (
                    <button className={btnGhost} aria-label={`Khôi phục phiên bản ${h.version}`} onClick={() => void rollback(h.version)}>Khôi phục</button>
                  )}
                </li>
              ))}
            </ul>
          </section>
        </div>

        <div className="min-w-0">
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <span className="text-sm font-medium">Xem trước bản nháp đã lưu:</span>
            {viewports.map(({ label: l, width, Icon }) => (
              <button key={l} type="button" aria-pressed={viewport === width} className={viewport === width ? btnPrimary : btnGhost} onClick={() => setViewport(width)}>
                <Icon aria-hidden className="h-4 w-4" /> {l}
              </button>
            ))}
          </div>
          <div className="overflow-auto rounded border border-slate-300 bg-slate-200 p-2">
            <iframe
              key={previewKey}
              title={`Xem trước ${title.toLowerCase()}`}
              src={`/admin/xem-truoc?nguon=${encodeURIComponent(previewSource)}&v=${previewKey}`}
              width={viewport}
              height={900}
              className="mx-auto block bg-white"
            />
          </div>
        </div>
      </div>
    </div>
  )
}

function ConfigEditor({ row, onChange }: { row: Section; onChange: (patch: Config) => void }) {
  const c = row.config
  const num = (key: string, labelText: string, min: number, max: number) => (
    <Field label={labelText}><input type="number" min={min} max={max} className={input} value={c[key] ?? ''} onChange={(e) => onChange({ [key]: Number(e.target.value) })} /></Field>
  )
  switch (row.type) {
    case 'featured_news':
    case 'news_list':
    case 'news_grid':
      return (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Đường dẫn chuyên mục (để trống: mọi chuyên mục)"><input className={input} value={c.categorySlug ?? ''} onChange={(e) => onChange({ categorySlug: e.target.value || null })} /></Field>
          {num('limit', 'Số tin', 1, 20)}
          {row.type === 'news_grid' && num('columns', 'Số cột', 2, 4)}
        </div>
      )
    case 'banner':
      return <Field label="Vị trí banner"><input className={input} value={c.position ?? ''} onChange={(e) => onChange({ position: e.target.value })} /></Field>
    case 'documents':
      return (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Loại văn bản (để trống: tất cả)"><input className={input} value={c.docType ?? ''} onChange={(e) => onChange({ docType: e.target.value || null })} /></Field>
          {num('limit', 'Số văn bản', 1, 20)}
        </div>
      )
    case 'gallery':
      return (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Loại album">
            <select className={input} value={c.albumKind ?? ''} onChange={(e) => onChange({ albumKind: e.target.value || null })}>
              <option value="">Tất cả</option><option value="Photo">Ảnh</option><option value="Video">Video</option>
            </select>
          </Field>
          {num('limit', 'Số album', 1, 12)}
        </div>
      )
    case 'exam_notice':
      return num('limit', 'Số kỳ thi', 1, 10)
    case 'intro_text':
      return <Field label="Nội dung HTML" hint="Chỉ thẻ định dạng cơ bản; script bị loại bỏ khi lưu."><textarea className={`${input} font-mono text-sm`} rows={6} value={c.html ?? ''} onChange={(e) => onChange({ html: e.target.value })} /></Field>
    case 'quick_links':
    case 'links': {
      const items: { label: string; url: string }[] = c.items ?? []
      const set = (next: typeof items) => onChange({ items: next })
      return (
        <div className="space-y-2">
          {items.map((it, i) => (
            <div key={i} className="flex gap-2">
              <input aria-label={`Tên liên kết ${i + 1}`} className={input} value={it.label} onChange={(e) => set(items.map((x, j) => (j === i ? { ...x, label: e.target.value } : x)))} />
              <input aria-label={`Địa chỉ liên kết ${i + 1}`} className={input} value={it.url} onChange={(e) => set(items.map((x, j) => (j === i ? { ...x, url: e.target.value } : x)))} />
              <button type="button" className={btnGhost} aria-label={`Xóa liên kết ${i + 1}`} onClick={() => set(items.filter((_, j) => j !== i))}>×</button>
            </div>
          ))}
          <button type="button" className={btnGhost} onClick={() => set([...items, { label: '', url: '' }])}>+ Liên kết</button>
        </div>
      )
    }
    default:
      return null
  }
}
