'use client'

import Link from 'next/link'
import { use, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { safeHtml } from '@/lib/sanitize'

type Result = {
  examTitle: string; attemptNo: number; status: string; visible: boolean; finishedUtc?: string; score?: number | null; passed?: boolean | null
  correctCount?: number | null; questionCount: number; passScore?: number | null; message?: string | null
  questions?: { id: string; orderNo: number; content: string; isCorrect: boolean; awarded?: number | null; points: number; choices: { id: string; content: string; selected: boolean; isCorrect?: boolean | null }[]; explanation?: string | null }[] | null
}

export default function ResultPage({ params }: { params: Promise<{ attemptId: string }> }) {
  const { attemptId } = use(params)
  const [r, setR] = useState<Result | null>(null)
  useEffect(() => { api<Result>(`/api/thi/attempts/${attemptId}/result`, { cache: 'no-store' }).then(setR) }, [attemptId])
  if (!r) return <p className="p-6" role="status">Đang tải kết quả…</p>
  return (
    <main className="container-site max-w-3xl py-6">
      <h1 className="text-2xl font-bold text-brand-navy">Kết quả: {r.examTitle}</h1>
      <p className="text-sm text-slate-600">Lượt {r.attemptNo} · {r.status === 'Expired' ? 'Nộp tự động khi hết giờ' : 'Đã nộp'} {r.finishedUtc && `lúc ${formatDateTime(r.finishedUtc)}`}</p>
      {r.visible ? (
        <div className="mt-4 rounded bg-white p-6 text-center shadow-sm">
          <p className="text-sm text-slate-600">Điểm (thang 10)</p>
          <p className="text-5xl font-bold text-brand-navy">{r.score}</p>
          <p className={`mt-2 text-lg font-semibold ${r.passed ? 'text-green-700' : 'text-brand-red'}`}>{r.passed ? 'Đạt' : 'Chưa đạt'} (điểm đạt {r.passScore})</p>
          <p className="mt-1">Đúng {r.correctCount}/{r.questionCount} câu</p>
        </div>
      ) : (
        <p className="mt-4 rounded bg-white p-6">{r.message}</p>
      )}
      {r.questions && (
        <ol className="mt-6 space-y-3">
          {r.questions.map((q) => (
            <li key={q.id} className={`rounded p-4 ${q.isCorrect ? 'bg-green-50' : 'bg-red-50'}`}>
              <p className="font-semibold">Câu {q.orderNo} – {q.isCorrect ? 'Đúng' : 'Sai'}</p>
              <div className="prose max-w-none" dangerouslySetInnerHTML={{ __html: safeHtml(q.content) }} />
              <ul className="mt-2 space-y-1">{q.choices.map((c) => (
                <li key={c.id}>{c.selected ? '● ' : '○ '}{c.content}{c.isCorrect && <strong className="text-green-700"> (đáp án đúng)</strong>}</li>
              ))}</ul>
              {q.explanation && <div className="mt-2 text-sm text-slate-700" dangerouslySetInnerHTML={{ __html: safeHtml(q.explanation) }} />}
            </li>
          ))}
        </ol>
      )}
      <Link href="/thi" className="mt-6 inline-block rounded bg-brand-navy px-4 py-2 font-semibold text-white">Về danh sách kỳ thi</Link>
    </main>
  )
}
