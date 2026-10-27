'use client'

import { useCallback, useEffect, useState } from 'react'
import { ArrowDown, ArrowUp } from 'lucide-react'
import { api } from '@/lib/api'
import { Field, Notice, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Menu = { id: string; code: string; name: string }
type Item = {
  id: string; parentId?: string | null; label: string; slug: string; slugIsManual: boolean; path: string; linkType: string
  targetId?: string | null; url?: string | null; openInNewTab: boolean; sortOrder: number; isVisible: boolean; rowVersion: string; children: Item[]
}
type Option = { id: string; name: string }
type Draft = { id?: string; label: string; slug: string; linkType: string; targetId: string; url: string; parentId: string; openInNewTab: boolean; rowVersion?: string }

const empty: Draft = { label: '', slug: '', linkType: 'None', targetId: '', url: '', parentId: '', openInNewTab: false }
const linkTypes: [string, string][] = [['None', 'Nhóm (trang liệt kê mục con)'], ['Category', 'Chuyên mục'], ['Page', 'Trang tĩnh'], ['Url', 'Liên kết URL']]

export default function MenuPage() {
  const [menus, setMenus] = useState<Menu[]>([])
  const [menuId, setMenuId] = useState('')
  const [tree, setTree] = useState<Item[]>([])
  const [cats, setCats] = useState<Option[]>([])
  const [pages, setPages] = useState<Option[]>([])
  const [draft, setDraft] = useState<Draft | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  const reload = useCallback((id: string) => api<Item[]>(`/api/admin/menus/${id}/items`).then(setTree), [])

  useEffect(() => {
    api<Menu[]>('/api/admin/menus').then((m) => {
      setMenus(m)
      const main = m.find((x) => x.code === 'main') ?? m[0]
      if (main) {
        setMenuId(main.id)
        reload(main.id)
      }
    })
    api<Option[]>('/api/admin/categories').then(setCats).catch(() => undefined)
    api<{ items: { id: string; title: string }[] }>('/api/admin/pages?pageSize=200').then((p) => setPages(p.items.map((x) => ({ id: x.id, name: x.title })))).catch(() => undefined)
  }, [reload])

  async function save() {
    if (!draft) return
    const body = {
      label: draft.label, slug: draft.slug || null, linkType: draft.linkType, targetId: draft.targetId || null, url: draft.url || null,
      parentId: draft.parentId || null, openInNewTab: draft.openInNewTab, rowVersion: draft.rowVersion,
    }
    try {
      const saved = await api<Item>(draft.id ? `/api/admin/menu-items/${draft.id}` : `/api/admin/menus/${menuId}/items`, { method: draft.id ? 'PUT' : 'POST', body })
      setMsg({ kind: 'ok', text: `Đã lưu “${saved.label}” – đường dẫn ${saved.path}` })
      setDraft(null)
      await reload(menuId)
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function act(item: Item, action: 'hide' | 'show' | 'delete') {
    if (action === 'delete' && !window.confirm(`Xóa mục “${item.label}”? Có thể khôi phục trong thùng rác.`)) return
    await api(`/api/admin/menu-items/${item.id}${action === 'delete' ? '' : '/' + action}`, { method: action === 'delete' ? 'DELETE' : 'POST' })
    await reload(menuId)
  }

  async function move(siblings: Item[], index: number, delta: number) {
    const order = [...siblings]
    const [it] = order.splice(index, 1)
    order.splice(index + delta, 0, it)
    await api(`/api/admin/menus/${menuId}/reorder`, { method: 'POST', body: { items: order.map((x, i) => ({ id: x.id, parentId: x.parentId ?? null, sortOrder: i })) } })
    await reload(menuId)
  }

  const edit = (i: Item) => setDraft({ id: i.id, label: i.label, slug: i.slugIsManual ? i.slug : '', linkType: i.linkType, targetId: i.targetId ?? '', url: i.url ?? '', parentId: i.parentId ?? '', openInNewTab: i.openInNewTab, rowVersion: i.rowVersion })
  const flat = (items: Item[]): Item[] => items.flatMap((i) => [i, ...flat(i.children)])

  const renderList = (items: Item[], depth = 0) => (
    <ul className={depth ? 'ml-6 border-l border-slate-200 pl-3' : ''}>
      {items.map((i, idx) => (
        <li key={i.id} className="my-1">
          <div className={`flex flex-wrap items-center gap-2 rounded bg-white p-2 shadow-sm ${i.isVisible ? '' : 'opacity-60'}`}>
            <span className="font-semibold">{i.label}</span>
            <code className="text-xs text-slate-500">{i.linkType === 'Url' ? i.url : i.path}</code>
            {!i.isVisible && <span className="text-xs text-brand-red">Đang ẩn</span>}
            <span className="ml-auto flex gap-1">
              <button aria-label={`Lên: ${i.label}`} className={btnGhost} disabled={idx === 0} onClick={() => move(items, idx, -1)}><ArrowUp className="h-4 w-4" /></button>
              <button aria-label={`Xuống: ${i.label}`} className={btnGhost} disabled={idx === items.length - 1} onClick={() => move(items, idx, 1)}><ArrowDown className="h-4 w-4" /></button>
              <button aria-label={`Sửa ${i.label}`} className={btnGhost} onClick={() => edit(i)}>Sửa</button>
              <button className={btnGhost} onClick={() => act(i, i.isVisible ? 'hide' : 'show')}>{i.isVisible ? 'Ẩn' : 'Hiện'}</button>
              <button className={btnGhost} onClick={() => act(i, 'delete')}>Xóa</button>
            </span>
          </div>
          {i.children.length > 0 && renderList(i.children, depth + 1)}
        </li>
      ))}
    </ul>
  )

  return (
    <>
      <Title actions={<button className={btnPrimary} onClick={() => setDraft({ ...empty })}>Thêm mục</button>}>Menu</Title>
      <div className="mb-4 flex gap-2" role="tablist">
        {menus.map((m) => (
          <button key={m.id} role="tab" aria-selected={m.id === menuId} className={m.id === menuId ? btnPrimary : btnGhost} onClick={() => { setMenuId(m.id); reload(m.id) }}>{m.name}</button>
        ))}
      </div>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
        <div>{tree.length ? renderList(tree) : <p className="rounded bg-white p-4">Menu chưa có mục nào.</p>}</div>
        {draft && (
          <form className="space-y-3 rounded bg-white p-4 shadow-sm" onSubmit={(e) => { e.preventDefault(); void save() }}>
            <h2 className="font-bold text-brand-navy">{draft.id ? 'Sửa mục menu' : 'Thêm mục menu'}</h2>
            <Field label="Tên mục"><input className={input} required value={draft.label} onChange={(e) => setDraft({ ...draft, label: e.target.value })} /></Field>
            <Field label="Đường dẫn (tùy chọn)" hint="Để trống: tự tạo từ tên và tự đổi khi đổi tên. Địa chỉ cũ được chuyển hướng.">
              <input className={input} value={draft.slug} onChange={(e) => setDraft({ ...draft, slug: e.target.value })} />
            </Field>
            <Field label="Loại liên kết">
              <select className={input} value={draft.linkType} onChange={(e) => setDraft({ ...draft, linkType: e.target.value, targetId: '' })}>
                {linkTypes.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </Field>
            {(draft.linkType === 'Category' || draft.linkType === 'Page') && (
              <Field label={draft.linkType === 'Category' ? 'Chuyên mục' : 'Trang'}>
                <select className={input} required value={draft.targetId} onChange={(e) => setDraft({ ...draft, targetId: e.target.value })}>
                  <option value="">— Chọn —</option>
                  {(draft.linkType === 'Category' ? cats : pages).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                </select>
              </Field>
            )}
            {draft.linkType === 'Url' && (
              <Field label="URL" hint="https://… hoặc đường dẫn nội bộ bắt đầu bằng /"><input className={input} required value={draft.url} onChange={(e) => setDraft({ ...draft, url: e.target.value })} /></Field>
            )}
            <Field label="Mục cha">
              <select className={input} value={draft.parentId} onChange={(e) => setDraft({ ...draft, parentId: e.target.value })}>
                <option value="">— Cấp đầu —</option>
                {flat(tree).filter((i) => i.id !== draft.id).map((i) => <option key={i.id} value={i.id}>{i.label}</option>)}
              </select>
            </Field>
            <label className="flex items-center gap-2"><input type="checkbox" checked={draft.openInNewTab} onChange={(e) => setDraft({ ...draft, openInNewTab: e.target.checked })} /> Mở tab mới</label>
            <div className="flex gap-2">
              <button type="submit" className={btnPrimary}>Lưu mục</button>
              <button type="button" className={btnGhost} onClick={() => setDraft(null)}>Hủy</button>
            </div>
          </form>
        )}
      </div>
    </>
  )
}
