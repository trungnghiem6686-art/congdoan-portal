'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { useAdmin } from './AdminContext'
import { Field, Notice, Title, btnGhost, btnPrimary, errorText, input } from './ui'

type Choice = { content: string; isCorrect: boolean }
type Q = { type: string; topic: string; difficulty: string; content: string; explanation: string; points: number; choices: Choice[]; code?: string; rowVersion?: string; status?: string; versionNo?: number }

const blank: Q = { type: 'Single', topic: '', difficulty: 'Medium', content: '', explanation: '', points: 1, choices: [{ content: '', isCorrect: true }, { content: '', isCorrect: false }, { content: '', isCorrect: false }, { content: '', isCorrect: false }] }
const statusLabel: Record<string, string> = { Draft: 'Chưa duyệt', Approved: 'Đã duyệt', Archived: 'Lưu trữ' }

export function QuestionEditor({ id }: { id?: string }) {
  const router = useRouter()
  const { can, setDirty } = useAdmin()
  const [q, setQ] = useState<Q | null>(id ? null : blank)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  useEffect(() => {
    if (id) api<Q>(`/api/admin/questions/${id}`).then((x) => setQ({ ...x, explanation: x.explanation ?? '' }))
  }, [id])
  useEffect(() => () => setDirty(false), [setDirty])

  if (!q) return <p role="status">Đang tải…</p>
  const set = (patch: Partial<Q>) => { setDirty(true); setQ({ ...q, ...patch }) }
  const setChoice = (i: number, patch: Partial<Choice>) =>
    set({ choices: q.choices.map((c, j) => (j === i ? { ...c, ...patch } : q.type !== 'Multiple' && patch.isCorrect ? { ...c, isCorrect: false } : c)) })

  async function save() {
    try {
      const saved = await api<Q & { id: string }>(id ? `/api/admin/questions/${id}` : '/api/admin/questions', { method: id ? 'PUT' : 'POST', body: q })
      setDirty(false)
      setMsg({ kind: 'ok', text: `Đã lưu phiên bản ${saved.versionNo}. Câu hỏi cần được duyệt lại trước khi dùng trong đề.` })
      if (!id) router.replace(`/admin/cau-hoi/${saved.id}`)
      else setQ({ ...saved, explanation: saved.explanation ?? '' })
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function approve() {
    try {
      const saved = await api<Q>(`/api/admin/questions/${id}/approve`, { method: 'POST' })
      setQ({ ...saved, explanation: saved.explanation ?? '' })
      setMsg({ kind: 'ok', text: 'Đã duyệt câu hỏi.' })
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <>
      <Title actions={<>
        <button className={btnPrimary} onClick={save}>Lưu câu hỏi</button>
        {id && q.status !== 'Approved' && can('exam.question.approve') && <button className={btnGhost} onClick={approve}>Duyệt</button>}
      </>}>
        {id ? `Câu hỏi (phiên bản ${q.versionNo})` : 'Câu hỏi mới'}
      </Title>
      {q.status && <p className="mb-3 text-sm">Trạng thái: {statusLabel[q.status]}</p>}
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div className="grid max-w-4xl gap-4 rounded bg-white p-4 shadow-sm sm:grid-cols-2">
        <Field label="Loại câu hỏi">
          <select className={input} value={q.type} onChange={(e) => set({ type: e.target.value, choices: e.target.value === 'TrueFalse' ? [{ content: 'Đúng', isCorrect: true }, { content: 'Sai', isCorrect: false }] : q.choices })}>
            <option value="Single">Một đáp án</option><option value="Multiple">Nhiều đáp án</option><option value="TrueFalse">Đúng/Sai</option>
          </select>
        </Field>
        <Field label="Mức độ">
          <select className={input} value={q.difficulty} onChange={(e) => set({ difficulty: e.target.value })}>
            <option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option>
          </select>
        </Field>
        <Field label="Chủ đề"><input className={input} value={q.topic} onChange={(e) => set({ topic: e.target.value })} /></Field>
        <Field label="Điểm"><input type="number" step="0.5" min="0.5" className={input} value={q.points} onChange={(e) => set({ points: Number(e.target.value) })} /></Field>
        <div className="sm:col-span-2"><Field label="Nội dung câu hỏi"><textarea className={input} rows={4} value={q.content} onChange={(e) => set({ content: e.target.value })} /></Field></div>
        <fieldset className="space-y-2 sm:col-span-2">
          <legend className="mb-1 text-sm font-medium">Phương án ({q.type === 'Multiple' ? 'đánh dấu mọi đáp án đúng; chỉ chọn đủ và đúng mới được điểm' : 'chọn một đáp án đúng'})</legend>
          {q.choices.map((c, i) => (
            <div key={i} className="flex items-center gap-2">
              <input type={q.type === 'Multiple' ? 'checkbox' : 'radio'} name="correct" aria-label={`Đáp án đúng ${String.fromCharCode(65 + i)}`} checked={c.isCorrect} onChange={(e) => setChoice(i, { isCorrect: e.target.checked })} />
              <span className="w-5 font-semibold">{String.fromCharCode(65 + i)}</span>
              <input aria-label={`Phương án ${String.fromCharCode(65 + i)}`} className={input} value={c.content} disabled={q.type === 'TrueFalse'} onChange={(e) => setChoice(i, { content: e.target.value })} />
              {q.type !== 'TrueFalse' && q.choices.length > 2 && <button className={btnGhost} aria-label={`Xóa phương án ${String.fromCharCode(65 + i)}`} onClick={() => set({ choices: q.choices.filter((_, j) => j !== i) })}>×</button>}
            </div>
          ))}
          {q.type !== 'TrueFalse' && q.choices.length < 8 && <button className={btnGhost} onClick={() => set({ choices: [...q.choices, { content: '', isCorrect: false }] })}>+ Phương án</button>}
        </fieldset>
        <div className="sm:col-span-2"><Field label="Lời giải (chỉ hiện khi kỳ thi cho phép)"><textarea className={input} rows={3} value={q.explanation} onChange={(e) => set({ explanation: e.target.value })} /></Field></div>
      </div>
    </>
  )
}
