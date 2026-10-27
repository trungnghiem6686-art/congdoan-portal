'use client'

import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { uploadFile } from '@/lib/upload'
import { Field, Notice, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Link = { label: string; url: string }
type Any = Record<string, any>

function Links({ value, onChange }: { value: Link[]; onChange: (v: Link[]) => void }) {
  return (
    <div className="space-y-2">
      {value.map((l, i) => (
        <div key={i} className="flex gap-2">
          <input aria-label="Tên" className={input} value={l.label} onChange={(e) => onChange(value.map((x, j) => (j === i ? { ...x, label: e.target.value } : x)))} />
          <input aria-label="Địa chỉ" className={input} value={l.url} onChange={(e) => onChange(value.map((x, j) => (j === i ? { ...x, url: e.target.value } : x)))} />
          <button type="button" className={btnGhost} onClick={() => onChange(value.filter((_, j) => j !== i))}>×</button>
        </div>
      ))}
      <button type="button" className={btnGhost} onClick={() => onChange([...value, { label: '', url: '' }])}>+ Liên kết</button>
    </div>
  )
}

export default function SettingsPage() {
  const [s, setS] = useState<Any | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  useEffect(() => {
    Promise.all(['site', 'contact', 'footer', 'utility', 'seo'].map((k) => api<Any>(`/api/admin/settings/${k}`).then((v) => [k, v] as const)))
      .then((all) => setS(Object.fromEntries(all)))
  }, [])

  if (!s) return <p role="status">Đang tải…</p>
  const set = (key: string, patch: Any) => setS({ ...s, [key]: { ...s[key], ...patch } })
  const save = async (key: string) => {
    try {
      await api(`/api/admin/settings/${key}`, { method: 'PUT', body: s[key] })
      setMsg({ kind: 'ok', text: 'Đã lưu. Trang công khai cập nhật ngay.' })
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }
  const card = 'space-y-3 rounded bg-white p-4 shadow-sm'

  return (
    <>
      <Title>Cấu hình website</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="grid gap-6 xl:grid-cols-2">
        <section className={card}>
          <h2 className="font-bold">Thông tin chung</h2>
          <Field label="Tên website"><input className={input} value={s.site.name ?? ''} onChange={(e) => set('site', { name: e.target.value })} /></Field>
          <Field label="Tên viết tắt"><input className={input} value={s.site.shortName ?? ''} onChange={(e) => set('site', { shortName: e.target.value })} /></Field>
          <Field label="Khẩu hiệu"><input className={input} value={s.site.tagline ?? ''} onChange={(e) => set('site', { tagline: e.target.value })} /></Field>
          <Field label="Logo"><input type="file" accept="image/*" onChange={async (e) => { const f = e.target.files?.[0]; if (f) set('site', { logoMediaId: (await uploadFile(f)).id }) }} /></Field>
          <button className={btnPrimary} onClick={() => save('site')}>Lưu thông tin chung</button>
        </section>
        <section className={card}>
          <h2 className="font-bold">Liên hệ</h2>
          {(['organization', 'address', 'phone', 'email', 'workingHours'] as const).map((k) => (
            <Field key={k} label={{ organization: 'Tên cơ quan', address: 'Địa chỉ', phone: 'Điện thoại', email: 'Email', workingHours: 'Giờ làm việc' }[k]}>
              <input className={input} value={s.contact[k] ?? ''} onChange={(e) => set('contact', { [k]: e.target.value })} />
            </Field>
          ))}
          <button className={btnPrimary} onClick={() => save('contact')}>Lưu liên hệ</button>
        </section>
        <section className={card}>
          <h2 className="font-bold">Chân trang</h2>
          <Field label="Dòng bản quyền"><input className={input} value={s.footer.copyright ?? ''} onChange={(e) => set('footer', { copyright: e.target.value })} /></Field>
          <Field label="Ghi chú"><input className={input} value={s.footer.note ?? ''} onChange={(e) => set('footer', { note: e.target.value })} /></Field>
          {(s.footer.columns as { title: string; links: Link[] }[]).map((col, i) => (
            <fieldset key={i} className="rounded border border-slate-200 p-3">
              <legend className="px-1 text-sm font-medium">Cột {i + 1}</legend>
              <input aria-label={`Tiêu đề cột ${i + 1}`} className={`${input} mb-2`} value={col.title}
                onChange={(e) => set('footer', { columns: s.footer.columns.map((c: Any, j: number) => (j === i ? { ...c, title: e.target.value } : c)) })} />
              <Links value={col.links} onChange={(links) => set('footer', { columns: s.footer.columns.map((c: Any, j: number) => (j === i ? { ...c, links } : c)) })} />
            </fieldset>
          ))}
          <button type="button" className={btnGhost} onClick={() => set('footer', { columns: [...s.footer.columns, { title: 'Cột mới', links: [] }] })}>+ Cột</button>
          <button className={btnPrimary} onClick={() => save('footer')}>Lưu chân trang</button>
        </section>
        <section className={card}>
          <h2 className="font-bold">Thanh tiện ích (đầu trang)</h2>
          <Links value={s.utility.links} onChange={(links) => set('utility', { links })} />
          <button className={btnPrimary} onClick={() => save('utility')}>Lưu thanh tiện ích</button>
          <h2 className="pt-4 font-bold">SEO</h2>
          <Field label="Mô tả website"><textarea className={input} value={s.seo.description ?? ''} onChange={(e) => set('seo', { description: e.target.value })} /></Field>
          <button className={btnPrimary} onClick={() => save('seo')}>Lưu SEO</button>
        </section>
      </div>
    </>
  )
}
