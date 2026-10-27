/**
 * The single client for the backend. Every response is an envelope { success, data, error };
 * callers get `data` or an ApiClientError, never the envelope itself.
 */
export type ApiErrorBody = { code: string; message: string; requestId: string }
export type Envelope<T> = { success: boolean; data: T | null; error: ApiErrorBody | null }

export class ApiClientError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
    public readonly requestId?: string,
    public readonly data?: unknown,
  ) {
    super(message)
    this.name = 'ApiClientError'
  }
}

export type ApiInit = Omit<RequestInit, 'body'> & {
  body?: unknown
  /** Server-side cache tags (Next.js data cache). Only used for GET on the server. */
  tags?: string[]
  /** Seconds before cached data is refreshed even without a tag invalidation. */
  revalidate?: number
}

const isBrowser = typeof window !== 'undefined'
const unsafe = (m?: string) => !!m && !['GET', 'HEAD', 'OPTIONS'].includes(m.toUpperCase())

export function apiBase(): string {
  if (isBrowser) return ''
  return process.env.API_BASE_URL ?? 'http://localhost:5000'
}

let csrfToken: string | null = null

export async function csrf(refresh = false): Promise<string> {
  if (!csrfToken || refresh) {
    const data = await api<{ token: string }>('/api/antiforgery', { cache: 'no-store' })
    csrfToken = data.token
  }
  return csrfToken
}

export function resetCsrf() {
  csrfToken = null
}

export async function api<T>(path: string, init: ApiInit = {}): Promise<T> {
  const { body, tags, revalidate, headers, ...rest } = init
  const h = new Headers(headers)
  h.set('Accept', 'application/json')
  let payload: BodyInit | undefined
  if (body instanceof FormData || body instanceof Blob || typeof body === 'string') payload = body as BodyInit
  else if (body !== undefined) {
    payload = JSON.stringify(body)
    h.set('Content-Type', 'application/json')
  }
  if (isBrowser && unsafe(rest.method) && path !== '/api/antiforgery') h.set('X-XSRF-TOKEN', await csrf())

  const request: RequestInit = { ...rest, headers: h, body: payload }
  if (isBrowser) request.credentials = 'include'
  else if (tags?.length && !unsafe(rest.method)) {
    request.cache = rest.cache ?? 'force-cache'
    request.next = { tags, revalidate: revalidate ?? 300 }
  }

  let res: Response
  try {
    res = await fetch(apiBase() + path, request)
  } catch {
    throw new ApiClientError('network_error', 'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.', 0)
  }
  let envelope: Envelope<T>
  try {
    envelope = (await res.json()) as Envelope<T>
  } catch {
    throw new ApiClientError('network_error', `Máy chủ trả về phản hồi không hợp lệ (${res.status}).`, res.status)
  }
  if (!res.ok || !envelope.success) {
    const e = envelope.error ?? { code: 'unknown_error', message: 'Đã có lỗi xảy ra.', requestId: '' }
    // A stale anti-forgery token is refreshed once automatically.
    if (isBrowser && e.code === 'csrf_invalid' && !(init as { _retried?: boolean })._retried) {
      await csrf(true)
      return api<T>(path, { ...init, _retried: true } as ApiInit)
    }
    throw new ApiClientError(e.code, e.message, res.status, e.requestId, envelope.data)
  }
  return envelope.data as T
}

/** Paged list shape returned by the backend. */
export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number }
