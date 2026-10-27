'use client'

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { Flag } from 'lucide-react'
import { api, ApiClientError } from '@/lib/api'
import { safeHtml } from '@/lib/sanitize'
import { Dialog } from '@/components/admin/ui'
import { clearPending, formatClock, loadPending, merge, remainingMs, savePending, type LocalAnswer } from './runnerLogic'

type Question = { id: string; orderNo: number; type: string; content: string; points: number; choices: { id: string; content: string }[] }
type View = {
  attemptId: string; examId: string; examTitle: string; instructions?: string | null; status: string; deadlineUtc: string; serverNowUtc: string
  lastSeq: number; logTabSwitches: boolean; questions: Question[]; answers: { questionId: string; choiceIds: string[]; flagged: boolean; seq: number }[]
}
type SyncState = 'saved' | 'saving' | 'pending' | 'error'
type Mode = 'loading' | 'writing' | 'readonly' | 'locked'

const writerKey = (id: string) => `cd-writer-${id}`
const RETRY_MS = 5000

export function AttemptRunner({ attemptId }: { attemptId: string }) {
  const router = useRouter()
  const [view, setView] = useState<View | null>(null)
  const [answers, setAnswers] = useState<Record<string, LocalAnswer>>({})
  const [sync, setSync] = useState<Record<string, SyncState>>({})
  const [offline, setOffline] = useState(false)
  const [mode, setMode] = useState<Mode>('loading')
  const [notice, setNotice] = useState<string | null>(null)
  const [index, setIndex] = useState(0)
  const [showAll, setShowAll] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const offset = useRef(0)
  const seq = useRef(0)
  const writer = useRef<string | null>(null)
  const answersRef = useRef(answers)
  answersRef.current = answers
  const inFlight = useRef<Set<string>>(new Set())
  const finishing = useRef(false)

  // ---- load, merge local queue, claim the writer lease ----
  useEffect(() => {
    let alive = true
    ;(async () => {
      const v = await api<View>(`/api/thi/attempts/${attemptId}`, { cache: 'no-store' })
      if (!alive) return
      offset.current = new Date(v.serverNowUtc).getTime() - Date.now()
      const server: Record<string, LocalAnswer> = {}
      for (const a of v.answers) server[a.questionId] = { choiceIds: a.choiceIds, flagged: a.flagged, seq: a.seq }
      const { answers: merged, resend } = merge(server, loadPending(attemptId))
      seq.current = Math.max(v.lastSeq, ...Object.values(merged).map((a) => a.seq), 0)
      setView(v)
      setAnswers(merged)
      setSync(Object.fromEntries(resend.map((q) => [q, 'pending' as SyncState])))
      if (v.status !== 'InProgress') {
        setMode('locked')
        setNotice('Bài thi đã kết thúc.')
        return
      }
      await claim(false)
    })().catch((e) => setNotice(e instanceof ApiClientError ? e.message : 'Không tải được bài thi.'))
    return () => {
      alive = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attemptId])

  async function claim(force: boolean) {
    const stored = sessionStorage.getItem(writerKey(attemptId))
    if (stored && !force) {
      try {
        await api(`/api/thi/attempts/${attemptId}/heartbeat`, { method: 'POST', headers: { 'X-Attempt-Writer': stored } })
        writer.current = stored
        setMode('writing')
        return
      } catch {
        /* lease lost; claim again below */
      }
    }
    try {
      const r = await api<{ writerToken: string }>(`/api/thi/attempts/${attemptId}/claim`, { method: 'POST', body: { force } })
      writer.current = r.writerToken
      sessionStorage.setItem(writerKey(attemptId), r.writerToken)
      setMode('writing')
      setNotice(null)
    } catch (e) {
      if (e instanceof ApiClientError && e.code === 'another_tab_active') {
        setMode('readonly')
        setNotice(e.message)
      } else handleFatal(e)
    }
  }

  const handleFatal = useCallback((e: unknown) => {
    if (!(e instanceof ApiClientError)) return false
    if (e.code === 'deadline_passed' || e.code === 'attempt_finished') {
      setMode('locked')
      setNotice(e.code === 'deadline_passed' ? 'Đã hết giờ làm bài. Bài đã được nộp với các câu trả lời đã lưu.' : 'Bài thi đã kết thúc.')
      clearPending(attemptId)
      return true
    }
    if (e.code === 'writer_superseded' || e.code === 'read_only') {
      setMode('readonly')
      setNotice(e.message)
      return true
    }
    return false
  }, [attemptId])

  // ---- saving ----
  const send = useCallback(async (questionId: string) => {
    const a = answersRef.current[questionId]
    if (!a || !writer.current || inFlight.current.has(questionId)) return
    inFlight.current.add(questionId)
    setSync((s) => ({ ...s, [questionId]: 'saving' }))
    try {
      await api(`/api/thi/attempts/${attemptId}/answers/${questionId}`, {
        method: 'PUT', body: { choiceIds: a.choiceIds, seq: a.seq, flagged: a.flagged }, headers: { 'X-Attempt-Writer': writer.current },
      })
      setOffline(false)
      const latest = answersRef.current[questionId]
      if (latest && latest.seq > a.seq) {
        setSync((s) => ({ ...s, [questionId]: 'pending' }))
      } else {
        setSync((s) => ({ ...s, [questionId]: 'saved' }))
        const pending = loadPending(attemptId)
        if (pending[questionId]?.seq === a.seq) {
          delete pending[questionId]
          savePending(attemptId, pending)
        }
      }
    } catch (e) {
      if (!handleFatal(e)) {
        setOffline(typeof navigator !== 'undefined' && !navigator.onLine || (e instanceof ApiClientError && e.code === 'network_error'))
        setSync((s) => ({ ...s, [questionId]: 'error' }))
      }
    } finally {
      inFlight.current.delete(questionId)
    }
  }, [attemptId, handleFatal])

  // Send newer pending answers; retry failed ones on a timer and when the network returns.
  useEffect(() => {
    if (mode !== 'writing') return
    const t = setTimeout(() => {
      for (const [q, st] of Object.entries(sync)) if (st === 'pending') void send(q)
    }, 0)
    return () => clearTimeout(t)
  }, [sync, mode, send])

  useEffect(() => {
    if (mode !== 'writing') return
    const retry = () => {
      for (const [q, st] of Object.entries(sync)) if (st === 'error') void send(q)
    }
    const t = setInterval(retry, RETRY_MS)
    window.addEventListener('online', retry)
    return () => {
      clearInterval(t)
      window.removeEventListener('online', retry)
    }
  }, [sync, mode, send])

  function answer(q: Question, choiceId: string, checked: boolean) {
    if (mode !== 'writing') return
    const prev = answers[q.id]?.choiceIds ?? []
    const choiceIds = q.type === 'Multiple' ? (checked ? [...prev, choiceId] : prev.filter((c) => c !== choiceId)) : [choiceId]
    update(q.id, { choiceIds })
  }

  function update(questionId: string, patch: Partial<LocalAnswer>) {
    const base: LocalAnswer = answers[questionId] ?? { choiceIds: [], flagged: false, seq: 0 }
    const next: LocalAnswer = { ...base, ...patch, seq: ++seq.current }
    setAnswers((a) => ({ ...a, [questionId]: next }))
    answersRef.current = { ...answersRef.current, [questionId]: next }
    const pending = loadPending(attemptId)
    pending[questionId] = next
    savePending(attemptId, pending)
    setSync((s) => ({ ...s, [questionId]: 'pending' }))
  }

  // ---- timer, heartbeat, tab switch signal ----
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(t)
  }, [])

  const left = view ? remainingMs(view.deadlineUtc, offset.current, now) : 0

  useEffect(() => {
    if (!view || mode === 'locked' || mode === 'loading' || left > 0 || finishing.current) return
    finishing.current = true
    setMode('locked')
    setNotice('Đã hết giờ làm bài. Bài đã được nộp với các câu trả lời đã lưu.')
    api(`/api/thi/attempts/${attemptId}/submit`, { method: 'POST' }).then(() => clearPending(attemptId)).catch(() => undefined)
  }, [left, view, mode, attemptId])

  useEffect(() => {
    if (mode !== 'writing') return
    const t = setInterval(() => {
      api(`/api/thi/attempts/${attemptId}/heartbeat`, { method: 'POST', headers: { 'X-Attempt-Writer': writer.current ?? '' } }).catch(handleFatal)
    }, 20_000)
    return () => clearInterval(t)
  }, [mode, attemptId, handleFatal])

  useEffect(() => {
    if (!view?.logTabSwitches) return
    const onVis = () => {
      api(`/api/thi/attempts/${attemptId}/events`, { method: 'POST', body: { kind: document.hidden ? 'tab_hidden' : 'tab_visible' } }).catch(() => undefined)
    }
    document.addEventListener('visibilitychange', onVis)
    return () => document.removeEventListener('visibilitychange', onVis)
  }, [view, attemptId])

  // ---- submit ----
  const unsynced = Object.values(sync).filter((s) => s !== 'saved').length
  const blank = view ? view.questions.filter((q) => !(answers[q.id]?.choiceIds.length)).length : 0
  const flagged = view ? view.questions.filter((q) => answers[q.id]?.flagged).length : 0

  async function submit() {
    setConfirming(false)
    // Flush everything not yet confirmed by the server before submitting.
    await Promise.all(Object.entries(sync).filter(([, s]) => s !== 'saved').map(([q]) => send(q)))
    try {
      await api(`/api/thi/attempts/${attemptId}/submit`, { method: 'POST' })
      clearPending(attemptId)
      sessionStorage.removeItem(writerKey(attemptId))
      router.push(`/thi/ket-qua/${attemptId}`)
    } catch (e) {
      if (!handleFatal(e)) setNotice(e instanceof ApiClientError ? e.message : 'Chưa nộp được bài, vui lòng thử lại.')
    }
  }

  const statusText = useMemo(() => {
    const saving = Object.values(sync).some((s) => s === 'saving')
    if (offline && unsynced > 0) return `Mất kết nối – ${unsynced} câu chưa đồng bộ (đã giữ trên máy này)`
    if (saving) return 'Đang lưu…'
    const errors = Object.values(sync).filter((s) => s === 'error').length
    if (errors > 0) return `Chưa lưu được ${errors} câu – đang thử lại`
    if (Object.keys(sync).length > 0) return 'Đã lưu'
    return 'Câu trả lời được lưu tự động'
  }, [sync, offline, unsynced])

  if (!view) return <p className="p-6" role="status">{notice ?? 'Đang tải bài thi…'}</p>
  const disabled = mode !== 'writing'
  const shown = showAll ? view.questions : [view.questions[index]]

  return (
    <div className="pb-10">
      <div className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 shadow-sm backdrop-blur">
        <div className="container-site flex flex-wrap items-center justify-between gap-x-4 gap-y-1 py-2">
          <p className="min-w-0 truncate font-semibold text-brand-navy">{view.examTitle}</p>
          <div className="flex items-center gap-3">
            <span role="timer" aria-label="Thời gian còn lại" className={`font-mono text-xl font-bold ${left < 5 * 60_000 ? 'text-brand-red' : 'text-brand-navy'}`}>{formatClock(left)}</span>
            <button className="rounded bg-brand-red px-3 py-1.5 font-semibold text-white disabled:opacity-50" disabled={mode === 'locked'} onClick={() => setConfirming(true)}>Nộp bài</button>
          </div>
          <p data-testid="save-status" role="status" aria-live="polite" className={`w-full text-sm ${offline || statusText.startsWith('Chưa') ? 'text-brand-red' : 'text-slate-600'}`}>{statusText}</p>
        </div>
      </div>

      <div className="container-site mt-4 grid gap-6 lg:grid-cols-[1fr_260px]">
        <div className="min-w-0 space-y-4">
          {notice && (
            <div role="alert" className="rounded bg-yellow-50 p-3 text-yellow-900">
              {notice}
              {mode === 'readonly' && <button className="ml-2 underline" onClick={() => claim(true)}>Làm bài ở tab này</button>}
              {mode === 'locked' && <button className="ml-2 underline" onClick={() => router.push(`/thi/ket-qua/${attemptId}`)}>Xem kết quả</button>}
            </div>
          )}
          {view.instructions && index === 0 && !showAll && <p className="rounded bg-blue-50 p-3 text-sm">{view.instructions}</p>}
          {shown.map((q) => (
            <section key={q.id} className="scroll-mt-28 rounded bg-white p-4 shadow-sm" aria-labelledby={`q-${q.id}`}>
              <div className="flex items-start justify-between gap-2">
                <h2 id={`q-${q.id}`} className="font-bold text-brand-navy">Câu {q.orderNo} <span className="text-sm font-normal text-slate-500">({q.points} điểm{q.type === 'Multiple' ? ', chọn tất cả đáp án đúng' : ''})</span></h2>
                <button type="button" disabled={disabled} aria-pressed={!!answers[q.id]?.flagged} onClick={() => update(q.id, { flagged: !answers[q.id]?.flagged })}
                  className={`inline-flex shrink-0 items-center gap-1 rounded border px-2 py-1 text-sm ${answers[q.id]?.flagged ? 'border-amber-500 bg-amber-50 text-amber-800' : 'border-slate-300'}`}>
                  <Flag aria-hidden className="h-4 w-4" /> Xem lại
                </button>
              </div>
              <div data-testid="question-text" className="prose mt-2 max-w-none text-base" dangerouslySetInnerHTML={{ __html: safeHtml(q.content) }} />
              <fieldset className="mt-3 space-y-2">
                <legend className="sr-only">Phương án câu {q.orderNo}</legend>
                {q.choices.map((c) => {
                  const checked = !!answers[q.id]?.choiceIds.includes(c.id)
                  return (
                    <label key={c.id} className={`flex min-h-[44px] cursor-pointer items-center gap-3 rounded border p-3 ${checked ? 'border-brand-blue bg-blue-50' : 'border-slate-200'}`}>
                      <input type={q.type === 'Multiple' ? 'checkbox' : 'radio'} name={`q-${q.id}`} checked={checked} disabled={disabled}
                        onChange={(e) => answer(q, c.id, e.target.checked)} className="h-5 w-5" />
                      <span>{c.content}</span>
                    </label>
                  )
                })}
              </fieldset>
            </section>
          ))}
          {!showAll && (
            <div className="flex justify-between">
              <button className="rounded border border-slate-300 bg-white px-4 py-2 disabled:opacity-40" disabled={index === 0} onClick={() => setIndex(index - 1)}>← Câu trước</button>
              <button className="rounded border border-slate-300 bg-white px-4 py-2 disabled:opacity-40" disabled={index === view.questions.length - 1} onClick={() => setIndex(index + 1)}>Câu sau →</button>
            </div>
          )}
        </div>

        <aside className="lg:sticky lg:top-24 lg:self-start">
          <button className="mb-3 w-full rounded border border-slate-300 bg-white px-3 py-2" onClick={() => setShowAll((s) => !s)}>{showAll ? 'Xem từng câu' : 'Xem tất cả câu'}</button>
          <nav aria-label="Danh sách câu hỏi" className="rounded bg-white p-3 shadow-sm">
            <ol className="grid grid-cols-6 gap-2 lg:grid-cols-5">
              {view.questions.map((q, i) => {
                const a = answers[q.id]
                const done = !!a?.choiceIds.length
                return (
                  <li key={q.id}>
                    <button
                      aria-label={`Câu ${q.orderNo}${done ? ', đã trả lời' : ', chưa trả lời'}${a?.flagged ? ', cần xem lại' : ''}`}
                      aria-current={!showAll && i === index ? 'step' : undefined}
                      onClick={() => { setShowAll(false); setIndex(i) }}
                      className={`h-10 w-full rounded text-sm font-semibold ${a?.flagged ? 'ring-2 ring-amber-500' : ''} ${done ? 'bg-brand-navy text-white' : 'bg-slate-100'} ${!showAll && i === index ? 'outline outline-2 outline-brand-red' : ''}`}
                    >
                      {q.orderNo}
                    </button>
                  </li>
                )
              })}
            </ol>
            <p className="mt-3 text-xs text-slate-600">Đã trả lời {view.questions.length - blank}/{view.questions.length} · Xem lại {flagged}</p>
          </nav>
        </aside>
      </div>

      {confirming && (
        <Dialog title="Nộp bài?" onClose={() => setConfirming(false)}>
          {blank > 0 ? <p className="font-semibold text-brand-red">Bạn còn {blank} câu chưa trả lời.</p> : <p>Bạn đã trả lời tất cả các câu.</p>}
          {flagged > 0 && <p className="mt-1">Có {flagged} câu đánh dấu xem lại.</p>}
          {unsynced > 0 && <p className="mt-1">{unsynced} câu chưa đồng bộ sẽ được gửi trước khi nộp.</p>}
          <p className="mt-2 text-sm text-slate-600">Sau khi nộp, bạn không thể sửa câu trả lời.</p>
          <div className="mt-4 flex justify-end gap-2">
            <button className="rounded border border-slate-300 px-4 py-2" onClick={() => setConfirming(false)}>Quay lại làm bài</button>
            <button className="rounded bg-brand-red px-4 py-2 font-semibold text-white" onClick={submit}>Nộp bài</button>
          </div>
        </Dialog>
      )}
    </div>
  )
}
