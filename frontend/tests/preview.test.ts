import { describe, expect, it } from 'vitest'
import { previewApiUrl } from '@/components/admin/previewSource'

describe('previewApiUrl', () => {
  it('maps the homepage and a page id to their admin preview endpoints', () => {
    expect(previewApiUrl(null)).toBe('/api/admin/homepage/preview')
    expect(previewApiUrl('homepage')).toBe('/api/admin/homepage/preview')
    expect(previewApiUrl('page:3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b')).toBe('/api/admin/pages/3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b/layout/preview')
  })

  it('rejects anything that is not a page id instead of building another API path', () => {
    expect(previewApiUrl('page:../../users')).toBeNull()
    expect(previewApiUrl('page:abc')).toBeNull()
    expect(previewApiUrl('page:3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b/../x')).toBeNull()
    expect(previewApiUrl('khac')).toBeNull()
  })
})
