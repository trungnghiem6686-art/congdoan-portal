'use client'

import { use } from 'react'
import { AttemptRunner } from '@/components/exam/AttemptRunner'

export default function AttemptPage({ params }: { params: Promise<{ attemptId: string }> }) {
  const { attemptId } = use(params)
  return <AttemptRunner attemptId={attemptId} />
}
