export type CandidateExam = {
  id: string; title: string; description?: string | null; opensAtUtc: string; closesAtUtc: string; durationMinutes: number; phase: string
  maxAttempts: number; attemptsUsed: number; attemptsRemaining: number; activeAttemptId?: string | null; finalScore?: number | null; passed?: boolean | null; resultVisible: boolean
}
