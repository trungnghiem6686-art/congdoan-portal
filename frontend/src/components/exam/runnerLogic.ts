/** Pure helpers for the exam runner (unit-tested). */

export type LocalAnswer = { choiceIds: string[]; flagged: boolean; seq: number }

/** Remaining milliseconds using the server clock: offset = serverNow − deviceNow at load time. */
export function remainingMs(deadlineUtc: string, offsetMs: number, deviceNow = Date.now()) {
  return new Date(deadlineUtc).getTime() - (deviceNow + offsetMs)
}

export function formatClock(ms: number) {
  const total = Math.max(0, Math.ceil(ms / 1000))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  const mm = String(m).padStart(2, '0')
  const ss = String(s).padStart(2, '0')
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`
}

const key = (attemptId: string) => `cd-attempt-${attemptId}`

/** Unsynced answers kept on this device: only question and choice ids, never any answer key. */
export function loadPending(attemptId: string): Record<string, LocalAnswer> {
  try {
    return JSON.parse(localStorage.getItem(key(attemptId)) ?? '{}')
  } catch {
    return {}
  }
}

export function savePending(attemptId: string, pending: Record<string, LocalAnswer>) {
  try {
    if (Object.keys(pending).length === 0) localStorage.removeItem(key(attemptId))
    else localStorage.setItem(key(attemptId), JSON.stringify(pending))
  } catch {
    // Storage may be unavailable (private mode); the server copy still applies.
  }
}

export function clearPending(attemptId: string) {
  try {
    localStorage.removeItem(key(attemptId))
  } catch {
    /* ignore */
  }
}

/** Server answers merged with newer local ones (higher seq wins). */
export function merge(server: Record<string, LocalAnswer>, local: Record<string, LocalAnswer>) {
  const out = { ...server }
  const resend: string[] = []
  for (const [q, a] of Object.entries(local)) {
    if (!out[q] || a.seq > out[q].seq) {
      out[q] = a
      resend.push(q)
    }
  }
  return { answers: out, resend }
}
