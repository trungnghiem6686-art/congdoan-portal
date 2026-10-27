const tz = 'Asia/Ho_Chi_Minh'

/** All times are stored in UTC and shown in Vietnam time. */
export function formatDate(iso?: string | null): string {
  if (!iso) return ''
  return new Intl.DateTimeFormat('vi-VN', { timeZone: tz, day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(iso))
}

export function formatDateTime(iso?: string | null): string {
  if (!iso) return ''
  return new Intl.DateTimeFormat('vi-VN', { timeZone: tz, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(iso))
}

/** DateOnly values ("2026-01-15") are calendar dates, not instants. */
export function formatDay(day?: string | null): string {
  if (!day) return ''
  const [y, m, d] = day.slice(0, 10).split('-')
  return `${d}/${m}/${y}`
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
