'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from '@/components/admin/AdminContext'
import { Pager, Table, Title, btnGhost, btnPrimary, input } from '@/components/admin/ui'

type Row = { id: string; title: string; slug: string; status: string; updatedUtc: string; publishedUtc?: string | null; isFeatured: boolean }
const statuses: [string, string][] = [['', 'Tất cả'], ['Draft', 'Nháp'], ['PendingReview', 'Chờ duyệt'], ['Approved', 'Đã duyệt'], ['Scheduled', 'Lên lịch'], ['Published', 'Đã xuất bản'], ['Archived', 'Lưu trữ']]

export default function PostsPage() {
  const { can } = useAdmin()
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [trash, setTrash] = useState(false)
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)

  useEffect(() => {
    const s = new URLSearchParams({ page: String(page), pageSize: '20', q, status, deleted: String(trash) })
    api<Paged<Row>>(`/api/admin/posts?${s}`).then(setData)
  }, [q, status, trash, page])

  async function act(id: string, action: 'delete' | 'restore') {
    if (action === 'delete' && !window.confirm('Chuyển bài viết vào thùng rác?')) return
    await api(`/api/admin/posts/${id}${action === 'delete' ? '' : '/restore'}`, { method: action === 'delete' ? 'DELETE' : 'POST' })
    setData((d) => d && { ...d, items: d.items.filter((r) => r.id !== id), total: d.total - 1 })
  }

  return (
    <>
      <Title actions={can('cms.post.create') && <Link href="/admin/bai-viet/moi" className={btnPrimary}>Viết bài mới</Link>}>Bài viết</Title>
      <div className="mb-4 flex flex-wrap gap-2">
        <input aria-label="Tìm bài viết" placeholder="Tìm theo tiêu đề…" className={`${input} max-w-xs`} value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
        <select aria-label="Trạng thái" className={`${input} max-w-[200px]`} value={status} onChange={(e) => { setPage(1); setStatus(e.target.value) }}>
          {statuses.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
        <label className="flex items-center gap-2"><input type="checkbox" checked={trash} onChange={(e) => { setPage(1); setTrash(e.target.checked) }} /> Thùng rác</label>
      </div>
      <Table head={['Tiêu đề', 'Trạng thái', 'Cập nhật', '']}>
        {data?.items.map((r) => (
          <tr key={r.id} className="border-t border-slate-100">
            <td className="p-3"><Link href={`/admin/bai-viet/${r.id}`} className="font-medium text-brand-blue hover:underline">{r.title}</Link>{r.isFeatured && <span className="ml-2 text-xs text-brand-red">Nổi bật</span>}</td>
            <td className="p-3">{statuses.find(([v]) => v === r.status)?.[1]}</td>
            <td className="p-3">{formatDateTime(r.updatedUtc)}</td>
            <td className="p-3 text-right">
              {can('cms.post.delete') && (trash
                ? <button className={btnGhost} onClick={() => act(r.id, 'restore')}>Khôi phục</button>
                : <button className={btnGhost} onClick={() => act(r.id, 'delete')}>Xóa</button>)}
            </td>
          </tr>
        ))}
      </Table>
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}
    </>
  )
}
