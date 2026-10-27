import { api } from './api'

export type Media = { id: string; kind: string; originalName: string; contentType: string; size: number; url: string; thumbnailUrl?: string | null; width?: number | null; height?: number | null }

const VIDEO = /\.(mp4|webm)$/i

/** Images/documents go in one request; videos use the chunked API so large files survive slow links. */
export async function uploadFile(file: File, onProgress?: (pct: number) => void): Promise<Media> {
  if (!VIDEO.test(file.name)) {
    const form = new FormData()
    form.append('file', file)
    const m = await api<Media>('/api/admin/media', { method: 'POST', body: form })
    onProgress?.(100)
    return m
  }
  const start = await api<{ uploadId: string; chunkSize: number; chunkCount: number }>('/api/admin/media/uploads', {
    method: 'POST', body: { fileName: file.name, size: file.size },
  })
  for (let i = 0; i < start.chunkCount; i++) {
    const part = file.slice(i * start.chunkSize, Math.min(file.size, (i + 1) * start.chunkSize))
    await api(`/api/admin/media/uploads/${start.uploadId}/chunks/${i}`, {
      method: 'PUT', body: part, headers: { 'Content-Type': 'application/octet-stream' },
    })
    onProgress?.(Math.round(((i + 1) / start.chunkCount) * 100))
  }
  return api<Media>(`/api/admin/media/uploads/${start.uploadId}/complete`, { method: 'POST' })
}

/** YouTube / Vimeo page links → embeddable https URL, or null when the source is not allowed. */
export function toEmbedUrl(raw: string): string | null {
  let u: URL
  try {
    u = new URL(raw.trim())
  } catch {
    return null
  }
  if (u.protocol !== 'https:') return null
  const host = u.host.replace(/^m\./, 'www.')
  if ((host === 'www.youtube.com' || host === 'youtube.com') && u.pathname === '/watch' && u.searchParams.get('v'))
    return `https://www.youtube.com/embed/${encodeURIComponent(u.searchParams.get('v')!)}`
  if ((host === 'www.youtube.com' || host === 'youtube.com' || host === 'www.youtube-nocookie.com') && u.pathname.startsWith('/embed/'))
    return `https://${host}${u.pathname}`
  if (host === 'youtu.be' && u.pathname.length > 1) return `https://www.youtube.com/embed/${encodeURIComponent(u.pathname.slice(1))}`
  if (host === 'vimeo.com' && /^\/\d+$/.test(u.pathname)) return `https://player.vimeo.com/video${u.pathname}`
  if (host === 'player.vimeo.com') return u.toString()
  return null
}
