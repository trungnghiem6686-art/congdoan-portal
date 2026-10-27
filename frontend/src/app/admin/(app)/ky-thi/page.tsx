'use client'

import { ExamList } from '@/components/admin/ExamList'

export default function ExamsPage() {
  return <ExamList title="Kỳ thi" linkBase="/admin/ky-thi" canCreate />
}
