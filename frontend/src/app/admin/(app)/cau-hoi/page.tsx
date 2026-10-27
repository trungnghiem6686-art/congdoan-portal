'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from '@/components/admin/AdminContext'
import { Pager, Table, Title, btnGhost, btnPrimary, input } from '@/components/admin/ui'

type Row = { id: string; type: string; topic: string; difficulty: string; status: string; versionNo: number; content: string; updatedUtc: string }
const lbl: Record<string, string> = { Single: 'Một đáp án', Multiple: 'Nhiều đáp án', TrueFalse: 'Đúng/Sai', Easy: 'Dễ', Medium: 'Trung bình', Hard: 'Khó', Draft: 'Chưa duyệt', Approved: 'Đã duyệt', Archived: 'Lưu trữ' }

export default function QuestionsPage() {
  const { can } = useAdmin()
  const [f, setF] = useState({ q: '', topic: '', difficulty: '', status: '', type: '' })
  const [page, setPage] = useState(1)
  const [topics, setTopics] = useState<string[]>([])
  const [data, setData] = useState<Paged<Row> | null>(null)
  const reload = useCallback(() => {
    api<Paged<Row>>(`/api/admin/questions?${new URLSearchParams({ ...f, page: String(page), pageSize: '20' })}`).then(setData)
  }, [f, page])
  useEffect(() => reload(), [reload])
  useEffect(() => { api<string[]>('/api/admin/questions/topics').then(setTopics) }, [])
  const upd = (k: keyof typeof f, v: string) => { setPage(1); setF({ ...f, [k]: v }) }

  return (
    <>
      <Title actions={<>
        <Link className={btnGhost} href="/admin/cau-hoi/nhap">Nhập từ tệp</Link>
        <Link className={btnPrimary} href="/admin/cau-hoi/moi">Thêm câu hỏi</Link>
      </>}>Ngân hàng câu hỏi</Title>
      <div className="mb-4 flex flex-wrap gap-2">
        <input aria-label="Tìm nội dung" placeholder="Tìm nội dung…" className={`${input} max-w-xs`} value={f.q} onChange={(e) => upd('q', e.target.value)} />
        <select aria-label="Chủ đề" className={`${input} max-w-[200px]`} value={f.topic} onChange={(e) => upd('topic', e.target.value)}><option value="">Mọi chủ đề</option>{topics.map((t) => <option key={t}>{t}</option>)}</select>
        <select aria-label="Mức độ" className={`${input} max-w-[160px]`} value={f.difficulty} onChange={(e) => upd('difficulty', e.target.value)}><option value="">Mọi mức độ</option><option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option></select>
        <select aria-label="Trạng thái" className={`${input} max-w-[160px]`} value={f.status} onChange={(e) => upd('status', e.target.value)}><option value="">Mọi trạng thái</option><option value="Draft">Chưa duyệt</option><option value="Approved">Đã duyệt</option><option value="Archived">Lưu trữ</option></select>
      </div>
      <Table head={['Nội dung', 'Chủ đề', 'Loại', 'Mức độ', 'Trạng thái', 'Cập nhật', '']}>
        {data?.items.map((r) => (
          <tr key={r.id} className="border-t border-slate-100">
            <td className="max-w-md p-3"><Link className="line-clamp-2 text-brand-blue hover:underline" href={`/admin/cau-hoi/${r.id}`}>{r.content.replace(/<[^>]+>/g, '')}</Link></td>
            <td className="p-3">{r.topic}</td><td className="p-3">{lbl[r.type]}</td><td className="p-3">{lbl[r.difficulty]}</td>
            <td className="p-3">{lbl[r.status]} (v{r.versionNo})</td><td className="p-3">{formatDateTime(r.updatedUtc)}</td>
            <td className="p-3">{r.status === 'Draft' && can('exam.question.approve') && <button className={btnGhost} onClick={async () => { await api(`/api/admin/questions/${r.id}/approve`, { method: 'POST' }); reload() }}>Duyệt</button>}</td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
    </>
  )
}
