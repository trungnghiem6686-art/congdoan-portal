import { describe, expect, it } from 'vitest'
import { render } from '@testing-library/react'
import { BlockRenderer, BLOCK_TYPES, type Block } from '@/components/blocks/BlockRenderer'

const post = { id: 'p1', title: 'Tin mẫu', slug: 'tin-mau', summary: 'Tóm tắt', coverUrl: null, categoryName: 'Tin tức', categorySlug: 'tin-tuc', publishedUtc: '2026-03-01T01:00:00Z', isFeatured: true }

const samples: Record<string, Block> = {
  featured_news: { id: 'b1', type: 'featured_news', title: 'Nổi bật', config: { limit: 3, layout: 'hero' }, data: { posts: [post, { ...post, id: 'p2', slug: 'b' }] } },
  news_list: { id: 'b2', type: 'news_list', title: 'Tin mới', config: { limit: 5, layout: 'list' }, data: { posts: [post] } },
  news_grid: { id: 'b3', type: 'news_grid', title: 'Lưới', config: { limit: 4, layout: 'grid', columns: 3 }, data: { posts: [post] } },
  banner: { id: 'b4', type: 'banner', title: null, config: { position: 'home_top', layout: 'wide' }, data: { banners: [{ id: 'bn', title: 'Banner', imageUrl: '/media/x', linkUrl: '/tin-tuc' }] } },
  quick_links: { id: 'b5', type: 'quick_links', title: 'Tiện ích', config: { items: [{ label: 'Thi', url: '/thi' }], layout: 'icons' }, data: null },
  documents: { id: 'b6', type: 'documents', title: 'Văn bản', config: { limit: 5, layout: 'table' }, data: { documents: [{ id: 'd', number: '01/NQ', title: 'Nghị quyết', slug: 'nq', docType: 'Nghị quyết', issuedDate: '2026-01-01', fileUrl: null }] } },
  gallery: { id: 'b7', type: 'gallery', title: 'Thư viện', config: { limit: 6, layout: 'grid' }, data: { albums: [{ id: 'a', title: 'Album', slug: 'album', kind: 'Photo', coverUrl: null }] } },
  exam_notice: { id: 'b8', type: 'exam_notice', title: 'Kỳ thi', config: { limit: 5, layout: 'list' }, data: { exams: [{ id: 'e', title: 'Thi tìm hiểu', opensAtUtc: '2026-03-01T01:00:00Z', closesAtUtc: '2026-03-05T01:00:00Z', durationMinutes: 30, phase: 'open' }] } },
  intro_text: { id: 'b9', type: 'intro_text', title: 'Giới thiệu', config: { html: '<p>Xin chào <script>alert(1)</script></p>', layout: 'plain' }, data: null },
  links: { id: 'b10', type: 'links', title: 'Liên kết', config: { items: [{ label: 'Tổng LĐLĐ', url: 'https://example.com' }], layout: 'list' }, data: null },
}

describe('BlockRenderer', () => {
  it('block_renderer_handles_all_10_types', () => {
    expect(BLOCK_TYPES).toHaveLength(10)
    for (const type of BLOCK_TYPES) {
      const { container, unmount } = render(<BlockRenderer blocks={[samples[type]]} />)
      const section = container.querySelector(`[data-block-type="${type}"]`)
      expect(section, type).not.toBeNull()
      unmount()
    }
  })

  it('intro_html_is_sanitized_again_on_render', () => {
    const { container } = render(<BlockRenderer blocks={[samples.intro_text]} />)
    expect(container.innerHTML).not.toContain('<script')
    expect(container.textContent).toContain('Xin chào')
  })

  it('empty_block_renders_nothing', () => {
    const empty: Block[] = [
      { ...samples.news_list, data: { posts: [] } },
      { ...samples.documents, data: { documents: [] } },
      { ...samples.banner, data: { banners: [] } },
      { id: 'u', type: 'unknown_type', title: 'x', config: {}, data: null },
    ]
    const { container } = render(<BlockRenderer blocks={empty} />)
    expect(container.querySelectorAll('[data-block-type]')).toHaveLength(0)
  })
})
