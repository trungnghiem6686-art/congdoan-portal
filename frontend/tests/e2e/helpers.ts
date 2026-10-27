import { request, type APIRequestContext } from '@playwright/test'

export const API = process.env.E2E_API_URL ?? 'http://localhost:5000'
const ADMIN_USER = process.env.E2E_ADMIN_USER ?? 'admin'
const ADMIN_PASS = process.env.E2E_ADMIN_PASSWORD ?? 'Admin123456'

export const unique = (p: string) => `${p} ${Math.random().toString(36).slice(2, 8)}`

/** Authenticated admin API client (cookies + anti-forgery) used to arrange test data. */
export class AdminApi {
  private csrf = ''
  private constructor(private ctx: APIRequestContext) {}

  static async create(user = ADMIN_USER, password = ADMIN_PASS) {
    const ctx = await request.newContext({ baseURL: API })
    const a = new AdminApi(ctx)
    await a.refresh()
    await a.call('POST', '/api/admin/auth/login', { userName: user, password })
    await a.refresh()
    return a
  }

  private async refresh() {
    const r = await this.ctx.get('/api/antiforgery')
    this.csrf = (await r.json()).data.token
  }

  async call<T = any>(method: string, path: string, body?: unknown): Promise<T> {
    const r = await this.ctx.fetch(path, { method, data: body, headers: method === 'GET' ? {} : { 'X-XSRF-TOKEN': this.csrf } })
    const json = await r.json()
    if (!json.success) throw new Error(`${method} ${path} → ${r.status()} ${JSON.stringify(json.error)}`)
    return json.data as T
  }

  async upload(name: string, bytes: Buffer, mime: string) {
    const r = await this.ctx.post('/api/admin/media', { multipart: { file: { name, mimeType: mime, buffer: bytes } }, headers: { 'X-XSRF-TOKEN': this.csrf } })
    return (await r.json()).data
  }

  async publishedPost(title: string, extra: Record<string, unknown> = {}) {
    const p = await this.call('POST', '/api/admin/posts', { title, summary: 'Tóm tắt kiểm thử', body: '<p>Nội dung kiểm thử</p>', ...extra })
    await this.call('POST', `/api/admin/posts/${p.id}/transition`, { to: 'Published' })
    return p
  }

  dispose() {
    return this.ctx.dispose()
  }
}

/** Small valid PNG (1x1). */
export const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')
export const PDF = Buffer.from('%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n')
