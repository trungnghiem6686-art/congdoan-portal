'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime, formatSize } from '@/lib/format'
import { uploadFile, type Media } from '@/lib/upload'
import { Notice, Pager, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Row = Media & { createdUtc: string; isDeleted: boolean }
type Usage = { ownerType: string; ownerId: string; ownerTitle: string }

export default function MediaPage() {
  const [kind, setKind] = useState('')
  const [q, setQ] = useState('')
  const [trash, setTrash] = useState(false)
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [usages, setUsages] = useState<Record<string, Usage[]>>({})

  const reload = useCallback(() => {
    api<Paged<Row>>(`/api/admin/media?${new URLSearchParams({ page: String(page), pageSize: '24', q, kind, deleted: String(trash) })}`).then(setData)
  }, [page, q, kind, trash])
  useEffect(() => reload(), [reload])

  async function upload(files: File[]) {
    for (const f of files) {
      try {
        setMsg({ kind: 'ok', text: `Đang tải ${f.name}…` })
        await uploadFile(f, (p) => setMsg({ kind: 'ok', text: `Đang tải ${f.name} (${p}%)` }))
        setMsg({ kind: 'ok', text: `Đã tải ${f.name}` })
      } catch (e) {
        setMsg({ kind: 'error', text: `${f.name}: ${errorText(e)}` })
      }
    }
    reload()
  }

  async function remove(m: Row, restore = false) {
    try {
      await api(`/api/admin/media/${m.id}${restore ? '/restore' : ''}`, { method: restore ? 'POST' : 'DELETE' })
      reload()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <>
      <Title actions={<label className={`${btnPrimary} cursor-pointer`}>Tải tệp lên<input type="file" multiple className="sr-only" onChange={(e) => void upload(Array.from(e.target.files ?? []))} /></label>}>Tệp đã tải lên</Title>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="mb-4 flex flex-wrap gap-2">
        <input aria-label="Tìm tệp" placeholder="Tìm theo tên…" className={`${input} max-w-xs`} value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
        <select aria-label="Loại tệp" className={`${input} max-w-[180px]`} value={kind} onChange={(e) => { setPage(1); setKind(e.target.value) }}>
          <option value="">Tất cả</option><option value="image">Ảnh</option><option value="video">Video</option><option value="document">Tài liệu</option>
        </select>
        <label className="flex items-center gap-2"><input type="checkbox" checked={trash} onChange={(e) => setTrash(e.target.checked)} /> Thùng rác</label>
      </div>
      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
        {data?.items.map((m) => (
          <li key={m.id} className="rounded bg-white p-2 text-xs shadow-sm">
            {m.thumbnailUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={m.thumbnailUrl} alt="" className="aspect-square w-full rounded object-cover" />
            ) : <div className="flex aspect-square items-center justify-center rounded bg-slate-100 uppercase">{m.kind}</div>}
            <p className="mt-1 truncate font-medium" title={m.originalName}>{m.originalName}</p>
            <p className="text-slate-500">{formatSize(m.size)} · {formatDateTime(m.createdUtc)}</p>
            <div className="mt-1 flex flex-wrap gap-1">
              <button className={btnGhost} onClick={() => navigator.clipboard?.writeText(location.origin + m.url)}>Chép link</button>
              <button className={btnGhost} onClick={async () => setUsages({ ...usages, [m.id]: await api<Usage[]>(`/api/admin/media/${m.id}/usages`) })}>Nơi dùng</button>
              {trash ? <button className={btnGhost} onClick={() => remove(m, true)}>Khôi phục</button> : <button className={btnGhost} onClick={() => remove(m)}>Xóa</button>}
            </div>
            {usages[m.id] && (
              <ul className="mt-1 text-slate-600">{usages[m.id].length === 0 ? <li>Chưa dùng ở đâu.</li> : usages[m.id].map((u) => <li key={u.ownerId}>{u.ownerType}: {u.ownerTitle}</li>)}</ul>
            )}
          </li>
        ))}
      </ul>
      {data && <Pager page={page} pageSize={24} total={data.total} onPage={setPage} />}
    </>
  )
}
