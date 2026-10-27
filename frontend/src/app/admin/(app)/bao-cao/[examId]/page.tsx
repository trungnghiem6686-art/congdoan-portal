'use client'

import { use, useEffect, useState } from 'react'
import { api, csrf } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from '@/components/admin/AdminContext'
import { Notice, Table, Title, btnGhost, errorText, input } from '@/components/admin/ui'

type Report = {
  examTitle: string; scoreRule: string; passScore: number
  summary: { assignedPeople: number; attemptedPeople: number; notAttemptedPeople: number; attemptsTotal: number; finishedAttempts: number; inProgressAttempts: number; passedPeople: number; passRatePercent: number | null; averageFinalScore: number | null; minFinalScore: number | null; maxFinalScore: number | null; distribution: number[] }
  rows: { candidateId: string; code: string; name: string; orgUnit?: string; attempts: number; finalScore: number | null; passed: boolean | null; lastFinishedUtc?: string; tabSwitches: number }[]
}

export default function ExamReportPage({ params }: { params: Promise<{ examId: string }> }) {
  const { examId } = use(params)
  const { can } = useAdmin()
  const [unit, setUnit] = useState('')
  const [units, setUnits] = useState<{ id: string; name: string }[]>([])
  const [r, setR] = useState<Report | null>(null)
  const [err, setErr] = useState<string | null>(null)
  useEffect(() => { api<{ id: string; name: string }[]>('/api/admin/org-units').then(setUnits) }, [])
  useEffect(() => { api<Report>(`/api/admin/reports/exams/${examId}${unit ? `?orgUnitId=${unit}` : ''}`).then(setR).catch((e) => setErr(errorText(e))) }, [examId, unit])

  /** POST export (audited server-side), then hand the file to the browser. */
  async function exportFile(format: 'csv' | 'xlsx') {
    try {
      const res = await fetch(`/api/admin/reports/exams/${examId}/export?format=${format}${unit ? `&orgUnitId=${unit}` : ''}`, {
        method: 'POST', credentials: 'include', headers: { 'X-XSRF-TOKEN': await csrf() },
      })
      if (!res.ok) throw new Error('Không xuất được tệp.')
      const name = /filename\*?=(?:UTF-8'')?"?([^";]+)/i.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? `ket-qua.${format}`
      const url = URL.createObjectURL(await res.blob())
      const a = document.createElement('a')
      a.href = url
      a.download = decodeURIComponent(name)
      document.body.appendChild(a)
      a.click()
      a.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (e) {
      setErr(errorText(e))
    }
  }

  if (err) return <Notice kind="error">{err}</Notice>
  if (!r) return <p role="status">Đang tải…</p>
  const s = r.summary
  const stats: [string, string | number][] = [
    ['Được giao', s.assignedPeople], ['Đã làm bài (người)', s.attemptedPeople], ['Chưa làm (người)', s.notAttemptedPeople],
    ['Tổng lượt', s.attemptsTotal], ['Lượt đã nộp', s.finishedAttempts], ['Đang làm', s.inProgressAttempts],
    ['Số người đạt', s.passedPeople], ['Tỉ lệ đạt', s.passRatePercent == null ? '—' : `${s.passRatePercent}% số người đã làm`],
    ['Điểm TB / thấp / cao', s.averageFinalScore == null ? '—' : `${s.averageFinalScore} / ${s.minFinalScore} / ${s.maxFinalScore}`],
  ]
  const max = Math.max(1, ...s.distribution)
  return (
    <>
      <Title actions={can('report.export') && <>
        <button className={btnGhost} onClick={() => exportFile('csv')}>Xuất CSV</button>
        <button className={btnGhost} onClick={() => exportFile('xlsx')}>Xuất Excel</button>
      </>}>Báo cáo: {r.examTitle}</Title>
      <p className="mb-3 text-sm text-slate-600">Điểm cuối mỗi người theo quy tắc “{r.scoreRule === 'Highest' ? 'cao nhất' : r.scoreRule === 'Latest' ? 'gần nhất' : 'đầu tiên'}”, điểm đạt {r.passScore}/10.</p>
      <select aria-label="Lọc theo đơn vị" className={`${input} mb-4 max-w-xs`} value={unit} onChange={(e) => setUnit(e.target.value)}>
        <option value="">Tất cả đơn vị</option>{units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
      </select>
      <dl className="mb-6 grid gap-3 sm:grid-cols-3">
        {stats.map(([k, v]) => <div key={k} className="rounded bg-white p-3 shadow-sm"><dt className="text-sm text-slate-600">{k}</dt><dd className="text-xl font-bold text-brand-navy">{v}</dd></div>)}
      </dl>
      <h2 className="mb-2 font-semibold">Phân bố điểm cuối (số người)</h2>
      <div className="mb-6 flex h-32 items-end gap-1 rounded bg-white p-3 shadow-sm" role="img" aria-label={`Phân bố điểm: ${s.distribution.map((n, i) => `${i}–${i + 1}: ${n}`).join(', ')}`}>
        {s.distribution.map((n, i) => (
          <div key={i} className="flex flex-1 flex-col items-center justify-end gap-1 text-xs">
            <span>{n}</span>
            <div className="w-full rounded-t bg-brand-blue" style={{ height: `${(n / max) * 80}px` }} />
            <span>{i}–{i + 1}</span>
          </div>
        ))}
      </div>
      <Table head={['Mã', 'Họ tên', 'Đơn vị', 'Số lượt', 'Điểm cuối', 'Kết quả', 'Nộp gần nhất', 'Rời tab']}>
        {r.rows.map((x) => (
          <tr key={x.candidateId} className="border-t">
            <td className="p-2">{x.code}</td><td className="p-2">{x.name}</td><td className="p-2">{x.orgUnit}</td><td className="p-2">{x.attempts}</td>
            <td className="p-2">{x.finalScore ?? '—'}</td><td className="p-2">{x.passed == null ? 'Chưa có' : x.passed ? 'Đạt' : 'Chưa đạt'}</td>
            <td className="p-2">{formatDateTime(x.lastFinishedUtc)}</td><td className="p-2">{x.tabSwitches}</td>
          </tr>
        ))}
      </Table>
    </>
  )
}
