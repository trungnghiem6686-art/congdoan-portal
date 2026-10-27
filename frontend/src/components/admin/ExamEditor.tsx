'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api, type Paged } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from './AdminContext'
import { Dialog, Field, Notice, Table, Title, btnDanger, btnGhost, btnPrimary, errorText, input } from './ui'

type Rule = { topic?: string | null; difficulty?: string | null; type?: string | null; count: number; available?: number }
type Assignment = { id: string; targetType: string; targetId: string }
type Exam = {
  id: string; title: string; code?: string | null; description?: string | null; instructions?: string | null; opensAtUtc: string; closesAtUtc: string
  durationMinutes: number; maxAttempts: number; scoreRule: string; passScore: number; roundingDecimals: number; resultRelease: string
  showAnswerDetail: boolean; showExplanation: boolean; shuffleQuestions: boolean; shuffleChoices: boolean; logTabSwitches: boolean
  questionSource: string; status: string; phase: string; questionIds: string[]; matrix: Rule[]; assignments: Assignment[]; attemptCount: number; rowVersion: string
}
type QuestionRow = { id: string; content: string; topic: string; difficulty: string }
type Named = { id: string; name: string; code?: string }
type Attempt = { id: string; candidateCode?: string; candidateName?: string; attemptNo: number; status: string; startedUtc: string; finishedUtc?: string; score?: number | null; passed?: boolean | null; tabSwitchCount: number }

const toLocal = (utc: string) => new Date(new Date(utc).getTime() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16)
const statusLabel: Record<string, string> = { Draft: 'Nháp', Published: 'Đã công bố', Closed: 'Đã đóng', Archived: 'Lưu trữ' }
const phaseLabel: Record<string, string> = { draft: 'chưa công bố', upcoming: 'sắp mở', open: 'đang mở', closed: 'đã đóng', archived: 'lưu trữ' }

function defaults(): Partial<Exam> {
  const now = Date.now()
  return {
    title: '', opensAtUtc: new Date(now + 3600_000).toISOString(), closesAtUtc: new Date(now + 7 * 86400_000).toISOString(), durationMinutes: 30, maxAttempts: 1,
    scoreRule: 'Highest', passScore: 5, roundingDecimals: 2, resultRelease: 'Immediately', showAnswerDetail: false, showExplanation: false,
    shuffleQuestions: true, shuffleChoices: true, logTabSwitches: false, questionSource: 'Manual',
  }
}

export function ExamEditor({ id }: { id?: string }) {
  const router = useRouter()
  const { can } = useAdmin()
  const [exam, setExam] = useState<Partial<Exam> | null>(id ? null : defaults())
  const [tab, setTab] = useState('settings')
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)

  const load = useCallback(() => {
    if (id) api<Exam>(`/api/admin/exams/${id}`).then(setExam)
  }, [id])
  useEffect(() => load(), [load])

  if (!exam) return <p role="status">Đang tải…</p>
  const set = (patch: Partial<Exam>) => setExam({ ...exam, ...patch })
  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn()
      setMsg({ kind: 'ok', text: ok })
      load()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  async function saveSettings() {
    const body = { ...exam, rowVersion: exam!.rowVersion }
    try {
      const saved = await api<Exam>(id ? `/api/admin/exams/${id}` : '/api/admin/exams', { method: id ? 'PUT' : 'POST', body })
      setMsg({ kind: 'ok', text: 'Đã lưu cài đặt kỳ thi.' })
      if (!id) router.replace(`/admin/ky-thi/${saved.id}`)
      else setExam(saved)
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  const started = (exam.attemptCount ?? 0) > 0
  const tabs: [string, string][] = id ? [['settings', 'Cài đặt'], ['questions', 'Đề thi'], ['assign', 'Giao thi'], ['attempts', `Lượt thi (${exam.attemptCount})`]] : [['settings', 'Cài đặt']]

  return (
    <>
      <Title actions={id && <>
        {exam.status === 'Draft' && can('exam.exam.publish') && <button className={btnDanger} onClick={() => run(() => api(`/api/admin/exams/${id}/publish`, { method: 'POST' }), 'Đã công bố kỳ thi.')}>Công bố</button>}
        {exam.status === 'Published' && !started && can('exam.exam.publish') && <button className={btnGhost} onClick={() => run(() => api(`/api/admin/exams/${id}/unpublish`, { method: 'POST' }), 'Đã hủy công bố.')}>Hủy công bố</button>}
        {exam.status === 'Published' && <button className={btnGhost} onClick={() => window.confirm('Đóng kỳ thi ngay? Thí sinh không thể bắt đầu thêm lượt.') && run(() => api(`/api/admin/exams/${id}/close`, { method: 'POST' }), 'Đã đóng kỳ thi.')}>Đóng kỳ thi</button>}
        {exam.resultRelease === 'AfterApproval' && exam.status !== 'Draft' && can('exam.exam.publish') && <button className={btnGhost} onClick={() => run(() => api(`/api/admin/exams/${id}/approve-results`, { method: 'POST' }), 'Đã duyệt công bố kết quả.')}>Duyệt công bố kết quả</button>}
        {exam.status !== 'Archived' && exam.status !== 'Draft' && <button className={btnGhost} onClick={() => run(() => api(`/api/admin/exams/${id}/archive`, { method: 'POST' }), 'Đã lưu trữ.')}>Lưu trữ</button>}
        <Link className={btnGhost} href={`/admin/bao-cao/${id}`}>Báo cáo</Link>
      </>}>{id ? exam.title : 'Kỳ thi mới'}</Title>
      {id && <p className="mb-3 text-sm">Trạng thái: {statusLabel[exam.status!]} · {phaseLabel[exam.phase!]}{started && ' · đã có lượt làm bài (không đổi được đề, thời lượng)'}</p>}
      {msg && <div className="mb-3"><Notice kind={msg.kind}>{msg.text}</Notice></div>}
      <div role="tablist" className="mb-4 flex flex-wrap gap-2">
        {tabs.map(([k, l]) => <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? btnPrimary : btnGhost} onClick={() => setTab(k)}>{l}</button>)}
      </div>

      {tab === 'settings' && (
        <div className="grid max-w-5xl gap-4 rounded bg-white p-4 shadow-sm md:grid-cols-2">
          <Field label="Tên kỳ thi"><input className={input} value={exam.title ?? ''} onChange={(e) => set({ title: e.target.value })} /></Field>
          <Field label="Mã kỳ thi"><input className={input} value={exam.code ?? ''} onChange={(e) => set({ code: e.target.value })} /></Field>
          <Field label="Giờ mở (giờ máy bạn)"><input type="datetime-local" className={input} value={toLocal(exam.opensAtUtc!)} onChange={(e) => set({ opensAtUtc: new Date(e.target.value).toISOString() })} /></Field>
          <Field label="Giờ đóng"><input type="datetime-local" className={input} value={toLocal(exam.closesAtUtc!)} onChange={(e) => set({ closesAtUtc: new Date(e.target.value).toISOString() })} /></Field>
          <Field label="Thời lượng (phút)" hint="Hạn nộp = sớm hơn giữa (giờ bắt đầu + thời lượng) và giờ đóng."><input type="number" min={1} className={input} value={exam.durationMinutes} onChange={(e) => set({ durationMinutes: Number(e.target.value) })} /></Field>
          <Field label="Số lượt tối đa"><input type="number" min={1} className={input} value={exam.maxAttempts} onChange={(e) => set({ maxAttempts: Number(e.target.value) })} /></Field>
          <Field label="Điểm cuối lấy theo">
            <select className={input} value={exam.scoreRule} onChange={(e) => set({ scoreRule: e.target.value })}><option value="Highest">Lượt cao nhất</option><option value="Latest">Lượt gần nhất</option><option value="First">Lượt đầu tiên</option></select>
          </Field>
          <Field label="Điểm đạt (thang 10)"><input type="number" step="0.25" min={0} max={10} className={input} value={exam.passScore} onChange={(e) => set({ passScore: Number(e.target.value) })} /></Field>
          <Field label="Công bố kết quả">
            <select className={input} value={exam.resultRelease} onChange={(e) => set({ resultRelease: e.target.value })}><option value="Immediately">Ngay sau khi nộp</option><option value="AfterClose">Sau khi kỳ thi đóng</option><option value="AfterApproval">Sau khi duyệt</option></select>
          </Field>
          <Field label="Nguồn đề">
            <select className={input} value={exam.questionSource} disabled={started} onChange={(e) => set({ questionSource: e.target.value })}><option value="Manual">Chọn tay</option><option value="Random">Rút ngẫu nhiên theo ma trận</option></select>
          </Field>
          <div className="space-y-2 md:col-span-2">
            {([['shuffleQuestions', 'Đảo thứ tự câu'], ['shuffleChoices', 'Đảo thứ tự phương án'], ['showAnswerDetail', 'Cho thí sinh xem đúng/sai từng câu'], ['showExplanation', 'Cho thí sinh xem lời giải'], ['logTabSwitches', 'Ghi nhận khi thí sinh rời tab (chỉ là tín hiệu tham khảo)']] as const).map(([k, l]) => (
              <label key={k} className="flex items-center gap-2"><input type="checkbox" checked={!!exam[k]} onChange={(e) => set({ [k]: e.target.checked })} /> {l}</label>
            ))}
          </div>
          <div className="md:col-span-2"><Field label="Hướng dẫn cho thí sinh"><textarea className={input} rows={3} value={exam.instructions ?? ''} onChange={(e) => set({ instructions: e.target.value })} /></Field></div>
          <div><button className={btnPrimary} onClick={saveSettings}>Lưu cài đặt</button></div>
        </div>
      )}
      {tab === 'questions' && id && <QuestionsTab exam={exam as Exam} onSaved={load} setMsg={setMsg} />}
      {tab === 'assign' && id && <AssignTab exam={exam as Exam} onSaved={load} setMsg={setMsg} />}
      {tab === 'attempts' && id && <AttemptsTab examId={id} />}
    </>
  )
}

type SetMsg = (m: { kind: 'ok' | 'error'; text: string }) => void

function QuestionsTab({ exam, onSaved, setMsg }: { exam: Exam; onSaved: () => void; setMsg: SetMsg }) {
  const [rules, setRules] = useState<Rule[]>(exam.matrix)
  const [selected, setSelected] = useState<string[]>(exam.questionIds)
  const [q, setQ] = useState('')
  const [found, setFound] = useState<QuestionRow[]>([])
  const [topics, setTopics] = useState<string[]>([])
  const [check, setCheck] = useState<{ ok: boolean; problems: string[]; questionCount: number } | null>(null)
  useEffect(() => { api<string[]>('/api/admin/questions/topics').then(setTopics) }, [])
  useEffect(() => {
    const t = setTimeout(() => api<Paged<QuestionRow>>(`/api/admin/questions?status=Approved&pageSize=30&q=${encodeURIComponent(q)}`).then((r) => setFound(r.items)), 250)
    return () => clearTimeout(t)
  }, [q])

  const save = async () => {
    try {
      if (exam.questionSource === 'Manual') await api(`/api/admin/exams/${exam.id}/questions`, { method: 'PUT', body: { questionIds: selected } })
      else await api(`/api/admin/exams/${exam.id}/matrix`, { method: 'PUT', body: { rules } })
      setMsg({ kind: 'ok', text: 'Đã lưu đề thi.' })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }

  return (
    <div className="space-y-4">
      {exam.questionSource === 'Manual' ? (
        <div className="grid gap-4 lg:grid-cols-2">
          <div className="rounded bg-white p-3 shadow-sm">
            <h2 className="mb-2 font-semibold">Câu đã chọn ({selected.length})</h2>
            <ol className="list-decimal space-y-1 pl-5 text-sm">{selected.map((s) => <li key={s} className="flex justify-between gap-2"><span className="truncate">{found.find((f) => f.id === s)?.content.replace(/<[^>]+>/g, '') ?? s.slice(0, 8)}</span><button className="text-brand-red" onClick={() => setSelected(selected.filter((x) => x !== s))}>Bỏ</button></li>)}</ol>
          </div>
          <div className="rounded bg-white p-3 shadow-sm">
            <input aria-label="Tìm câu hỏi đã duyệt" placeholder="Tìm câu hỏi đã duyệt…" className={`${input} mb-2`} value={q} onChange={(e) => setQ(e.target.value)} />
            <ul className="max-h-96 space-y-1 overflow-y-auto text-sm">{found.map((f) => (
              <li key={f.id}><label className="flex gap-2"><input type="checkbox" checked={selected.includes(f.id)} onChange={(e) => setSelected(e.target.checked ? [...selected, f.id] : selected.filter((x) => x !== f.id))} /><span>{f.content.replace(/<[^>]+>/g, '')} <span className="text-slate-500">({f.topic})</span></span></label></li>
            ))}</ul>
          </div>
        </div>
      ) : (
        <div className="space-y-2 rounded bg-white p-3 shadow-sm">
          <h2 className="font-semibold">Ma trận rút đề</h2>
          {rules.map((r, i) => (
            <div key={i} className="flex flex-wrap items-center gap-2">
              <select aria-label="Chủ đề" className={`${input} max-w-[220px]`} value={r.topic ?? ''} onChange={(e) => setRules(rules.map((x, j) => (j === i ? { ...x, topic: e.target.value || null } : x)))}><option value="">Mọi chủ đề</option>{topics.map((t) => <option key={t}>{t}</option>)}</select>
              <select aria-label="Mức độ" className={`${input} max-w-[160px]`} value={r.difficulty ?? ''} onChange={(e) => setRules(rules.map((x, j) => (j === i ? { ...x, difficulty: e.target.value || null } : x)))}><option value="">Mọi mức độ</option><option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option></select>
              <input aria-label="Số câu" type="number" min={1} className={`${input} max-w-[100px]`} value={r.count} onChange={(e) => setRules(rules.map((x, j) => (j === i ? { ...x, count: Number(e.target.value) } : x)))} />
              {r.available !== undefined && <span className="text-sm text-slate-600">(ngân hàng có {r.available})</span>}
              <button className={btnGhost} onClick={() => setRules(rules.filter((_, j) => j !== i))}>Xóa</button>
            </div>
          ))}
          <button className={btnGhost} onClick={() => setRules([...rules, { topic: null, difficulty: null, count: 5 }])}>+ Quy tắc</button>
        </div>
      )}
      <div className="flex gap-2">
        <button className={btnPrimary} onClick={save}>Lưu đề</button>
        <button className={btnGhost} onClick={async () => setCheck(await api(`/api/admin/exams/${exam.id}/check`, { method: 'POST' }))}>Kiểm tra đề</button>
      </div>
      {check && (check.ok ? <Notice kind="ok">Đề hợp lệ ({check.questionCount} câu). Có thể công bố.</Notice> : <Notice kind="error">{check.problems.join(' ')}</Notice>)}
    </div>
  )
}

function AssignTab({ exam, onSaved, setMsg }: { exam: Exam; onSaved: () => void; setMsg: SetMsg }) {
  const [units, setUnits] = useState<Named[]>([])
  const [groups, setGroups] = useState<Named[]>([])
  const [codes, setCodes] = useState('')
  const [people, setPeople] = useState<Paged<{ id: string; code: string; displayName: string; orgUnitName?: string }> | null>(null)
  useEffect(() => {
    api<Named[]>('/api/admin/org-units').then(setUnits)
    api<Named[]>('/api/admin/candidate-groups').then(setGroups).catch(() => setGroups([]))
    api<typeof people>(`/api/admin/exams/${exam.id}/candidates?pageSize=50`).then(setPeople)
  }, [exam.id, exam.assignments])
  const name = (a: Assignment) =>
    a.targetType === 'OrgUnit' ? `Đơn vị: ${units.find((u) => u.id === a.targetId)?.name ?? a.targetId}`
      : a.targetType === 'Group' ? `Nhóm: ${groups.find((g) => g.id === a.targetId)?.name ?? a.targetId}`
        : `Thí sinh: ${a.targetId.slice(0, 8)}`
  const change = async (body: object) => {
    try {
      await api(`/api/admin/exams/${exam.id}/assignments`, { method: 'PUT', body })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'error', text: errorText(e) })
    }
  }
  const addCodes = async () => {
    const list = codes.split(/[\s,;]+/).filter(Boolean)
    const ids: string[] = []
    for (const c of list) {
      const r = await api<Paged<{ id: string; code: string }>>(`/api/admin/candidates?q=${encodeURIComponent(c)}&pageSize=5`)
      const hit = r.items.find((x) => x.code === c.toLowerCase())
      if (hit) ids.push(hit.id)
    }
    if (ids.length < list.length) setMsg({ kind: 'error', text: `Không tìm thấy ${list.length - ids.length} mã thí sinh.` })
    await change({ add: ids.map((targetId) => ({ targetType: 'Candidate', targetId })) })
    setCodes('')
  }
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <div className="space-y-3 rounded bg-white p-3 shadow-sm">
        <h2 className="font-semibold">Đối tượng được giao</h2>
        <ul className="space-y-1 text-sm">{exam.assignments.map((a) => <li key={a.id} className="flex justify-between"><span>{name(a)}</span><button className="text-brand-red" onClick={() => change({ remove: [a.id] })}>Gỡ</button></li>)}</ul>
        <Field label="Giao cho đơn vị (gồm đơn vị con)">
          <select className={input} value="" onChange={(e) => e.target.value && change({ add: [{ targetType: 'OrgUnit', targetId: e.target.value }] })}><option value="">— Chọn đơn vị —</option>{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
        </Field>
        <Field label="Giao cho nhóm">
          <select className={input} value="" onChange={(e) => e.target.value && change({ add: [{ targetType: 'Group', targetId: e.target.value }] })}><option value="">— Chọn nhóm —</option>{groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</select>
        </Field>
        <Field label="Giao cho thí sinh theo mã (cách nhau bởi dấu phẩy)"><textarea className={input} rows={2} value={codes} onChange={(e) => setCodes(e.target.value)} /></Field>
        <button className={btnPrimary} disabled={!codes.trim()} onClick={addCodes}>Thêm thí sinh</button>
      </div>
      <div className="rounded bg-white p-3 shadow-sm">
        <h2 className="mb-2 font-semibold">Danh sách thí sinh được giao ({people?.total ?? 0})</h2>
        <ul className="max-h-96 overflow-y-auto text-sm">{people?.items.map((p) => <li key={p.id}>{p.code} – {p.displayName} {p.orgUnitName && <span className="text-slate-500">({p.orgUnitName})</span>}</li>)}</ul>
      </div>
    </div>
  )
}

function AttemptsTab({ examId }: { examId: string }) {
  const { can } = useAdmin()
  const [rows, setRows] = useState<Attempt[]>([])
  const [regrade, setRegrade] = useState<Attempt | null>(null)
  const [detail, setDetail] = useState<unknown>(null)
  const [err, setErr] = useState<string | null>(null)
  const load = useCallback(() => { api<Attempt[]>(`/api/admin/exams/${examId}/attempts`).then(setRows) }, [examId])
  useEffect(() => load(), [load])
  return (
    <>
      <Table head={['Thí sinh', 'Lượt', 'Trạng thái', 'Bắt đầu', 'Nộp', 'Điểm', 'Rời tab', '']}>
        {rows.map((a) => (
          <tr key={a.id} className="border-t">
            <td className="p-2">{a.candidateCode} – {a.candidateName}</td><td className="p-2">{a.attemptNo}</td><td className="p-2">{a.status}</td>
            <td className="p-2">{formatDateTime(a.startedUtc)}</td><td className="p-2">{formatDateTime(a.finishedUtc)}</td>
            <td className="p-2">{a.score ?? '—'} {a.passed === true ? '(Đạt)' : a.passed === false ? '(Chưa đạt)' : ''}</td><td className="p-2">{a.tabSwitchCount}</td>
            <td className="p-2">
              {can('exam.result.detail') && <button className={btnGhost} onClick={async () => setDetail(await api(`/api/admin/attempts/${a.id}/detail`))}>Chi tiết</button>}
              {can('exam.result.regrade') && a.score != null && <button className={btnGhost} onClick={() => setRegrade(a)}>Chấm lại</button>}
            </td>
          </tr>
        ))}
      </Table>
      {regrade && (
        <Dialog title={`Chấm lại: ${regrade.candidateName}`} onClose={() => setRegrade(null)}>
          <form className="space-y-3" onSubmit={async (e) => {
            e.preventDefault()
            const f = new FormData(e.currentTarget)
            try {
              await api(`/api/admin/attempts/${regrade.id}/regrade`, { method: 'POST', body: { score: Number(f.get('score')), reason: f.get('reason') } })
              setRegrade(null)
              load()
            } catch (x) { setErr(errorText(x)) }
          }}>
            {err && <p role="alert" className="text-brand-red">{err}</p>}
            <p className="text-sm">Điểm hiện tại: {regrade.score}. Điểm gốc và lý do được lưu lại.</p>
            <Field label="Điểm mới (thang 10)"><input name="score" type="number" step="0.01" min={0} max={10} required className={input} defaultValue={regrade.score ?? 0} /></Field>
            <Field label="Lý do"><textarea name="reason" required className={input} rows={3} /></Field>
            <button className={btnPrimary}>Lưu điểm</button>
          </form>
        </Dialog>
      )}
      {detail !== null && (
        <Dialog title="Chi tiết bài làm" onClose={() => setDetail(null)}>
          <AttemptDetail data={detail} />
        </Dialog>
      )}
    </>
  )
}

function AttemptDetail({ data }: { data: unknown }) {
  const d = data as { questions: { id: string; orderNo: number; content: string; isCorrect: boolean; choices: { id: string; content: string; selected: boolean; isCorrect: boolean }[]; explanation?: string }[]; events: { kind: string; atUtc: string }[] }
  return (
    <div className="max-h-[70vh] space-y-3 overflow-y-auto text-sm">
      {d.questions.map((q) => (
        <div key={q.id} className={`rounded p-2 ${q.isCorrect ? 'bg-green-50' : 'bg-red-50'}`}>
          <p className="font-semibold">Câu {q.orderNo}: {q.content.replace(/<[^>]+>/g, '')}</p>
          <ul>{q.choices.map((c) => <li key={c.id}>{c.selected ? '☑' : '☐'} {c.content} {c.isCorrect && <strong>(đáp án)</strong>}</li>)}</ul>
          {q.explanation && <p className="text-slate-600">Lời giải: {q.explanation.replace(/<[^>]+>/g, '')}</p>}
        </div>
      ))}
      <p className="font-semibold">Sự kiện</p>
      <ul>{d.events.map((e, i) => <li key={i}>{formatDateTime(e.atUtc)} – {e.kind}</li>)}</ul>
    </div>
  )
}
