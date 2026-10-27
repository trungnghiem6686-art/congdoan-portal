'use client'

import { use, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api, ApiClientError } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import type { CandidateExam } from '@/components/exam/types'

export default function ExamIntro({ params }: { params: Promise<{ examId: string }> }) {
  const { examId } = use(params)
  const router = useRouter()
  const [exam, setExam] = useState<CandidateExam | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { api<CandidateExam[]>('/api/thi/exams', { cache: 'no-store' }).then((l) => setExam(l.find((e) => e.id === examId) ?? null)) }, [examId])

  if (exam === undefined) return <p className="p-6" role="status">Đang tải…</p>
  if (exam === null)
    return <main className="container-site py-6"><p role="alert" className="rounded bg-red-50 p-4 text-brand-red">Bạn không được giao kỳ thi này hoặc kỳ thi không tồn tại.</p></main>

  async function start() {
    setBusy(true)
    setError(null)
    try {
      const r = await api<{ attemptId: string }>(`/api/thi/exams/${examId}/start`, { method: 'POST' })
      router.push(`/thi/bai-lam/${r.attemptId}`)
    } catch (e) {
      setError(e instanceof ApiClientError ? e.message : 'Không bắt đầu được bài thi.')
      setBusy(false)
    }
  }

  const canStart = !!exam.activeAttemptId || (exam.phase === 'open' && exam.attemptsRemaining > 0)
  return (
    <main className="container-site max-w-3xl py-6">
      <h1 className="text-2xl font-bold text-brand-navy">{exam.title}</h1>
      {exam.description && <p className="mt-2">{exam.description}</p>}
      <ul className="mt-4 space-y-1 rounded bg-white p-4">
        <li>Thời gian mở: {formatDateTime(exam.opensAtUtc)} – {formatDateTime(exam.closesAtUtc)}</li>
        <li>Thời lượng: {exam.durationMinutes} phút (tính từ lúc bắt đầu; không vượt quá giờ đóng)</li>
        <li>Số lượt: đã dùng {exam.attemptsUsed}/{exam.maxAttempts}</li>
        <li>Câu trả lời được lưu tự động. Nếu mất mạng, bài được giữ trên máy và gửi lại khi có mạng; thời gian vẫn tính theo máy chủ.</li>
      </ul>
      {error && <p role="alert" className="mt-4 rounded bg-red-50 p-3 text-brand-red">{error}</p>}
      {canStart ? (
        <button disabled={busy} onClick={start} className="mt-6 rounded bg-brand-red px-6 py-3 text-lg font-semibold text-white hover:bg-red-800 disabled:opacity-60">
          {exam.activeAttemptId ? 'Tiếp tục làm bài' : 'Bắt đầu làm bài'}
        </button>
      ) : (
        <p className="mt-6 rounded bg-white p-4">{exam.phase === 'upcoming' ? 'Kỳ thi chưa mở.' : exam.phase === 'closed' ? 'Kỳ thi đã đóng.' : 'Bạn đã dùng hết số lượt.'}</p>
      )}
    </main>
  )
}
