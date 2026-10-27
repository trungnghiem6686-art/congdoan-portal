'use client'

import { useState } from 'react'
import { api } from '@/lib/api'
import { uploadFile, toEmbedUrl } from '@/lib/upload'
import { ResourcePage, type Item } from '@/components/admin/ResourcePage'
import { Dialog, btnGhost, btnPrimary, errorText } from '@/components/admin/ui'

function ItemsButton({ album }: { album: Item }) {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<Item[]>([])
  const [error, setError] = useState<string | null>(null)
  const load = () => api<Item>(`/api/admin/albums/${album.id}`).then((a) => setItems(a.items))
  const add = async (body: Record<string, unknown>) => {
    setError(null)
    try { await api(`/api/admin/albums/${album.id}/items`, { method: 'POST', body }); await load() } catch (e) { setError(errorText(e)) }
  }
  return (
    <>
      <button className={btnGhost} onClick={() => { setOpen(true); void load() }}>Ảnh/video</button>
      {open && (
        <Dialog title={`Nội dung album: ${album.title}`} onClose={() => setOpen(false)}>
          {error && <p role="alert" className="mb-2 text-brand-red">{error}</p>}
          <ul className="mb-3 grid max-h-80 grid-cols-3 gap-2 overflow-y-auto">
            {items.map((i) => (
              <li key={i.id} className="relative rounded bg-slate-100 p-1 text-xs">
                {i.thumbnailUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={i.thumbnailUrl} alt="" className="aspect-square w-full object-cover" />
                ) : <span className="block truncate">{i.embedUrl ?? i.url}</span>}
                <button className="absolute right-1 top-1 rounded bg-white px-1 text-brand-red" aria-label="Gỡ"
                  onClick={async () => { await api(`/api/admin/albums/${album.id}/items/${i.id}`, { method: 'DELETE' }); await load() }}>×</button>
              </li>
            ))}
          </ul>
          <div className="flex flex-wrap gap-2">
            <label className={`${btnPrimary} cursor-pointer`}>
              Tải ảnh/video từ máy
              <input type="file" multiple accept="image/*,video/mp4,video/webm" className="sr-only" onChange={async (e) => {
                for (const f of Array.from(e.target.files ?? [])) {
                  try { const m = await uploadFile(f); await add({ mediaId: m.id }) } catch (x) { setError(errorText(x)) }
                }
              }} />
            </label>
            <button className={btnGhost} onClick={() => {
              const url = window.prompt('Liên kết YouTube/Vimeo:')
              const embed = url ? toEmbedUrl(url) : null
              if (url && !embed) setError('Nguồn video không được hỗ trợ.')
              else if (embed) void add({ embedUrl: embed })
            }}>Nhúng YouTube</button>
          </div>
        </Dialog>
      )}
    </>
  )
}

export default function AlbumsAdminPage() {
  return (
    <ResourcePage
      title="Thư viện ảnh/video"
      endpoint="/api/admin/albums"
      defaults={{ kind: 'Photo', isVisible: true, sortOrder: 0 }}
      fields={[
        { key: 'title', label: 'Tên album', type: 'text', required: true },
        { key: 'kind', label: 'Loại', type: 'select', required: true, options: [['Photo', 'Ảnh'], ['Video', 'Video']] },
        { key: 'description', label: 'Mô tả', type: 'textarea' },
        { key: 'coverMediaId', label: 'Ảnh bìa', type: 'media', accept: 'image/*' },
        { key: 'eventDate', label: 'Thời điểm sự kiện', type: 'datetime' },
        { key: 'isVisible', label: 'Hiển thị', type: 'checkbox' },
      ]}
      columns={[
        { label: 'Tên', render: (i) => i.title },
        { label: 'Loại', render: (i) => (i.kind === 'Video' ? 'Video' : 'Ảnh') },
      ]}
      extra={(i) => <ItemsButton album={i} />}
    />
  )
}
