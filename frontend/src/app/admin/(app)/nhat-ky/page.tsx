'use client'

import { useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Pager, Table, Title, input } from '@/components/admin/ui'

type Row = { id: number; atUtc: string; action: string; entity: string; entityId: string; detail: string; actorName?: string; actorKind?: string; ipAddress?: string; requestId?: string }

export default function AuditPage() {
  const [action, setAction] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)
  useEffect(() => { api<Paged<Row>>(`/api/admin/audit?page=${page}&pageSize=50&action=${encodeURIComponent(action)}`).then(setData) }, [action, page])
  return (
    <>
      <Title>Nhật ký hệ thống</Title>
      <input aria-label="Lọc theo thao tác" placeholder="Lọc thao tác (vd: cms.post, auth.login)…" className={`${input} mb-4 max-w-sm`} value={action} onChange={(e) => { setPage(1); setAction(e.target.value) }} />
      <Table head={['Thời gian', 'Người thực hiện', 'Thao tác', 'Đối tượng', 'Chi tiết', 'IP / Mã yêu cầu']}>
        {data?.items.map((r) => (
          <tr key={r.id} className="border-t align-top">
            <td className="whitespace-nowrap p-2">{formatDateTime(r.atUtc)}</td><td className="p-2">{r.actorName ?? '—'}</td><td className="p-2"><code>{r.action}</code></td>
            <td className="p-2">{r.entity} <span className="text-xs text-slate-500">{r.entityId.slice(0, 8)}</span></td>
            <td className="max-w-md break-all p-2 text-xs">{r.detail}</td><td className="p-2 text-xs">{r.ipAddress}<br />{r.requestId}</td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={50} total={data.total} onPage={setPage} />}
    </>
  )
}
