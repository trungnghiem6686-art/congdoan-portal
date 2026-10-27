'use client'

import { useCallback, useEffect, useState } from 'react'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Dialog, Field, Notice, Pager, Table, Title, btnGhost, btnPrimary, errorText, input } from '@/components/admin/ui'

type Row = { id: string; oldPath: string; newPath: string; statusCode: number; hits: number; createdUtc: string }

export default function RedirectsPage() {
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<Row> | null>(null)
  const [adding, setAdding] = useState<{ oldPath: string; newPath: string } | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  const reload = useCallback(() => {
    const s = new URLSearchParams({ page: String(page), pageSize: '20', q })
    api<Paged<Row>>(`/api/admin/redirects?${s}`).then(setData)
  }, [page, q])
  useEffect(() => reload(), [reload])

  async function save() {
    if (!adding) return
    try {
      await api('/api/admin/redirects', { method: 'POST', body: adding })
      setAdding(null)
      setMsg({ kind: 'ok', text: 'Đã thêm chuyển hướng' })
      reload()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function remove(r: Row) {
    if (!window.confirm(`Xóa chuyển hướng ${r.oldPath}? Đường dẫn cũ sẽ trả về trang không tìm thấy.`)) return
    try {
      await api(`/api/admin/redirects/${r.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <>
      <Title actions={<button className={btnPrimary} onClick={() => setAdding({ oldPath: '', newPath: '' })}>Thêm chuyển hướng</button>}>Chuyển hướng</Title>
      <p className="mb-4 text-slate-600">
        Đường dẫn cũ chuyển vĩnh viễn (301) sang đường dẫn mới. Hệ thống tự thêm khi đổi đường dẫn bài viết, chuyên mục, menu và khi nhập dữ liệu cũ.
      </p>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <input aria-label="Tìm kiếm" placeholder="Tìm đường dẫn…" className={`${input} mb-4 max-w-xs`} value={q} onChange={(e) => { setPage(1); setQ(e.target.value) }} />
      <Table head={['Đường dẫn cũ', 'Chuyển tới', 'Lượt dùng', 'Tạo lúc', '']}>
        {data?.items.map((r) => (
          <tr key={r.id} className="border-t border-slate-100">
            <td className="break-all p-3 font-mono text-sm">{r.oldPath}</td>
            <td className="break-all p-3 font-mono text-sm">{r.newPath}</td>
            <td className="p-3">{r.hits}</td>
            <td className="p-3">{formatDateTime(r.createdUtc)}</td>
            <td className="p-3 text-right"><button className={btnGhost} onClick={() => remove(r)}>Xóa</button></td>
          </tr>
        ))}
      </Table>
      {data && data.items.length === 0 && <p className="mt-2 rounded bg-white p-4 text-slate-600">Chưa có chuyển hướng.</p>}
      {data && <Pager page={page} pageSize={20} total={data.total} onPage={setPage} />}

      {adding && (
        <Dialog title="Thêm chuyển hướng" onClose={() => setAdding(null)}>
          <div className="space-y-3">
            <Field label="Đường dẫn cũ" hint="Bắt đầu bằng /, ví dụ /tin-cu/123.html">
              <input className={input} required value={adding.oldPath} onChange={(e) => setAdding({ ...adding, oldPath: e.target.value })} />
            </Field>
            <Field label="Đường dẫn mới" hint="Ví dụ /tin-tuc/ten-bai hoặc https://…">
              <input className={input} required value={adding.newPath} onChange={(e) => setAdding({ ...adding, newPath: e.target.value })} />
            </Field>
            <div className="flex justify-end gap-2">
              <button className={btnGhost} onClick={() => setAdding(null)}>Hủy</button>
              <button className={btnPrimary} onClick={save} disabled={!adding.oldPath || !adding.newPath}>Lưu</button>
            </div>
          </div>
        </Dialog>
      )}
    </>
  )
}
