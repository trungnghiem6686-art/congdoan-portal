'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Pager, Table, Title, btnPrimary, input } from './ui'

type Row = { id: string; title: string; code?: string; opensAtUtc: string; closesAtUtc: string; status: string; phase: string; attemptCount: number }
const phase: Record<string, string> = { draft: 'Nháp', upcoming: 'Sắp mở', open: 'Đang mở', closed: 'Đã đóng', archived: 'Lưu trữ' }

/** Exam list used by "Kỳ thi" (links to the editor) and "Báo cáo" (links to the report). */
export function ExamList({ title, linkBase, canCreate }: { title: string; linkBase: string; canCreate?: boolean }) {
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)
  useEffect(() => { api<Paged<Row>>(`/api/admin/exams?page=${page}&pageSize=20&q=${encodeURIComponent(q)}`).then(setData) }, [q, page])
  return (
    <>
      <Title actions={canCreate && <Link className={btnPrimary} href="/admin/ky-thi/moi">Tạo kỳ thi</Link>}>{title}</Title>
      <input aria-label="Tìm kỳ thi" placeholder="Tìm kỳ thi…" className={`${input} mb-4 max-w-xs`} value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
      <Table head={['Kỳ thi', 'Thời gian', 'Tình trạng', 'Lượt làm bài']}>
        {data?.items.map((e) => (
          <tr key={e.id} className="border-t">
            <td className="p-3"><Link className="font-medium text-brand-blue hover:underline" href={`${linkBase}/${e.id}`}>{e.title}</Link></td>
            <td className="p-3">{formatDateTime(e.opensAtUtc)} → {formatDateTime(e.closesAtUtc)}</td>
            <td className="p-3">{phase[e.phase]}</td><td className="p-3">{e.attemptCount}</td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
    </>
  )
}
