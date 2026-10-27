'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Pager, Title, btnGhost, input } from '@/components/admin/ui'

type Row = { id: string; name: string; email: string; phone?: string; subject?: string; message: string; createdUtc: string; status: string; handlerNote?: string }
const label: Record<string, string> = { New: 'Mới', Handled: 'Đã xử lý', Spam: 'Rác' }

export default function ContactsPage() {
  const [status, setStatus] = useState('New')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)
  const reload = useCallback(() => {
    api<Paged<Row>>(`/api/admin/contacts?page=${page}&pageSize=20&status=${status}`).then(setData)
  }, [page, status])
  useEffect(() => reload(), [reload])

  async function handle(r: Row, to: string) {
    const note = to === 'Handled' ? window.prompt('Ghi chú xử lý (không bắt buộc):') ?? undefined : undefined
    await api(`/api/admin/contacts/${r.id}/handle`, { method: 'POST', body: { status: to, note } })
    reload()
  }

  return (
    <>
      <Title>Liên hệ</Title>
      <select aria-label="Trạng thái" className={`${input} mb-4 max-w-[200px]`} value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
        {Object.entries(label).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select>
      <ul className="space-y-3">
        {data?.items.map((r) => (
          <li key={r.id} className="rounded bg-white p-4 shadow-sm">
            <p className="font-semibold">{r.subject || '(Không tiêu đề)'} <span className="text-sm font-normal text-slate-500">– {formatDateTime(r.createdUtc)}</span></p>
            <p className="text-sm">{r.name} · <a className="text-brand-blue underline" href={`mailto:${r.email}`}>{r.email}</a> {r.phone && `· ${r.phone}`}</p>
            <p className="mt-2 whitespace-pre-line">{r.message}</p>
            {r.handlerNote && <p className="mt-2 text-sm text-slate-600">Ghi chú: {r.handlerNote}</p>}
            <div className="mt-2 flex gap-2">
              {r.status !== 'Handled' && <button className={btnGhost} onClick={() => handle(r, 'Handled')}>Đánh dấu đã xử lý</button>}
              {r.status !== 'Spam' && <button className={btnGhost} onClick={() => handle(r, 'Spam')}>Đánh dấu rác</button>}
            </div>
          </li>
        ))}
      </ul>
      {data && data.items.length === 0 && <p className="rounded bg-white p-4">Không có liên hệ.</p>}
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
    </>
  )
}
