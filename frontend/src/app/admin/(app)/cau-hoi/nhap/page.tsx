'use client'

import { useState } from 'react'
import { api } from '@/lib/api'
import { Field, Notice, Table, Title, btnGhost, btnPrimary, errorText } from '@/components/admin/ui'

type Preview = { previewId: string; rowCount: number; errors: { row: number; message: string }[]; rows: { row: number; type: string; topic: string; difficulty: string; content: string; choiceCount: number; correct: string }[] }

export default function ImportQuestionsPage() {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  async function runPreview() {
    if (!file) return
    setMsg(null)
    const form = new FormData()
    form.append('file', file)
    try {
      setPreview(await api<Preview>('/api/admin/questions/import/preview', { method: 'POST', body: form }))
    } catch (e) {
      setPreview(null)
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function commit() {
    if (!preview) return
    try {
      const r = await api<{ created: number }>('/api/admin/questions/import/commit', { method: 'POST', body: { previewId: preview.previewId } })
      setMsg({ kind: 'ok', text: `Đã ghi ${r.created} câu hỏi vào ngân hàng (trạng thái chưa duyệt).` })
      setPreview(null)
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <>
      <Title actions={<>
        <a className={btnGhost} href="/api/admin/questions/import/template?format=xlsx" download>Tải mẫu XLSX</a>
        <a className={btnGhost} href="/api/admin/questions/import/template?format=csv" download>Tải mẫu CSV</a>
      </>}>Nhập câu hỏi từ tệp</Title>
      <p className="mb-4 max-w-3xl text-slate-700">
        Bước 1: chọn tệp và xem trước. Bước 2: nếu không có lỗi, bấm “Ghi vào ngân hàng”. Tệp có bất kỳ dòng lỗi nào sẽ không được ghi dòng nào.
      </p>
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="mb-4 flex flex-wrap items-end gap-3 rounded bg-white p-4 shadow-sm">
        <Field label="Tệp câu hỏi (CSV hoặc XLSX)"><input type="file" accept=".csv,.xlsx" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setPreview(null) }} /></Field>
        <button className={btnPrimary} disabled={!file} onClick={runPreview}>Xem trước</button>
      </div>
      {preview && (
        <>
          <p className="mb-2">Tổng {preview.rowCount} dòng, {preview.errors.length} dòng lỗi.</p>
          {preview.errors.length > 0 && (
            <section aria-label="Lỗi theo dòng" className="mb-4 rounded bg-red-50 p-3 text-brand-red">
              <h2 className="font-semibold">Lỗi theo dòng – sửa tệp rồi xem trước lại</h2>
              <ul className="mt-1 list-disc pl-5">{preview.errors.map((e) => <li key={e.row}>Dòng {e.row}: {e.message}</li>)}</ul>
            </section>
          )}
          <Table head={['Dòng', 'Chủ đề', 'Loại', 'Mức độ', 'Nội dung', 'Số PA', 'Đáp án']}>
            {preview.rows.map((r) => (
              <tr key={r.row} className={`border-t ${preview.errors.some((e) => e.row === r.row) ? 'bg-red-50' : ''}`}>
                <td className="p-2">{r.row}</td><td className="p-2">{r.topic}</td><td className="p-2">{r.type}</td><td className="p-2">{r.difficulty}</td>
                <td className="p-2">{r.content}</td><td className="p-2">{r.choiceCount}</td><td className="p-2">{r.correct}</td>
              </tr>
            ))}
          </Table>
          <button className={`${btnPrimary} mt-4`} disabled={preview.errors.length > 0} onClick={commit}>Ghi vào ngân hàng</button>
        </>
      )}
    </>
  )
}
