import { describe, expect, it } from 'vitest'
import { formatClock, merge, remainingMs } from '@/components/exam/runnerLogic'

describe('runner logic', () => {
  it('uses the server clock offset, not the device clock', () => {
    const server = Date.parse('2026-03-01T01:00:00Z')
    const device = server + 3 * 3600_000 // device clock 3 hours fast
    const offset = server - device
    expect(remainingMs('2026-03-01T01:30:00Z', offset, device)).toBe(30 * 60_000)
  })

  it('formats the clock', () => {
    expect(formatClock(29 * 60_000 + 5_000)).toBe('29:05')
    expect(formatClock(3_700_000)).toBe('1:01:40')
    expect(formatClock(-5)).toBe('00:00')
  })

  it('keeps the newer of server and local answers', () => {
    const r = merge(
      { a: { choiceIds: ['1'], flagged: false, seq: 5 }, b: { choiceIds: ['2'], flagged: false, seq: 2 } },
      { a: { choiceIds: ['x'], flagged: false, seq: 3 }, b: { choiceIds: ['y'], flagged: true, seq: 7 }, c: { choiceIds: ['z'], flagged: false, seq: 8 } },
    )
    expect(r.answers.a.choiceIds).toEqual(['1'])
    expect(r.answers.b.choiceIds).toEqual(['y'])
    expect(r.resend.sort()).toEqual(['b', 'c'])
  })
})
