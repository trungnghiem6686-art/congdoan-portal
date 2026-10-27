'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

type Dashboard = {
  publishedPosts: number; pendingReviewPosts: number; draftPosts: number; newContacts: number; candidates: number
  approvedQuestions: number; openExams: number; attemptsInProgress: number; attemptsFinishedToday: number
}

const cards: { key: keyof Dashboard; label: string; href: string }[] = [
  { key: 'publishedPosts', label: 'Bài đã xuất bản', href: '/admin/bai-viet' },
  { key: 'pendingReviewPosts', label: 'Bài chờ duyệt', href: '/admin/bai-viet' },
  { key: 'draftPosts', label: 'Bài nháp', href: '/admin/bai-viet' },
  { key: 'newContacts', label: 'Liên hệ mới', href: '/admin/lien-he' },
  { key: 'candidates', label: 'Thí sinh', href: '/admin/thi-sinh' },
  { key: 'approvedQuestions', label: 'Câu hỏi đã duyệt', href: '/admin/cau-hoi' },
  { key: 'openExams', label: 'Kỳ thi đang mở', href: '/admin/ky-thi' },
  { key: 'attemptsInProgress', label: 'Đang làm bài', href: '/admin/ky-thi' },
  { key: 'attemptsFinishedToday', label: 'Bài nộp hôm nay', href: '/admin/bao-cao' },
]

/** Numbers come straight from the database through /api/admin/dashboard. */
export function DashboardStats() {
  const [d, setD] = useState<Dashboard | null>(null)
  useEffect(() => {
    api<Dashboard>('/api/admin/dashboard', { cache: 'no-store' }).then(setD)
  }, [])
  if (!d) return <p role="status">Đang tải số liệu…</p>
  return (
    <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {cards.map((c) => (
        <li key={c.key}>
          <Link href={c.href} className="block rounded bg-white p-4 shadow-sm hover:ring-2 hover:ring-brand-blue">
            <span className="block text-sm text-slate-600">{c.label}</span>
            <span data-testid={`stat-${c.key}`} className="text-3xl font-bold text-brand-navy">{d[c.key]}</span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
