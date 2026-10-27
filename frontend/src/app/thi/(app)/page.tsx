'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import type { CandidateExam } from '@/components/exam/types'

const phase: Record<string, string> = { upcoming: 'Sắp mở', open: 'Đang mở', closed: 'Đã đóng' }

export default function CandidateHome() {
  const [exams, setExams] = useState<CandidateExam[] | null>(null)
  useEffect(() => { api<CandidateExam[]>('/api/thi/exams', { cache: 'no-store' }).then(setExams) }, [])
  return (
    <main className="container-site py-6">
      <h1 className="mb-4 text-2xl font-bold text-brand-navy">Kỳ thi của bạn</h1>
      {!exams ? <p role="status">Đang tải…</p> : exams.length === 0 ? (
        <p className="rounded bg-white p-6">Bạn chưa được giao kỳ thi nào.</p>
      ) : (
        <ul className="space-y-3">
          {exams.map((e) => (
            <li key={e.id} className="rounded bg-white p-4 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="text-lg font-semibold text-brand-navy">{e.title}</h2>
                  <p className="text-sm text-slate-600">{formatDateTime(e.opensAtUtc)} – {formatDateTime(e.closesAtUtc)} · {e.durationMinutes} phút · {phase[e.phase] ?? e.phase}</p>
                  <p className="text-sm">Đã làm {e.attemptsUsed}/{e.maxAttempts} lượt{e.resultVisible && e.finalScore != null && ` · Điểm: ${e.finalScore} (${e.passed ? 'Đạt' : 'Chưa đạt'})`}</p>
                </div>
                <Link href={`/thi/ky-thi/${e.id}`} className="rounded bg-brand-navy px-4 py-2 font-semibold text-white hover:bg-brand-blue">
                  {e.activeAttemptId ? 'Tiếp tục làm bài' : e.phase === 'open' && e.attemptsRemaining > 0 ? 'Vào thi' : 'Xem'}
                </Link>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}
