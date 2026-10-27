import type { Metadata } from 'next'
import { CandidateShell } from '@/components/exam/CandidateShell'

export const metadata: Metadata = { title: 'Thi trực tuyến', robots: { index: false, follow: false } }

export default function CandidateLayout({ children }: { children: React.ReactNode }) {
  return <CandidateShell>{children}</CandidateShell>
}
