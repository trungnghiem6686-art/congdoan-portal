import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ApiClientError } from '@/lib/api'

function respond(body: unknown, status = 200) {
  return vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }))
}

afterEach(() => vi.unstubAllGlobals())

describe('api client', () => {
  it('api_unwraps_envelope_to_data', async () => {
    vi.stubGlobal('fetch', respond({ success: true, data: { items: [1, 2], total: 2 }, error: null }))
    const data = await api<{ items: number[]; total: number }>('/api/public/posts')
    expect(data).toEqual({ items: [1, 2], total: 2 })
  })

  it('api_throws_on_error_envelope', async () => {
    vi.stubGlobal('fetch', respond({ success: false, data: null, error: { code: 'not_found', message: 'Không tìm thấy', requestId: 'r1' } }, 404))
    const err = (await api('/api/public/posts/x').catch((e) => e)) as ApiClientError
    expect(err).toBeInstanceOf(ApiClientError)
    expect(err.code).toBe('not_found')
    expect(err.status).toBe(404)
    expect(err.requestId).toBe('r1')
    expect(err.message).toBe('Không tìm thấy')
  })

  it('api_throws_network_error_on_non_json', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>Bad gateway</html>', { status: 502 })))
    const err = (await api('/api/public/site').catch((e) => e)) as ApiClientError
    expect(err).toBeInstanceOf(ApiClientError)
    expect(err.code).toBe('network_error')
  })

  it('api_sends_csrf_header_on_writes_in_browser', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true, data: { token: 'tok-1' }, error: null })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true, data: { id: 'x' }, error: null })))
    vi.stubGlobal('fetch', fetchMock)
    await api('/api/public/contact', { method: 'POST', body: { name: 'A' } })
    const [, init] = fetchMock.mock.calls[1]
    expect(new Headers(init.headers).get('X-XSRF-TOKEN')).toBe('tok-1')
    expect(init.credentials).toBe('include')
    expect(init.body).toBe(JSON.stringify({ name: 'A' }))
  })
})
