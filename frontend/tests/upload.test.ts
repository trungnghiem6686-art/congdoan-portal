import { describe, expect, it } from 'vitest'
import { toEmbedUrl } from '@/lib/upload'

describe('toEmbedUrl', () => {
  it('converts youtube and vimeo links', () => {
    expect(toEmbedUrl('https://www.youtube.com/watch?v=abc123')).toBe('https://www.youtube.com/embed/abc123')
    expect(toEmbedUrl('https://youtu.be/abc123')).toBe('https://www.youtube.com/embed/abc123')
    expect(toEmbedUrl('https://vimeo.com/76979871')).toBe('https://player.vimeo.com/video/76979871')
  })
  it('rejects other hosts and non-https', () => {
    expect(toEmbedUrl('https://evil.example/video')).toBeNull()
    expect(toEmbedUrl('http://www.youtube.com/watch?v=abc')).toBeNull()
    expect(toEmbedUrl('javascript:alert(1)')).toBeNull()
  })
})
